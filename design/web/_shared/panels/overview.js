/* ============================================================
   SA · 股析 — 个股面板：概览
   只有摘要字段的股票（生成股）显示摘要卡；
   主演示股 300750 额外显示行情、估值与财务摘要。
   ============================================================ */
(function () {
  "use strict";

  var D = window.SA_DATA;

  /* 惰性取图表工厂：模块顶层就解引用 SA_CHARTS，会在加载顺序有变时直接抛错，
     而面板除图表外的内容本来是可以渲染的。 */
  function charts() {
    return (window.SA_CHARTS && window.SA_CHARTS.F) || null;
  }

  function kpi(label, value, tone, unit) {
    return '<div class="kpi"><span class="kpi-label">' + label + "</span>" +
      '<span class="kpi-value is-sm ' + (tone || "") + '">' + value +
      (unit ? ' <span class="fs-11 t-3">' + unit + "</span>" : "") + "</span></div>";
  }

  function render(host, code) {
    var s = (D.allStockByCode && D.allStockByCode[code]) || (D.stockByCode && D.stockByCode[code]);
    if (!s) {
      host.innerHTML = '<div class="card"><div class="card-body fs-12 t-3">无该股票数据</div></div>';
      return;
    }

    var pctTone = s.pct >= 0 ? "t-up" : "t-down";
    var amount = s.cap * s.turnover / 100;

    var html =
      '<div class="grid grid-2">' +
        '<div class="card">' +
          '<div class="card-head"><span class="card-title">行情</span><span class="card-sub">当日收盘</span></div>' +
          '<div class="card-body grid grid-3 gap-3">' +
            kpi("现价", s.price.toFixed(2), pctTone, "元") +
            kpi("涨跌幅", (s.pct >= 0 ? "+" : "") + s.pct.toFixed(2) + "%", pctTone) +
            kpi("成交额", amount.toFixed(1), "", "亿") +
            kpi("换手率", s.turnover.toFixed(2) + "%") +
            kpi("量比", s.volRatio.toFixed(2)) +
            kpi("成交均价", (s.price / (1 + s.pct / 200)).toFixed(2), "", "元") +
          "</div>" +
        "</div>" +

        '<div class="card">' +
          '<div class="card-head"><span class="card-title">估值与盈利</span><span class="card-sub">最新报告期</span></div>' +
          '<div class="card-body grid grid-3 gap-3">' +
            kpi("PE(TTM)", s.pe > 0 ? s.pe.toFixed(1) : "—", s.pe > 0 ? "" : "t-3", s.pe > 0 ? "倍" : "亏损") +
            kpi("PB", s.pb.toFixed(2), "", "倍") +
            kpi("总市值", s.cap.toLocaleString(), "", "亿") +
            kpi("ROE", s.roe.toFixed(1) + "%", s.roe >= 0 ? "t-up" : "t-down") +
            kpi("主力净流入", (s.mainFlow >= 0 ? "+" : "") + s.mainFlow.toFixed(2), s.mainFlow >= 0 ? "t-up" : "t-down", "亿") +
            kpi("行业", s.industry) +
          "</div>" +
        "</div>" +
      "</div>" +

      '<div class="card mt-4">' +
        '<div class="card-head"><span class="card-title">近 30 日走势</span><span class="card-sub">前复权</span></div>' +
        '<div class="card-body"><div id="ovTrend" class="chart chart-md"></div></div>' +
      "</div>";

    host.innerHTML = html;

    var F = charts();
    if (s.spark && F) {
      /* F.area 的签名是 (el, {labels, data, unit})：颜色取图表色板第 1 位（品牌橙） */
      F.area("#ovTrend", {
        labels: s.spark.map(function (_, i) { return "D" + (i + 1); }),
        data: s.spark,
        unit: " 元"
      });
    }
  }

  window.SA_PANELS = window.SA_PANELS || {};
  window.SA_PANELS.overview = { key: "overview", title: "概览", render: render };
})();
