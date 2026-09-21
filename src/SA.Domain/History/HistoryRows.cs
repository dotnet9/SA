namespace SA.Domain.History;

/// <summary>
/// 日线一行。字段与详细设计 §4 的 <c>daily</c> 数据集一致。
/// </summary>
/// <param name="Date">交易日。</param>
/// <param name="Open">开盘价（元）。</param>
/// <param name="High">最高价（元）。</param>
/// <param name="Low">最低价（元）。</param>
/// <param name="Close">收盘价（元，前复权口径）。</param>
/// <param name="Volume">成交量（手）。</param>
/// <param name="Amount">
/// 成交额（元）；<c>null</c> 表示<b>该源不提供</b>（腾讯 K 线每行只有 6 段，无成交额）。
/// </param>
/// <param name="Turnover">
/// 换手率（百分数）；<c>null</c> 表示<b>该源不提供</b>（同 <paramref name="Amount"/>）。
/// </param>
/// <param name="VolRatio">量比。</param>
/// <param name="AdjFactor">复权因子；前复权序列为 1.0，仅作口径标记。</param>
/// <remarks>
/// <b><paramref name="Amount"/> 与 <paramref name="Turnover"/> 为什么是可空的</b>：
/// 腾讯 K 线（<c>web.ifzq.gtimg.cn</c>）每行只有 <c>[日期, 开, 收, 高, 低, 量]</c> 六段，
/// 既没有成交额也没有换手率。参考实现把它写成 <c>parseFloat(item[6] || '0')</c>，
/// 于是成交额被<b>静默变成 0</b>——这是数据失真，会污染「成交额均值」这类计算。
/// 本项目按实施计划 §1.3/§10 的要求留 <c>null</c> 并在界面标注「该源不提供」，
/// 绝不用 0 顶替。
/// </remarks>
public readonly record struct DailyBar(
    DateOnly Date,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal Volume,
    decimal? Amount,
    decimal? Turnover,
    decimal VolRatio,
    decimal AdjFactor);

/// <summary>
/// 指标一行。字段为详细设计 §4 的 <c>indicator</c> 数据集，另加 BOLL 三轨
/// （§5.1 要求计算 BOLL，趋势页的 <c>band</c> 图直接消费）。
/// </summary>
/// <param name="Date">交易日。</param>
/// <param name="Ma5">5 日均线。</param>
/// <param name="Ma10">10 日均线。</param>
/// <param name="Ma20">20 日均线。</param>
/// <param name="Ma60">60 日均线。</param>
/// <param name="Dif">MACD 快慢线之差。</param>
/// <param name="Dea">DIF 的信号线。</param>
/// <param name="Macd">MACD 柱。</param>
/// <param name="K">KDJ 的 K。</param>
/// <param name="D">KDJ 的 D。</param>
/// <param name="J">KDJ 的 J。</param>
/// <param name="Rsi6">6 日 RSI。</param>
/// <param name="Rsi12">12 日 RSI。</param>
/// <param name="Rsi24">24 日 RSI。</param>
/// <param name="BollUp">布林上轨。</param>
/// <param name="BollMid">布林中轨（= MA20）。</param>
/// <param name="BollLow">布林下轨。</param>
public readonly record struct IndicatorRow(
    DateOnly Date,
    decimal? Ma5,
    decimal? Ma10,
    decimal? Ma20,
    decimal? Ma60,
    decimal? Dif,
    decimal? Dea,
    decimal? Macd,
    decimal? K,
    decimal? D,
    decimal? J,
    decimal? Rsi6,
    decimal? Rsi12,
    decimal? Rsi24,
    decimal? BollUp,
    decimal? BollMid,
    decimal? BollLow);
