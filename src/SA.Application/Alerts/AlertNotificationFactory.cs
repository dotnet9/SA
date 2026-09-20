using System.Text;
using SA.Domain.Alerts;
using SA.Domain.Entities.Alerts;
using SA.Domain.Entities.Market;

namespace SA.Application.Alerts;

/// <summary>
/// 提醒通知的文案与级别生成。抽出来是为了让「通知里到底写什么」也能被单测覆盖。
/// </summary>
public static class AlertNotificationFactory
{
    /// <summary>
    /// 由评估结果生成一条通知。
    /// </summary>
    /// <param name="rule">规则。</param>
    /// <param name="name">证券名称。</param>
    /// <param name="evaluation">评估结果（<c>Triggered</c> 必须为 true）。</param>
    /// <param name="now">触发时间。</param>
    public static Notification Create(AlertRule rule, string? name, AlertEvaluation evaluation, DateTimeOffset now)
    {
        var builder = new StringBuilder();
        builder.Append(name ?? rule.Code).Append(' ').Append(rule.Code);
        if (!string.IsNullOrWhiteSpace(rule.Note))
        {
            builder.Append(" · ").Append(rule.Note);
        }

        builder.Append('\n').Append(evaluation.Body);

        return new Notification
        {
            UserId = rule.UserId,
            Title = $"{name ?? rule.Code} {evaluation.Title}",
            Body = builder.ToString(),
            Level = evaluation.Level ?? "info",
            Code = rule.Code,
            RuleId = rule.Id,
            IsRead = false,
            CreatedAt = now
        };
    }

    /// <summary>
    /// 把同一用户在同一轮里触发的多条提醒合并为一条通知。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>为什么必须合并</b>：一次行情跳动常常同时命中多条规则（价格上穿 + 涨幅超阈值 + 量比超阈值），
    /// 逐条下发会让用户在几十秒内连收好几条通知，体验上等同于骚扰。
    /// </para>
    /// <para>
    /// 合并规则：单条时保持原样（不给标题加「1 条提醒」这种噪音）；
    /// 多条时标题写「N 条提醒同时触发」，正文逐条列出，级别取其中最强的一条。
    /// </para>
    /// </remarks>
    /// <param name="userId">接收用户。</param>
    /// <param name="hits">本轮命中（已按用户分组）。</param>
    /// <param name="now">触发时间。</param>
    /// <param name="maxListed">正文最多列出多少条（超出部分只计数）。</param>
    public static Notification CreateMerged(
        string userId,
        IReadOnlyList<MergedHit> hits,
        DateTimeOffset now,
        int maxListed = 8)
    {
        if (hits.Count == 0)
        {
            throw new ArgumentException("至少需要一条命中", nameof(hits));
        }

        if (hits.Count == 1)
        {
            var single = hits[0];
            return new Notification
            {
                UserId = userId,
                Title = $"{single.Name} {single.Title}",
                Body = single.Body,
                Level = single.Level,
                Code = single.Code,
                RuleId = single.RuleId,
                IsRead = false,
                CreatedAt = now
            };
        }

        var builder = new StringBuilder();
        foreach (var hit in hits.Take(maxListed))
        {
            builder.Append("· ").Append(hit.Name).Append(' ').Append(hit.Code).Append('：')
                .Append(hit.Title).Append('\n');
        }

        if (hits.Count > maxListed)
        {
            builder.Append($"（另有 {hits.Count - maxListed} 条未列出）");
        }

        return new Notification
        {
            UserId = userId,
            // 合并后的标题必须让用户看出「这是一批」而不是某一条，
            // 否则他会以为只触发了一个条件、漏看其他信号
            Title = $"{hits.Count} 条提醒同时触发",
            Body = builder.ToString().TrimEnd('\n'),
            Level = ResolveLevel(hits),
            // 涉及多个标的时不指向单一代码，避免点进去只看到其中一个
            Code = hits.All(hit => hit.Code == hits[0].Code) ? hits[0].Code : null,
            RuleId = hits[0].RuleId,
            IsRead = false,
            CreatedAt = now
        };
    }

    /// <summary>
    /// 合并后的级别：取其中最强的一条（down &gt; up &gt; warn &gt; info）。
    /// </summary>
    /// <remarks>
    /// 消极信号优先：一批提醒里只要有一条利空，就该让用户先看到它；
    /// 按「多数票」取会让利空被淹没在几条中性提醒里。
    /// </remarks>
    private static string ResolveLevel(IReadOnlyList<MergedHit> hits)
    {
        if (hits.Any(hit => hit.Level == "down"))
        {
            return "down";
        }

        if (hits.Any(hit => hit.Level == "up"))
        {
            return "up";
        }

        return hits.Any(hit => hit.Level == "warn") ? "warn" : "info";
    }
}

/// <summary>
/// 合并前的一条命中。独立于 API 层的私有结构，便于单测直接构造。
/// </summary>
/// <param name="RuleId">规则 Id。</param>
/// <param name="Code">证券代码。</param>
/// <param name="Name">证券名称。</param>
/// <param name="Title">触发标题。</param>
/// <param name="Body">触发正文。</param>
/// <param name="Level">级别。</param>
public readonly record struct MergedHit(
    string RuleId,
    string Code,
    string Name,
    string Title,
    string Body,
    string Level);
