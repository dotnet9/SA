namespace SA.Infrastructure.Storage;

/// <summary>
/// 数据目录布局。删除 <see cref="Root"/> 后重启可完整重建（唯一例外是密码与自选等元数据，
/// 见实施计划 §9），因此不提供数据迁移脚本。
/// </summary>
public sealed class DataPaths : SA.Application.Abstractions.IDataPaths
{
    /// <summary>
    /// 建立数据目录布局。
    /// </summary>
    /// <param name="root">数据根目录的绝对路径。</param>
    public DataPaths(string root)
    {
        Root = Path.GetFullPath(root);
        DatabaseFile = Path.Combine(Root, "sa.db");
        ParquetRoot = Path.Combine(Root, "parquet");
        LogRoot = Path.Combine(Root, "logs");
    }

    /// <summary>数据根目录。</summary>
    public string Root { get; }

    /// <summary>SQLite 数据库文件。</summary>
    public string DatabaseFile { get; }

    /// <summary>时序历史（Parquet）根目录。</summary>
    public string ParquetRoot { get; }

    /// <summary>日志目录。</summary>
    public string LogRoot { get; }

    /// <summary>
    /// 解析数据根目录的绝对路径。相对路径锚定到仓库根（见 <see cref="FindRepositoryRoot"/>），
    /// 找不到仓库根时退回 <paramref name="fallback"/>，再退回当前工作目录。
    /// </summary>
    /// <param name="configured">配置中的值，可为空。</param>
    /// <param name="fallback">回退锚点，通常是内容根。</param>
    public static string ResolveRoot(string? configured, string? fallback = null)
    {
        var root = string.IsNullOrWhiteSpace(configured) ? "data" : configured;
        if (Path.IsPathRooted(root))
        {
            return Path.GetFullPath(root);
        }

        var anchor = FindRepositoryRoot() ?? fallback ?? Directory.GetCurrentDirectory();
        return Path.GetFullPath(Path.Combine(anchor, root));
    }

    /// <summary>
    /// 向上查找含 SA.sln 的目录，作为相对路径的锚点。
    /// </summary>
    public static string? FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SA.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    /// <summary>
    /// 幂等创建所需目录。
    /// </summary>
    public void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(ParquetRoot);
        Directory.CreateDirectory(LogRoot);
    }

    /// <summary>
    /// 返回某个时序数据集的分片目录（不存在时仅计算路径，不创建）。
    /// </summary>
    /// <param name="dataset">数据集名，如 daily、indicator。</param>
    public string DatasetDirectory(string dataset) => Path.Combine(ParquetRoot, dataset);
}
