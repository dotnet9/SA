/* ============================================================
   SA · 股析 — 演示数据（原型用）
   全部为演示数据，非实时、非真实行情，不构成投资建议。
   序列数据用确定性伪随机生成器产出，保证每次打开一致。
   ============================================================ */
(function () {
  "use strict";

  /* ---------- 确定性伪随机 ---------- */
  function mulberry32(seed) {
    let a = seed >>> 0;
    return function () {
      a |= 0;
      a = (a + 0x6d2b79f5) | 0;
      let t = Math.imul(a ^ (a >>> 15), 1 | a);
      t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
      return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
  }

  /* 生成走势序列：起止价格 + 波动率，返回 [{d, o, h, l, c, v}] */
  function kline(seed, n, start, drift, vol, baseVol, endDate) {
    const rnd = mulberry32(seed);
    const out = [];
    let c = start;
    const end = endDate ? new Date(endDate) : new Date(2026, 8, 18);
    const days = [];
    let d = new Date(end);
    while (days.length < n) {
      const wd = d.getDay();
      if (wd !== 0 && wd !== 6) days.unshift(new Date(d));
      d.setDate(d.getDate() - 1);
    }
    for (let i = 0; i < n; i++) {
      const shock = (rnd() - 0.5) * 2;
      const trend = drift / n;
      const o = c;
      c = Math.max(0.5, o * (1 + trend + shock * vol));
      const h = Math.max(o, c) * (1 + rnd() * vol * 0.6);
      const l = Math.min(o, c) * (1 - rnd() * vol * 0.6);
      const v = Math.round(baseVol * (0.55 + rnd() * 1.1));
      out.push({
        d: days[i].toISOString().slice(0, 10),
        o: +o.toFixed(2),
        h: +h.toFixed(2),
        l: +l.toFixed(2),
        c: +c.toFixed(2),
        v
      });
    }
    return out;
  }

  /* 简单折线序列 */
  function line(seed, n, start, drift, vol, digits) {
    const rnd = mulberry32(seed);
    const out = [];
    let c = start;
    for (let i = 0; i < n; i++) {
      c = c * (1 + drift / n + (rnd() - 0.5) * 2 * vol);
      out.push(+(+c).toFixed(digits === undefined ? 2 : digits));
    }
    return out;
  }

  const END = "2026-09-18";

  /* ============================================================
     1. 指数与大盘
     ============================================================ */
  const indices = [
    { code: "000001", name: "上证指数", price: 3286.42, chg: 14.86, pct: 0.45, amount: 4862, spark: line(11, 40, 3271, 0.004, 0.003) },
    { code: "399001", name: "深证成指", price: 10412.35, chg: 78.24, pct: 0.76, amount: 6120, spark: line(12, 40, 10334, 0.008, 0.004) },
    { code: "399006", name: "创业板指", price: 2148.66, chg: 26.31, pct: 1.24, amount: 3086, spark: line(13, 40, 2122, 0.012, 0.006) },
    { code: "000688", name: "科创50", price: 1102.87, chg: 19.42, pct: 1.79, amount: 986, spark: line(14, 40, 1083, 0.018, 0.009) },
    { code: "899050", name: "北证50", price: 1348.11, chg: -8.62, pct: -0.64, amount: 214, spark: line(15, 40, 1356, -0.006, 0.008) }
  ];

  const breadth = {
    up: 3124,
    down: 1786,
    flat: 213,
    limitUp: 68,
    limitDown: 12,
    turnover: 10982,      // 两市成交额（亿元）
    turnoverPct: 8.6,     // 较上一交易日
    northbound: 42.6,     // 北向资金净流入（亿元）
    northbound5: [18.2, -12.4, 26.8, 9.6, 42.6],
    marginBalance: 18426, // 融资余额（亿元）
    marginChg: 0.62
  };

  const industries = [
    { name: "电池", pct: 3.86, leader: "宁德时代", flow: 28.6, pe: 22.4, pePct: 38 },
    { name: "光伏设备", pct: 2.94, leader: "隆基绿能", flow: 16.2, pe: 18.9, pePct: 21 },
    { name: "半导体", pct: 2.41, leader: "寒武纪", flow: 22.8, pe: 78.6, pePct: 72 },
    { name: "汽车整车", pct: 1.82, leader: "比亚迪", flow: 12.4, pe: 24.1, pePct: 44 },
    { name: "白酒", pct: 1.24, leader: "贵州茅台", flow: 8.6, pe: 21.7, pePct: 12 },
    { name: "消费电子", pct: 1.06, leader: "立讯精密", flow: 6.2, pe: 26.3, pePct: 51 },
    { name: "医疗器械", pct: 0.42, leader: "迈瑞医疗", flow: 2.1, pe: 29.8, pePct: 33 },
    { name: "银行", pct: 0.18, leader: "招商银行", flow: 3.4, pe: 6.2, pePct: 28 },
    { name: "证券", pct: -0.36, leader: "东方财富", flow: -4.8, pe: 24.6, pePct: 46 },
    { name: "房地产开发", pct: -1.24, leader: "保利发展", flow: -9.2, pe: 12.4, pePct: 18 },
    { name: "煤炭开采", pct: -1.86, leader: "中国神华", flow: -6.4, pe: 9.8, pePct: 34 },
    { name: "航空机场", pct: -2.42, leader: "中国国航", flow: -5.1, pe: 34.2, pePct: 62 }
  ];

  const hotEvents = [
    { date: "09-18", tag: "政策", level: "up", title: "固态电池产业化路线图征求意见稿发布，明确 2027 年装车目标", related: ["300750", "835185"] },
    { date: "09-18", tag: "业绩", level: "up", title: "宁德时代三季度业绩预告：净利润同比预增 32%–45%", related: ["300750"] },
    { date: "09-17", tag: "订单", level: "up", title: "比亚迪海外单月销量首破 8 万辆，欧洲工厂提前投产", related: ["002594"] },
    { date: "09-17", tag: "风险", level: "down", title: "某锂矿企业收到交易所问询函，涉及关联交易定价", related: ["002466"] },
    { date: "09-16", tag: "监管", level: "down", title: "证监会就程序化交易新规公开征求意见，短期或压制活跃度", related: [] },
    { date: "09-16", tag: "产业", level: "up", title: "AI 服务器出货预期上调，光模块与算力芯片链条受益", related: ["300308", "688256"] },
    { date: "09-15", tag: "资金", level: "up", title: "北向资金连续 3 日净流入电池与半导体板块", related: ["300750", "688256"] },
    { date: "09-15", tag: "风险", level: "down", title: "地产链信用风险事件发酵，拖累相关产业链估值", related: ["600048"] }
  ];

  const rankings = {
    amount: [
      { code: "300750", name: "宁德时代", price: 268.42, pct: 4.86, amount: 186.4 },
      { code: "002594", name: "比亚迪", price: 312.66, pct: 2.41, amount: 142.8 },
      { code: "600519", name: "贵州茅台", price: 1568.2, pct: 1.24, amount: 98.6 },
      { code: "300308", name: "中际旭创", price: 186.44, pct: 6.82, amount: 92.4 },
      { code: "688256", name: "寒武纪", price: 892.6, pct: 8.42, amount: 88.2 },
      { code: "601318", name: "中国平安", price: 58.42, pct: -0.86, amount: 76.4 }
    ],
    gainers: [
      { code: "300308", name: "中际旭创", price: 186.44, pct: 6.82, amount: 92.4 },
      { code: "688256", name: "寒武纪", price: 892.6, pct: 8.42, amount: 88.2 },
      { code: "300750", name: "宁德时代", price: 268.42, pct: 4.86, amount: 186.4 },
      { code: "835185", name: "贝特瑞", price: 42.86, pct: 5.62, amount: 6.8 },
      { code: "002594", name: "比亚迪", price: 312.66, pct: 2.41, amount: 142.8 },
      { code: "600438", name: "通威股份", price: 28.66, pct: 3.24, amount: 42.1 }
    ],
    losers: [
      { code: "601318", name: "中国平安", price: 58.42, pct: -0.86, amount: 76.4 },
      { code: "600048", name: "保利发展", price: 8.62, pct: -2.84, amount: 38.2 },
      { code: "601088", name: "中国神华", price: 38.24, pct: -1.86, amount: 44.6 },
      { code: "600111", name: "北方稀土", price: 22.86, pct: -3.42, amount: 32.8 },
      { code: "601111", name: "中国国航", price: 7.86, pct: -2.42, amount: 18.4 },
      { code: "600030", name: "中信证券", price: 24.62, pct: -1.24, amount: 56.2 }
    ]
  };

  /* ============================================================
     2. 股票池
     ============================================================ */
  const stocks = [
    { code: "600519", name: "贵州茅台", py: "gzmt", board: "沪市主板", industry: "白酒", price: 1568.2, chg: 19.24, pct: 1.24, volRatio: 1.12, turnover: 0.28, pe: 21.7, pb: 7.86, cap: 19680, roe: 32.4, mainFlow: 2.86, spark: line(101, 30, 1549, 0.012, 0.006) },
    { code: "300750", name: "宁德时代", py: "ndsd", board: "创业板", industry: "电池", price: 268.42, chg: 12.46, pct: 4.86, volRatio: 1.86, turnover: 1.42, pe: 22.4, pb: 4.62, cap: 11820, roe: 22.8, mainFlow: 18.62, spark: line(102, 30, 256, 0.048, 0.011) },
    { code: "002594", name: "比亚迪", py: "byd", board: "深市主板", industry: "汽车整车", price: 312.66, chg: 7.36, pct: 2.41, volRatio: 1.42, turnover: 1.06, pe: 24.1, pb: 4.18, cap: 9104, roe: 19.6, mainFlow: 6.42, spark: line(103, 30, 305, 0.024, 0.009) },
    { code: "688256", name: "寒武纪", py: "hwj", board: "科创板", industry: "半导体", price: 892.6, chg: 69.28, pct: 8.42, volRatio: 2.42, turnover: 3.86, pe: 0, pb: 42.6, cap: 3726, roe: -4.2, mainFlow: 9.86, spark: line(104, 30, 823, 0.082, 0.019) },
    { code: "835185", name: "贝特瑞", py: "btr", board: "北交所", industry: "电池", price: 42.86, chg: 2.28, pct: 5.62, volRatio: 3.16, turnover: 4.24, pe: 18.6, pb: 2.42, cap: 312, roe: 12.8, mainFlow: 0.86, spark: line(105, 30, 40.6, 0.056, 0.016) },
    { code: "300308", name: "中际旭创", py: "zjxc", board: "创业板", industry: "光模块", price: 186.44, chg: 11.92, pct: 6.82, volRatio: 2.14, turnover: 5.62, pe: 38.4, pb: 9.24, cap: 2086, roe: 24.6, mainFlow: 12.86, spark: line(106, 30, 174, 0.068, 0.017) },
    { code: "601318", name: "中国平安", py: "zgpa", board: "沪市主板", industry: "保险", price: 58.42, chg: -0.51, pct: -0.86, volRatio: 0.86, turnover: 0.42, pe: 8.6, pb: 0.92, cap: 10624, roe: 11.2, mainFlow: -1.24, spark: line(107, 30, 58.9, -0.008, 0.007) },
    { code: "600048", name: "保利发展", py: "blfz", board: "沪市主板", industry: "房地产开发", price: 8.62, chg: -0.25, pct: -2.84, volRatio: 1.34, turnover: 1.18, pe: 12.4, pb: 0.68, cap: 1032, roe: 5.4, mainFlow: -2.16, spark: line(108, 30, 8.87, -0.028, 0.012) },
    { code: "601088", name: "中国神华", py: "zgsh", board: "沪市主板", industry: "煤炭开采", price: 38.24, chg: -0.72, pct: -1.86, volRatio: 0.94, turnover: 0.36, pe: 9.8, pb: 1.42, cap: 7598, roe: 15.6, mainFlow: -3.42, spark: line(109, 30, 38.96, -0.019, 0.008) },
    { code: "600111", name: "北方稀土", py: "bfxt", board: "沪市主板", industry: "小金属", price: 22.86, chg: -0.81, pct: -3.42, volRatio: 1.62, turnover: 2.14, pe: 42.6, pb: 3.24, cap: 828, roe: 7.8, mainFlow: -4.28, spark: line(110, 30, 23.67, -0.034, 0.014) },
    { code: "600438", name: "通威股份", py: "twgf", board: "沪市主板", industry: "光伏设备", price: 28.66, chg: 0.9, pct: 3.24, volRatio: 1.76, turnover: 2.86, pe: 18.9, pb: 1.86, cap: 1290, roe: 10.4, mainFlow: 3.86, spark: line(111, 30, 27.76, 0.032, 0.013) },
    { code: "601012", name: "隆基绿能", py: "ljln", board: "沪市主板", industry: "光伏设备", price: 21.42, chg: 0.62, pct: 2.98, volRatio: 1.44, turnover: 1.86, pe: 16.2, pb: 1.62, cap: 1624, roe: 9.8, mainFlow: 4.12, spark: line(112, 30, 20.8, 0.03, 0.012) },
    { code: "002475", name: "立讯精密", py: "lxjm", board: "深市主板", industry: "消费电子", price: 42.86, chg: 0.46, pct: 1.08, volRatio: 1.16, turnover: 0.94, pe: 26.3, pb: 4.86, cap: 3086, roe: 18.4, mainFlow: 2.14, spark: line(113, 30, 42.4, 0.011, 0.009) },
    { code: "300760", name: "迈瑞医疗", py: "mryl", board: "创业板", industry: "医疗器械", price: 268.86, chg: 1.12, pct: 0.42, volRatio: 0.92, turnover: 0.52, pe: 29.8, pb: 7.24, cap: 3262, roe: 26.8, mainFlow: 0.86, spark: line(114, 30, 267.7, 0.004, 0.007) },
    { code: "600036", name: "招商银行", py: "zsyh", board: "沪市主板", industry: "银行", price: 42.16, chg: 0.08, pct: 0.19, volRatio: 0.88, turnover: 0.24, pe: 6.2, pb: 0.94, cap: 10632, roe: 15.8, mainFlow: 1.24, spark: line(115, 30, 42.08, 0.002, 0.005) },
    { code: "300059", name: "东方财富", py: "dfcf", board: "创业板", industry: "证券", price: 18.64, chg: -0.07, pct: -0.37, volRatio: 1.24, turnover: 2.42, pe: 24.6, pb: 2.86, cap: 2946, roe: 12.4, mainFlow: -1.86, spark: line(116, 30, 18.71, -0.004, 0.011) },
    { code: "600030", name: "中信证券", py: "zxzq", board: "沪市主板", industry: "证券", price: 24.62, chg: -0.31, pct: -1.24, volRatio: 1.08, turnover: 0.86, pe: 18.4, pb: 1.42, cap: 3648, roe: 8.2, mainFlow: -2.42, spark: line(117, 30, 24.93, -0.012, 0.009) },
    { code: "601111", name: "中国国航", py: "zggh", board: "沪市主板", industry: "航空机场", price: 7.86, chg: -0.19, pct: -2.42, volRatio: 1.42, turnover: 1.24, pe: 34.2, pb: 1.86, cap: 1276, roe: 4.6, mainFlow: -2.86, spark: line(118, 30, 8.05, -0.024, 0.012) },
    { code: "002466", name: "天齐锂业", py: "tqly", board: "深市主板", industry: "小金属", price: 34.28, chg: -1.16, pct: -3.28, volRatio: 2.16, turnover: 3.42, pe: 0, pb: 1.68, cap: 562, roe: -2.4, mainFlow: -5.62, spark: line(119, 30, 35.44, -0.032, 0.015) },
    { code: "688111", name: "金山办公", py: "jsbg", board: "科创板", industry: "软件服务", price: 286.42, chg: 4.86, pct: 1.72, volRatio: 1.32, turnover: 1.62, pe: 68.4, pb: 12.6, cap: 1326, roe: 14.2, mainFlow: 1.86, spark: line(120, 30, 281.6, 0.017, 0.012) },
    { code: "688981", name: "中芯国际", py: "zxgj", board: "科创板", industry: "半导体", price: 86.24, chg: 2.42, pct: 2.88, volRatio: 1.68, turnover: 2.24, pe: 62.4, pb: 4.24, cap: 6842, roe: 5.8, mainFlow: 6.42, spark: line(121, 30, 83.8, 0.028, 0.013) },
    { code: "601899", name: "紫金矿业", py: "zjky", board: "沪市主板", industry: "贵金属", price: 19.86, chg: 0.34, pct: 1.74, volRatio: 1.22, turnover: 0.86, pe: 14.2, pb: 3.42, cap: 5246, roe: 22.6, mainFlow: 3.24, spark: line(122, 30, 19.52, 0.017, 0.01) },
    { code: "600900", name: "长江电力", py: "cjdl", board: "沪市主板", industry: "电力", price: 28.42, chg: 0.06, pct: 0.21, volRatio: 0.76, turnover: 0.18, pe: 19.6, pb: 2.86, cap: 6952, roe: 14.8, mainFlow: 0.62, spark: line(123, 30, 28.36, 0.002, 0.004) },
    { code: "000858", name: "五粮液", py: "wly", board: "深市主板", industry: "白酒", price: 142.86, chg: 1.42, pct: 1.0, volRatio: 1.06, turnover: 0.62, pe: 18.4, pb: 4.24, cap: 5546, roe: 24.6, mainFlow: 1.86, spark: line(124, 30, 141.4, 0.01, 0.008) },
    { code: "002415", name: "海康威视", py: "hkws", board: "深市主板", industry: "安防设备", price: 32.46, chg: 0.18, pct: 0.56, volRatio: 1.14, turnover: 0.48, pe: 22.4, pb: 3.86, cap: 2986, roe: 18.2, mainFlow: 0.94, spark: line(125, 30, 32.28, 0.006, 0.007) },
    { code: "603259", name: "药明康德", py: "ymkd", board: "沪市主板", industry: "CXO", price: 68.42, chg: -0.86, pct: -1.24, volRatio: 1.46, turnover: 1.86, pe: 21.6, pb: 3.42, cap: 1986, roe: 16.4, mainFlow: -2.24, spark: line(126, 30, 69.28, -0.012, 0.011) },
    { code: "300124", name: "汇川技术", py: "hcjs", board: "创业板", industry: "自动化设备", price: 62.86, chg: 1.24, pct: 2.01, volRatio: 1.38, turnover: 1.24, pe: 32.4, pb: 6.24, cap: 1682, roe: 20.6, mainFlow: 2.42, spark: line(127, 30, 61.62, 0.02, 0.011) },
    { code: "600309", name: "万华化学", py: "whhx", board: "沪市主板", industry: "化工", price: 78.24, chg: -0.42, pct: -0.53, volRatio: 0.96, turnover: 0.42, pe: 14.6, pb: 2.68, cap: 2456, roe: 17.8, mainFlow: -0.86, spark: line(128, 30, 78.66, -0.005, 0.007) },
    { code: "002230", name: "科大讯飞", py: "kdxf", board: "深市主板", industry: "软件服务", price: 52.86, chg: 2.16, pct: 4.26, volRatio: 2.06, turnover: 3.24, pe: 0, pb: 6.42, cap: 1224, roe: 1.2, mainFlow: 4.86, spark: line(129, 30, 50.7, 0.042, 0.016) },
    { code: "601127", name: "赛力斯", py: "sls", board: "沪市主板", industry: "汽车整车", price: 128.42, chg: 5.24, pct: 4.26, volRatio: 1.92, turnover: 2.86, pe: 42.6, pb: 12.4, cap: 1938, roe: 28.4, mainFlow: 7.24, spark: line(130, 30, 123.2, 0.042, 0.015) },
    { code: "688041", name: "海光信息", py: "hgxx", board: "科创板", industry: "半导体", price: 168.86, chg: 6.42, pct: 3.96, volRatio: 1.84, turnover: 2.42, pe: 96.4, pb: 14.2, cap: 3926, roe: 9.6, mainFlow: 5.62, spark: line(131, 30, 162.4, 0.039, 0.014) },
    { code: "603501", name: "韦尔股份", py: "wegf", board: "沪市主板", industry: "半导体", price: 118.24, chg: 1.86, pct: 1.6, volRatio: 1.28, turnover: 1.42, pe: 36.8, pb: 5.24, cap: 1442, roe: 12.6, mainFlow: 2.86, spark: line(132, 30, 116.4, 0.016, 0.011) },
    { code: "002460", name: "赣锋锂业", py: "gfly", board: "深市主板", industry: "小金属", price: 38.62, chg: 1.24, pct: 3.32, volRatio: 2.24, turnover: 3.86, pe: 0, pb: 1.86, cap: 778, roe: -3.8, mainFlow: 3.42, spark: line(133, 30, 37.38, 0.033, 0.016) },
    { code: "600809", name: "山西汾酒", py: "sxfj", board: "沪市主板", industry: "白酒", price: 186.42, chg: 2.86, pct: 1.56, volRatio: 1.18, turnover: 0.86, pe: 19.2, pb: 5.42, cap: 2274, roe: 28.2, mainFlow: 1.62, spark: line(134, 30, 183.6, 0.015, 0.01) },
    { code: "601668", name: "中国建筑", py: "zgjz", board: "沪市主板", industry: "建筑装饰", price: 5.86, chg: -0.02, pct: -0.34, volRatio: 0.92, turnover: 0.32, pe: 4.6, pb: 0.62, cap: 2426, roe: 13.4, mainFlow: -1.24, spark: line(135, 30, 5.88, -0.003, 0.006) },
    { code: "300142", name: "沃森生物", py: "wsw", board: "创业板", industry: "生物制品", price: 22.86, chg: -0.62, pct: -2.64, volRatio: 1.86, turnover: 2.64, pe: 0, pb: 2.24, cap: 366, roe: -6.2, mainFlow: -1.86, spark: line(136, 30, 23.48, -0.026, 0.014) },
    { code: "688599", name: "天合光能", py: "thgn", board: "科创板", industry: "光伏设备", price: 24.86, chg: 0.86, pct: 3.58, volRatio: 1.62, turnover: 1.86, pe: 0, pb: 1.24, cap: 542, roe: -1.8, mainFlow: 1.42, spark: line(137, 30, 24, 0.036, 0.015) },
    { code: "000333", name: "美的集团", py: "mdjt", board: "深市主板", industry: "白色家电", price: 78.62, chg: 0.42, pct: 0.54, volRatio: 0.88, turnover: 0.26, pe: 14.8, pb: 3.24, cap: 5986, roe: 22.4, mainFlow: 1.06, spark: line(138, 30, 78.2, 0.005, 0.006) },
    { code: "601766", name: "中国中车", py: "zgzc", board: "沪市主板", industry: "轨交设备", price: 7.24, chg: 0.06, pct: 0.84, volRatio: 1.06, turnover: 0.42, pe: 16.2, pb: 1.24, cap: 2078, roe: 7.8, mainFlow: 0.86, spark: line(139, 30, 7.18, 0.008, 0.007) },
    { code: "688008", name: "澜起科技", py: "lqkj", board: "科创板", industry: "半导体", price: 86.42, chg: 4.24, pct: 5.16, volRatio: 2.32, turnover: 3.42, pe: 58.6, pb: 9.86, cap: 986, roe: 14.8, mainFlow: 4.26, spark: line(140, 30, 82.2, 0.051, 0.017) }
  ];

  const stockByCode = {};
  stocks.forEach(function (s) { stockByCode[s.code] = s; });

  /* ============================================================
     3. 主演示股 300750 宁德时代 —— 明细数据
     ============================================================ */
  const FOCUS = "300750";

  const focusProfile = {
    code: "300750",
    name: "宁德时代",
    py: "ndsd",
    board: "创业板",
    industry: "电池",
    fullName: "宁德时代新能源科技股份有限公司",
    listDate: "2018-06-11",
    price: 268.42,
    chg: 12.46,
    pct: 4.86,
    open: 256.8,
    high: 271.24,
    low: 255.62,
    prevClose: 255.96,
    volume: 62.86,      // 万手
    amount: 186.4,      // 亿元
    turnover: 1.42,
    volRatio: 1.86,
    amplitude: 6.11,
    cap: 11820,
    floatCap: 10426,
    pe: 22.4,
    peTtm: 24.8,
    pb: 4.62,
    ps: 3.86,
    dividend: 1.24,
    roe: 22.8,
    eps: 11.96,
    bps: 58.1,
    high52: 296.4,
    low52: 168.2,
    asOf: "2026-09-18 15:00"
  };

  const focusKline = kline(40276, 240, 168.4, 0.58, 0.021, 42);
  const focusWeekly = kline(7502, 120, 186.2, 0.44, 0.038, 186);
  const focusMonthly = kline(7503, 60, 214.6, 0.24, 0.072, 720);

  /* --- 财务 --- */
  const finance = {
    years: ["2021", "2022", "2023", "2024", "2025", "2026E"],
    revenue: [1303.6, 3285.9, 4009.2, 3620.3, 4268.6, 5120.4],       // 亿元
    netProfit: [159.3, 307.3, 441.2, 507.4, 622.8, 786.2],
    grossMargin: [26.3, 20.2, 22.9, 24.4, 25.8, 26.4],
    netMargin: [12.2, 9.4, 11.0, 14.0, 14.6, 15.4],
    roe: [19.4, 24.6, 22.8, 21.4, 22.8, 24.2],
    ocf: [429.0, 612.4, 928.6, 866.2, 1012.4, 1186.8],
    rd: [76.9, 155.1, 183.6, 186.4, 218.6, 264.2],
    assetLiability: [69.9, 70.6, 65.8, 62.4, 60.2, 58.6],
    receivable: [268.4, 486.2, 512.4, 486.8, 542.6, 612.4],
    inventory: [402.6, 766.8, 812.4, 742.6, 786.2, 862.4],
    goodwill: [12.4, 24.6, 24.6, 22.8, 22.8, 22.8],
    debt: [486.2, 812.4, 986.2, 1024.6, 1086.2, 1142.8]
  };

  const financeQuarters = {
    labels: ["25Q1", "25Q2", "25Q3", "25Q4", "26Q1", "26Q2", "26Q3E"],
    revenue: [847.6, 1002.4, 1086.2, 1332.4, 962.8, 1186.4, 1426.8],
    netProfit: [139.6, 162.4, 158.6, 162.2, 158.4, 196.2, 248.6],
    grossMargin: [24.4, 25.2, 26.1, 27.4, 26.8, 27.6, 28.4],
    netMargin: [16.5, 16.2, 14.6, 12.2, 16.5, 16.5, 17.4],
    yoy: [18.6, 22.4, 26.8, 31.2, 13.6, 18.4, 31.3],
    qoq: [-6.2, 18.3, 8.4, 22.7, -27.7, 23.2, 20.3]
  };

  /* --- 股权与投资 --- */
  const equity = {
    /* 关系拓扑：控股方 → 本公司 → 参控股子公司 / 合资 */
    graph: {
      nodes: [
        { id: "ctl1", name: "曾毓群", cat: "control", value: 100, desc: "实际控制人，直接持股 22.4%，通过宁波梅山保税港区瑞庭投资间接持股" },
        { id: "ctl2", name: "宁波联合创新", cat: "control", value: 100, desc: "一致行动人，持股 10.6%" },
        { id: "self", name: "宁德时代", cat: "self", value: 200, desc: "本公司 300750 · 创业板" },
        { id: "sub1", name: "宁德新能源（CATL-SC）", cat: "sub", value: 100, desc: "全资子公司，动力电池系统研发与制造" },
        { id: "sub2", name: "四川时代新能源", cat: "sub", value: 80, desc: "持股 80%，宜宾基地，面向西南与出口" },
        { id: "sub3", name: "时代广汽动力电池", cat: "sub", value: 51, desc: "持股 51%，合资绑定广汽集团订单" },
        { id: "sub4", name: "时代上汽动力电池", cat: "sub", value: 51, desc: "持股 51%，合资绑定上汽集团订单" },
        { id: "sub5", name: "邦普循环科技", cat: "sub", value: 64, desc: "持股 64%，电池回收与正极材料前驱体" },
        { id: "sub6", name: "宁德时代储能", cat: "sub", value: 100, desc: "全资，储能系统集成，海外占比高" },
        { id: "assoc1", name: "洛阳钼业（参股）", cat: "assoc", value: 24, desc: "间接参股，锁定钴镍资源" },
        { id: "assoc2", name: "时代智能", cat: "assoc", value: 34, desc: "持股 34%，智能座舱与域控" },
        { id: "jv1", name: "福特（技术授权）", cat: "partner", value: 0, desc: "LRS 技术授权模式，收取专利许可费" },
        { id: "jv2", name: "Stellantis 合资", cat: "partner", value: 50, desc: "欧洲合资建厂，持股 50%" }
      ],
      links: [
        { source: "ctl1", target: "self", ratio: 22.4, type: "持股" },
        { source: "ctl2", target: "self", ratio: 10.6, type: "一致行动" },
        { source: "self", target: "sub1", ratio: 100, type: "全资" },
        { source: "self", target: "sub2", ratio: 80, type: "控股" },
        { source: "self", target: "sub3", ratio: 51, type: "合资控股" },
        { source: "self", target: "sub4", ratio: 51, type: "合资控股" },
        { source: "self", target: "sub5", ratio: 64, type: "控股" },
        { source: "self", target: "sub6", ratio: 100, type: "全资" },
        { source: "self", target: "assoc1", ratio: 24, type: "参股" },
        { source: "self", target: "assoc2", ratio: 34, type: "参股" },
        { source: "self", target: "jv1", ratio: 0, type: "技术授权" },
        { source: "self", target: "jv2", ratio: 50, type: "合资" }
      ]
    },
    topHolders: [
      { rank: 1, name: "曾毓群", type: "个人", shares: 9.86, pct: 22.42, chg: 0, chgType: "不变", value: 2650.2 },
      { rank: 2, name: "宁波梅山保税港区瑞庭投资", type: "投资公司", shares: 4.66, pct: 10.6, chg: 0, chgType: "不变", value: 1252.9 },
      { rank: 3, name: "香港中央结算有限公司", type: "北向", shares: 3.12, pct: 7.09, chg: 0.28, chgType: "增持", value: 838.0 },
      { rank: 4, name: "李平", type: "个人", shares: 1.86, pct: 4.24, chg: -0.12, chgType: "减持", value: 500.6 },
      { rank: 5, name: "中国证券金融股份有限公司", type: "国家队", shares: 1.24, pct: 2.82, chg: 0, chgType: "不变", value: 333.4 },
      { rank: 6, name: "招商银行－兴全合润混合", type: "公募基金", shares: 0.62, pct: 1.41, chg: 0.18, chgType: "增持", value: 166.7 },
      { rank: 7, name: "全国社保基金一一五组合", type: "社保", shares: 0.52, pct: 1.18, chg: 0.06, chgType: "增持", value: 139.9 },
      { rank: 8, name: "黄世霖", type: "个人", shares: 0.48, pct: 1.09, chg: -0.36, chgType: "减持", value: 129.1 },
      { rank: 9, name: "易方达蓝筹精选混合", type: "公募基金", shares: 0.36, pct: 0.82, chg: 0.1, chgType: "增持", value: 96.9 },
      { rank: 10, name: "工商银行－华泰柏瑞沪深300ETF", type: "指数基金", shares: 0.32, pct: 0.73, chg: 0.04, chgType: "增持", value: 86.1 }
    ],
    institutions: {
      labels: ["24Q4", "25Q1", "25Q2", "25Q3", "25Q4", "26Q1", "26Q2"],
      fund: [18.6, 19.4, 21.2, 20.6, 22.4, 23.8, 25.2],
      social: [2.4, 2.6, 2.8, 2.8, 3.2, 3.4, 3.6],
      northbound: [6.2, 6.4, 6.6, 6.8, 6.9, 7.0, 7.09],
      qfii: [1.8, 1.6, 1.4, 1.6, 1.8, 2.0, 2.2]
    },
    pledge: { ratio: 2.4, shares: 1.06, times: 3, note: "控股股东质押比例低，无平仓风险" },
    investments: [
      { date: "2026-08-12", target: "时代智能", type: "增资", amount: 28.6, stake: "34%", note: "智能座舱域控制器，投向下一代电子电气架构" },
      { date: "2026-06-24", target: "Stellantis 欧洲合资", type: "合资建厂", amount: 82.4, stake: "50%", note: "西班牙工厂，规划 50GWh，2027 投产" },
      { date: "2026-04-18", target: "邦普循环", type: "增资", amount: 36.2, stake: "64%", note: "扩产三元前驱体与锂回收产线" },
      { date: "2025-12-06", target: "洛阳钼业", type: "参股", amount: 68.4, stake: "24%", note: "锁定钴镍资源，成本端对冲" },
      { date: "2025-09-22", target: "四川时代", type: "增资", amount: 120.0, stake: "80%", note: "宜宾基地四期扩产，配套出口订单" },
      { date: "2025-05-14", target: "时代广汽", type: "合资设立", amount: 24.6, stake: "51%", note: "绑定广汽集团动力电池订单" }
    ],
    /* 股权穿透：实际控制人链路 */
    penetration: [
      { level: 0, name: "曾毓群", pct: 22.42, note: "自然人 · 实际控制人" },
      { level: 1, name: "宁波梅山保税港区瑞庭投资有限公司", pct: 100, note: "曾毓群 100% 持股" },
      { level: 2, name: "宁德时代新能源科技股份有限公司", pct: 10.6, note: "瑞庭投资直接持股" }
    ]
  };

  /* --- 资金与筹码 --- */
  const capital = {
    mainFlow: {
      labels: ["09-08", "09-09", "09-10", "09-11", "09-12", "09-15", "09-16", "09-17", "09-18"],
      net: [2.86, -4.24, 6.82, 8.46, -2.18, 12.42, 4.86, -1.62, 18.62],
      superLarge: [1.86, -2.42, 4.12, 5.24, -1.24, 7.86, 2.94, -0.86, 11.42],
      large: [1.0, -1.82, 2.7, 3.22, -0.94, 4.56, 1.92, -0.76, 7.2],
      medium: [-0.86, 1.42, -2.24, -2.86, 0.86, -4.12, -1.64, 0.62, -5.86],
      small: [-2.0, 2.82, -4.58, -5.6, 1.32, -8.3, -3.22, 1.0, -12.76]
    },
    mainFlowSummary: { today: 18.62, d5: 38.14, d10: 46.32, d20: 52.86, d60: 28.42 },
    northbound: {
      labels: ["03", "04", "05", "06", "07", "08", "09"],
      ratio: [6.42, 6.58, 6.72, 6.86, 6.94, 7.02, 7.09],
      shares: [2.82, 2.89, 2.96, 3.02, 3.06, 3.09, 3.12]
    },
    margin: {
      labels: ["04", "05", "06", "07", "08", "09"],
      balance: [186.4, 192.6, 204.8, 216.4, 228.6, 242.4],
      buy: [18.6, 21.4, 24.2, 22.8, 26.4, 31.2],
      short: [1.24, 1.36, 1.42, 1.38, 1.46, 1.52]
    },
    dragonTiger: [
      { date: "2026-09-18", reason: "日涨幅达 7% 的证券", buy: 18.42, sell: 9.86, net: 8.56, seats: 5 },
      { date: "2026-09-15", reason: "连续三个交易日涨幅偏离值达 20%", buy: 26.84, sell: 18.62, net: 8.22, seats: 5 },
      { date: "2026-08-26", reason: "日跌幅达 7% 的证券", buy: 12.46, sell: 22.84, net: -10.38, seats: 5 },
      { date: "2026-07-18", reason: "日换手率达 30% 的证券", buy: 22.62, sell: 14.28, net: 8.34, seats: 5 }
    ],
    blockTrades: [
      { date: "2026-09-17", price: 258.4, volume: 12.6, amount: 3.26, discount: -3.86, buyer: "机构专用", seller: "机构专用" },
      { date: "2026-09-10", price: 246.8, volume: 26.4, amount: 6.52, discount: -5.24, buyer: "中信证券总部", seller: "机构专用" },
      { date: "2026-08-28", price: 238.6, volume: 18.2, amount: 4.34, discount: -2.14, buyer: "机构专用", seller: "国泰海通证券" },
      { date: "2026-08-14", price: 232.4, volume: 42.6, amount: 9.9, discount: -1.86, buyer: "中金公司总部", seller: "机构专用" }
    ],
    holderChanges: [
      { date: "2026-09-12", holder: "李平", type: "减持", shares: 420, pct: 0.1, amount: 10.8, progress: "进行中", note: "计划减持不超过 0.5%" },
      { date: "2026-08-30", holder: "香港中央结算", type: "增持", shares: 986, pct: 0.22, amount: 24.6, progress: "已完成", note: "陆股通持续增持" },
      { date: "2026-07-22", holder: "黄世霖", type: "减持", shares: 1520, pct: 0.36, amount: 34.2, progress: "已完成", note: "减持计划实施完毕" },
      { date: "2026-06-18", holder: "全国社保基金一一五组合", type: "增持", shares: 260, pct: 0.06, amount: 6.2, progress: "已完成", note: "二季度加仓" },
      { date: "2026-05-28", holder: "兴全合润混合", type: "增持", shares: 760, pct: 0.18, amount: 16.4, progress: "已完成", note: "一季报新进前十" }
    ],
    /* 筹码分布：价格 → 占比 */
    chips: (function () {
      const rnd = mulberry32(9911);
      const out = [];
      for (let p = 160; p <= 300; p += 5) {
        const dist = Math.exp(-Math.pow((p - 248) / 42, 2));
        out.push({ price: p, ratio: +(dist * 6.8 + rnd() * 0.9).toFixed(2) });
      }
      return out;
    })(),
    chipsSummary: { avgCost: 246.8, profitRatio: 72.4, concentration: 0.186, concentration90: 0.244, aboveRatio: 27.6 }
  };

  /* --- 行业与同业 --- */
  const industry = {
    chain: {
      nodes: [
        { name: "锂矿 / 碳酸锂", stage: "上游", share: 0, boom: 38, note: "价格触底回升，成本压力缓解" },
        { name: "正极材料", stage: "上游", share: 0, boom: 46, note: "三元与磷酸铁锂双线，产能过剩缓解" },
        { name: "负极材料", stage: "上游", share: 0, boom: 52, note: "石墨化自供率提升" },
        { name: "隔膜", stage: "上游", share: 0, boom: 58, note: "湿法隔膜格局稳定" },
        { name: "电解液", stage: "上游", share: 0, boom: 44, note: "六氟磷酸锂价格低位" },
        { name: "结构件 / 设备", stage: "上游", share: 0, boom: 56, note: "跟随扩产节奏" },
        { name: "宁德时代", stage: "中游", share: 36.8, boom: 68, note: "动力电池全球市占率第一" },
        { name: "比亚迪弗迪", stage: "中游", share: 15.2, boom: 62, note: "自供为主，外供放量" },
        { name: "中创新航", stage: "中游", share: 6.8, boom: 54, note: "二线龙头，价格竞争激烈" },
        { name: "亿纬锂能", stage: "中游", share: 4.6, boom: 58, note: "大圆柱与储能双线" },
        { name: "新能源汽车", stage: "下游", share: 0, boom: 72, note: "渗透率提升，出口高增" },
        { name: "储能系统", stage: "下游", share: 0, boom: 82, note: "海外大储需求旺盛，毛利更高" },
        { name: "消费电子", stage: "下游", share: 0, boom: 48, note: "需求平稳" },
        { name: "电池回收", stage: "下游", share: 0, boom: 64, note: "政策驱动，闭环降本" }
      ],
      links: [
        { source: "锂矿 / 碳酸锂", target: "正极材料", value: 100, note: "成本传导" },
        { source: "正极材料", target: "宁德时代", value: 42, note: "占电池成本约 42%" },
        { source: "负极材料", target: "宁德时代", value: 12, note: "占成本约 12%" },
        { source: "隔膜", target: "宁德时代", value: 9, note: "占成本约 9%" },
        { source: "电解液", target: "宁德时代", value: 8, note: "占成本约 8%" },
        { source: "结构件 / 设备", target: "宁德时代", value: 14, note: "占成本约 14%" },
        { source: "正极材料", target: "比亚迪弗迪", value: 40, note: "自供比例高" },
        { source: "正极材料", target: "中创新航", value: 38, note: "外购为主" },
        { source: "正极材料", target: "亿纬锂能", value: 36, note: "外购为主" },
        { source: "宁德时代", target: "新能源汽车", value: 58, note: "动力电池主供" },
        { source: "宁德时代", target: "储能系统", value: 26, note: "储能电池，海外占比高" },
        { source: "宁德时代", target: "消费电子", value: 6, note: "小动力与消费电池" },
        { source: "宁德时代", target: "电池回收", value: 10, note: "邦普循环闭环" },
        { source: "比亚迪弗迪", target: "新能源汽车", value: 90, note: "自供为主" },
        { source: "中创新航", target: "新能源汽车", value: 88, note: "主供二线车企" },
        { source: "亿纬锂能", target: "储能系统", value: 46, note: "大储与户储" }
      ]
    },
    peers: [
      { code: "300750", name: "宁德时代", revenue: 4268.6, netProfit: 622.8, grossMargin: 25.8, roe: 22.8, pe: 22.4, pb: 4.62, cap: 11820, pct: 4.86, self: true },
      { code: "002594", name: "比亚迪", revenue: 8626.4, netProfit: 462.8, grossMargin: 19.6, roe: 19.6, pe: 24.1, pb: 4.18, cap: 9104, pct: 2.41 },
      { code: "300014", name: "亿纬锂能", revenue: 586.4, netProfit: 48.6, grossMargin: 17.8, roe: 12.4, pe: 28.6, pb: 3.24, cap: 1246, pct: 3.24 },
      { code: "300207", name: "欣旺达", revenue: 682.4, netProfit: 26.4, grossMargin: 14.2, roe: 9.8, pe: 32.4, pb: 2.42, cap: 486, pct: 2.86 },
      { code: "688567", name: "孚能科技", revenue: 186.2, netProfit: -8.6, grossMargin: 11.4, roe: -6.2, pe: 0, pb: 1.86, cap: 168, pct: 1.42 },
      { code: "835185", name: "贝特瑞", revenue: 218.6, netProfit: 18.4, grossMargin: 21.4, roe: 12.8, pe: 18.6, pb: 2.42, cap: 312, pct: 5.62 }
    ],
    peerRadar: {
      indicators: [
        { name: "毛利率", max: 40 },
        { name: "净利率", max: 20 },
        { name: "ROE", max: 30 },
        { name: "营收增速", max: 60 },
        { name: "研发强度", max: 12 },
        { name: "市占率", max: 40 }
      ],
      series: [
        { name: "宁德时代", values: [25.8, 14.6, 22.8, 18.6, 5.1, 36.8] },
        { name: "比亚迪", values: [19.6, 5.4, 19.6, 24.2, 4.2, 15.2] },
        { name: "亿纬锂能", values: [17.8, 8.3, 12.4, 12.4, 6.8, 4.6] }
      ]
    },
    valuation: {
      peBand: { current: 22.4, min: 14.2, p25: 18.6, median: 24.8, p75: 42.6, max: 168.4, percentile: 38 },
      pbBand: { current: 4.62, min: 2.86, p25: 3.86, median: 5.42, p75: 8.62, max: 24.6, percentile: 42 },
      industryPe: { name: "电池", current: 22.4, median: 26.8, percentile: 38, history: line(7711, 36, 28.4, -0.06, 0.06) },
      peersScatter: [
        { name: "宁德时代", pe: 22.4, roe: 22.8, cap: 11820, self: true },
        { name: "比亚迪", pe: 24.1, roe: 19.6, cap: 9104 },
        { name: "亿纬锂能", pe: 28.6, roe: 12.4, cap: 1246 },
        { name: "欣旺达", pe: 32.4, roe: 9.8, cap: 486 },
        { name: "贝特瑞", pe: 18.6, roe: 12.8, cap: 312 },
        { name: "国轩高科", pe: 36.2, roe: 6.4, cap: 542 },
        { name: "孚能科技", pe: 0, roe: -6.2, cap: 168 },
        { name: "鹏辉能源", pe: 26.4, roe: 8.6, cap: 186 }
      ]
    }
  };

  /* --- 事件与拓扑 --- */
  const events = {
    /* ① 事件因果网络 */
    causal: {
      nodes: [
        { id: "e1", name: "固态电池路线图征求意见", cat: "policy", date: "2026-09-18", impact: "up", strength: 82, type: "政策" },
        { id: "e2", name: "三季度业绩预告超预期", cat: "finance", date: "2026-09-18", impact: "up", strength: 88, type: "业绩" },
        { id: "e3", name: "储能海外大单落地", cat: "order", date: "2026-09-10", impact: "up", strength: 74, type: "订单" },
        { id: "e4", name: "产能利用率回升至 82%", cat: "operate", date: "2026-09-06", impact: "up", strength: 66, type: "经营" },
        { id: "e5", name: "碳酸锂价格触底回升", cat: "supply", date: "2026-08-28", impact: "down", strength: 58, type: "成本" },
        { id: "e6", name: "北向资金连续净买入", cat: "fund", date: "2026-09-15", impact: "up", strength: 62, type: "资金" },
        { id: "e7", name: "股价放量突破年线", cat: "price", date: "2026-09-16", impact: "up", strength: 70, type: "价格" },
        { id: "e8", name: "欧洲合资工厂提前投产", cat: "capacity", date: "2026-08-20", impact: "up", strength: 68, type: "产能" },
        { id: "e9", name: "机构上调目标价至 320 元", cat: "rating", date: "2026-09-18", impact: "up", strength: 60, type: "评级" },
        { id: "e10", name: "程序化交易新规征求意见", cat: "policy", date: "2026-09-16", impact: "down", strength: 44, type: "监管" },
        { id: "e11", name: "二线厂商价格战加剧", cat: "compete", date: "2026-09-04", impact: "down", strength: 52, type: "竞争" },
        { id: "e12", name: "毛利率连续三季回升", cat: "finance", date: "2026-09-18", impact: "up", strength: 76, type: "业绩" }
      ],
      links: [
        { source: "e1", target: "e2", strength: 46, note: "政策利好提升行业景气预期" },
        { source: "e1", target: "e8", strength: 38, note: "技术路线明确加速海外扩产" },
        { source: "e3", target: "e2", strength: 72, note: "高毛利储能订单贡献利润" },
        { source: "e4", target: "e12", strength: 68, note: "产能利用率提升摊薄固定成本" },
        { source: "e5", target: "e12", strength: -42, note: "原材料涨价压制毛利（负向）" },
        { source: "e2", target: "e9", strength: 74, note: "业绩超预期驱动评级上调" },
        { source: "e12", target: "e9", strength: 58, note: "盈利质量改善支撑估值" },
        { source: "e6", target: "e7", strength: 62, note: "增量资金推动价格突破" },
        { source: "e7", target: "e9", strength: 40, note: "技术面转强增强机构信心" },
        { source: "e9", target: "e6", strength: 52, note: "评级上调吸引北向增配" },
        { source: "e10", target: "e7", strength: -36, note: "监管收紧压制短期活跃度（负向）" },
        { source: "e11", target: "e12", strength: -48, note: "价格战侵蚀行业整体毛利（负向）" },
        { source: "e8", target: "e3", strength: 64, note: "海外产能支撑大单交付" }
      ]
    },
    /* ④ 自上而下影响传导 */
    topdown: {
      nodes: [
        { name: "宏观：稳增长政策加码", level: 0, impact: "up", strength: 72 },
        { name: "宏观：美联储降息预期", level: 0, impact: "up", strength: 64 },
        { name: "政策：固态电池路线图", level: 0, impact: "up", strength: 82 },
        { name: "政策：程序化交易新规", level: 0, impact: "down", strength: 44 },
        { name: "行业：电池", level: 1, impact: "up", strength: 78 },
        { name: "行业：储能", level: 1, impact: "up", strength: 86 },
        { name: "行业：新能源汽车", level: 1, impact: "up", strength: 74 },
        { name: "行业：锂资源", level: 1, impact: "down", strength: 52 },
        { name: "宁德时代", level: 2, impact: "up", strength: 84 },
        { name: "比亚迪", level: 2, impact: "up", strength: 68 },
        { name: "亿纬锂能", level: 2, impact: "up", strength: 62 },
        { name: "天齐锂业", level: 2, impact: "down", strength: 58 }
      ],
      links: [
        { source: "宏观：稳增长政策加码", target: "行业：新能源汽车", value: 62, note: "以旧换新与购置税优惠延续" },
        { source: "宏观：美联储降息预期", target: "行业：储能", value: 48, note: "海外融资成本下降，大储装机提速" },
        { source: "政策：固态电池路线图", target: "行业：电池", value: 82, note: "技术路线明确，龙头受益" },
        { source: "政策：程序化交易新规", target: "行业：电池", value: 18, note: "短期活跃度下降" },
        { source: "行业：电池", target: "宁德时代", value: 74, note: "全球市占率第一" },
        { source: "行业：电池", target: "亿纬锂能", value: 42, note: "二线受益" },
        { source: "行业：储能", target: "宁德时代", value: 68, note: "海外大储主供" },
        { source: "行业：新能源汽车", target: "宁德时代", value: 52, note: "动力电池配套" },
        { source: "行业：新能源汽车", target: "比亚迪", value: 88, note: "整车与电池一体化" },
        { source: "行业：锂资源", target: "天齐锂业", value: 76, note: "锂价承压" }
      ]
    },
    /* ③ 产业链传导（复用 industry.chain，事件页展示事件化版本） */
    /* ② 公司股权关系（复用 equity.graph） */
    /* 时间轴泳道 */
    lanes: [
      { name: "政策 / 监管", color: "var(--chart-1)", items: [
        { date: "2026-06-12", title: "动力电池回收管理办法发布", impact: "up" },
        { date: "2026-09-16", title: "程序化交易新规征求意见", impact: "down" },
        { date: "2026-09-18", title: "固态电池产业化路线图征求意见", impact: "up" }
      ] },
      { name: "公司公告", color: "var(--chart-2)", items: [
        { date: "2026-05-14", title: "时代广汽合资设立公告", impact: "up" },
        { date: "2026-06-24", title: "欧洲合资建厂公告", impact: "up" },
        { date: "2026-08-12", title: "增资时代智能", impact: "up" },
        { date: "2026-09-18", title: "三季度业绩预告", impact: "up" }
      ] },
      { name: "行业动态", color: "var(--chart-3)", items: [
        { date: "2026-07-08", title: "碳酸锂价格触底", impact: "down" },
        { date: "2026-08-20", title: "欧洲工厂提前投产", impact: "up" },
        { date: "2026-09-04", title: "二线厂商价格战加剧", impact: "down" },
        { date: "2026-09-10", title: "储能海外大单落地", impact: "up" }
      ] },
      { name: "资金动向", color: "var(--chart-4)", items: [
        { date: "2026-08-30", title: "北向增持 0.22%", impact: "up" },
        { date: "2026-09-12", title: "高管减持计划披露", impact: "down" },
        { date: "2026-09-15", title: "北向连续三日净买入", impact: "up" },
        { date: "2026-09-18", title: "主力净流入 18.6 亿", impact: "up" }
      ] },
      { name: "机构观点", color: "var(--chart-7)", items: [
        { date: "2026-07-28", title: "中金维持增持，目标价 300", impact: "up" },
        { date: "2026-08-16", title: "某券商下调至中性", impact: "down" },
        { date: "2026-09-18", title: "机构上调目标价至 320", impact: "up" }
      ] }
    ],
    table: [
      { date: "2026-09-18", type: "业绩", title: "三季度业绩预告：净利润同比预增 32%–45%", impact: "up", strength: 88, source: "公司公告", parties: "宁德时代", auto: true },
      { date: "2026-09-18", type: "政策", title: "固态电池产业化路线图征求意见稿发布", impact: "up", strength: 82, source: "工信部", parties: "电池行业", auto: true },
      { date: "2026-09-18", type: "评级", title: "3 家机构上调目标价，一致目标价 320 元", impact: "up", strength: 60, source: "机构评级汇总", parties: "宁德时代", auto: true },
      { date: "2026-09-16", type: "监管", title: "证监会就程序化交易新规公开征求意见", impact: "down", strength: 44, source: "证监会", parties: "全市场", auto: true },
      { date: "2026-09-15", type: "资金", title: "北向资金连续 3 日净买入电池板块", impact: "up", strength: 62, source: "资金流统计", parties: "电池行业", auto: true },
      { date: "2026-09-12", type: "减持", title: "董事李平披露减持计划，不超过 0.5%", impact: "down", strength: 38, source: "公司公告", parties: "宁德时代", auto: true },
      { date: "2026-09-10", type: "订单", title: "签订海外储能框架协议，规模约 12GWh", impact: "up", strength: 74, source: "公司公告", parties: "宁德时代 · 时代储能", auto: true },
      { date: "2026-09-04", type: "竞争", title: "二线电池厂商报价下调 8%，价格战加剧", impact: "down", strength: 52, source: "行业资讯", parties: "电池行业", auto: true },
      { date: "2026-08-28", type: "成本", title: "碳酸锂现货价格触底回升 6.4%", impact: "down", strength: 58, source: "大宗商品行情", parties: "上游材料", auto: true },
      { date: "2026-08-20", type: "产能", title: "西班牙合资工厂提前投产，规划 50GWh", impact: "up", strength: 68, source: "公司公告", parties: "宁德时代 · Stellantis", auto: true },
      { date: "2026-08-12", type: "投资", title: "增资时代智能 28.6 亿元，持股升至 34%", impact: "up", strength: 56, source: "公司公告", parties: "宁德时代 · 时代智能", auto: true },
      { date: "2026-07-28", type: "评级", title: "中金维持增持评级，目标价 300 元", impact: "up", strength: 48, source: "机构评级汇总", parties: "宁德时代", auto: true }
    ],
    typeStats: [
      { type: "业绩", up: 8, down: 1, neutral: 2 },
      { type: "政策", up: 6, down: 4, neutral: 3 },
      { type: "资金", up: 9, down: 3, neutral: 1 },
      { type: "订单", up: 5, down: 0, neutral: 1 },
      { type: "竞争", up: 1, down: 6, neutral: 2 },
      { type: "监管", up: 0, down: 5, neutral: 2 }
    ]
  };

  /* --- 风险与舆情 --- */
  const risk = {
    matrix: [
      { type: "商誉减值", severity: 2, probability: 2, level: 2, note: "商誉 22.8 亿元，占净资产 1.2%，风险低" },
      { type: "股权质押", severity: 1, probability: 1, level: 1, note: "质押比例 2.4%，无平仓风险" },
      { type: "诉讼仲裁", severity: 2, probability: 2, level: 2, note: "专利诉讼 3 起，均为主动维权" },
      { type: "监管问询", severity: 2, probability: 3, level: 3, note: "2025 年收到 1 次年报问询，已回复" },
      { type: "客户集中", severity: 3, probability: 3, level: 3, note: "前五大客户占比 42%，相对分散" },
      { type: "价格战", severity: 4, probability: 4, level: 4, note: "二线厂商降价，行业毛利承压" },
      { type: "原材料涨价", severity: 4, probability: 3, level: 4, note: "碳酸锂价格波动直接影响成本" },
      { type: "技术路线迭代", severity: 4, probability: 2, level: 3, note: "固态电池对现有产线的替代风险" },
      { type: "海外贸易壁垒", severity: 4, probability: 4, level: 4, note: "欧美关税与本地化要求提升" },
      { type: "减持压力", severity: 2, probability: 4, level: 3, note: "高管减持计划进行中" },
      { type: "退市风险", severity: 5, probability: 1, level: 2, note: "不存在退市风险" },
      { type: "汇率波动", severity: 3, probability: 3, level: 3, note: "海外收入占比提升，汇兑影响增大" }
    ],
    alerts: [
      { date: "2026-09-12", level: 3, type: "减持", title: "董事李平披露减持计划", detail: "计划自公告日起 15 个交易日后 3 个月内，以集中竞价方式减持不超过 420 万股，占总股本 0.1%。", source: "公司公告" },
      { date: "2026-09-04", level: 4, type: "竞争", title: "二线厂商报价下调 8%", detail: "行业资讯显示，中创新航、欣旺达对部分车企客户报价下调约 8%，价格竞争加剧。", source: "行业资讯" },
      { date: "2026-08-28", level: 4, type: "成本", title: "碳酸锂价格触底回升 6.4%", detail: "电池级碳酸锂现货均价自低点回升 6.4%，短期对成本端形成压力。", source: "大宗商品行情" },
      { date: "2026-08-16", level: 3, type: "评级", title: "某券商下调至中性", detail: "某券商将评级由增持下调至中性，理由为行业竞争加剧与海外政策不确定性。", source: "机构评级汇总" },
      { date: "2026-07-15", level: 4, type: "贸易", title: "欧盟对动力电池加征关税终裁", detail: "欧盟终裁对原产于中国的动力电池加征关税，公司通过本地化建厂对冲。", source: "新闻资讯" },
      { date: "2026-06-20", level: 2, type: "专利", title: "公司主动提起专利侵权诉讼", detail: "公司对两家二线厂商提起专利侵权诉讼，涉及电池结构件相关专利。", source: "公司公告" },
      { date: "2026-05-08", level: 3, type: "质押", title: "股东质押比例变动", detail: "某股东质押比例由 1.8% 升至 2.4%，仍处低位。", source: "公司公告" },
      { date: "2026-04-22", level: 2, type: "问询", title: "收到年报问询函并已回复", detail: "交易所就应收账款与存货跌价准备计提进行问询，公司已按期回复。", source: "交易所" }
    ],
    sentiment: {
      labels: ["04", "05", "06", "07", "08", "09"],
      score: [58, 62, 56, 64, 68, 74],
      positive: [42, 48, 40, 52, 58, 66],
      negative: [18, 16, 22, 14, 12, 9],
      neutral: [40, 36, 38, 34, 30, 25]
    },
    hotWords: [
      { word: "业绩预增", weight: 92, tone: "up" },
      { word: "固态电池", weight: 86, tone: "up" },
      { word: "储能大单", weight: 74, tone: "up" },
      { word: "目标价上调", weight: 68, tone: "up" },
      { word: "北向增持", weight: 62, tone: "up" },
      { word: "价格战", weight: 58, tone: "down" },
      { word: "碳酸锂涨价", weight: 52, tone: "down" },
      { word: "欧盟关税", weight: 48, tone: "down" },
      { word: "高管减持", weight: 44, tone: "down" },
      { word: "产能利用率", weight: 38, tone: "neutral" }
    ],
    radar: {
      indicators: [
        { name: "财务风险", max: 100 },
        { name: "经营风险", max: 100 },
        { name: "治理风险", max: 100 },
        { name: "市场风险", max: 100 },
        { name: "合规风险", max: 100 },
        { name: "舆情风险", max: 100 }
      ],
      series: [
        { name: "本公司", values: [18, 32, 16, 58, 22, 26] },
        { name: "行业均值", values: [34, 46, 28, 52, 32, 38] }
      ]
    }
  };

  /* --- 机构评级与盈利预测 --- */
  const ratings = {
    distribution: [
      { name: "买入", value: 18, color: "var(--up)" },
      { name: "增持", value: 12, color: "var(--chart-1)" },
      { name: "中性", value: 4, color: "var(--flat)" },
      { name: "减持", value: 1, color: "var(--down)" },
      { name: "卖出", value: 0, color: "var(--text-3)" }
    ],
    targetPrices: [
      { org: "中金公司", analyst: "王某某", date: "2026-09-18", rating: "买入", target: 320, prev: 300, upside: 19.2 },
      { org: "华泰证券", analyst: "李某某", date: "2026-09-18", rating: "买入", target: 312, prev: 296, upside: 16.2 },
      { org: "中信证券", analyst: "张某某", date: "2026-09-18", rating: "增持", target: 305, prev: 288, upside: 13.6 },
      { org: "国泰海通", analyst: "陈某某", date: "2026-09-10", rating: "买入", target: 318, prev: 302, upside: 18.5 },
      { org: "招商证券", analyst: "刘某某", date: "2026-09-06", rating: "增持", target: 296, prev: 290, upside: 10.3 },
      { org: "广发证券", analyst: "赵某某", date: "2026-08-28", rating: "买入", target: 308, prev: 292, upside: 14.7 },
      { org: "东吴证券", analyst: "孙某某", date: "2026-08-20", rating: "增持", target: 288, prev: 276, upside: 7.3 },
      { org: "某券商", analyst: "周某某", date: "2026-08-16", rating: "中性", target: 252, prev: 286, upside: -6.1 },
      { org: "兴业证券", analyst: "吴某某", date: "2026-08-08", rating: "买入", target: 302, prev: 284, upside: 12.5 },
      { org: "民生证券", analyst: "郑某某", date: "2026-07-28", rating: "增持", target: 286, prev: 272, upside: 6.6 },
      { org: "申万宏源", analyst: "冯某某", date: "2026-07-18", rating: "买入", target: 298, prev: 280, upside: 11.0 },
      { org: "长江证券", analyst: "何某某", date: "2026-07-08", rating: "增持", target: 282, prev: 268, upside: 5.1 }
    ],
    consensus: {
      years: ["2026E", "2027E", "2028E"],
      eps: [15.86, 19.42, 23.64],
      revenue: [5120.4, 6186.2, 7268.4],
      netProfit: [786.2, 962.4, 1172.6],
      pe: [16.9, 13.8, 11.4],
      analystCount: [35, 33, 28]
    },
    history: {
      labels: ["25-10", "25-11", "25-12", "26-01", "26-02", "26-03", "26-04", "26-05", "26-06", "26-07", "26-08", "26-09"],
      buy: [12, 13, 14, 14, 15, 15, 16, 16, 17, 17, 18, 18],
      hold: [10, 10, 11, 11, 12, 12, 12, 12, 13, 13, 12, 12],
      neutral: [6, 6, 5, 5, 5, 5, 4, 4, 4, 5, 5, 4],
      reduce: [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1],
      avgTarget: [268, 272, 276, 278, 282, 284, 286, 288, 292, 296, 302, 308]
    }
  };

  /* ============================================================
     4. 自选股
     ============================================================ */
  const watchlist = {
    groups: [
      { id: "all", name: "全部", count: 14 },
      { id: "core", name: "核心持仓", count: 4 },
      { id: "watch", name: "重点观察", count: 6 },
      { id: "chain", name: "产业链跟踪", count: 4 }
    ],
    items: [
      { code: "300750", name: "宁德时代", group: "core", price: 268.42, chg: 12.46, pct: 4.86, d5: 9.86, d20: 22.4, volRatio: 1.86, turnover: 1.42, mainFlow: 18.62, industry: "电池", alert: true, spark: line(201, 24, 256, 0.05, 0.012) },
      { code: "600519", name: "贵州茅台", group: "core", price: 1568.2, chg: 19.24, pct: 1.24, d5: 2.42, d20: 6.86, volRatio: 1.12, turnover: 0.28, mainFlow: 2.86, industry: "白酒", alert: false, spark: line(202, 24, 1549, 0.012, 0.006) },
      { code: "002594", name: "比亚迪", group: "core", price: 312.66, chg: 7.36, pct: 2.41, d5: 5.24, d20: 12.86, volRatio: 1.42, turnover: 1.06, mainFlow: 6.42, industry: "汽车整车", alert: true, spark: line(203, 24, 305, 0.024, 0.009) },
      { code: "688256", name: "寒武纪", group: "core", price: 892.6, chg: 69.28, pct: 8.42, d5: 18.62, d20: 42.86, volRatio: 2.42, turnover: 3.86, mainFlow: 9.86, industry: "半导体", alert: true, spark: line(204, 24, 823, 0.082, 0.019) },
      { code: "300308", name: "中际旭创", group: "watch", price: 186.44, chg: 11.92, pct: 6.82, d5: 14.26, d20: 32.4, volRatio: 2.14, turnover: 5.62, mainFlow: 12.86, industry: "光模块", alert: true, spark: line(205, 24, 174, 0.068, 0.017) },
      { code: "601012", name: "隆基绿能", group: "watch", price: 21.42, chg: 0.62, pct: 2.98, d5: 6.42, d20: 9.86, volRatio: 1.44, turnover: 1.86, mainFlow: 4.12, industry: "光伏设备", alert: false, spark: line(206, 24, 20.8, 0.03, 0.012) },
      { code: "600438", name: "通威股份", group: "watch", price: 28.66, chg: 0.9, pct: 3.24, d5: 7.24, d20: 4.62, volRatio: 1.76, turnover: 2.86, mainFlow: 3.86, industry: "光伏设备", alert: false, spark: line(207, 24, 27.76, 0.032, 0.013) },
      { code: "688981", name: "中芯国际", group: "watch", price: 86.24, chg: 2.42, pct: 2.88, d5: 8.42, d20: 16.24, volRatio: 1.68, turnover: 2.24, mainFlow: 6.42, industry: "半导体", alert: false, spark: line(208, 24, 83.8, 0.028, 0.013) },
      { code: "688041", name: "海光信息", group: "watch", price: 168.86, chg: 6.42, pct: 3.96, d5: 12.86, d20: 26.42, volRatio: 1.84, turnover: 2.42, mainFlow: 5.62, industry: "半导体", alert: false, spark: line(209, 24, 162.4, 0.039, 0.014) },
      { code: "300124", name: "汇川技术", group: "watch", price: 62.86, chg: 1.24, pct: 2.01, d5: 4.86, d20: 8.62, volRatio: 1.38, turnover: 1.24, mainFlow: 2.42, industry: "自动化设备", alert: false, spark: line(210, 24, 61.62, 0.02, 0.011) },
      { code: "835185", name: "贝特瑞", group: "chain", price: 42.86, chg: 2.28, pct: 5.62, d5: 11.42, d20: 18.62, volRatio: 3.16, turnover: 4.24, mainFlow: 0.86, industry: "电池", alert: false, spark: line(211, 24, 40.6, 0.056, 0.016) },
      { code: "002466", name: "天齐锂业", group: "chain", price: 34.28, chg: -1.16, pct: -3.28, d5: -8.62, d20: -14.26, volRatio: 2.16, turnover: 3.42, mainFlow: -5.62, industry: "小金属", alert: true, spark: line(212, 24, 35.44, -0.032, 0.015) },
      { code: "002460", name: "赣锋锂业", group: "chain", price: 38.62, chg: 1.24, pct: 3.32, d5: 6.86, d20: -2.42, volRatio: 2.24, turnover: 3.86, mainFlow: 3.42, industry: "小金属", alert: false, spark: line(213, 24, 37.38, 0.033, 0.016) },
      { code: "601127", name: "赛力斯", group: "chain", price: 128.42, chg: 5.24, pct: 4.26, d5: 9.62, d20: 21.42, volRatio: 1.92, turnover: 2.86, mainFlow: 7.24, industry: "汽车整车", alert: false, spark: line(214, 24, 123.2, 0.042, 0.015) }
    ]
  };

  /* ============================================================
     5. 选股器
     ============================================================ */
  const screener = {
    presets: [
      { id: "p1", name: "均线多头排列", desc: "MA5 > MA10 > MA20 > MA60 且当日放量", hits: 186, tags: ["技术面"] },
      { id: "p2", name: "主力连续三日净流入", desc: "连续 3 个交易日主力资金净流入且累计超 1 亿", hits: 92, tags: ["资金面"] },
      { id: "p3", name: "低估值高 ROE", desc: "PE < 25 且 ROE > 15% 且资产负债率 < 60%", hits: 64, tags: ["估值", "盈利"] },
      { id: "p4", name: "放量突破年线", desc: "当日收盘上穿 MA250 且成交量 > 5 日均量 1.8 倍", hits: 41, tags: ["技术面"] },
      { id: "p5", name: "北向持续增持", desc: "北向持股比例连续 4 周上升", hits: 128, tags: ["资金面"] },
      { id: "p6", name: "业绩预增 + 估值合理", desc: "业绩预告净利润增速 > 30% 且 PE < 35", hits: 57, tags: ["业绩", "估值"] }
    ],
    groups: [
      { id: "valuation", name: "估值", open: true, conds: [
        { id: "pe", name: "市盈率 PE", unit: "倍", min: "", max: "25" },
        { id: "pb", name: "市净率 PB", unit: "倍", min: "", max: "" },
        { id: "ps", name: "市销率 PS", unit: "倍", min: "", max: "" },
        { id: "div", name: "股息率", unit: "%", min: "1", max: "" }
      ] },
      { id: "growth", name: "成长", open: true, conds: [
        { id: "revg", name: "营收增速", unit: "%", min: "10", max: "" },
        { id: "npg", name: "净利增速", unit: "%", min: "20", max: "" }
      ] },
      { id: "profit", name: "盈利", open: true, conds: [
        { id: "roe", name: "ROE", unit: "%", min: "15", max: "" },
        { id: "gm", name: "毛利率", unit: "%", min: "", max: "" },
        { id: "nm", name: "净利率", unit: "%", min: "", max: "" }
      ] },
      { id: "fund", name: "资金", open: false, conds: [
        { id: "mainflow", name: "主力净流入", unit: "亿元", min: "1", max: "" },
        { id: "north", name: "北向持股变化", unit: "%", min: "", max: "" },
        { id: "margin", name: "融资余额变化", unit: "%", min: "", max: "" }
      ] },
      { id: "tech", name: "技术", open: false, conds: [
        { id: "ma", name: "均线形态", unit: "", min: "", max: "", select: ["均线多头排列", "均线空头排列", "站上年线", "跌破年线"] },
        { id: "macd", name: "MACD", unit: "", min: "", max: "", select: ["金叉", "死叉"] },
        { id: "vol", name: "放量倍数", unit: "倍", min: "1.8", max: "" },
        { id: "newhigh", name: "创 N 日新高", unit: "日", min: "60", max: "" }
      ] },
      { id: "scope", name: "范围", open: true, conds: [
        { id: "board", name: "板块", unit: "", min: "", max: "", select: ["全部", "沪市主板", "深市主板", "创业板", "科创板", "北交所"] },
        { id: "industry", name: "行业", unit: "", min: "", max: "", select: ["全部", "电池", "半导体", "光伏设备", "白酒", "汽车整车", "证券"] },
        { id: "cap", name: "总市值", unit: "亿元", min: "100", max: "20000" }
      ] }
    ],
    activeConds: [
      { label: "PE ≤ 25", group: "估值" },
      { label: "净利增速 ≥ 20%", group: "成长" },
      { label: "ROE ≥ 15%", group: "盈利" },
      { label: "板块 = 全部", group: "范围" },
      { label: "总市值 ≥ 100 亿", group: "范围" }
    ],
    results: [
      { code: "300750", name: "宁德时代", industry: "电池", board: "创业板", price: 268.42, pct: 4.86, pe: 22.4, pb: 4.62, roe: 22.8, npg: 31.3, revg: 18.6, mainFlow: 18.62, cap: 11820, score: 92 },
      { code: "600519", name: "贵州茅台", industry: "白酒", board: "沪市主板", price: 1568.2, pct: 1.24, pe: 21.7, pb: 7.86, roe: 32.4, npg: 14.2, revg: 12.4, mainFlow: 2.86, cap: 19680, score: 88 },
      { code: "002594", name: "比亚迪", industry: "汽车整车", board: "深市主板", price: 312.66, pct: 2.41, pe: 24.1, pb: 4.18, roe: 19.6, npg: 24.2, revg: 21.8, mainFlow: 6.42, cap: 9104, score: 86 },
      { code: "601899", name: "紫金矿业", industry: "贵金属", board: "沪市主板", price: 19.86, pct: 1.74, pe: 14.2, pb: 3.42, roe: 22.6, npg: 28.6, revg: 16.2, mainFlow: 3.24, cap: 5246, score: 84 },
      { code: "300124", name: "汇川技术", industry: "自动化设备", board: "创业板", price: 62.86, pct: 2.01, pe: 32.4, pb: 6.24, roe: 20.6, npg: 26.4, revg: 22.4, mainFlow: 2.42, cap: 1682, score: 79 },
      { code: "600809", name: "山西汾酒", industry: "白酒", board: "沪市主板", price: 186.42, pct: 1.56, pe: 19.2, pb: 5.42, roe: 28.2, npg: 22.8, revg: 18.4, mainFlow: 1.62, cap: 2274, score: 82 },
      { code: "000333", name: "美的集团", industry: "白色家电", board: "深市主板", price: 78.62, pct: 0.54, pe: 14.8, pb: 3.24, roe: 22.4, npg: 12.6, revg: 8.4, mainFlow: 1.06, cap: 5986, score: 76 },
      { code: "600900", name: "长江电力", industry: "电力", board: "沪市主板", price: 28.42, pct: 0.21, pe: 19.6, pb: 2.86, roe: 14.8, npg: 6.4, revg: 4.2, mainFlow: 0.62, cap: 6952, score: 68 },
      { code: "601127", name: "赛力斯", industry: "汽车整车", board: "沪市主板", price: 128.42, pct: 4.26, pe: 42.6, pb: 12.4, roe: 28.4, npg: 62.4, revg: 48.6, mainFlow: 7.24, cap: 1938, score: 74 },
      { code: "688981", name: "中芯国际", industry: "半导体", board: "科创板", price: 86.24, pct: 2.88, pe: 62.4, pb: 4.24, roe: 5.8, npg: 18.6, revg: 14.2, mainFlow: 6.42, cap: 6842, score: 66 },
      { code: "300760", name: "迈瑞医疗", industry: "医疗器械", board: "创业板", price: 268.86, pct: 0.42, pe: 29.8, pb: 7.24, roe: 26.8, npg: 12.4, revg: 10.6, mainFlow: 0.86, cap: 3262, score: 72 },
      { code: "002415", name: "海康威视", industry: "安防设备", board: "深市主板", price: 32.46, pct: 0.56, pe: 22.4, pb: 3.86, roe: 18.2, npg: 10.4, revg: 8.6, mainFlow: 0.94, cap: 2986, score: 70 }
    ],
    industryDist: [
      { name: "白酒", value: 2 },
      { name: "汽车整车", value: 2 },
      { name: "电池", value: 1 },
      { name: "半导体", value: 1 },
      { name: "贵金属", value: 1 },
      { name: "自动化设备", value: 1 },
      { name: "白色家电", value: 1 },
      { name: "电力", value: 1 },
      { name: "医疗器械", value: 1 },
      { name: "安防设备", value: 1 }
    ]
  };

  /* ============================================================
     6. 提醒与通知
     ============================================================ */
  const alerts = {
    rules: [
      { id: "r1", code: "300750", name: "宁德时代", type: "价格", desc: "股价上穿 265.00 元", threshold: "265.00", status: "on", triggers: 2, lastTrigger: "2026-09-18 14:32", channels: ["站内", "浏览器", "震动"] },
      { id: "r2", code: "300750", name: "宁德时代", type: "技术指标", desc: "MACD 金叉（日线）", threshold: "—", status: "on", triggers: 3, lastTrigger: "2026-09-16 15:00", channels: ["站内", "浏览器"] },
      { id: "r3", code: "600519", name: "贵州茅台", type: "价格", desc: "跌幅超过 3%", threshold: "-3%", status: "on", triggers: 1, lastTrigger: "2026-08-26 14:52", channels: ["站内", "浏览器", "震动"] },
      { id: "r4", code: "002466", name: "天齐锂业", type: "技术指标", desc: "跌破 MA20（日线）", threshold: "—", status: "on", triggers: 4, lastTrigger: "2026-09-17 15:00", channels: ["站内", "浏览器"] },
      { id: "r5", code: "688256", name: "寒武纪", type: "资金流", desc: "主力净流入超 5 亿元", threshold: "5.00", status: "on", triggers: 5, lastTrigger: "2026-09-18 14:05", channels: ["站内", "浏览器", "震动"] },
      { id: "r6", code: "300308", name: "中际旭创", type: "资金流", desc: "北向持股比例上升超 0.5%", threshold: "0.50", status: "off", triggers: 0, lastTrigger: "—", channels: ["站内"] },
      { id: "r7", code: "002594", name: "比亚迪", type: "公告事件", desc: "发布定期报告 / 业绩预告", threshold: "—", status: "on", triggers: 2, lastTrigger: "2026-08-28 08:30", channels: ["站内", "浏览器"] },
      { id: "r8", code: "601127", name: "赛力斯", type: "公告事件", desc: "股东增减持公告", threshold: "—", status: "on", triggers: 1, lastTrigger: "2026-07-22 19:10", channels: ["站内"] },
      { id: "r9", code: "835185", name: "贝特瑞", type: "价格", desc: "创 60 日新高", threshold: "60日", status: "on", triggers: 1, lastTrigger: "2026-09-18 13:48", channels: ["站内", "浏览器", "震动"] },
      { id: "r10", code: "601012", name: "隆基绿能", type: "技术指标", desc: "均线多头排列", threshold: "—", status: "off", triggers: 0, lastTrigger: "—", channels: ["站内"] }
    ],
    types: [
      { id: "price", name: "价格与涨跌幅", desc: "股价上穿/下穿指定价格、当日涨跌幅超阈值、创 N 日新高/新低", icon: "◈" },
      { id: "tech", name: "技术指标", desc: "上穿/下穿 MA5/10/20/60、均线金叉死叉、MACD 金叉、放量突破", icon: "◉" },
      { id: "flow", name: "资金流异动", desc: "主力净流入超阈值、北向持股变化、龙虎榜上榜、融资余额异动", icon: "◐" },
      { id: "event", name: "公告与事件", desc: "定期报告、业绩预告、重大合同、股东增减持、监管函", icon: "◍" }
    ],
    history: [
      { time: "2026-09-18 14:32", code: "300750", name: "宁德时代", rule: "股价上穿 265.00 元", value: "268.42", channel: "站内 + 浏览器 + 震动" },
      { time: "2026-09-18 14:05", code: "688256", name: "寒武纪", rule: "主力净流入超 5 亿元", value: "9.86 亿元", channel: "站内 + 浏览器 + 震动" },
      { time: "2026-09-18 13:48", code: "835185", name: "贝特瑞", rule: "创 60 日新高", value: "42.86", channel: "站内 + 浏览器 + 震动" },
      { time: "2026-09-17 15:00", code: "002466", name: "天齐锂业", rule: "跌破 MA20（日线）", value: "MA20 = 36.42", channel: "站内 + 浏览器" },
      { time: "2026-09-16 15:00", code: "300750", name: "宁德时代", rule: "MACD 金叉（日线）", value: "DIF 上穿 DEA", channel: "站内 + 浏览器" },
      { time: "2026-08-28 08:30", code: "002594", name: "比亚迪", rule: "发布定期报告 / 业绩预告", value: "半年报", channel: "站内 + 浏览器" },
      { time: "2026-08-26 14:52", code: "600519", name: "贵州茅台", rule: "跌幅超过 3%", value: "-3.24%", channel: "站内 + 浏览器 + 震动" }
    ]
  };

  const notifications = [
    { id: "n1", time: "2026-09-18 14:32", code: "300750", name: "宁德时代", rule: "价格提醒", title: "股价上穿 265.00 元", body: "现价 268.42 元，涨幅 4.86%，成交量放大至 1.86 倍。", read: false, level: "up" },
    { id: "n2", time: "2026-09-18 14:05", code: "688256", name: "寒武纪", rule: "资金流提醒", title: "主力净流入 9.86 亿元", body: "超大单净流入 6.42 亿元，居半导体板块首位。", read: false, level: "up" },
    { id: "n3", time: "2026-09-18 13:48", code: "835185", name: "贝特瑞", rule: "价格提醒", title: "创 60 日新高 42.86 元", body: "换手率 4.24%，量比 3.16，固态电池主题催化。", read: false, level: "up" },
    { id: "n4", time: "2026-09-18 09:02", code: "300750", name: "宁德时代", rule: "公告事件", title: "三季度业绩预告发布", body: "预计净利润同比预增 32%–45%，超出市场一致预期。", read: false, level: "up" },
    { id: "n5", time: "2026-09-17 15:00", code: "002466", name: "天齐锂业", rule: "技术指标提醒", title: "跌破 MA20", body: "收盘 34.28 元，MA20 = 36.42 元，趋势转弱。", read: true, level: "down" },
    { id: "n6", time: "2026-09-16 15:00", code: "300750", name: "宁德时代", rule: "技术指标提醒", title: "MACD 金叉（日线）", body: "DIF 上穿 DEA，柱状线由负转正。", read: true, level: "up" },
    { id: "n7", time: "2026-09-12 19:20", code: "300750", name: "宁德时代", rule: "公告事件", title: "董事减持计划披露", body: "董事李平计划减持不超过 420 万股，占总股本 0.1%。", read: true, level: "down" },
    { id: "n8", time: "2026-09-10 10:36", code: "300750", name: "宁德时代", rule: "公告事件", title: "签订海外储能框架协议", body: "规模约 12GWh，预计 2027 年起分批交付。", read: true, level: "up" },
    { id: "n9", time: "2026-08-28 08:30", code: "002594", name: "比亚迪", rule: "公告事件", title: "半年报披露", body: "营收同比增长 21.8%，净利润同比增长 24.2%。", read: true, level: "up" },
    { id: "n10", time: "2026-08-26 14:52", code: "600519", name: "贵州茅台", rule: "价格提醒", title: "跌幅超过 3%", body: "当日收跌 3.24%，成交额 98.6 亿元。", read: true, level: "down" }
  ];

  /* ============================================================
     7. 用户、角色与功能点
     ============================================================ */
  const functionPoints = [
    { group: "模块访问", items: [
      { code: "market.view", name: "市场概览", desc: "查看大盘指数、涨跌家数、行业排行" },
      { code: "stock.search", name: "股票搜索", desc: "按代码、名称、拼音搜索股票" },
      { code: "stock.trend", name: "趋势与价格结构", desc: "K 线、均线、MACD、相对强弱" },
      { code: "stock.finance", name: "盈利与财务表现", desc: "营收、利润、毛利、ROE、现金流" },
      { code: "stock.equity", name: "公司投资与股权结构", desc: "股权拓扑、股东、机构持仓、质押" },
      { code: "stock.capital", name: "资金面与筹码", desc: "主力资金、北向、两融、龙虎榜、筹码分布" },
      { code: "stock.industry", name: "行业与同业对比", desc: "产业链拓扑、同业对比、估值分位" },
      { code: "stock.events", name: "事件时间线与影响", desc: "四种拓扑图与事件表" },
      { code: "stock.risk", name: "风险与舆情监控", desc: "风险矩阵、告警、舆情情绪" },
      { code: "stock.rating", name: "机构评级与盈利预测", desc: "评级分布、目标价、一致预期" },
      { code: "topology.view", name: "拓扑图总览", desc: "四种拓扑图集中对照页" }
    ] },
    { group: "自选与选股", items: [
      { code: "watchlist.view", name: "查看自选股", desc: "查看自选股盯盘列表" },
      { code: "watchlist.edit", name: "编辑自选股", desc: "添加、删除、分组、排序" },
      { code: "screener.use", name: "使用条件选股器", desc: "组合条件筛选全市场" },
      { code: "screener.saveStrategy", name: "保存选股策略", desc: "保存与载入个人策略" }
    ] },
    { group: "提醒与通知", items: [
      { code: "alert.manage", name: "管理提醒规则", desc: "新建、编辑、启停提醒规则" },
      { code: "notify.view", name: "查看通知中心", desc: "查看站内通知与历史提醒" }
    ] },
    { group: "数据范围", items: [
      { code: "data.scope.all", name: "全市场数据", desc: "可查询全市场任意股票" },
      { code: "data.scope.watchlist", name: "仅自选数据", desc: "仅可查询自选股范围内的股票" }
    ] },
    { group: "操作权限", items: [
      { code: "export.data", name: "导出数据", desc: "导出表格与图表为 CSV / PNG" },
      { code: "history.years", name: "历史数据年限", desc: "可查看的历史数据最大年限" },
      { code: "quota.daily", name: "单日查询上限", desc: "单日可发起的分析查询次数上限" }
    ] },
    { group: "编辑权限", items: [
      { code: "event.edit", name: "编辑事件标注", desc: "修改事件影响方向与强度" },
      { code: "strategy.share", name: "共享策略", desc: "将选股策略共享给其他用户" }
    ] },
    { group: "后台管理", items: [
      { code: "admin.users", name: "用户管理", desc: "新增、禁用、重置密码、分配角色" },
      { code: "admin.permissions", name: "角色与权限", desc: "配置角色与功能点开关" },
      { code: "admin.datasource", name: "数据源监控", desc: "查看采集状态、手工触发、重试" },
      { code: "admin.security", name: "登录与安全", desc: "登录日志、在线会话、锁定策略" }
    ] }
  ];

  const allFpCodes = [];
  functionPoints.forEach(function (g) { g.items.forEach(function (i) { allFpCodes.push(i.code); }); });

  const roles = [
    {
      id: "admin", name: "管理员", desc: "全部功能点，含后台管理", users: 1, builtin: true,
      fps: allFpCodes.slice()
    },
    {
      id: "user", name: "普通用户", desc: "只读分析 + 自选 + 选股 + 提醒，无导出与后台", users: 3, builtin: true,
      fps: [
        "market.view", "stock.search", "stock.trend", "stock.finance", "stock.equity",
        "stock.capital", "stock.industry", "stock.events", "stock.risk", "stock.rating",
        "topology.view", "watchlist.view", "watchlist.edit", "screener.use",
        "screener.saveStrategy", "alert.manage", "notify.view", "data.scope.all",
        "history.years", "quota.daily"
      ]
    },
    {
      id: "guest", name: "访客", desc: "仅市场概览 + 趋势 + 搜索，数据范围仅自选", users: 0, builtin: true,
      fps: ["market.view", "stock.search", "stock.trend", "data.scope.watchlist"]
    }
  ];

  const users = [
    { id: "u1", username: "admin", nickname: "管理员", role: "admin", status: "启用", lastLogin: "2026-09-18 09:12", ip: "192.168.1.8", devices: 2, created: "2026-01-06" },
    { id: "u2", username: "user", nickname: "普通用户", role: "user", status: "启用", lastLogin: "2026-09-18 13:48", ip: "192.168.1.24", devices: 3, created: "2026-02-14" },
    { id: "u3", username: "zhang", nickname: "张工", role: "user", status: "启用", lastLogin: "2026-09-17 20:06", ip: "10.0.0.32", devices: 1, created: "2026-03-02" },
    { id: "u4", username: "li", nickname: "李工", role: "user", status: "启用", lastLogin: "2026-09-15 08:42", ip: "10.0.0.51", devices: 2, created: "2026-04-18" },
    { id: "u5", username: "wang", nickname: "王工", role: "guest", status: "禁用", lastLogin: "2026-07-22 19:30", ip: "10.0.0.77", devices: 0, created: "2026-05-09" },
    { id: "u6", username: "visitor", nickname: "访客账号", role: "guest", status: "启用", lastLogin: "2026-09-18 10:24", ip: "203.0.113.42", devices: 1, created: "2026-06-01" }
  ];

  /* ============================================================
     8. 数据源与采集监控
     ============================================================ */
  const datasource = {
    sources: [
      { name: "东方财富", type: "行情 / 财务 / 资金流", status: "ok", lastOk: "2026-09-18 15:02", latency: 186, fails: 0, uptime: 99.8, domains: ["实时行情", "财务报表", "资金流向", "龙虎榜", "股东数据"] },
      { name: "新浪财经", type: "实时行情 / 分时", status: "ok", lastOk: "2026-09-18 15:02", latency: 124, fails: 0, uptime: 99.9, domains: ["实时行情", "分时数据", "指数行情"] },
      { name: "腾讯财经", type: "实时行情 / 公告", status: "warn", lastOk: "2026-09-18 14:58", latency: 486, fails: 3, uptime: 97.2, domains: ["实时行情", "公告标题", "板块数据"] },
      { name: "交易所公开数据", type: "公告 / 龙虎榜 / 两融", status: "ok", lastOk: "2026-09-18 15:00", latency: 642, fails: 1, uptime: 99.1, domains: ["公告原文", "龙虎榜", "融资融券"] },
      { name: "巨潮资讯", type: "公告全文", status: "ok", lastOk: "2026-09-18 14:30", latency: 862, fails: 2, uptime: 98.4, domains: ["公告全文", "定期报告"] },
      { name: "国家统计局 / 央行", type: "宏观数据", status: "idle", lastOk: "2026-09-15 09:00", latency: 386, fails: 0, uptime: 100, domains: ["宏观指标", "利率数据"] }
    ],
    tasks: [
      { name: "全市场行情快照", cycle: "交易时段每 3 秒", last: "2026-09-18 15:00:03", cost: "0.8s", status: "ok", next: "2026-09-21 09:30", rows: "5,428", source: "东方财富 + 新浪" },
      { name: "自选股实时推送", cycle: "交易时段每 3 秒", last: "2026-09-18 15:00:03", cost: "0.2s", status: "ok", next: "2026-09-21 09:30", rows: "14", source: "新浪财经" },
      { name: "日线行情入库", cycle: "每交易日 15:30", last: "2026-09-18 15:31:22", cost: "42.6s", status: "ok", next: "2026-09-21 15:30", rows: "5,428", source: "东方财富" },
      { name: "财务报表同步", cycle: "每交易日 18:00", last: "2026-09-18 18:04:16", cost: "126.4s", status: "ok", next: "2026-09-21 18:00", rows: "1,286", source: "东方财富" },
      { name: "资金流向采集", cycle: "每交易日 15:20", last: "2026-09-18 15:22:48", cost: "38.2s", status: "ok", next: "2026-09-21 15:20", rows: "5,428", source: "东方财富" },
      { name: "龙虎榜与两融", cycle: "每交易日 19:00", last: "2026-09-18 19:01:06", cost: "12.4s", status: "ok", next: "2026-09-21 19:00", rows: "86", source: "交易所公开数据" },
      { name: "公告抓取与事件抽取", cycle: "每交易日 07:30 / 12:30 / 19:30", last: "2026-09-18 19:32:44", cost: "218.6s", status: "warn", next: "2026-09-21 07:30", rows: "412", source: "巨潮资讯 + 交易所" },
      { name: "机构评级汇总", cycle: "每交易日 20:00", last: "2026-09-18 20:02:12", cost: "26.8s", status: "ok", next: "2026-09-21 20:00", rows: "186", source: "东方财富" },
      { name: "舆情情绪计算", cycle: "每交易日 20:30", last: "2026-09-18 20:33:58", cost: "64.2s", status: "ok", next: "2026-09-21 20:30", rows: "1,842", source: "新闻资讯" },
      { name: "技术指标计算", cycle: "每交易日 16:00", last: "2026-09-18 16:02:36", cost: "88.4s", status: "ok", next: "2026-09-21 16:00", rows: "5,428", source: "本地计算" },
      { name: "提醒规则巡检", cycle: "交易时段每 30 秒", last: "2026-09-18 15:00:00", cost: "0.4s", status: "ok", next: "2026-09-21 09:30", rows: "10", source: "本地计算" },
      { name: "数据源连通性探测", cycle: "每 5 分钟", last: "2026-09-18 15:00:00", cost: "1.2s", status: "ok", next: "2026-09-18 15:05", rows: "6", source: "全部数据源" }
    ],
    failures: [
      { time: "2026-09-18 14:58", task: "公告抓取与事件抽取", source: "腾讯财经", error: "HTTP 429 Too Many Requests：触发频率限制", retry: "已自动重试 1 次，成功", level: "warn" },
      { time: "2026-09-18 14:42", task: "公告抓取与事件抽取", source: "腾讯财经", error: "响应超时（>5000ms）", retry: "已自动重试 2 次，成功", level: "warn" },
      { time: "2026-09-18 11:16", task: "财务报表同步", source: "巨潮资讯", error: "PDF 解析失败：字体编码异常（600xxx 一季报）", retry: "已加入待重试队列", level: "err" },
      { time: "2026-09-17 19:04", task: "龙虎榜与两融", source: "交易所公开数据", error: "字段结构变更：新增「交易单元代码」列", retry: "已按新结构解析", level: "warn" },
      { time: "2026-09-16 15:22", task: "资金流向采集", source: "东方财富", error: "连接重置", retry: "已自动重试 1 次，成功", level: "warn" }
    ],
    logs: [
      { time: "15:02:14", level: "INFO", msg: "行情快照完成：5,428 只，耗时 812ms" },
      { time: "15:02:11", level: "INFO", msg: "自选股推送：14 只，SignalR 广播成功 3 个连接" },
      { time: "15:00:03", level: "INFO", msg: "收盘，停止盘中轮询" },
      { time: "14:58:22", level: "WARN", msg: "腾讯财经 429，退避 2000ms 后重试" },
      { time: "14:42:06", level: "WARN", msg: "腾讯财经超时 5000ms，重试 2/3" },
      { time: "14:05:18", level: "INFO", msg: "提醒规则命中：688256 主力净流入 9.86 亿" },
      { time: "13:48:42", level: "INFO", msg: "提醒规则命中：835185 创 60 日新高" },
      { time: "11:16:38", level: "ERROR", msg: "PDF 解析失败：600xxx 一季报 字体编码异常，已入队重试" },
      { time: "09:30:00", level: "INFO", msg: "开盘，启动盘中轮询（3s）" },
      { time: "07:30:12", level: "INFO", msg: "公告抓取完成：412 条，事件抽取 186 条，人工待确认 0 条" }
    ]
  };

  /* ============================================================
     9. 会话与登录日志
     ============================================================ */
  const security = {
    sessions: [
      { user: "admin", device: "Chrome 142 · Windows 11", ip: "192.168.1.8", login: "2026-09-18 09:12", active: "2 分钟前", current: true },
      { user: "admin", device: "SA PWA · Android 16", ip: "203.0.113.42", login: "2026-09-18 08:40", active: "1 小时前", current: false },
      { user: "user", device: "Safari 20 · iOS 20", ip: "203.0.113.88", login: "2026-09-18 13:48", active: "3 分钟前", current: false },
      { user: "user", device: "Chrome 142 · macOS 16", ip: "10.0.0.24", login: "2026-09-18 10:06", active: "5 小时前", current: false },
      { user: "zhang", device: "Edge 142 · Windows 11", ip: "10.0.0.32", login: "2026-09-17 20:06", active: "20 小时前", current: false }
    ],
    loginLogs: [
      { time: "2026-09-18 13:48", user: "user", ip: "203.0.113.88", device: "Safari 20 · iOS 20", result: "成功", note: "PWA 登录" },
      { time: "2026-09-18 10:24", user: "visitor", ip: "203.0.113.42", device: "Chrome 142 · Android 16", result: "成功", note: "访客只读" },
      { time: "2026-09-18 10:12", user: "user", ip: "203.0.113.88", device: "Safari 20 · iOS 20", result: "失败", note: "密码错误 1/5" },
      { time: "2026-09-18 09:12", user: "admin", ip: "192.168.1.8", device: "Chrome 142 · Windows 11", result: "成功", note: "本机" },
      { time: "2026-09-18 08:40", user: "admin", ip: "203.0.113.42", device: "SA PWA · Android 16", result: "成功", note: "二次验证通过" },
      { time: "2026-09-17 20:06", user: "zhang", ip: "10.0.0.32", device: "Edge 142 · Windows 11", result: "成功", note: "局域网" },
      { time: "2026-09-17 19:58", user: "wang", ip: "10.0.0.77", device: "Chrome 142 · Windows 11", result: "拒绝", note: "账号已禁用" },
      { time: "2026-09-16 22:14", user: "unknown", ip: "198.51.100.7", device: "curl/8.6", result: "失败", note: "账号不存在，已触发限流" }
    ],
    policy: {
      twoFactor: true,
      lockThreshold: 5,
      lockMinutes: 15,
      sessionHours: 72,
      ipWhitelist: false,
      strongPassword: true
    }
  };

  /* ============================================================
     10. 导出
     ============================================================ */
  window.SA_DATA = {
    meta: {
      appName: "股析",
      appShort: "SA",
      appFull: "Stock Analysis",
      asOf: "2026-09-18 15:00",
      notice: "演示数据 · 非实时 · 不构成投资建议",
      focus: FOCUS
    },
    indices: indices,
    breadth: breadth,
    industries: industries,
    hotEvents: hotEvents,
    rankings: rankings,
    stocks: stocks,
    stockByCode: stockByCode,
    focusProfile: focusProfile,
    focusKline: focusKline,
    focusWeekly: focusWeekly,
    focusMonthly: focusMonthly,
    finance: finance,
    financeQuarters: financeQuarters,
    equity: equity,
    capital: capital,
    industry: industry,
    events: events,
    risk: risk,
    ratings: ratings,
    watchlist: watchlist,
    screener: screener,
    alerts: alerts,
    notifications: notifications,
    functionPoints: functionPoints,
    allFpCodes: allFpCodes,
    roles: roles,
    users: users,
    datasource: datasource,
    security: security,
    /* 工具 */
    util: { mulberry32: mulberry32, kline: kline, line: line }
  };
})();
