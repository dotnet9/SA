namespace SA.Domain.Common;

/// <summary>
/// A 股证券代码的约定与交易时段判定。纯函数，放在领域层以便服务端与测试共用。
/// </summary>
public static class MarketCodes
{
    /// <summary>沪市主板代码前缀。</summary>
    private static readonly string[] ShanghaiMainPrefixes = ["600", "601", "603", "605"];

    /// <summary>科创板代码前缀。</summary>
    private static readonly string[] StarPrefixes = ["688", "689"];

    /// <summary>深市主板代码前缀。</summary>
    private static readonly string[] ShenzhenMainPrefixes = ["000", "001", "002", "003"];

    /// <summary>创业板代码前缀。</summary>
    private static readonly string[] ChiNextPrefixes = ["300", "301", "302"];

    /// <summary>北交所代码前缀（含 920 段）。</summary>
    private static readonly string[] BeijingPrefixes = ["430", "830", "831", "832", "833", "834", "835", "836", "837", "838", "839", "870", "871", "872", "873", "874", "875", "920"];

    /// <summary>
    /// 推导东财 <c>secid</c> 的市场标志：1=沪市，0=深市与北交所。
    /// </summary>
    /// <remarks>
    /// 实测结论（实施计划 §3.1）：北交所同样走 <c>0.</c>。
    /// 注意不能简单按「9 开头 = 沪市 B 股」判断：北交所自 2025 年起使用 <c>920xxx</c> 号段
    /// （如 <c>920298</c> 腾信精密），与沪市 B 股 <c>900xxx</c> 同以 9 开头但市场不同，
    /// 因此先判北交所号段，再按首位判断。
    /// </remarks>
    public static int MarketOf(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return 0;
        }

        var trimmed = code.Trim();

        if (HasPrefix(trimmed, BeijingPrefixes))
        {
            return 0;
        }

        // 沪市：60x/68x 股票、5xx 基金与债券、900 B 股、7xx 配股
        return trimmed[0] is '5' or '6' or '7' or '9' ? 1 : 0;
    }

    /// <summary>
    /// 拼出东财 <c>secid</c>，形如 <c>0.300750</c> / <c>1.600519</c>。
    /// </summary>
    public static string SecId(string code) => $"{MarketOf(code)}.{code.Trim()}";

    /// <summary>
    /// 由代码推导板块名（与需求规格 §5.3 的 Board 枚举一致）。
    /// </summary>
    public static string BoardOf(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return "其他";
        }

        var trimmed = code.Trim();

        if (HasPrefix(trimmed, StarPrefixes))
        {
            return "科创板";
        }

        if (HasPrefix(trimmed, ChiNextPrefixes))
        {
            return "创业板";
        }

        if (HasPrefix(trimmed, BeijingPrefixes))
        {
            return "北交所";
        }

        if (HasPrefix(trimmed, ShanghaiMainPrefixes))
        {
            return "沪市主板";
        }

        if (HasPrefix(trimmed, ShenzhenMainPrefixes))
        {
            return "深市主板";
        }

        // 兜底按市场归属，避免出现空板块
        return MarketOf(trimmed) == 1 ? "沪市主板" : "深市主板";
    }

    /// <summary>
    /// 判断名称是否带 ST / 退市风险标记（列表与详情需要显著标注，实施计划 §10）。
    /// </summary>
    public static bool IsSt(string? name) =>
        !string.IsNullOrEmpty(name)
        && (name.Contains("ST", StringComparison.OrdinalIgnoreCase)
            || name.Contains("退", StringComparison.Ordinal)
            || name.StartsWith('*'));

    /// <summary>
    /// 判断是否为新股 / 次新股（名称前缀 <c>N</c> 为上市首日、<c>C</c> 为上市后第 2–5 日，
    /// 二者都是东财的行情标注，到期自动消失）。
    /// </summary>
    /// <remarks>
    /// 涨幅榜必须剔除这类标的：上市首日不设涨跌幅（或幅度远大于常态），
    /// 否则「C沈鼓 +177.74%」会长期霸榜，让榜单失去参考价值。
    /// 名称前缀是列表接口唯一可得的信号，因此以此判定，并在口径说明中写明。
    /// </remarks>
    public static bool IsNewListing(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        var first = name[0];
        return first is 'N' or 'C';
    }

    /// <summary>
    /// 是否为可分析的股票代码（排除指数、基金、债券、回购等非股票品种）。
    /// </summary>
    public static bool IsStockCode(string code)
    {
        var board = BoardOf(code);
        return board is "沪市主板" or "深市主板" or "创业板" or "科创板" or "北交所"
            && code.Trim().Length == 6
            && code.Trim().All(char.IsAsciiDigit);
    }

    private static bool HasPrefix(string code, string[] prefixes) =>
        Array.Exists(prefixes, p => code.StartsWith(p, StringComparison.Ordinal));
}

/// <summary>
/// A 股交易时段。统一用 <see cref="SaTime"/> 取业务时间，避免各调用点自行拼时间。
/// </summary>
public static class MarketSession
{
    /// <summary>集合竞价开始（视为盘中起始）。</summary>
    public static TimeOnly Open { get; } = new(9, 15);

    /// <summary>收盘集合竞价结束。</summary>
    public static TimeOnly Close { get; } = new(15, 5);

    /// <summary>午间休市开始。</summary>
    private static TimeOnly NoonStart { get; } = new(11, 30);

    /// <summary>午间休市结束。</summary>
    private static TimeOnly NoonEnd { get; } = new(13, 0);

    /// <summary>当前业务日是否处于盘中时段（不含节假日判断）。</summary>
    public static bool IsTradingTime()
    {
        var time = TimeOnly.FromDateTime(SaTime.Now.DateTime);
        return time >= Open && time <= Close;
    }

    /// <summary>
    /// 市场阶段文案：盘前 / 交易中 / 午间休市 / 已收盘 / 非交易日。
    /// 数据最后归属日早于今日时一律为「已收盘」，避免把上一交易日的数据标成实时。
    /// </summary>
    public static string Phase(bool isTradingDay, DateOnly? dataDate)
    {
        if (!isTradingDay)
        {
            return "非交易日";
        }

        if (dataDate is null || dataDate != SaTime.Today)
        {
            return "已收盘";
        }

        var time = TimeOnly.FromDateTime(SaTime.Now.DateTime);
        if (time < Open)
        {
            return "盘前";
        }

        if (time > Close)
        {
            return "已收盘";
        }

        return time >= NoonStart && time < NoonEnd ? "午间休市" : "交易中";
    }
}
