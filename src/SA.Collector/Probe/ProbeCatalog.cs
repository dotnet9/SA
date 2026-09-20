namespace SA.Collector.Probe;

/// <summary>
/// 探针响应形态，决定如何从响应中抽取「字段名与序号」这一关键证据。
/// </summary>
internal enum ProbeShape
{
    /// <summary>普通 JSON，自动识别 data.diff / result.data 等常见包裹。</summary>
    Json,

    /// <summary>F10 PageAjax：顶层每个键是一个 section，逐一列出条目数与首行字段。</summary>
    F10Sections,

    /// <summary>K 线：data.klines 为逗号分隔字符串数组，需给出字段序号对照。</summary>
    Kline,

    /// <summary>腾讯行情：波浪号分隔的定长字段，需给出索引对照。</summary>
    TildeDelimited,

    /// <summary>非 JSON 文本，仅打印首段。</summary>
    PlainText
}

/// <summary>
/// 一个数据源探针定义。列表顺序即报告顺序。
/// </summary>
internal sealed record ProbeDefinition(
    string Name,
    string Domain,
    string Purpose,
    string Url,
    ProbeShape Shape,
    string? Referer = null,
    bool Gbk = false,
    string? Note = null);

/// <summary>
/// 探针目录：实施计划 §3.5「首日实测清单」的落地形式。
/// 每一条都对应一个真实上游端点，报告输出字段名与样例，作为后续解析单测的固化依据。
/// </summary>
internal static class ProbeCatalog
{
    private const string Em = "https://datacenter-web.eastmoney.com/api/data/v1/get";
    private const string Push = "https://push2.eastmoney.com/api/qt";
    private const string PushHis = "https://push2his.eastmoney.com/api/qt";
    private const string F10 = "https://emweb.securities.eastmoney.com/PC_HSF10";

    /// <summary>被探测的样例标的：宁德时代（深）与贵州茅台（沪）。</summary>
    private const string SecId = "0.300750";

    /// <summary>已确认可用的报表过滤串，注意 <c>%3D</c>/<c>%22</c> 必须保持已编码形态。</summary>
    private static string Filter(string field, string value) => $"filter=({field}%3D%22{value}%22)";

    private static string F10Url(string page) => $"{F10}/{page}/PageAjax?code=SZ300750";

