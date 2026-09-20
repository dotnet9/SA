namespace SA.Domain.Entities.Market;

/// <summary>
/// 证券基础信息（全市场股票池）。由东财全市场列表采集而来，
/// 是搜索、拼音索引、行业聚合与排行榜的共同底座。
/// </summary>
/// <remarks>
/// 行业口径统一为<b>东财行业</b>（列表接口 <c>f100</c>，与 <see cref="Sector"/> 的板块名同名），
/// 需求规格中的申万一级暂无直取源，DTO 侧预留 <c>swIndustry</c>（实施计划 §2 决策 11）。
/// </remarks>
public sealed class Instrument
{
    /// <summary>证券代码，如 <c>300750</c>。</summary>
    public required string Code { get; set; }

    /// <summary>证券名称。</summary>
    public required string Name { get; set; }

    /// <summary>名称的拼音首字母（小写），如「宁德时代」→ <c>ndsd</c>，用于拼音搜索。</summary>
    public string? Pinyin { get; set; }

    /// <summary>东财市场标志：1=沪市，0=深市与北交所。</summary>
    public int Market { get; set; }

    /// <summary>板块（由代码前缀推导）：沪市主板 / 深市主板 / 创业板 / 科创板 / 北交所。</summary>
    public required string Board { get; set; }

    /// <summary>东财行业名，如「电池」。</summary>
    public string? Industry { get; set; }

    /// <summary>名称含 ST / *ST / 退市标记。</summary>
    public bool IsSt { get; set; }

    /// <summary>最后一次在列表接口中出现的业务日期。</summary>
    public DateOnly UpdatedOn { get; set; }
}
