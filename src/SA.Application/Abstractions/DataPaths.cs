namespace SA.Application.Abstractions;

/// <summary>
/// 数据目录的只读视图。后台「系统设置与存储」需要展示路径与占用，
/// 但应用层不能引用基础设施层的具体实现（依赖方向 Api → Application → Domain），
/// 因此把路径信息抽象成接口，由基础设施层实现。
/// </summary>
public interface IDataPaths
{
    /// <summary>数据根目录。</summary>
    string Root { get; }

    /// <summary>SQLite 数据库文件路径。</summary>
    string DatabaseFile { get; }

    /// <summary>Parquet 根目录。</summary>
    string ParquetRoot { get; }

    /// <summary>日志目录。</summary>
    string LogRoot { get; }
}