    /// <summary>全部探针。</summary>
    public static IReadOnlyList<ProbeDefinition> All { get; } =
    [
        // ── 行情 ──────────────────────────────────────────────
        new("quote-snapshot", "行情", "多标的实时快照（自选与详情页头部数据源）",
            $"{Push}/ulist.np/get?fltt=2&secids={SecId},1.600519,0.000001&fields=f1,f2,f3,f4,f5,f6,f8,f9,f10,f12,f13,f14,f15,f16,f17,f18,f20,f21,f23,f115",
            ProbeShape.Json),

        new("quote-list", "行情", "全市场证券列表（universe 与排行基础）",
            $"{Push}/clist/get?pn=1&pz=5&po=1&np=1&fltt=2&invt=2&fid=f3&fs=m:0+t:6,m:0+t:80,m:1+t:2,m:1+t:23,m:0+t:81+s:2048&fields=f12,f13,f14,f2,f3,f8,f9,f10,f20,f21,f23,f100",
            ProbeShape.Json),

        new("index-quote", "行情", "指数快照（市场概览头部四张卡）",
            $"{Push}/ulist.np/get?fltt=2&secids=1.000001,0.399001,1.000300,0.399006&fields=f1,f2,f3,f4,f5,f6,f12,f13,f14",
            ProbeShape.Json),

        new("sector-list", "行情", "东财行业板块列表（行业热力与排行）",
            $"{Push}/clist/get?pn=1&pz=5&po=1&np=1&fltt=2&invt=2&fid=f3&fs=m:90+t:2+f:!50&fields=f12,f14,f2,f3,f62,f104,f105,f128,f140",
            ProbeShape.Json),

        new("concept-members", "行情", "板块成分股（概念关键词搜索与产业链）",
            $"{Push}/clist/get?pn=1&pz=5&po=1&np=1&fltt=2&invt=2&fid=f3&fs=b:BK1201+f:!50&fields=f12,f13,f14,f2,f3",
            ProbeShape.Json,
            Note: "BK 码取自 sector-list 实测结果（BK1201=电子）"),

        new("kline-daily-front", "行情", "日线（前复权，指标与图形口径）",
            $"{PushHis}/stock/kline/get?secid={SecId}&fields1=f1,f2,f3,f4,f5,f6&fields2=f51,f52,f53,f54,f55,f56,f57,f58,f59,f60,f61&klt=101&fqt=1&beg=0&end=20500101&lmt=6",
            ProbeShape.Kline,
            Note: "必须带 beg/end，仅给 lmt 时上游返回空 data"),

        new("kline-daily-raw", "行情", "日线（不复权，用于除权除息检测）",
            $"{PushHis}/stock/kline/get?secid={SecId}&fields1=f1,f2,f3,f4,f5,f6&fields2=f51,f52,f53,f54,f55,f56,f57,f58,f59,f60,f61&klt=101&fqt=0&beg=0&end=20500101&lmt=6",
            ProbeShape.Kline),

        new("kline-daily-back", "行情", "日线（后复权：长周期序列不会出现负价，用于指标与长周期图形）",
            $"{PushHis}/stock/kline/get?secid={SecId}&fields1=f1,f2,f3,f4,f5,f6&fields2=f51,f52,f53,f54,f55,f56,f57,f58,f59,f60,f61&klt=101&fqt=2&beg=0&end=20500101&lmt=6",
            ProbeShape.Kline,
            Note: "实测前复权(fqt=1)对高分红股会出现负价，复权口径需在 daily 数据集中明确"),

        new("kline-weekly", "行情", "周线（趋势页切换周期）",
            $"{PushHis}/stock/kline/get?secid={SecId}&fields1=f1,f2,f3,f4,f5,f6&fields2=f51,f52,f53,f54,f55,f56,f57,f58,f59,f60,f61&klt=102&fqt=1&beg=0&end=20500101&lmt=4",
            ProbeShape.Kline),

        new("kline-monthly", "行情", "月线（趋势页切换周期）",
            $"{PushHis}/stock/kline/get?secid={SecId}&fields1=f1,f2,f3,f4,f5,f6&fields2=f51,f52,f53,f54,f55,f56,f57,f58,f59,f60,f61&klt=103&fqt=1&beg=0&end=20500101&lmt=4",
            ProbeShape.Kline),

        new("limitup-pool", "行情", "涨停池（市场情绪与事件）",
            "https://push2ex.eastmoney.com/getTopicZTPool?ut=7eea3edcaed734bea9cbfc24409ed989&dpt=wz.ztzt&Pageindex=0&pagesize=5&sort=fbt%3Aasc&date=20260918",
            ProbeShape.Json,
            Note: "ut 为公开固定值，若失效需从行情页 JS 重新提取"),

        // ── 资金 ──────────────────────────────────────────────
        new("capital-flow", "资金", "个股分层资金流（主力/大单/超大单口径）",
            $"{PushHis}/stock/fflow/daykline/get?lmt=5&klt=101&secid={SecId}&fields1=f1,f2,f3,f7&fields2=f51,f52,f53,f54,f55,f56,f57,f58,f59,f60,f61,f62,f63",
            ProbeShape.Kline),

        new("capital-flow-board-rank", "资金", "行业资金流排行（行业页与市场页）",
            $"{Push}/clist/get?pn=1&pz=5&po=1&np=1&fltt=2&invt=2&fid=f62&fs=m:90+t:2+f:!50&fields=f12,f14,f2,f3,f62,f184",
            ProbeShape.Json),

        new("margin-detail", "资金", "个股两融明细（融资余额与净买入）",
            $"{Em}?reportName=RPTA_WEB_RZRQ_GGMX&columns=ALL&{Filter("SCODE", "300750")}&pageSize=3&pageNumber=1&sortColumns=DATE&sortTypes=-1",
            ProbeShape.Json),

        new("margin-market", "资金", "沪深两融余额合计（市场页）",
            $"{Em}?reportName=RPTA_WEB_RZRQ_LSSH&columns=ALL&pageSize=3&pageNumber=1",
            ProbeShape.Json,
            Note: "报表名来自 data.eastmoney.com/rzrq 的 JS 包"),

        new("billboard", "资金", "龙虎榜每日明细（按交易日拉取）",
            $"{Em}?reportName=RPT_DAILYBILLBOARD_DETAILSNEW&columns=ALL&filter=(TRADE_DATE%3D%272026-09-18%27)&pageSize=2&pageNumber=1&sortColumns=BILLBOARD_NET_AMT&sortTypes=-1",
            ProbeShape.Json),

        new("billboard-seats", "资金", "龙虎榜营业部席位明细（实施计划 §3.5 待验证项）",
            $"{Em}?reportName=RPT_OPERATEDEPT_TRADE_DETAILSNEW&columns=ALL&pageSize=2&pageNumber=1&sortColumns=TRADE_DATE&sortTypes=-1",
            ProbeShape.Json),

        new("blocktrade", "资金", "大宗交易明细",
            $"{Em}?reportName=RPT_DATA_BLOCKTRADE&columns=ALL&{Filter("SECURITY_CODE", "300750")}&pageSize=2&pageNumber=1&sortColumns=TRADE_DATE&sortTypes=-1",
            ProbeShape.Json),

        new("north-holdrank", "资金", "北向（陆股通）个股持股排行——替代已失效的 RPT_MUTUAL_STOCK_NORTHSTA",
            $"{Em}?reportName=RPT_MUTUAL_HOLDRANK_NEW&columns=ALL&pageSize=3&pageNumber=1&sortColumns=HOLD_DATE&sortTypes=-1",
            ProbeShape.Json),

        new("north-netflow", "资金", "北向资金净流入统计（市场页北向卡）",
            $"{Em}?reportName=RPT_MUTUAL_NETINFLOW_STATISTICS&columns=ALL&pageSize=3&pageNumber=1",
            ProbeShape.Json),

        // ── 财务 ──────────────────────────────────────────────
        new("finance-report", "财务", "业绩报表（营收/净利/ROE/毛利率，财务页主数据）",
            $"{Em}?reportName=RPT_LICO_FN_CPD&columns=ALL&{Filter("SECURITY_CODE", "300750")}&pageSize=5&pageNumber=1&sortColumns=REPORTDATE&sortTypes=-1",
            ProbeShape.Json),

        new("finance-forecast", "财务", "业绩预告（事件模块的业绩类事件来源）",
            $"{Em}?reportName=RPT_PUBLIC_OP_NEWPREDICT&columns=ALL&{Filter("SECURITY_CODE", "300750")}&pageSize=2&pageNumber=1",
            ProbeShape.Json),

        new("finance-performance", "财务", "业绩快报",
            $"{Em}?reportName=RPT_FCI_PERFORMANCEE&columns=ALL&{Filter("SECURITY_CODE", "300750")}&pageSize=2&pageNumber=1",
            ProbeShape.Json),

        // ── 股权 ──────────────────────────────────────────────
        new("holder-top10free", "股权", "十大流通股东（必须带 END_DATE，否则单只 370 行）",
            $"{Em}?reportName=RPT_F10_EH_FREEHOLDERS&columns=ALL&{Filter("SECUCODE", "300750.SZ")}&pageSize=3&pageNumber=1&sortColumns=HOLDER_RANK&sortTypes=1",
            ProbeShape.Json),

        new("holder-top10", "股权", "十大股东",
            $"{Em}?reportName=RPT_F10_EH_HOLDERS&columns=ALL&{Filter("SECUCODE", "300750.SZ")}&pageSize=3&pageNumber=1&sortColumns=HOLDER_RANK&sortTypes=1",
            ProbeShape.Json),

        new("holder-count", "股权", "股东户数与户均持股（筹码集中度）",
            $"{Em}?reportName=RPT_HOLDERNUMLATEST&columns=ALL&{Filter("SECURITY_CODE", "300750")}&pageSize=2&pageNumber=1",
            ProbeShape.Json),

        new("holder-count-history", "股权", "股东户数历史序列",
            $"{Em}?reportName=RPT_HOLDERNUM_DET&columns=ALL&{Filter("SECURITY_CODE", "300750")}&pageSize=3&pageNumber=1&sortColumns=END_DATE&sortTypes=-1",
            ProbeShape.Json),

        new("f10-shareholder", "股权", "F10 股东研究：含机构持仓分类与股东户数（一份响应多 section）",
            F10Url("ShareholderResearch"),
            ProbeShape.F10Sections),

        new("pledge-overview", "股权", "股权质押市场总览（周度）",
            $"{Em}?reportName=RPT_CSDC_STATISTICS&columns=ALL&pageSize=2&pageNumber=1&sortColumns=TRADE_DATE&sortTypes=-1",
            ProbeShape.Json),

        new("pledge-stock", "股权", "个股质押比例（替代已失效的 RPT_CUSTOM_STOCK_PLEDGE_STATISTICS）",
            $"{Em}?reportName=RPT_CSDC_LIST_NEWEST&columns=ALL&{Filter("SECURITY_CODE", "300750")}&pageSize=2&pageNumber=1",
            ProbeShape.Json),

        new("pledge-detail", "股权", "质押明细（股东、质权方、预警线/平仓线）",
            $"{Em}?reportName=RPTA_APP_ACCUMDETAILS&columns=ALL&{Filter("SECURITY_CODE", "300750")}&pageSize=2&pageNumber=1",
            ProbeShape.Json),

        new("f10-business", "股权", "F10 经营分析：经营范围与主营构成（产业链与业务占比）",
            F10Url("BusinessAnalysis"),
            ProbeShape.F10Sections),

        new("f10-companysurvey", "股权", "F10 公司概况：基本资料",
            F10Url("CompanySurvey"),
            ProbeShape.F10Sections),

        new("f10-subsidiary", "股权", "参控股子公司候选端点（股权拓扑上游；不存在则拓扑降级为「股东 → 公司」两层）",
            F10Url("Subsidiary"),
            ProbeShape.F10Sections,
            Note: "端点存在性未验证，实施计划 §11 已登记降级方案"),

        new("f10-coreconception", "股权", "F10 核心题材：所属概念板块（概念关键词与产业链映射）",
            F10Url("CoreConception"),
            ProbeShape.F10Sections),

        // ── 事件 ──────────────────────────────────────────────
        new("announcement", "事件", "公告列表（事件抽取的原始语料，可回溯 art_code）",
            "https://np-anotice-stock.eastmoney.com/api/security/ann?sr=-1&page_size=3&page_index=1&ann_type=A&client_source=web&stock_list=300750&f_node=0&s_node=0",
            ProbeShape.Json),

        new("f10-opsrequired", "事件", "F10 大事提醒：事件与时间线的现成聚合",
            F10Url("OperationsRequired"),
            ProbeShape.F10Sections),

        // ── 舆情 ──────────────────────────────────────────────
        new("news-fast", "舆情", "7×24 全球快讯（关键词匹配生成个股舆情，标注估算）",
            "https://np-listapi.eastmoney.com/comm/web/getFastNewsList?client=web&biz=web_724&fastColumn=102&sortEnd=&pageSize=3&req_trace=1",
            ProbeShape.Json),

        new("news-kuaixun", "舆情", "快讯备选源（JSONP 形态）",
            "https://newsapi.eastmoney.com/kuaixun/v1/getlist_102_ajaxResult_3_1_.html",
            ProbeShape.PlainText),

        new("news-stock", "舆情", "个股资讯候选端点（个股舆情首选）",
            "https://np-listapi.eastmoney.com/comm/web/getListInfo?client=web&biz=web_news_col&column=347&order=1&needInteractData=0&page_index=1&page_size=5&req_trace=1&mTypeAndCode=0.300750",
            ProbeShape.Json,
            Note: "参数形态未验证，失败则退化为快讯关键词匹配"),

        new("news-search", "舆情", "搜索接口按关键词取个股相关文章（个股舆情备选）",
            "https://search-api-web.eastmoney.com/search/jsonp?cb=sa&param=%7B%22uid%22%3A%22%22%2C%22keyword%22%3A%22300750%22%2C%22type%22%3A%5B%22cmsArticleWebOld%22%5D%2C%22client%22%3A%22web%22%2C%22clientType%22%3A%22web%22%2C%22clientVersion%22%3A%22curr%22%2C%22param%22%3A%7B%22cmsArticleWebOld%22%3A%7B%22searchScope%22%3A%22default%22%2C%22sort%22%3A%22default%22%2C%22pageIndex%22%3A1%2C%22pageSize%22%3A5%7D%7D%7D",
            ProbeShape.PlainText),

        // ── 评级 ──────────────────────────────────────────────
        new("rating-list", "评级", "研报与评级明细（机构评级页主数据）",
            "https://reportapi.eastmoney.com/report/list?industryCode=*&pageSize=3&industry=*&rating=&ratingChange=&beginTime=2026-01-01&endTime=2026-12-31&pageNo=1&qType=0&code=300750&pageNum=1&pageNumber=1",
            ProbeShape.Json),

        new("rating-consensus", "评级", "一致预期（EPS/PE 预期，目标价缺失时的替代口径）",
            $"{Em}?reportName=RPT_WEB_RESPREDICT&columns=ALL&{Filter("SECURITY_CODE", "300750")}&pageSize=3&pageNumber=1",
            ProbeShape.Json),

        // ── 降级源 ────────────────────────────────────────────
        new("tencent-quote", "降级源", "腾讯行情备源（GBK 编码，字段为定长索引）",
            "https://qt.gtimg.cn/q=sh600519,sz300750",
            ProbeShape.TildeDelimited,
            Gbk: true),

        new("sina-quote", "降级源", "新浪行情备源（需 Referer，实施前未验证）",
            "https://hq.sinajs.cn/list=sh600519,sz300750",
            ProbeShape.PlainText,
            Referer: "https://finance.sina.com.cn",
            Gbk: true,
            Note: "无 Referer 时返回 403；通过后登记为第三降级源，否则保持关闭")
    ];

    /// <summary>
    /// 按名称或域筛选探针。传入 <c>all</c> 或空表示全部。
    /// </summary>
    public static IReadOnlyList<ProbeDefinition> Select(string? selector)
    {
        if (string.IsNullOrWhiteSpace(selector) || selector.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return All;
        }

        var matched = All
            .Where(p => p.Name.Equals(selector, StringComparison.OrdinalIgnoreCase)
                        || p.Domain.Equals(selector, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return matched;
    }

    /// <summary>
    /// 全部可用域，供帮助文本使用。
    /// </summary>
    public static IEnumerable<string> Domains => All.Select(p => p.Domain).Distinct();
}
