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
}
