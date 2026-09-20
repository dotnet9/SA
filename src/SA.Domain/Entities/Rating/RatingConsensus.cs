namespace SA.Domain.Entities.Rating;

/// <summary>
/// 机构评级共识。对应东财 <c>RPT_WEB_RESPREDICT</c>：每只股票一行，随新研报滚动更新。
/// </summary>
/// <remarks>
/// 上游直接给出各评级档位的机构家数与未来数年的 EPS 预测，因此本地不做任何加权或拟合：
/// 评级的口径（谁算「买入」）由上游决定，本地只负责换算与展示，避免出现第二套评级标准。
/// <c>YEAR_MARK</c> 标记该年度是实际值（<c>A</c>）还是预测值（<c>E</c>），展示时必须区分。
/// </remarks>
public sealed class RatingConsensus
{
    /// <summary>证券代码。</summary>
    public required string Code { get; set; }

    /// <summary>给出评级的机构总数。</summary>
    public int RatingOrgNum { get; set; }

    /// <summary>买入家数。</summary>
    public int BuyNum { get; set; }

    /// <summary>增持家数。</summary>
    public int AddNum { get; set; }

    /// <summary>中性家数。</summary>
    public int? NeutralNum { get; set; }

    /// <summary>减持家数。</summary>
    public int? ReduceNum { get; set; }

    /// <summary>卖出家数。</summary>
    public int? SaleNum { get; set; }

    /// <summary>目标价上限（元）。</summary>
    public decimal? AimPriceMax { get; set; }

    /// <summary>目标价下限（元）。</summary>
    public decimal? AimPriceMin { get; set; }

    /// <summary>长期评级机构数（上游 RATING_LONG_NUM）。</summary>
    public int? LongTermNum { get; set; }

    /// <summary>预测年度 1。</summary>
    public int? Year1 { get; set; }

    /// <summary>年度 1 的 EPS。</summary>
    public decimal? Eps1 { get; set; }

    /// <summary>年度 1 标记：A 实际 / E 预测。</summary>
    public string? YearMark1 { get; set; }

    /// <summary>预测年度 2。</summary>
    public int? Year2 { get; set; }

    /// <summary>年度 2 的 EPS。</summary>
    public decimal? Eps2 { get; set; }

    /// <summary>年度 2 标记。</summary>
    public string? YearMark2 { get; set; }

    /// <summary>预测年度 3。</summary>
    public int? Year3 { get; set; }

    /// <summary>年度 3 的 EPS。</summary>
    public decimal? Eps3 { get; set; }

    /// <summary>年度 3 标记。</summary>
    public string? YearMark3 { get; set; }

    /// <summary>预测年度 4。</summary>
    public int? Year4 { get; set; }

    /// <summary>年度 4 的 EPS。</summary>
    public decimal? Eps4 { get; set; }

    /// <summary>年度 4 标记。</summary>
    public string? YearMark4 { get; set; }

    /// <summary>所属行业板块。</summary>
    public string? IndustryBoard { get; set; }

    /// <summary>写入时间。</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
