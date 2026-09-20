namespace SA.Contracts.Events;

/// <summary>
/// 事件类型。取值与详细设计 §3.4 的事件分类一致。
/// </summary>
public static class EventTypes
{
    /// <summary>业绩预告。</summary>
    public const string Forecast = "forecast";

    /// <summary>定期报告披露。</summary>
    public const string Report = "report";

    /// <summary>分红方案。</summary>
    public const string Dividend = "dividend";

    /// <summary>龙虎榜上榜。</summary>
    public const string Billboard = "billboard";

    /// <summary>大宗交易。</summary>
    public const string BlockTrade = "blocktrade";

    /// <summary>股东户数变化。</summary>
    public const string HolderCount = "holderCount";

    /// <summary>股权质押。</summary>
    public const string Pledge = "pledge";

    /// <summary>陆股通持股变化。</summary>
    public const string Northbound = "northbound";

    /// <summary>人工标注（用户补充的事件说明）。</summary>
    public const string Annotation = "annotation";
}

/// <summary>
/// 事件时间线一条。
/// </summary>
/// <param name="Key">稳定键（用于人工标注与去重），形如 <c>forecast:2026-06-30</c>。</param>
/// <param name="Date">事件日期。</param>
/// <param name="Type">事件类型，取值见 <see cref="EventTypes"/>。</param>
/// <param name="TypeName">类型中文名。</param>
/// <param name="Title">标题。</param>
/// <param name="Detail">详情。</param>
/// <param name="Tone">影响方向：up / down / neutral。</param>
/// <param name="Impact">影响强度：1（弱）–5（强）。</param>
/// <param name="Source">数据来源说明。</param>
/// <param name="Annotated">是否已被人工标注过（影响方向由人工覆盖）。</param>
/// <param name="AnnotationNote">人工标注备注。</param>
public sealed record EventItemDto(
    string Key,
    string Date,
    string Type,
    string TypeName,
    string Title,
    string? Detail,
    string Tone,
    int Impact,
    string Source,
    bool Annotated,
    string? AnnotationNote);

/// <summary>
/// 事件类型汇总（时间线侧栏与事件拓扑共用）。
/// </summary>
/// <param name="Type">类型。</param>
/// <param name="TypeName">中文名。</param>
/// <param name="Count">条数。</param>
/// <param name="LatestDate">最近一次。</param>
/// <param name="NetTone">该类型的整体倾向。</param>
public sealed record EventSummaryDto(string Type, string TypeName, int Count, string? LatestDate, string NetTone);

/// <summary>
/// 拓扑图节点。
/// </summary>
/// <param name="Id">节点 Id。</param>
/// <param name="Name">显示名。</param>
/// <param name="Category">分类下标。</param>
/// <param name="Value">权重（映射为节点大小）。</param>
/// <param name="Highlight">是否高亮当前标的。</param>
/// <param name="Note">悬浮说明。</param>
public sealed record TopologyNodeDto(string Id, string Name, int Category, decimal Value, bool Highlight, string? Note);

/// <summary>
/// 拓扑图边。
/// </summary>
/// <param name="Source">起点节点 Id。</param>
/// <param name="Target">终点节点 Id。</param>
/// <param name="Label">边标注。</param>
/// <param name="Tone">影响方向。</param>
public sealed record TopologyEdgeDto(string Source, string Target, string? Label, string Tone);

/// <summary>
/// 一张拓扑图。
/// </summary>
/// <param name="Key">键：shareholder / industry / event / counterparty。</param>
/// <param name="Name">名称。</param>
/// <param name="Description">这张图回答什么问题（界面上直接展示，避免看图猜含义）。</param>
/// <param name="Categories">分类名（图例）。</param>
/// <param name="Nodes">节点。</param>
/// <param name="Edges">边。</param>
/// <param name="Available">数据是否足够画图；false 时界面显示原因。</param>
/// <param name="UnavailableReason">不可用原因。</param>
public sealed record TopologyDto(
    string Key,
    string Name,
    string Description,
    IReadOnlyList<string> Categories,
    IReadOnlyList<TopologyNodeDto> Nodes,
    IReadOnlyList<TopologyEdgeDto> Edges,
    bool Available,
    string? UnavailableReason);

/// <summary>
/// 事件与影响（<c>GET /api/stocks/{code}/events</c>）。
/// </summary>
/// <param name="Code">证券代码。</param>
/// <param name="Name">证券名称。</param>
/// <param name="AsOf">数据时间。</param>
/// <param name="Events">时间线（按日期倒序）。</param>
/// <param name="Summary">按类型汇总。</param>
/// <param name="Topologies">四张拓扑图。</param>
/// <param name="Insights">结论。</param>
/// <param name="CanAnnotate">当前账号是否可标注（<c>event.edit</c>）。</param>
/// <param name="Collecting">是否仍在采集（true 时界面自动轮询刷新）。</param>
/// <param name="Notes">口径说明。</param>
public sealed record EventTimelineDto(
    string Code,
    string Name,
    string? AsOf,
    IReadOnlyList<EventItemDto> Events,
    IReadOnlyList<EventSummaryDto> Summary,
    IReadOnlyList<TopologyDto> Topologies,
    IReadOnlyList<string> Insights,
    bool CanAnnotate,
    bool Collecting,
    IReadOnlyList<string> Notes);

/// <summary>人工标注请求。</summary>
/// <param name="EventKey">事件稳定键。</param>
/// <param name="Scope">适用范围：全部事件 key（此处固定为单事件）。</param>
/// <param name="Tone">人工判定的影响方向：up / down / neutral。</param>
/// <param name="Impact">人工判定的影响强度：1–5。</param>
/// <param name="Note">备注。</param>
public sealed record EventAnnotationRequest(
    string EventKey,
    string Tone,
    int Impact,
    string? Note);
