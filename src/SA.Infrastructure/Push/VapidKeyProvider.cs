using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SA.Application.Abstractions;
using SA.Infrastructure.Storage;
using WebPush;

namespace SA.Infrastructure.Push;

/// <summary>
/// VAPID 密钥的生成与持久化。
/// </summary>
/// <remarks>
/// <para>
/// VAPID 用 P-256 的 ECDSA 密钥对给推送请求签名（RFC 8292）：公钥交给浏览器用于订阅，
/// 私钥留在服务端签名。密钥必须<b>持久化</b>——换掉私钥会让所有已有订阅失效，
/// 用户会莫名其妙收不到通知。
/// </para>
/// <para>
/// 存放在 <c>data/keys/vapid.json</c>（与 Data Protection 密钥同目录），
/// 用 .NET 自带的 ECDsa 生成，不依赖任何第三方密钥生成逻辑。
/// </para>
/// </remarks>
public sealed class VapidKeyProvider : IPushSender
{
    /// <summary>密钥文件名。</summary>
    private const string FileName = "vapid.json";

    private readonly ILogger<VapidKeyProvider> _logger;
    private readonly WebPushClient _client = new();

    private readonly Lazy<(string PublicKey, string PrivateKey)> _keys;

    /// <summary>
    /// 构造并加载（或首次生成）VAPID 密钥。
    /// </summary>
    public VapidKeyProvider(DataPaths paths, ILogger<VapidKeyProvider> logger)
    {
        _logger = logger;
        _keys = new Lazy<(string, string)>(() => LoadOrCreate(paths));

        _client.SetVapidDetails(
            // subject 必须是 mailto: 或 https: 前缀的 URI，否则部分推送服务会拒绝
            subject: "mailto:admin@localhost",
            publicKey: _keys.Value.PublicKey,
            privateKey: _keys.Value.PrivateKey);
    }

    /// <inheritdoc />
    public string PublicKey => _keys.Value.PublicKey;

    /// <inheritdoc />
    public async Task<PushSendResult> SendAsync(
        Domain.Entities.Alerts.PushSubscription subscription,
        string payloadJson,
        CancellationToken cancellationToken = default)
    {
        var target = new WebPush.PushSubscription(subscription.Endpoint, subscription.P256dh, subscription.Auth);

        try
        {
            await _client.SendNotificationAsync(target, payloadJson).ConfigureAwait(false);
            return PushSendResult.Success();
        }
        catch (WebPushException ex)
        {
            var status = (int?)ex.StatusCode;

            // 404/410 是「订阅已失效」的标准信号（用户卸载、清数据、授权被撤销）：
            // 这类失败重试多少次都不会成功，必须删除该订阅
            if (status is 404 or 410)
            {
                return PushSendResult.Gone($"HTTP {status}（订阅已失效）");
            }

            _logger.LogWarning(ex, "Web Push 发送失败（HTTP {Status}）", status);
            return PushSendResult.Transient($"HTTP {status}：{Truncate(ex.Message, 200)}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 网络层异常（DNS、TLS、超时）属于可重试
            return PushSendResult.Transient(Truncate(ex.Message, 200));
        }
    }

    /// <summary>
    /// 读取已有密钥；不存在或损坏时重新生成并落盘。
    /// </summary>
    private (string PublicKey, string PrivateKey) LoadOrCreate(DataPaths paths)
    {
        var file = Path.Combine(paths.Root, "keys", FileName);

        try
        {
            if (File.Exists(file))
            {
                var text = File.ReadAllText(file);
                var loaded = JsonSerializer.Deserialize<VapidKeyFile>(text);

                if (!string.IsNullOrWhiteSpace(loaded?.PublicKey) && !string.IsNullOrWhiteSpace(loaded.PrivateKey))
                {
                    return (loaded.PublicKey, loaded.PrivateKey);
                }

                _logger.LogWarning("VAPID 密钥文件内容不完整，将重新生成：{File}", file);
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // 读不出来就重新生成：宁可让已有订阅失效，也不能让整个推送链路起不来
            _logger.LogWarning(ex, "读取 VAPID 密钥失败，将重新生成：{File}", file);
        }

        var created = Generate();

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, JsonSerializer.Serialize(new VapidKeyFile
            {
                PublicKey = created.PublicKey,
                PrivateKey = created.PrivateKey
            }));

            _logger.LogInformation("已生成新的 VAPID 密钥：{File}", file);
        }
        catch (IOException ex)
        {
            // 落盘失败不阻断：本次进程内仍可签名，只是重启后会换密钥（并记一条警告）
            _logger.LogWarning(ex, "VAPID 密钥写入失败，重启后订阅将失效：{File}", file);
        }

        return created;
    }

    /// <summary>
    /// 生成一对 VAPID 密钥。
    /// </summary>
    /// <remarks>
    /// 公钥按 Web Push 约定输出<b>非压缩点</b>（<c>0x04 || X(32) || Y(32)</c>，共 65 字节），
    /// 私钥输出 <c>D</c>（32 字节），两者都用 base64url 编码且不带填充。
    /// 这是浏览器 <c>applicationServerKey</c> 与推送服务共同期望的格式。
    /// </remarks>
    private static (string PublicKey, string PrivateKey) Generate()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parameters = ecdsa.ExportParameters(includePrivateParameters: true);

        var x = parameters.Q.X!;
        var y = parameters.Q.Y!;
        var d = parameters.D!;

        var publicKey = new byte[65];
        publicKey[0] = 0x04;
        x.CopyTo(publicKey, 1);
        y.CopyTo(publicKey, 33);

        return (Base64Url(publicKey), Base64Url(d));
    }

    /// <summary>base64url 编码（去掉填充、+/ 换成 -_）。</summary>
    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];

    /// <summary>密钥文件的结构。</summary>
    private sealed class VapidKeyFile
    {
        /// <summary>公钥（base64url，非压缩点）。</summary>
        public string? PublicKey { get; set; }

        /// <summary>私钥（base64url，D）。</summary>
        public string? PrivateKey { get; set; }
    }
}
