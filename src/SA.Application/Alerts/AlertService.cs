using SA.Application.Abstractions;
using SA.Application.Common;
using SA.Contracts.Alerts;
using SA.Contracts.Common;
using SA.Domain.Alerts;
using SA.Domain.Common;
using SA.Domain.Entities.Alerts;

namespace SA.Application.Alerts;

/// <summary>
/// 提醒规则的增删改查与触发评估。
/// </summary>
/// <remarks>
/// <list type="number">
/// <item>规则数量受角色配额 <c>alert.max</c> 约束（超限返回 3002）；</item>
/// <item>阈值必填的类型若缺阈值直接拒绝，避免建出一条永远不会触发的规则；</item>
/// <item>触发判定是<b>纯函数</b>（<see cref="AlertEvaluator"/>），因此可以用构造数据逐条钉住边界；</item>
/// <item>触发后写通知并记录时间，冷却窗口由评估器统一控制，避免价格在阈值附近震荡产生刷屏。</item>
/// </list>
/// </remarks>
public sealed class AlertService(
    IAlertStore store,
    IInstrumentStore instruments,
    IQuoteSnapshotStore quotes,
    IDailyHistoryStore daily)
{
    /// <summary>
    /// 取规则列表。
    /// </summary>
    /// <param name="userId">当前用户。</param>
    /// <param name="quota">角色配额上限。</param>
    public async Task<ServiceResult<AlertRuleListDto>> GetRulesAsync(
        string userId,
        int quota,
        CancellationToken cancellationToken = default)
    {
        var rules = await store.GetRulesAsync(userId, cancellationToken).ConfigureAwait(false);
        var codes = rules.Select(rule => rule.Code).Distinct(StringComparer.Ordinal).ToList();
        var instrumentMap = await instruments.GetByCodesAsync(codes, cancellationToken).ConfigureAwait(false);
        var quoteMap = await quotes.GetByCodesAsync(codes, cancellationToken).ConfigureAwait(false);

        DateOnly? asOf = quoteMap.Count == 0 ? null : quoteMap.Values.Max(quote => quote.AsOf);

        var dtos = rules
            .Select(rule => new AlertRuleDto(
                Id: rule.Id,
                Code: rule.Code,
                Name: instrumentMap.TryGetValue(rule.Code, out var instrument) ? instrument.Name : rule.Code,
                RuleType: rule.RuleType,
                RuleTypeName: AlertRuleCatalog.NameOf(rule.RuleType),
                Threshold: Display.Round(rule.Threshold),
                ThresholdUnit: AlertRuleCatalog.Find(rule.RuleType)?.Unit,
                Note: rule.Note,
                Enabled: rule.Enabled,
                CreatedAt: SaTime.Format(rule.CreatedAt),
                LastTriggeredAt: rule.LastTriggeredAt is null ? null : SaTime.Format(rule.LastTriggeredAt.Value),
                TriggerCount: rule.TriggerCount))
            .ToList();

        return ServiceResult<AlertRuleListDto>.Success(new AlertRuleListDto(
            Rules: dtos,
            Total: rules.Count,
            Quota: quota,
            Types: AlertRuleCatalog.All
                .Select(definition => new AlertTypeOptionDto(definition.Type, definition.Name, definition.Unit, definition.Hint))
                .ToList(),
            AsOf: asOf is null ? null : SaTime.Format(asOf.Value)));
    }

    /// <summary>
    /// 新建规则。
    /// </summary>
    public async Task<ServiceResult<string>> CreateAsync(
        string userId,
        AlertRuleCreateRequest request,
        int quota,
        CancellationToken cancellationToken = default)
    {
        var code = request.Code?.Trim();
        if (string.IsNullOrEmpty(code))
        {
            return ServiceResult<string>.Fail(ErrorCode.InvalidParameter, "请提供证券代码");
        }

        var definition = AlertRuleCatalog.Find(request.RuleType);
        if (definition is null)
        {
            return ServiceResult<string>.Fail(ErrorCode.InvalidParameter, $"未知的规则类型：{request.RuleType}");
        }

        // 只接受股票池内的标的：避免把拼错的代码建成一条永不触发的规则
        var instrument = await instruments.FindAsync(code, cancellationToken).ConfigureAwait(false);
        if (instrument is null)
        {
            return ServiceResult<string>.Fail(ErrorCode.NotFound, $"未找到证券代码 {code}");
        }

        if (AlertRuleCatalog.RequiresThreshold(request.RuleType) && request.Threshold is null)
        {
            return ServiceResult<string>.Fail(
                ErrorCode.InvalidParameter,
                $"规则「{definition.Value.Name}」需要填写阈值（单位：{definition.Value.Unit}）");
        }

        if (request.Threshold is { } threshold && threshold <= 0)
        {
            return ServiceResult<string>.Fail(ErrorCode.InvalidParameter, "阈值必须大于 0");
        }

        var existing = await store.CountRulesAsync(userId, cancellationToken).ConfigureAwait(false);
        if (existing >= quota)
        {
            return ServiceResult<string>.Fail(
                ErrorCode.QuotaExceeded,
                $"提醒规则上限为 {quota} 条，当前 {existing} 条");
        }

        var rule = new AlertRule
        {
            Id = Guid.NewGuid().ToString("N"),
            UserId = userId,
            Code = code,
            RuleType = request.RuleType,
            Threshold = request.Threshold,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            Enabled = true,
            CreatedAt = SaTime.Now
        };

        await store.AddRuleAsync(rule, cancellationToken).ConfigureAwait(false);
        return ServiceResult<string>.Success(rule.Id);
    }

    /// <summary>修改规则（启停 / 改阈值 / 改备注）。</summary>
    public async Task<ServiceResult<int>> UpdateAsync(
        string userId,
        string ruleId,
        AlertRuleUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var rule = await store.FindRuleAsync(userId, ruleId, cancellationToken).ConfigureAwait(false);
        if (rule is null)
        {
            return ServiceResult<int>.Fail(ErrorCode.NotFound, "规则不存在");
        }

        if (request.Threshold is { } threshold && threshold <= 0)
        {
            return ServiceResult<int>.Fail(ErrorCode.InvalidParameter, "阈值必须大于 0");
        }

        if (request.Threshold is null
            && AlertRuleCatalog.RequiresThreshold(rule.RuleType)
            && rule.Threshold is null)
        {
            return ServiceResult<int>.Fail(
                ErrorCode.InvalidParameter,
                $"规则「{AlertRuleCatalog.NameOf(rule.RuleType)}」需要填写阈值");
        }

        var updated = await store.UpdateRuleAsync(
            userId,
            ruleId,
            request.Threshold,
            string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            request.Enabled,
            cancellationToken).ConfigureAwait(false);

        return updated
            ? ServiceResult<int>.Success(1)
            : ServiceResult<int>.Fail(ErrorCode.NotFound, "规则不存在");
    }

    /// <summary>删除规则（已产生的通知保留）。</summary>
    public async Task<ServiceResult<int>> RemoveAsync(
        string userId,
        string ruleId,
        CancellationToken cancellationToken = default)
    {
        var removed = await store.RemoveRuleAsync(userId, ruleId, cancellationToken).ConfigureAwait(false);
        return removed > 0
            ? ServiceResult<int>.Success(removed)
            : ServiceResult<int>.Fail(ErrorCode.NotFound, "规则不存在");
    }

    /// <summary>取通知列表。</summary>
    public async Task<ServiceResult<NotificationListDto>> GetNotificationsAsync(
        string userId,
        bool unreadOnly,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var items = await store.GetNotificationsAsync(userId, unreadOnly, limit, cancellationToken).ConfigureAwait(false);
        var unread = await store.CountUnreadAsync(userId, cancellationToken).ConfigureAwait(false);

        return ServiceResult<NotificationListDto>.Success(new NotificationListDto(
            Items: items.Select(item => new NotificationDto(
                Id: item.Id,
                Title: item.Title,
                Body: item.Body,
                Level: item.Level,
                Code: item.Code,
                RuleId: item.RuleId,
                IsRead: item.IsRead,
                CreatedAt: SaTime.Format(item.CreatedAt))).ToList(),
            Unread: unread,
            Total: items.Count));
    }

    /// <summary>标记已读（<c>ids</c> 为空表示全部已读）。</summary>
    public async Task<ServiceResult<int>> MarkReadAsync(
        string userId,
        IReadOnlyList<long>? ids,
        CancellationToken cancellationToken = default)
    {
        var affected = await store.MarkReadAsync(userId, ids ?? [], cancellationToken).ConfigureAwait(false);
        return ServiceResult<int>.Success(affected);
    }

    /// <summary>
    /// 评估一条规则是否触发。
    /// </summary>
    /// <param name="rule">规则。</param>
    /// <param name="quote">最新快照（无行情时为 null）。</param>
    /// <param name="closes">最近日线收盘序列（按日期升序；可为空）。</param>
    /// <param name="highs">最近日线最高序列。</param>
    /// <param name="lows">最近日线最低序列。</param>
    /// <param name="now">当前时间（用于冷却判断）。</param>
    public static AlertEvaluation Evaluate(
        AlertRule rule,
        Domain.Entities.Market.QuoteSnapshot? quote,
        IReadOnlyList<decimal> closes,
        IReadOnlyList<decimal> highs,
        IReadOnlyList<decimal> lows,
        DateTimeOffset now)
    {
        if (!rule.Enabled)
        {
            return AlertEvaluation.NotTriggered("规则已停用");
        }

        if (quote is null || quote.Price <= 0)
        {
            return AlertEvaluation.NotTriggered("暂无行情");
        }

        // 冷却窗口：同一规则在窗口内只触发一次，避免价格在阈值附近震荡时刷屏
        if (rule.LastTriggeredAt is { } last && now - last < AlertEvaluator.Cooldown)
        {
            var remaining = AlertEvaluator.Cooldown - (now - last);
            return AlertEvaluation.NotTriggered($"冷却中（剩余 {remaining.TotalMinutes:F0} 分钟）");
        }

        return AlertEvaluator.Evaluate(rule, quote, closes, highs, lows);
    }
}
