namespace SA.Contracts.Trend;

/// <summary>
/// 一根 K 线。字段名与原型 <c>charts.js</c> 的 <c>bars</c> 元素一致（<c>d/o/h/l/c/v</c>），
/// 前端图表工厂可直接消费。
/// </summary>
/// <param name="D">交易日。</param>
/// <param name="O">开盘。</param>
/// <param name="H">最高。</param>
/// <param name="L">最低。</param>
/// <param name="C">收盘。</param>
/// <param name="V">成交量（手）。</param>
public sealed record CandleDto(string D, decimal O, decimal H, decimal L, decimal C, decimal V);

/// <summary>
/// 均线组，按日期与 <see cref="TrendDto.Candles"/> 一一对齐（不足周期处为 null）。
/// </summary>
/// <param name="Ma5">5 日均线。</param>
/// <param name="Ma10">10 日均线。</param>
/// <param name="Ma20">20 日均线。</param>
/// <param name="Ma60">60 日均线。</param>
public sealed record MaSeriesDto(
    IReadOnlyList<decimal?> Ma5,
    IReadOnlyList<decimal?> Ma10,
    IReadOnlyList<decimal?> Ma20,
    IReadOnlyList<decimal?> Ma60);

/// <summary>MACD 三线（与 K 线对齐）。</summary>
/// <param name="Dif">DIF。</param>
/// <param name="Dea">DEA。</param>
/// <param name="Macd">MACD 柱。</param>
public sealed record MacdSeriesDto(
    IReadOnlyList<decimal?> Dif,
    IReadOnlyList<decimal?> Dea,
    IReadOnlyList<decimal?> Macd);

/// <summary>KDJ 三线（与 K 线对齐）。</summary>
/// <param name="K">K。</param>
/// <param name="D">D。</param>
/// <param name="J">J。</param>
public sealed record KdjSeriesDto(
    IReadOnlyList<decimal?> K,
    IReadOnlyList<decimal?> D,
    IReadOnlyList<decimal?> J);

/// <summary>布林带三轨（与 K 线对齐）。</summary>
/// <param name="Up">上轨。</param>
/// <param name="Mid">中轨。</param>
/// <param name="Low">下轨。</param>
public sealed record BollSeriesDto(
    IReadOnlyList<decimal?> Up,
    IReadOnlyList<decimal?> Mid,
    IReadOnlyList<decimal?> Low);

/// <summary>相对强弱。</summary>
/// <param name="Benchmark">基准名称，如「沪深300」。</param>
/// <param name="BenchmarkCode">基准代码。</param>
/// <param name="Value">区间末的相对强弱（百分点）；样本不足为 null。区间与 K 线相同。</param>
/// <param name="Line">相对强弱曲线，与 <see cref="TrendDto.Candles"/> 按日期一一对齐；样本不足时为空数组。</param>
public sealed record RelativeStrengthDto(
    string Benchmark,
    string BenchmarkCode,
    decimal? Value,
    IReadOnlyList<decimal> Line);

/// <summary>
/// 关键价位与位置。任一字段样本不足时为 null，界面显示「—」（实施计划 §10）。
/// </summary>
/// <param name="High250">近 250 个交易日最高。</param>
/// <param name="Low250">近 250 个交易日最低。</param>
/// <param name="AboveMa20Pct">现价高于 MA20 的幅度（百分数）。</param>
/// <param name="AboveMa250Pct">现价高于 MA250 的幅度（百分数）。</param>
/// <param name="Quantile3y">近 3 年价格分位（百分数，0–100）。</param>
/// <param name="Quantile250">近 250 日价格分位（百分数）。</param>
/// <param name="Samples">参与分位计算的样本数，用于说明「为什么是 —」。</param>
public sealed record TrendLevelsDto(
    decimal? High250,
    decimal? Low250,
    decimal? AboveMa20Pct,
    decimal? AboveMa250Pct,
    decimal? Quantile3y,
    decimal? Quantile250,
    int Samples);

/// <summary>一条结论（趋势摘要的标签或摘要行）。</summary>
/// <param name="Label">维度名。</param>
/// <param name="Text">结论文案。</param>
/// <param name="Tone">色调：up / down / warn / neutral。</param>
public sealed record TrendInsightDto(string Label, string Text, string Tone);

/// <summary>
/// 趋势与价格结构（<c>GET /api/stocks/{code}/trend</c>）。
/// </summary>
/// <param name="Code">证券代码。</param>
/// <param name="Name">证券名称。</param>
/// <param name="Period">周期：daily / weekly / monthly。</param>
/// <param name="Adjust">复权口径：front / none / back。</param>
/// <param name="AsOf">最新交易日。</param>
/// <param name="Candles">K 线（最近 <c>limit</c> 根，最多 240）。</param>
/// <param name="Ma">均线组。</param>
/// <param name="Macd">MACD。</param>
/// <param name="Kdj">KDJ。</param>
/// <param name="Boll">布林带。</param>
/// <param name="RelativeStrength">相对强弱。</param>
/// <param name="Levels">关键价位与位置。</param>
/// <param name="Insights">趋势摘要（多头排列 / 量能 / 位置等）。</param>
/// <param name="Notes">口径说明，界面必须展示。</param>
public sealed record TrendDto(
    string Code,
    string Name,
    string Period,
    string Adjust,
    string AsOf,
    IReadOnlyList<CandleDto> Candles,
    MaSeriesDto Ma,
    MacdSeriesDto Macd,
    KdjSeriesDto Kdj,
    BollSeriesDto Boll,
    RelativeStrengthDto RelativeStrength,
    TrendLevelsDto Levels,
    IReadOnlyList<TrendInsightDto> Insights,
    IReadOnlyList<string> Notes);
