using System.Text;
using SA.Application.Abstractions;
using SA.Application.Authorization;
using SA.Application.Common;
using SA.Application.Market;
using SA.Contracts.Common;
using SA.Contracts.Screener;
using SA.Domain.Common;
using SA.Domain.Entities.Finance;
using SA.Domain.Entities.Market;

namespace SA.Application.Screener;

/// <summary>
/// 条件选股器。
/// </summary>
/// <remarks>
/// <para>
/// 全部基于内存中的全市场快照做筛选：横截面条件（市值、PE、涨跌幅…）只有在同一时点的
/// 全市场数据上才有意义，而快照恰好就是这个口径。因此本模块<b>不需要新的采集源</b>，
/// 也不做任何跨期计算（那是趋势模块的职责）。
/// </para>
/// <para>
/// 三条约定：
/// </para>
/// <list type="number">
/// <item>缺失值（PE 为负、无行情）<b>不参与</b>区间筛选：把亏损股当成「PE 很低」筛出来会得到反直觉的结果；</item>
/// <item>返回体带 <c>Applied</c>，逐条回显实际生效的条件，避免用户以为筛了其实字段名写错被忽略；</item>
/// <item>数据范围受限的角色只能选到自选内的标的（复用 <see cref="DataScopeFilter"/>）。</item>
/// </list>
/// </remarks>
public sealed class ScreenerService(
    MarketSnapshotCache cache,
    IQuoteSnapshotStore quotes,
    IInstrumentStore instruments,
    IFundamentalStore fundamentals,
    IFinanceStore finance,
    DataScopeFilter scopeFilter,
    IScreenerRunStore runs,
    IExportLogStore exportLogs)
{
    /// <summary>每页上限（与详细设计 §1.3 一致）。</summary>
    private const int MaxPageSize = 200;

    /// <summary>
    /// 非策略记录的保留条数（筛选日志上限）。
    /// </summary>
    /// <remarks>
    /// 只裁剪未保存为策略的记录：策略是用户显式保存的，不能被自动清理掉。
    /// 50 条足够覆盖「最近在筛什么」的追溯需求，又不至于让日志表无限增长。
    /// </remarks>
    private const int KeptRunLogs = 50;

    /// <summary>
    /// 预设条件。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 每个预设的说明都写清口径，用户不必猜「低估值」到底是 PE 低于多少。
    /// </para>
    /// <para>
    /// 预设有两种表达方式：<see cref="Filter"/>（对已算好的行做谓词）与
    /// <see cref="Ranges"/>/<see cref="Flags"/>/<see cref="Continuous"/>（条件片段）。
    /// 基本面预设走后者，因为它必须复用<b>同一套</b>区间与连续性判定逻辑——
    /// 若为预设另写一份判定，两处口径迟早会漂移。
    /// </para>
    /// </remarks>
    private sealed record Preset(
        string Key,
        string Name,
        string Description,
        Func<ScreenerRowDto, bool>? Filter = null,
        IReadOnlyList<ScreenerRange>? Ranges = null,
        IReadOnlyList<ScreenerFlag>? Flags = null,
        IReadOnlyList<ScreenerContinuous>? Continuous = null);

    private static readonly Preset[] Presets =
    [
        new("large-value", "大盘低估值", "总市值 ≥ 500 亿，PE(TTM) 在 0–15 之间，排除 ST 与停牌",
            Filter: row => row.Cap >= 500m && row.PeTtm is > 0 and <= 15m && !row.IsSt && row.Price > 0),
        new("active-turnover", "交投活跃", "换手率 ≥ 5% 且成交额 ≥ 5 亿，排除 ST 与停牌",
            Filter: row => row.Turnover >= 5m && row.Amount >= 5m && !row.IsSt && row.Price > 0),
        new("strong-today", "今日强势", "涨幅 ≥ 3% 且 量比 ≥ 1.5，排除 ST 与停牌",
            Filter: row => row.Pct >= 3m && row.VolRatio >= 1.5m && !row.IsSt && row.Price > 0),
        new("pullback", "今日回调", "跌幅 ≤ -3% 且换手率 ≥ 2%，排除 ST 与停牌（用于找错杀）",
            Filter: row => row.Pct <= -3m && row.Turnover >= 2m && !row.IsSt && row.Price > 0),
        new("small-cap", "小市值", "总市值 ≤ 100 亿、成交额 ≥ 1 亿、PE(TTM) > 0，排除 ST 与停牌",
            Filter: row => row.Cap <= 100m && row.Amount >= 1m && row.PeTtm is > 0 && !row.IsSt && row.Price > 0),
        new("volume-spike", "放量异动", "量比 ≥ 2 且换手率 ≥ 3%，排除 ST 与停牌",
            Filter: row => row.VolRatio >= 2m && row.Turnover >= 3m && !row.IsSt && row.Price > 0),

        // 价值投资预设（实施计划 §5.5）：只是把条件填进去，不做任何隐藏加权、不打分排序。
        // 排除金融业是显式的（Flags），不是隐式过滤——金融业的相关字段为空，
        // 不排除的话「连续 5 年 ROE ≥ 15%」会把银行按 null 静默筛掉，用户看不出原因。
        new("high-roe-low-debt", "高 ROE 低负债",
            "连续 5 年 ROE ≥ 15%，且最新一期资产负债率 ≤ 50%；排除金融业、ST 与停牌",
            Ranges: [new ScreenerRange(ScreenerFields.DebtRatio, null, 50m)],
            Flags: [new ScreenerFlag(ScreenerFields.IsSt, false), new ScreenerFlag(ScreenerFields.IncludeFinancials, false)],
            Continuous: [new ScreenerContinuous(ScreenerFields.Roe, 15m, null, 5)]),
        new("high-dividend", "高股息",
            "股息率 ≥ 4% 且连续 3 年 ROE ≥ 10%；排除金融业、ST 与停牌",
            Ranges: [new ScreenerRange(ScreenerFields.DividendYield, 4m, null)],
            Flags: [new ScreenerFlag(ScreenerFields.IsSt, false), new ScreenerFlag(ScreenerFields.IncludeFinancials, false)],
            Continuous: [new ScreenerContinuous(ScreenerFields.Roe, 10m, null, 3)]),
        new("steady-growth", "连续成长",
            "连续 3 年营收同比 ≥ 0% 且净利同比 ≥ 0%，排除 ST 与停牌",
            Flags: [new ScreenerFlag(ScreenerFields.IsSt, false)],
            Continuous:
            [
                new ScreenerContinuous(ScreenerFields.RevenueYoy, 0m, null, 3),
                new ScreenerContinuous(ScreenerFields.NetProfitYoy, 0m, null, 3)
            ])
    ];

    /// <summary>可筛选字段。</summary>
    private static readonly ScreenerFieldDto[] Fields =
    [
        new(ScreenerFields.Pct, "涨跌幅", "%", -20m, 20m),
        new(ScreenerFields.Turnover, "换手率", "%", 0m, 30m),
        new(ScreenerFields.VolRatio, "量比", "倍", 0m, 10m),
        new(ScreenerFields.Amount, "成交额", "亿元", 0m, 100m),
        new(ScreenerFields.Cap, "总市值", "亿元", 0m, 5000m),
        new(ScreenerFields.FloatCap, "流通市值", "亿元", 0m, 5000m),
        new(ScreenerFields.PeTtm, "PE(TTM)", "倍", 0m, 100m),
        new(ScreenerFields.Pb, "PB", "倍", 0m, 20m),
        new(ScreenerFields.Price, "现价", "元", 0m, 1000m),

        // 价值投资字段（实施计划 §5.2 / §5.5）：分组见 FieldGroups，
        // 默认只展开「质量」，其余折叠，避免一屏堆 30 个输入框
        new(ScreenerFields.Roe, "加权 ROE", "%", 0m, 30m),
        new(ScreenerFields.RoeDeducted, "扣非 ROE", "%", 0m, 30m),
        new(ScreenerFields.GrossMargin, "毛利率", "%", 0m, 80m),
        new(ScreenerFields.NetMargin, "净利率", "%", 0m, 50m),
        new(ScreenerFields.Roic, "ROIC", "%", 0m, 30m),
        new(ScreenerFields.DebtRatio, "资产负债率", "%", 0m, 100m),
        new(ScreenerFields.CurrentRatio, "流动比率", "倍", 0m, 10m),
        new(ScreenerFields.QuickRatio, "速动比率", "倍", 0m, 10m),
        new(ScreenerFields.InterestDebtRatio, "有息负债率", "%", 0m, 100m),
        new(ScreenerFields.InterestCoverageRatio, "利息保障倍数", "倍", 0m, 100m),
        new(ScreenerFields.OperatingCashFlowToRevenue, "经营现金流/营收", "倍", 0m, 1m),
        new(ScreenerFields.OperatingCashFlowToNetProfit, "经营现金流/净利", "倍", 0m, 2m),
        new(ScreenerFields.FreeCashFlow, "自由现金流", "亿元", 0m, 500m),
        new(ScreenerFields.InventoryTurnoverDays, "存货周转天数", "天", 0m, 365m),
        new(ScreenerFields.ReceivableTurnoverDays, "应收周转天数", "天", 0m, 365m),
        new(ScreenerFields.RevenueYoy, "营收同比", "%", -50m, 100m),
        new(ScreenerFields.NetProfitYoy, "净利同比", "%", -100m, 200m),
        new(ScreenerFields.DeductedNetProfitYoy, "扣非净利同比", "%", -100m, 200m),
        new(ScreenerFields.Eps, "每股收益", "元", 0m, 10m),
        new(ScreenerFields.Bps, "每股净资产", "元", 0m, 50m),
        new(ScreenerFields.DividendYield, "股息率", "%", 0m, 10m)
    ];

    /// <summary>
    /// 字段分组（实施计划 §5.5）。
    /// </summary>
    /// <remarks>
    /// 默认只展开「质量」组：一屏 30 个输入框会让用户无从下手，
    /// 而价值投资者最先看的就是 ROE 与毛利率。
    /// </remarks>
    private static readonly ScreenerFieldGroupDto[] FieldGroups =
    [
        new("quality", "质量",
            [ScreenerFields.Roe, ScreenerFields.RoeDeducted, ScreenerFields.GrossMargin,
             ScreenerFields.NetMargin, ScreenerFields.Roic],
            DefaultExpanded: true,
            Note: "ROE 为加权口径，扣非 ROE 剔除一次性损益。金融业的毛利率与 ROIC 不适用（上游不提供）。"),
        new("safety", "安全",
            [ScreenerFields.DebtRatio, ScreenerFields.CurrentRatio, ScreenerFields.QuickRatio,
             ScreenerFields.InterestDebtRatio, ScreenerFields.InterestCoverageRatio,
             ScreenerFields.OperatingCashFlowToNetProfit, ScreenerFields.FreeCashFlow],
            DefaultExpanded: false,
            Note: "资产负债率对金融业天然偏高（实测平安银行 90.91%），应与自身历史或同业比较，不宜套用统一阈值。"),
        new("growth", "成长",
            [ScreenerFields.RevenueYoy, ScreenerFields.NetProfitYoy, ScreenerFields.DeductedNetProfitYoy],
            DefaultExpanded: false),
        new("return", "回报",
            [ScreenerFields.Eps, ScreenerFields.Bps, ScreenerFields.DividendYield],
            DefaultExpanded: false,
            Note: "股息率来自业绩报表（RPT_LICO_FN_CPD 的 ZXGXL），与基本面报表的其它字段不同源。"),
        new("efficiency", "周转",
            [ScreenerFields.InventoryTurnoverDays, ScreenerFields.ReceivableTurnoverDays],
            DefaultExpanded: false),
        new("valuation", "估值与行情",
            [ScreenerFields.PeTtm, ScreenerFields.Pb, ScreenerFields.Cap, ScreenerFields.FloatCap,
             ScreenerFields.Price, ScreenerFields.Pct, ScreenerFields.Turnover,
             ScreenerFields.VolRatio, ScreenerFields.Amount],
            DefaultExpanded: false)
    ];

    /// <summary>可用于连续性条件的字段（只列基本面字段：年报序列才有意义）。</summary>
    private static readonly string[] ContinuousFields =
    [
        ScreenerFields.Roe, ScreenerFields.RoeDeducted, ScreenerFields.GrossMargin,
        ScreenerFields.NetMargin, ScreenerFields.Roic, ScreenerFields.DebtRatio,
        ScreenerFields.Eps, ScreenerFields.Bps,
        ScreenerFields.RevenueYoy, ScreenerFields.NetProfitYoy
    ];

    /// <summary>连续性条件可选年数。</summary>
    private static readonly int[] ContinuousYearOptions = [3, 5, 8];

    /// <summary>
    /// 口径提示，由后端下发（实施计划 §5.4）。
    /// </summary>
    private static readonly string[] CaliberNotes =
    [
        "金融业（银行/保险/证券）不适用毛利率、流动比率、速动比率、自由现金流、ROIC，这些字段上游返回空值；"
            + "按这些字段筛选会默认排除金融业，需要显式勾选「包含金融业」。",
        "资产负债率对金融业天然偏高（实测平安银行 90.91%，东芯股份 9.76%），"
            + "两者不可用同一阈值判断：应与自身历史或同业比较。",
        "退市与长期停牌标的在上游财报里仍有数据且数值异常"
            + "（实测 PT金田A 资产负债率 268.02%、神城A退 1607.40%）。基本面表按事实保留这些行，"
            + "但选股结果只包含有行情、且未被 ST/退市标记的标的，因此它们不会污染筛选结果。",
        "无财报数据的标的在结果中标为「无财报数据」，不会被当成 0 参与筛选。",
        "连续性条件只取年报，缺年报即中断（避免「2019 与 2025 都达标」被误判为连续）。",
        "股息率来自业绩报表，与其它基本面字段不同源。",
        "基本面按「最新已披露报告期」采集（实测 5,832 只上市 A 股；上游同一报告期另有约 7,600 行"
            + "是新三板与 IPO 申报主体，已在上游侧过滤掉）。"
    ];

    /// <summary>
    /// 取选股器元数据（字段、预设、板块、导出额度）。
    /// </summary>
    /// <param name="exportQuota">今日剩余导出次数；<c>-1</c> 表示未配置上限（不限）。</param>
    /// <param name="exportRowLimit">单次导出行数上限。</param>
    /// <param name="strategyQuota">可保存的策略数量上限；<c>-1</c> 表示不限。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<ServiceResult<ScreenerMetaDto>> GetMetaAsync(
        int exportQuota,
        int exportRowLimit,
        int strategyQuota,
        CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

        var boards = cache.Current.Instruments.Values
            .Select(instrument => instrument.Board)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(board => board, StringComparer.Ordinal)
            .ToList();

        return ServiceResult<ScreenerMetaDto>.Success(new ScreenerMetaDto(
            Fields: Fields,
            Presets: Presets.Select(preset => new ScreenerPresetDto(preset.Key, preset.Name, preset.Description)).ToList(),
            Boards: boards,
            ExportQuota: exportQuota,
            ExportRowLimit: exportRowLimit,
            StrategyQuota: strategyQuota,
            FieldGroups: FieldGroups,
            CaliberNotes: CaliberNotes,
            ContinuousFields: ContinuousFields,
            ContinuousYears: ContinuousYearOptions,
            FundamentalAsOf: cache.Current.Fundamentals.Count == 0
                ? null
                : SaTime.Format(cache.Current.Fundamentals.Values.Max(metric => metric.ReportDate))));
    }

    /// <summary>
    /// 执行筛选。
    /// </summary>
    /// <param name="request">筛选条件。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <remarks>
    /// 本方法<b>没有副作用</b>（不写筛选日志）：导出与分布统计都要用到同一套筛选结果，
    /// 若在筛选内部写日志，一次导出会额外留下一条「筛选」记录，日志会被噪音填满。
    /// 需要留痕时由调用方显式调用 <see cref="RecordRunAsync"/>。
    /// </remarks>
    public async Task<ServiceResult<ScreenerResultDto>> RunAsync(
        ScreenerRequest request,
        CancellationToken cancellationToken = default)
    {
        var filtered = await FilterAsync(request, cancellationToken).ConfigureAwait(false);
        // 元组是值类型：ServiceResult 的 Value 对它不可能是 null，因此只判 Ok
        if (!filtered.Ok)
        {
            return ServiceResult<ScreenerResultDto>.Fail(filtered.Error, filtered.Message ?? "筛选失败");
        }

        var (rows, applied, asOf, scopeNote) = filtered.Value;
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize <= 0 ? 50 : request.PageSize, 1, MaxPageSize);

        return ServiceResult<ScreenerResultDto>.Success(new ScreenerResultDto(
            Total: rows.Count,
            Page: page,
            PageSize: pageSize,
            Rows: rows.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            AsOf: asOf,
            Applied: applied,
            ScopeNote: scopeNote));
    }

    /// <summary>
    /// 执行筛选并计入筛选日志（用户在界面上点「开始筛选」走这条路径）。
    /// </summary>
    /// <param name="request">筛选条件。</param>
    /// <param name="userId">执行者。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<ServiceResult<ScreenerResultDto>> RunAndRecordAsync(
        ScreenerRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(request, cancellationToken).ConfigureAwait(false);
        if (!result.Ok || result.Value is null)
        {
            return result;
        }

        await RecordRunAsync(request, userId, result.Value.Total, cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// 写一条筛选日志。
    /// </summary>
    /// <remarks>
    /// 日志只保留最近 <see cref="KeptRunLogs"/> 条<b>非策略</b>记录（策略永久保留）：
    /// 用户每次调整条件都会产生一条，不裁剪会让日志表快速膨胀，而旧日志的追溯价值很低。
    /// </remarks>
    public async Task<long> RecordRunAsync(
        ScreenerRequest request,
        string userId,
        int total,
        CancellationToken cancellationToken = default)
    {
        var id = await runs.AddAsync(new Domain.Entities.Screener.ScreenerRun
        {
            UserId = userId,
            RequestJson = ScreenerRunCodec.Serialize(request),
            Summary = ScreenerRunCodec.Summarize(request),
            Total = total,
            PresetKey = string.IsNullOrWhiteSpace(request.Preset) ? null : request.Preset,
            CreatedAt = SaTime.Now
        }, cancellationToken).ConfigureAwait(false);

        await runs.TrimAsync(userId, KeptRunLogs, cancellationToken).ConfigureAwait(false);
        return id;
    }

    /// <summary>
    /// 计算结果的分布统计（分位数 + 直方图）。
    /// </summary>
    /// <param name="request">筛选条件（与列表用同一套条件）。</param>
    /// <param name="field">统计字段。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<ServiceResult<Contracts.Screener.ScreenerDistributionDto>> GetDistributionAsync(
        ScreenerRequest request,
        string field,
        CancellationToken cancellationToken = default)
    {
        if (DistributionStats.Resolve(field) is null)
        {
            return ServiceResult<Contracts.Screener.ScreenerDistributionDto>.Fail(
                ErrorCode.InvalidParameter, $"不支持分布统计的字段：{field}");
        }

        var filtered = await FilterAsync(request with { Page = 1, PageSize = MaxPageSize }, cancellationToken)
            .ConfigureAwait(false);
        if (!filtered.Ok)
        {
            return ServiceResult<Contracts.Screener.ScreenerDistributionDto>.Fail(
                filtered.Error, filtered.Message ?? "筛选失败");
        }

        var stats = DistributionStats.Compute(filtered.Value.Rows, field);
        return stats is null
            ? ServiceResult<Contracts.Screener.ScreenerDistributionDto>.Fail(
                ErrorCode.InvalidParameter, $"不支持分布统计的字段：{field}")
            : ServiceResult<Contracts.Screener.ScreenerDistributionDto>.Success(stats);
    }

    /// <summary>取筛选日志（<paramref name="strategiesOnly"/> 为 true 时只取已保存的策略）。</summary>
    public async Task<ServiceResult<ScreenerRunListDto>> GetRunsAsync(
        string userId,
        bool strategiesOnly,
        int limit,
        int strategyQuota,
        CancellationToken cancellationToken = default)
    {
        var items = await runs.GetRecentAsync(userId, strategiesOnly, limit, cancellationToken).ConfigureAwait(false);
        var strategies = await runs.CountStrategiesAsync(userId, cancellationToken).ConfigureAwait(false);

        return ServiceResult<ScreenerRunListDto>.Success(new ScreenerRunListDto(
            Items: items.Select(ToRunDto).ToList(),
            Strategies: strategies,
            Quota: strategyQuota));
    }

    /// <summary>
    /// 把一条记录保存为策略（或直接用新条件创建策略）。
    /// </summary>
    public async Task<ServiceResult<ScreenerRunDto>> SaveStrategyAsync(
        string userId,
        ScreenerStrategySaveRequest request,
        int strategyQuota,
        CancellationToken cancellationToken = default)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return ServiceResult<ScreenerRunDto>.Fail(ErrorCode.InvalidParameter, "策略名不能为空");
        }

        if (name.Length > 64)
        {
            return ServiceResult<ScreenerRunDto>.Fail(ErrorCode.InvalidParameter, "策略名不能超过 64 个字符");
        }

        // 同名策略会让「按名字回放」变得不确定，直接拒绝比事后困惑好
        if (await runs.FindByNameAsync(userId, name, cancellationToken).ConfigureAwait(false) is not null)
        {
            return ServiceResult<ScreenerRunDto>.Fail(ErrorCode.InvalidParameter, $"已存在同名策略「{name}」");
        }

        var current = await runs.CountStrategiesAsync(userId, cancellationToken).ConfigureAwait(false);
        if (strategyQuota >= 0 && current >= strategyQuota)
        {
            return ServiceResult<ScreenerRunDto>.Fail(
                ErrorCode.QuotaExceeded, $"策略数量上限为 {strategyQuota} 条，当前 {current} 条");
        }

        if (request.RunId is { } runId)
        {
            var row = await runs.FindAsync(userId, runId, cancellationToken).ConfigureAwait(false);
            if (row is null)
            {
                return ServiceResult<ScreenerRunDto>.Fail(ErrorCode.NotFound, "记录不存在");
            }

            await runs.RenameAsync(userId, runId, name, cancellationToken).ConfigureAwait(false);
            row.Name = name;
            return ServiceResult<ScreenerRunDto>.Success(ToRunDto(row));
        }

        if (request.Request is null)
        {
            return ServiceResult<ScreenerRunDto>.Fail(ErrorCode.InvalidParameter, "需要提供 runId 或 request");
        }

        // 直接用条件创建策略：先用该条件算一次命中数，保证策略里记的是真实结果
        var probe = await RunAsync(request.Request, cancellationToken).ConfigureAwait(false);
        if (!probe.Ok || probe.Value is null)
        {
            return ServiceResult<ScreenerRunDto>.Fail(probe.Error, probe.Message ?? "条件无效");
        }

        var id = await runs.AddAsync(new Domain.Entities.Screener.ScreenerRun
        {
            UserId = userId,
            RequestJson = ScreenerRunCodec.Serialize(request.Request),
            Summary = ScreenerRunCodec.Summarize(request.Request),
            Total = probe.Value.Total,
            Name = name,
            PresetKey = string.IsNullOrWhiteSpace(request.Request.Preset) ? null : request.Request.Preset,
            CreatedAt = SaTime.Now
        }, cancellationToken).ConfigureAwait(false);

        return ServiceResult<ScreenerRunDto>.Success(new ScreenerRunDto(
            Id: id,
            Name: name,
            IsStrategy: true,
            Summary: ScreenerRunCodec.Summarize(request.Request),
            Total: probe.Value.Total,
            PresetKey: request.Request.Preset,
            CreatedAt: SaTime.Format(SaTime.Now)));
    }

    /// <summary>重命名策略；<c>name</c> 为空表示取消策略标记（退回普通日志）。</summary>
    public async Task<ServiceResult<int>> RenameStrategyAsync(
        string userId,
        long id,
        string? name,
        CancellationToken cancellationToken = default)
    {
        var trimmed = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        if (trimmed is not null && trimmed.Length > 64)
        {
            return ServiceResult<int>.Fail(ErrorCode.InvalidParameter, "策略名不能超过 64 个字符");
        }

        if (trimmed is not null
            && await runs.FindByNameAsync(userId, trimmed, cancellationToken).ConfigureAwait(false) is { } existing
            && existing.Id != id)
        {
            return ServiceResult<int>.Fail(ErrorCode.InvalidParameter, $"已存在同名策略「{trimmed}」");
        }

        var updated = await runs.RenameAsync(userId, id, trimmed, cancellationToken).ConfigureAwait(false);
        return updated
            ? ServiceResult<int>.Success(1)
            : ServiceResult<int>.Fail(ErrorCode.NotFound, "记录不存在");
    }

    /// <summary>删除一条记录（策略或日志）。</summary>
    public async Task<ServiceResult<int>> DeleteRunAsync(
        string userId,
        long id,
        CancellationToken cancellationToken = default)
    {
        var removed = await runs.DeleteAsync(userId, id, cancellationToken).ConfigureAwait(false);
        return removed > 0
            ? ServiceResult<int>.Success(removed)
            : ServiceResult<int>.Fail(ErrorCode.NotFound, "记录不存在");
    }

    /// <summary>回放策略：按记录里的条件重新筛一次（并计入筛选日志）。</summary>
    public async Task<ServiceResult<ScreenerResultDto>> ReplayAsync(
        string userId,
        long id,
        CancellationToken cancellationToken = default)
    {
        var row = await runs.FindAsync(userId, id, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            return ServiceResult<ScreenerResultDto>.Fail(ErrorCode.NotFound, "记录不存在");
        }

        var request = ScreenerRunCodec.Deserialize(row.RequestJson);
        if (request is null)
        {
            return ServiceResult<ScreenerResultDto>.Fail(ErrorCode.InvalidParameter, "记录中的条件已损坏，无法回放");
        }

        return await RunAndRecordAsync(request, userId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>取导出记录（选股器页的「导出记录」区块）。</summary>
    public async Task<ServiceResult<IReadOnlyList<Contracts.Screener.ExportLogDto>>> GetExportLogsAsync(
        string userId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var rows = await exportLogs.GetRecentAsync(userId, limit, cancellationToken).ConfigureAwait(false);

        return ServiceResult<IReadOnlyList<Contracts.Screener.ExportLogDto>>.Success(
            rows.Select(row => new Contracts.Screener.ExportLogDto(
                Id: row.Id,
                Dataset: row.Dataset,
                Format: row.Format,
                Rows: row.Rows,
                CreatedAt: SaTime.Format(row.CreatedAt))).ToList());
    }

    /// <summary>写一条导出记录（导出成功后调用）。</summary>
    public Task RecordExportAsync(
        string userId,
        string dataset,
        int rows,
        CancellationToken cancellationToken = default) =>
        exportLogs.AddAsync(new Domain.Entities.System.ExportLog
        {
            UserId = userId,
            Dataset = dataset,
            Format = "csv",
            Rows = rows,
            CreatedAt = SaTime.Now
        }, cancellationToken);

    private static ScreenerRunDto ToRunDto(Domain.Entities.Screener.ScreenerRun row) =>
        new(
            Id: row.Id,
            Name: row.Name,
            IsStrategy: row.Name is not null,
            Summary: row.Summary,
            Total: row.Total,
            PresetKey: row.PresetKey,
            CreatedAt: SaTime.Format(row.CreatedAt));

    /// <summary>
    /// 筛选的核心：返回全部命中（不分页）与生效条件说明。
    /// </summary>
    /// <remarks>
    /// 元组元素带名字，且三处（返回类型、失败、成功）必须用同一个具名元组类型：
    /// 具名与不具名的元组在 C# 里是不同类型，混用会直接编译失败。
    /// </remarks>
    private async Task<ServiceResult<(List<ScreenerRowDto> Rows, List<string> Applied, string? AsOf, string? ScopeNote)>>
        FilterAsync(ScreenerRequest request, CancellationToken cancellationToken)
    {
        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

        var snapshot = cache.Current;
        if (snapshot.Rows.Count == 0)
        {
            return ServiceResult<(List<ScreenerRowDto> Rows, List<string> Applied, string? AsOf, string? ScopeNote)>.Fail(
                ErrorCode.DataNotReady, "全市场快照正在采集，请稍后重试");
        }

        var allowed = await scopeFilter.AllowedAsync(cancellationToken).ConfigureAwait(false);

        var dividends = snapshot.Dividends;
        var rows = snapshot.Rows
            .Where(row => DataScopeFilter.IsVisible(allowed, row.Code))
            .Select(row => ToRow(row, snapshot, dividends))
            .Where(row => row is not null)
            .Select(row => row!)
            .ToList();

        var applied = new List<string>();

        // 预设优先：用预设时忽略自定义条件（否则两者叠加的结果无法解释）
        if (!string.IsNullOrWhiteSpace(request.Preset))
        {
            var preset = Presets.FirstOrDefault(item => item.Key == request.Preset);
            if (preset is null)
            {
                return ServiceResult<(List<ScreenerRowDto> Rows, List<string> Applied, string? AsOf, string? ScopeNote)>.Fail(
                    ErrorCode.InvalidParameter, $"未知的预设条件：{request.Preset}");
            }

            applied.Add($"预设「{preset.Name}」：{preset.Description}");

            if (preset.Filter is not null)
            {
                rows = rows.Where(preset.Filter).ToList();
            }

            // 预设里的条件片段走与自定义条件完全相同的代码路径：
            // 另写一份判定会让两处口径迟早漂移
            rows = ApplyRanges(rows, preset.Ranges, applied);
            rows = ApplyFlags(rows, preset.Flags, applied);
            rows = ApplyContinuous(rows, preset.Continuous, snapshot, applied);
        }
        else
        {
            rows = ApplyRanges(rows, request.Ranges, applied);
            rows = ApplyEnums(rows, request.Enums, applied);
            rows = ApplyFlags(rows, request.Flags, applied);
            rows = ApplyContinuous(rows, request.Continuous, snapshot, applied);
        }

        var sortBy = string.IsNullOrWhiteSpace(request.SortBy) ? ScreenerFields.Amount : request.SortBy;
        rows = Sort(rows, sortBy, request.SortDesc);

        return ServiceResult<(List<ScreenerRowDto> Rows, List<string> Applied, string? AsOf, string? ScopeNote)>.Success((
            rows,
            applied.Count == 0 ? ["未设置条件：返回全市场按成交额倒序的结果"] : applied,
            snapshot.AsOf == DateOnly.MinValue ? null : SaTime.Format(snapshot.AsOf),
            allowed is null ? null : "当前账号的数据范围为「仅自选股」，结果已按自选范围过滤。"));
    }

    /// <summary>
    /// 导出命中结果为 CSV（受 <c>export.data</c> 权限与每日配额约束）。
    /// </summary>
    /// <remarks>
    /// CSV 用 UTF-8 BOM：Excel 打开无 BOM 的 UTF-8 CSV 会把中文显示成乱码。
    /// 导出条数上限同样由调用方（端点）按配额传入，本方法只负责生成内容。
    /// </remarks>
    public async Task<ServiceResult<(byte[] Content, string FileName, int Rows)>> ExportAsync(
        ScreenerRequest request,
        int maxRows,
        CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(
            request with { Page = 1, PageSize = Math.Clamp(maxRows, 1, MaxPageSize) },
            cancellationToken).ConfigureAwait(false);

        if (!result.Ok || result.Value is null)
        {
            return ServiceResult<(byte[], string, int)>.Fail(result.Error, result.Message ?? "导出失败");
        }

        var rows = result.Value.Rows;
        var builder = new StringBuilder();

        // 表头与筛选条件一并写入：导出的文件离开系统后仍能自证口径
        builder.AppendLine("# 股析 SA 选股结果导出");
        builder.AppendLine($"# 数据时间：{result.Value.AsOf ?? "—"}");
        foreach (var note in result.Value.Applied)
        {
            builder.AppendLine($"# 条件：{note}");
        }

        if (result.Value.ScopeNote is not null)
        {
            builder.AppendLine($"# 范围：{result.Value.ScopeNote}");
        }

        builder.AppendLine("代码,名称,板块,行业,现价,涨跌幅%,换手率%,量比,成交额(亿),PE(TTM),PB,总市值(亿),流通市值(亿),ST");

        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(',',
                Csv(row.Code),
                Csv(row.Name),
                Csv(row.Board),
                Csv(row.Industry ?? string.Empty),
                row.Price,
                row.Pct,
                row.Turnover,
                row.VolRatio,
                row.Amount,
                row.PeTtm?.ToString() ?? string.Empty,
                row.Pb?.ToString() ?? string.Empty,
                row.Cap,
                row.FloatCap,
                row.IsSt ? "是" : "否"));
        }

        var content = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
        var fileName = $"screener-{SaTime.Now:yyyyMMdd-HHmmss}.csv";

        return ServiceResult<(byte[], string, int)>.Success((content, fileName, rows.Count));
    }

    /* ------------------------------------------------------------------
       条件应用
       ------------------------------------------------------------------ */

    private static List<ScreenerRowDto> ApplyRanges(
        List<ScreenerRowDto> rows,
        IReadOnlyList<ScreenerRange>? ranges,
        List<string> applied)
    {
        if (ranges is null || ranges.Count == 0)
        {
            return rows;
        }

        foreach (var range in ranges)
        {
            if (!IsRangeField(range.Field))
            {
                // 未知字段不静默忽略：写进 Applied 让人看到「这条没生效」
                applied.Add($"忽略未知字段：{range.Field}");
                continue;
            }

            var before = rows.Count;
            rows = rows.Where(row => InRange(Value(row, range.Field), range.Min, range.Max)).ToList();

            applied.Add(
                $"{FieldName(range.Field)} ∈ [{Format(range.Min)}, {Format(range.Max)}]"
                + $"（命中 {rows.Count}，筛掉 {before - rows.Count}）");
        }

        return rows;
    }

    private static List<ScreenerRowDto> ApplyEnums(
        List<ScreenerRowDto> rows,
        IReadOnlyList<ScreenerEnum>? enums,
        List<string> applied)
    {
        if (enums is null || enums.Count == 0)
        {
            return rows;
        }

        foreach (var item in enums)
        {
            if (item.Values is null || item.Values.Count == 0)
            {
                continue;
            }

            var values = item.Values.ToHashSet(StringComparer.Ordinal);

            switch (item.Field)
            {
                case ScreenerFields.Board:
                    rows = rows.Where(row => values.Contains(row.Board)).ToList();
                    applied.Add($"板块 ∈ [{string.Join('/', values)}]（命中 {rows.Count}）");
                    break;

                case ScreenerFields.Industry:
                    rows = rows.Where(row => row.Industry is not null && values.Contains(row.Industry)).ToList();
                    applied.Add($"行业 ∈ [{string.Join('/', values)}]（命中 {rows.Count}）");
                    break;

                default:
                    applied.Add($"忽略未知枚举字段：{item.Field}");
                    break;
            }
        }

        return rows;
    }

    private static List<ScreenerRowDto> ApplyFlags(
        List<ScreenerRowDto> rows,
        IReadOnlyList<ScreenerFlag>? flags,
        List<string> applied)
    {
        if (flags is null || flags.Count == 0)
        {
            return rows;
        }

        foreach (var flag in flags)
        {
            switch (flag.Field)
            {
                case ScreenerFields.IsSt:
                    // 默认行为是「排除 ST」：绝大多数选股场景都不想要 ST，因此勾选语义是「保留 ST」
                    rows = flag.Value
                        ? rows.Where(row => row.IsSt).ToList()
                        : rows.Where(row => !row.IsSt).ToList();

                    applied.Add(flag.Value ? "仅保留 ST 标的" : "排除 ST 标的");
                    break;

                case ScreenerFields.IncludeFinancials:
                    // 显式开关而不是隐式过滤：金融业的毛利率/流动比率/ROIC 天然为空，
                    // 按这些字段筛选会静默排除整个银行保险板块，用户看不出原因（实施计划 §2.3 第 1 条）
                    var before = rows.Count;
                    if (!flag.Value)
                    {
                        rows = rows.Where(row => !row.IsFinancial).ToList();
                        applied.Add($"排除金融业（银行/保险/证券），筛掉 {before - rows.Count} 只");
                    }
                    else
                    {
                        applied.Add("包含金融业（其毛利率/流动比率/ROIC 等字段为空，属行业口径不同，不是数据缺失）");
                    }

                    break;

                default:
                    applied.Add($"忽略未知布尔字段：{flag.Field}");
                    break;
            }
        }

        return rows;
    }

    /// <summary>
    /// 应用连续性条件（「连续 N 年 …」）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 三条规则（实施计划 §5.3）：只取年报、缺年报即中断、任一年缺值即不满足。
    /// 判定本身在领域层 <see cref="ContinuousConditionRules"/>，这里只负责取序列与写回显。
    /// </para>
    /// <para>
    /// 历史数据不足的标的<b>不静默排除</b>：那会让用户以为「全市场都筛过了」。
    /// 这里照常排除（它确实不满足条件），但结果行带 <c>FundamentalYears</c>，
    /// 界面据此提示「历史数据不足（已有 M/N 年）」。
    /// </para>
    /// </remarks>
    private static List<ScreenerRowDto> ApplyContinuous(
        List<ScreenerRowDto> rows,
        IReadOnlyList<ScreenerContinuous>? conditions,
        MarketSnapshotCache.Snapshot snapshot,
        List<string> applied)
    {
        if (conditions is null || conditions.Count == 0)
        {
            return rows;
        }

        foreach (var condition in conditions)
        {
            var selector = ContinuousSelector(condition.Field);
            if (selector is null)
            {
                applied.Add($"忽略不支持连续性的字段：{condition.Field}");
                continue;
            }

            var years = Math.Clamp(condition.Years, 1, 20);
            var before = rows.Count;
            var insufficient = 0;

            rows = rows.Where(row =>
            {
                var annual = snapshot.AnnualFundamentals.TryGetValue(row.Code, out var series) ? series : null;
                var evaluation = ContinuousConditionRules.Evaluate(
                    annual ?? [], selector, condition.Min, condition.Max, years);

                if (evaluation.InsufficientHistory(years))
                {
                    insufficient++;
                }

                return evaluation.Satisfied;
            }).ToList();

            applied.Add(
                $"{FieldName(condition.Field)} 连续 {years} 年 ∈ [{Format(condition.Min)}, {Format(condition.Max)}]"
                + $"（命中 {rows.Count}，筛掉 {before - rows.Count}，其中 {insufficient} 只历史数据不足）");
        }

        return rows;
    }

    /// <summary>连续性条件支持的字段取值器；不支持的字段返回 null。</summary>
    private static Func<FundamentalMetric, decimal?>? ContinuousSelector(string field) => field switch
    {
        ScreenerFields.Roe => metric => metric.RoeWeighted,
        ScreenerFields.RoeDeducted => metric => metric.RoeDeducted,
        ScreenerFields.GrossMargin => metric => metric.GrossMargin,
        ScreenerFields.NetMargin => metric => metric.NetMargin,
        ScreenerFields.Roic => metric => metric.Roic,
        ScreenerFields.DebtRatio => metric => metric.DebtRatio,
        ScreenerFields.Eps => metric => metric.Eps,
        ScreenerFields.Bps => metric => metric.Bps,
        ScreenerFields.RevenueYoy => metric => metric.RevenueYoy,
        ScreenerFields.NetProfitYoy => metric => metric.NetProfitYoy,
        _ => null
    };

    private static List<ScreenerRowDto> Sort(List<ScreenerRowDto> rows, string sortBy, bool desc)
    {
        Func<ScreenerRowDto, decimal> selector = sortBy switch
        {
            ScreenerFields.Pct => row => row.Pct,
            ScreenerFields.Turnover => row => row.Turnover,
            ScreenerFields.VolRatio => row => row.VolRatio ?? -1m,
            ScreenerFields.Cap => row => row.Cap,
            ScreenerFields.FloatCap => row => row.FloatCap,
            // 缺失估值的标的排在最后：用 -1 占位，倒序时自然落到末尾
            ScreenerFields.PeTtm => row => row.PeTtm ?? -1m,
            ScreenerFields.Pb => row => row.Pb ?? -1m,
            ScreenerFields.Price => row => row.Price,

            // 基本面字段：同样用 -1 占位，缺失的排到最后
            ScreenerFields.Roe => row => row.Roe ?? -1m,
            ScreenerFields.RoeDeducted => row => row.RoeDeducted ?? -1m,
            ScreenerFields.GrossMargin => row => row.GrossMargin ?? -1m,
            ScreenerFields.NetMargin => row => row.NetMargin ?? -1m,
            ScreenerFields.Roic => row => row.Roic ?? -1m,
            ScreenerFields.DebtRatio => row => row.DebtRatio ?? -1m,
            ScreenerFields.CurrentRatio => row => row.CurrentRatio ?? -1m,
            ScreenerFields.QuickRatio => row => row.QuickRatio ?? -1m,
            ScreenerFields.InterestDebtRatio => row => row.InterestDebtRatio ?? -1m,
            ScreenerFields.InterestCoverageRatio => row => row.InterestCoverageRatio ?? -1m,
            ScreenerFields.OperatingCashFlowToRevenue => row => row.OperatingCashFlowToRevenue ?? -1m,
            ScreenerFields.OperatingCashFlowToNetProfit => row => row.OperatingCashFlowToNetProfit ?? -1m,
            ScreenerFields.FreeCashFlow => row => row.FreeCashFlow ?? -1m,
            ScreenerFields.InventoryTurnoverDays => row => row.InventoryTurnoverDays ?? -1m,
            ScreenerFields.ReceivableTurnoverDays => row => row.ReceivableTurnoverDays ?? -1m,
            ScreenerFields.RevenueYoy => row => row.RevenueYoy ?? -1m,
            ScreenerFields.NetProfitYoy => row => row.NetProfitYoy ?? -1m,
            ScreenerFields.DeductedNetProfitYoy => row => row.DeductedNetProfitYoy ?? -1m,
            ScreenerFields.Eps => row => row.Eps ?? -1m,
            ScreenerFields.Bps => row => row.Bps ?? -1m,
            ScreenerFields.DividendYield => row => row.DividendYield ?? -1m,

            _ => row => row.Amount
        };

        return desc
            ? rows.OrderByDescending(selector).ThenBy(row => row.Code, StringComparer.Ordinal).ToList()
            : rows.OrderBy(selector).ThenBy(row => row.Code, StringComparer.Ordinal).ToList();
    }

    private static bool IsRangeField(string field) =>
        field is ScreenerFields.Pct or ScreenerFields.Turnover or ScreenerFields.VolRatio
            or ScreenerFields.Amount or ScreenerFields.Cap or ScreenerFields.FloatCap
            or ScreenerFields.PeTtm or ScreenerFields.Pb or ScreenerFields.Price
            or ScreenerFields.Roe or ScreenerFields.RoeDeducted or ScreenerFields.GrossMargin
            or ScreenerFields.NetMargin or ScreenerFields.Roic or ScreenerFields.DebtRatio
            or ScreenerFields.CurrentRatio or ScreenerFields.QuickRatio
            or ScreenerFields.InterestDebtRatio or ScreenerFields.InterestCoverageRatio
            or ScreenerFields.OperatingCashFlowToRevenue or ScreenerFields.OperatingCashFlowToNetProfit
            or ScreenerFields.FreeCashFlow or ScreenerFields.InventoryTurnoverDays
            or ScreenerFields.ReceivableTurnoverDays or ScreenerFields.RevenueYoy
            or ScreenerFields.NetProfitYoy or ScreenerFields.DeductedNetProfitYoy
            or ScreenerFields.Eps or ScreenerFields.Bps or ScreenerFields.DividendYield;

    /// <summary>
    /// 取字段值。缺失返回 null 而不是 0：亏损股的 PE、无财报标的的 ROE
    /// 都必须能区分于「真的是 0」。
    /// </summary>
    private static decimal? Value(ScreenerRowDto row, string field) => field switch
    {
        ScreenerFields.Pct => row.Pct,
        ScreenerFields.Turnover => row.Turnover,
        ScreenerFields.VolRatio => row.VolRatio,
        ScreenerFields.Amount => row.Amount,
        ScreenerFields.Cap => row.Cap,
        ScreenerFields.FloatCap => row.FloatCap,
        ScreenerFields.PeTtm => row.PeTtm,
        ScreenerFields.Pb => row.Pb,
        ScreenerFields.Price => row.Price,

        ScreenerFields.Roe => row.Roe,
        ScreenerFields.RoeDeducted => row.RoeDeducted,
        ScreenerFields.GrossMargin => row.GrossMargin,
        ScreenerFields.NetMargin => row.NetMargin,
        ScreenerFields.Roic => row.Roic,
        ScreenerFields.DebtRatio => row.DebtRatio,
        ScreenerFields.CurrentRatio => row.CurrentRatio,
        ScreenerFields.QuickRatio => row.QuickRatio,
        ScreenerFields.InterestDebtRatio => row.InterestDebtRatio,
        ScreenerFields.InterestCoverageRatio => row.InterestCoverageRatio,
        ScreenerFields.OperatingCashFlowToRevenue => row.OperatingCashFlowToRevenue,
        ScreenerFields.OperatingCashFlowToNetProfit => row.OperatingCashFlowToNetProfit,
        ScreenerFields.FreeCashFlow => row.FreeCashFlow,
        ScreenerFields.InventoryTurnoverDays => row.InventoryTurnoverDays,
        ScreenerFields.ReceivableTurnoverDays => row.ReceivableTurnoverDays,
        ScreenerFields.RevenueYoy => row.RevenueYoy,
        ScreenerFields.NetProfitYoy => row.NetProfitYoy,
        ScreenerFields.DeductedNetProfitYoy => row.DeductedNetProfitYoy,
        ScreenerFields.Eps => row.Eps,
        ScreenerFields.Bps => row.Bps,
        ScreenerFields.DividendYield => row.DividendYield,
        _ => null
    };

    /// <summary>
    /// 区间判定：值为 null（缺失）时一律不命中，避免把亏损股当作「低 PE」筛出来。
    /// </summary>
    private static bool InRange(decimal? value, decimal? min, decimal? max)
    {
        if (value is null)
        {
            return false;
        }

        if (min is not null && value < min)
        {
            return false;
        }

        return max is null || value <= max;
    }

    private static string FieldName(string field) =>
        Fields.FirstOrDefault(item => item.Field == field)?.Name ?? field;

    private static string Format(decimal? value) => value?.ToString("0.##") ?? "不限";

    /// <summary>CSV 字段转义：含逗号、引号或换行时用双引号包裹并转义内部引号。</summary>
    private static string Csv(string value)
    {
        if (value.IndexOfAny([',', '"', '\n', '\r']) < 0)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static ScreenerRowDto? ToRow(
        QuoteSnapshot row,
        MarketSnapshotCache.Snapshot snapshot,
        IReadOnlyDictionary<string, decimal?> dividends)
    {
        // 无价格的标的（停牌 / 退市）不进入选股结果：它们的比率字段都是 0，会污染排序与统计
        if (row.Price <= 0)
        {
            return null;
        }

        snapshot.Instruments.TryGetValue(row.Code, out var instrument);

        // 基本面可能缺失（新股、未采集）：此时各字段为 null，
        // 区间筛选按「不满足」处理，结果行上标 HasFundamental=false 供界面提示
        snapshot.Fundamentals.TryGetValue(row.Code, out var fundamental);
        snapshot.AnnualFundamentals.TryGetValue(row.Code, out var annual);
        var years = AnnualYears(annual);

        return new ScreenerRowDto(
            Code: row.Code,
            Name: instrument?.Name ?? row.Code,
            Board: instrument?.Board ?? MarketCodes.BoardOf(row.Code),
            Industry: instrument?.Industry,
            Price: Display.Round(row.Price),
            Pct: Display.Round(row.Pct),
            Turnover: Display.Round(row.Turnover),
            // 量比 0 表示上游未提供：选股结果里显示「—」，排序时也不会被当成最小的真实值
            VolRatio: row.VolRatio <= 0 ? null : Display.Round(row.VolRatio),
            Amount: Display.ToYi(row.Amount),
            PeTtm: row.PeTtm > 0 ? Display.Round(row.PeTtm) : null,
            Pb: row.Pb > 0 ? Display.Round(row.Pb) : null,
            Cap: Display.ToYi(row.MarketCap),
            FloatCap: Display.ToYi(row.FloatCap),
            IsSt: instrument?.IsSt ?? false,
            HasFundamental: fundamental is not null,
            FundamentalYears: years,
            FundamentalAsOf: fundamental is null ? null : SaTime.Format(fundamental.ReportDate),
            IsFinancial: FundamentalOrgTypes.IsFinancial(fundamental?.OrgType),
            Roe: Round(fundamental?.RoeWeighted),
            RoeDeducted: Round(fundamental?.RoeDeducted),
            GrossMargin: Round(fundamental?.GrossMargin),
            NetMargin: Round(fundamental?.NetMargin),
            Roic: Round(fundamental?.Roic),
            DebtRatio: Round(fundamental?.DebtRatio),
            CurrentRatio: Round(fundamental?.CurrentRatio),
            QuickRatio: Round(fundamental?.QuickRatio),
            InterestDebtRatio: Round(fundamental?.InterestDebtRatio),
            InterestCoverageRatio: Round(fundamental?.InterestCoverageRatio),
            OperatingCashFlowToRevenue: Round(fundamental?.OperatingCashFlowToRevenue),
            OperatingCashFlowToNetProfit: Round(fundamental?.OperatingCashFlowToNetProfit),
            FreeCashFlow: fundamental?.FreeCashFlow is { } fcf ? Display.ToYi(fcf) : null,
            InventoryTurnoverDays: Round(fundamental?.InventoryTurnoverDays),
            ReceivableTurnoverDays: Round(fundamental?.ReceivableTurnoverDays),
            RevenueYoy: Round(fundamental?.RevenueYoy),
            NetProfitYoy: Round(fundamental?.NetProfitYoy),
            DeductedNetProfitYoy: Round(fundamental?.DeductedNetProfitYoy),
            Eps: Round(fundamental?.Eps),
            Bps: Round(fundamental?.Bps),
            // 股息率来自业绩报表（RPT_LICO_FN_CPD 的 ZXGXL），本报表不含该字段
            DividendYield: dividends.TryGetValue(row.Code, out var dividend) ? Round(dividend) : null);
    }

    /// <summary>
    /// 从最新年度往回数「连续有年报」的年数。
    /// </summary>
    /// <remarks>
    /// 与连续性条件同一口径（缺年报即中断），这样界面上的「已有 M/N 年」与判定结果一致，
    /// 不会出现「显示有 8 年、但条件说不足 5 年」的自相矛盾。
    /// </remarks>
    private static int AnnualYears(IReadOnlyList<FundamentalMetric>? annual)
    {
        if (annual is null || annual.Count == 0)
        {
            return 0;
        }

        var years = new HashSet<int>();
        foreach (var metric in annual)
        {
            years.Add(metric.ReportDate.Year);
        }

        var newest = years.Max();
        var count = 0;
        for (var year = newest; years.Contains(year); year--)
        {
            count++;
        }

        return count;
    }

    private static decimal? Round(decimal? value) => value is null ? null : Display.Round(value.Value);

    /// <summary>
    /// 确保行情快照与基本面都已载入内存。
    /// </summary>
    /// <remarks>
    /// 两者分开判断：行情每 60 秒整体替换（<c>cache.Replace</c> 会保留基本面），
    /// 而基本面是季频的、只加载一次。若合并判断，每轮行情刷新都会重新读万级基本面行。
    /// </remarks>
    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (cache.IsEmpty)
        {
            var rows = await quotes.GetAllAsync(cancellationToken).ConfigureAwait(false);
            if (rows.Count == 0)
            {
                return;
            }

            var instrumentRows = await instruments.GetAllAsync(cancellationToken).ConfigureAwait(false);
            cache.Replace(rows, instrumentRows.ToDictionary(item => item.Code, StringComparer.Ordinal));
        }

        if (cache.Current.FundamentalsLoaded)
        {
            return;
        }

        var latest = await fundamentals.GetLatestPerCodeAsync(cancellationToken).ConfigureAwait(false);
        var annual = await fundamentals.GetAnnualByCodeAsync(null, cancellationToken).ConfigureAwait(false);

        // 股息率只在业绩报表里，单独取一次（不分页，几千行）
        var dividends = await finance.GetLatestDividendYieldsAsync(cancellationToken).ConfigureAwait(false);

        cache.ReplaceFundamentals(latest, annual, dividends);
    }
}
