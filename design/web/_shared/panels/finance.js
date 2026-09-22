/* ============================================================
   SA · 股析 — 个股面板：财务
   由 design/web/stock.html#finance 机械迁移而来（批次 3）。
   面板只在激活时渲染：ECharts 在隐藏容器里初始化会得到 0×0 画布。
   ============================================================ */
(function () {
  "use strict";

  var D = window.SA_DATA;

  /* 明细数据只内置了主演示股：其它股票给出诚实说明，
     而不是拿别的股票的 K 线与财务冒充。 */
  var FOCUS = (D.meta && D.meta.focus) || "300750";

  function fallback(host, code) {
    var s = (D.allStockByCode && D.allStockByCode[code]) || (D.stockByCode && D.stockByCode[code]);
    var name = s ? s.name : code;
    host.innerHTML = '<div class="card"><div class="card-body">' +
      '<div class="fs-13 fw-600">' + name + " 的财务明细在演示版中未内置" +
      '</div><div class="fs-12 t-3 mt-2">原型只为 ' +
      ((D.stockByCode[FOCUS] && D.stockByCode[FOCUS].name) || FOCUS) + " " + FOCUS +
      ' 准备了完整的明细数据；其它标的可切到「概览」查看行情与估值。</div>' +
      '</div></div>';
  }

  function render(host, code) {
    if (code && code !== FOCUS) { fallback(host, code); return; }

    host.innerHTML = "      <div class=\"sa-split\">\n        <div class=\"col gap-4\">\n\n          <!-- 核心指标 -->\n          <div class=\"card is-accent\">\n            <div class=\"card-head\">\n              <span class=\"card-title\">盈利概览</span>\n              <span class=\"card-sub\">2025 年报 · 单位：亿元</span>\n              <div class=\"card-tools\">\n                <span class=\"segmented\" id=\"segMode\">\n                  <span class=\"is-active\" data-v=\"year\">年度</span>\n                  <span data-v=\"quarter\">单季度</span>\n                </span>\n                <button class=\"btn btn-sm btn-ghost\" id=\"btnExport\">导出</button>\n              </div>\n            </div>\n            <div class=\"card-body\">\n              <div class=\"grid grid-6\" id=\"kpiRow\"></div>\n            </div>\n          </div>\n\n          <!-- 营收利润趋势 -->\n          <div class=\"card\">\n            <div class=\"card-head\">\n              <span class=\"card-title\">营收 · 净利润 · 毛利率趋势</span>\n              <span class=\"card-sub\">柱 = 营收 · 线 = 净利与毛利率</span>\n            </div>\n            <div class=\"card-body\">\n              <div id=\"revChart\" class=\"chart chart-lg\"></div>\n\n            </div>\n          </div>\n\n          <!-- 盈利质量 -->\n          <div class=\"grid grid-2\">\n            <div class=\"card\">\n              <div class=\"card-head\"><span class=\"card-title\">盈利能力</span><span class=\"card-sub\">ROE / 净利率 / 研发强度</span></div>\n              <div class=\"card-body\"><div id=\"profitChart\" class=\"chart chart-md\"></div></div>\n            </div>\n            <div class=\"card\">\n              <div class=\"card-head\"><span class=\"card-title\">现金流质量</span><span class=\"card-sub\">经营 / 投资 / 筹资净额（亿元）</span></div>\n              <div class=\"card-body\">\n                <div id=\"cashChart\" class=\"chart chart-md\"></div>\n                <div class=\"grid grid-3 mt-3\">\n                  <div class=\"kpi\"><span class=\"kpi-label\">净现比</span><span class=\"kpi-value is-sm t-up\">1.63</span><span class=\"kpi-delta t-3\">经营现金流/净利润</span></div>\n                  <div class=\"kpi\"><span class=\"kpi-label\">收现比</span><span class=\"kpi-value is-sm t-up\">1.08</span><span class=\"kpi-delta t-3\">销售收现/营收</span></div>\n                  <div class=\"kpi\"><span class=\"kpi-label\">自由现金流</span><span class=\"kpi-value is-sm t-up\">486.2</span><span class=\"kpi-delta t-3\">经营 - 资本开支</span></div>\n                </div>\n              </div>\n            </div>\n          </div>\n\n          <!-- 增速表 -->\n          <div class=\"card\">\n            <div class=\"card-head\">\n              <span class=\"card-title\">同比 / 环比增速表</span>\n              <span class=\"card-sub\">单季度口径 · 单位：亿元</span>\n              <div class=\"card-tools\"><span class=\"tag tag-outline\">颜色深浅 = 增速高低</span></div>\n            </div>\n            <div class=\"card-body is-flush\">\n              <div class=\"tbl-wrap\">\n                <table class=\"tbl is-comfort\" id=\"growthTbl\">\n                  <thead>\n                    <tr>\n                      <th style=\"width:150px\">指标</th>\n                      <th class=\"num\">25Q1</th><th class=\"num\">25Q2</th><th class=\"num\">25Q3</th><th class=\"num\">25Q4</th>\n                      <th class=\"num\">26Q1</th><th class=\"num\">26Q2</th><th class=\"num\">26Q3E</th>\n                      <th class=\"num\" style=\"width:96px\">同比</th><th class=\"num\" style=\"width:96px\">环比</th>\n                    </tr>\n                  </thead>\n                  <tbody id=\"growthBody\"></tbody>\n                </table>\n              </div>\n              <div class=\"tbl-foot\">\n                <span>同比 = 与上年同期比；环比 = 与上一季度比；26Q3E 为公司业绩预告区间中值</span>\n              </div>\n            </div>\n          </div>\n\n          <!-- 三表关键项 -->\n          <div class=\"grid grid-2\">\n            <div class=\"card\">\n              <div class=\"card-head\"><span class=\"card-title\">资产负债表关键项</span><span class=\"card-sub\">单位：亿元</span></div>\n              <div class=\"card-body is-flush\">\n                <div class=\"tbl-wrap\">\n                  <table class=\"tbl is-comfort\">\n                    <thead><tr><th>项目</th><th class=\"num\">2023</th><th class=\"num\">2024</th><th class=\"num\">2025</th><th class=\"num\">同比</th></tr></thead>\n                    <tbody>\n                      <tr><td>货币资金</td><td class=\"num\">2,468.2</td><td class=\"num\">2,842.6</td><td class=\"num\">3,186.4</td><td class=\"num t-up\">+12.1%</td></tr>\n                      <tr><td>应收账款</td><td class=\"num\">512.4</td><td class=\"num\">486.8</td><td class=\"num\">542.6</td><td class=\"num t-down\">+11.5%</td></tr>\n                      <tr><td>存货</td><td class=\"num\">812.4</td><td class=\"num\">742.6</td><td class=\"num\">786.2</td><td class=\"num t-down\">+5.9%</td></tr>\n                      <tr><td>固定资产</td><td class=\"num\">1,286.4</td><td class=\"num\">1,462.8</td><td class=\"num\">1,684.2</td><td class=\"num t-down\">+15.1%</td></tr>\n                      <tr><td>商誉</td><td class=\"num\">24.6</td><td class=\"num\">22.8</td><td class=\"num\">22.8</td><td class=\"num t-flat\">0.0%</td></tr>\n                      <tr><td>有息负债</td><td class=\"num\">986.2</td><td class=\"num\">1,024.6</td><td class=\"num\">1,086.2</td><td class=\"num t-down\">+6.0%</td></tr>\n                      <tr><td>资产负债率</td><td class=\"num\">65.8%</td><td class=\"num\">62.4%</td><td class=\"num\">60.2%</td><td class=\"num t-up\">-2.2pct</td></tr>\n                      <tr><td>每股净资产</td><td class=\"num\">42.6</td><td class=\"num\">50.8</td><td class=\"num\">58.1</td><td class=\"num t-up\">+14.4%</td></tr>\n                    </tbody>\n                  </table>\n                </div>\n              </div>\n            </div>\n\n            <div class=\"card\">\n              <div class=\"card-head\"><span class=\"card-title\">利润表与费用</span><span class=\"card-sub\">单位：亿元</span></div>\n              <div class=\"card-body is-flush\">\n                <div class=\"tbl-wrap\">\n                  <table class=\"tbl is-comfort\">\n                    <thead><tr><th>项目</th><th class=\"num\">2023</th><th class=\"num\">2024</th><th class=\"num\">2025</th><th class=\"num\">同比</th></tr></thead>\n                    <tbody>\n                      <tr><td>营业收入</td><td class=\"num\">4,009.2</td><td class=\"num\">3,620.3</td><td class=\"num\">4,268.6</td><td class=\"num t-up\">+17.9%</td></tr>\n                      <tr><td>营业成本</td><td class=\"num\">3,091.4</td><td class=\"num\">2,736.9</td><td class=\"num\">3,167.3</td><td class=\"num t-down\">+15.7%</td></tr>\n                      <tr><td>销售费用</td><td class=\"num\">68.4</td><td class=\"num\">72.6</td><td class=\"num\">86.2</td><td class=\"num t-down\">+18.7%</td></tr>\n                      <tr><td>管理费用</td><td class=\"num\">112.6</td><td class=\"num\">118.4</td><td class=\"num\">136.8</td><td class=\"num t-down\">+15.5%</td></tr>\n                      <tr><td>研发费用</td><td class=\"num\">183.6</td><td class=\"num\">186.4</td><td class=\"num\">218.6</td><td class=\"num t-down\">+17.3%</td></tr>\n                      <tr><td>财务费用</td><td class=\"num\">-18.6</td><td class=\"num\">-24.2</td><td class=\"num\">-28.6</td><td class=\"num t-up\">利息净收入</td></tr>\n                      <tr><td>净利润</td><td class=\"num\">441.2</td><td class=\"num\">507.4</td><td class=\"num\">622.8</td><td class=\"num t-up\">+22.8%</td></tr>\n                      <tr><td>扣非净利润</td><td class=\"num\">402.6</td><td class=\"num\">468.2</td><td class=\"num\">586.4</td><td class=\"num t-up\">+25.2%</td></tr>\n                    </tbody>\n                  </table>\n                </div>\n              </div>\n            </div>\n          </div>\n\n          <!-- 同业财务对比 -->\n          <div class=\"card\">\n            <div class=\"card-head\">\n              <span class=\"card-title\">同业财务对比</span>\n              <span class=\"card-sub\">2025 年报口径 · 单位：亿元</span>\n              <div class=\"card-tools\"><a class=\"btn btn-sm btn-ghost\" href=\"stock.html#industry\">完整行业对比 →</a></div>\n            </div>\n            <div class=\"card-body is-flush\">\n              <div class=\"tbl-wrap\">\n                <table class=\"tbl is-comfort\">\n                  <thead>\n                    <tr><th>公司</th><th class=\"num\">营收</th><th class=\"num\">净利润</th><th class=\"num\">毛利率</th><th class=\"num\">净利率</th><th class=\"num\">ROE</th><th class=\"num\">资产负债率</th><th class=\"num\">研发强度</th></tr>\n                  </thead>\n                  <tbody id=\"peerFin\"></tbody>\n                </table>\n              </div>\n            </div>\n          </div>\n\n        </div>\n\n        <aside class=\"col gap-4\">\n          <div class=\"card\">\n            <div class=\"card-head\"><span class=\"card-title\">财务健康评分</span></div>\n            <div class=\"card-body\">\n              <div id=\"finGauge\" class=\"chart chart-sm\"></div>\n              <div class=\"col gap-2 mt-2\">\n                <div class=\"row-between\"><span class=\"fs-12 t-2\">盈利能力</span><span class=\"mono fs-12 t-up\">88</span></div>\n                <div class=\"row-between\"><span class=\"fs-12 t-2\">成长能力</span><span class=\"mono fs-12 t-up\">82</span></div>\n                <div class=\"row-between\"><span class=\"fs-12 t-2\">现金流质量</span><span class=\"mono fs-12 t-up\">90</span></div>\n                <div class=\"row-between\"><span class=\"fs-12 t-2\">偿债能力</span><span class=\"mono fs-12 t-warn\">62</span></div>\n                <div class=\"row-between\"><span class=\"fs-12 t-2\">营运效率</span><span class=\"mono fs-12 t-up\">74</span></div>\n              </div>\n\n            </div>\n          </div>\n\n          <div class=\"card\">\n            <div class=\"card-head\"><span class=\"card-title\">关键财务比率</span></div>\n            <div class=\"card-body is-flush\">\n              <table class=\"tbl\">\n                <tbody>\n                  <tr><td class=\"t-2\">EPS（2025）</td><td class=\"num\">11.96 元</td></tr>\n                  <tr><td class=\"t-2\">BPS</td><td class=\"num\">58.10 元</td></tr>\n                  <tr><td class=\"t-2\">ROA</td><td class=\"num\">8.6%</td></tr>\n                  <tr><td class=\"t-2\">毛利率</td><td class=\"num\">25.8%</td></tr>\n                  <tr><td class=\"t-2\">净利率</td><td class=\"num\">14.6%</td></tr>\n                  <tr><td class=\"t-2\">期间费用率</td><td class=\"num\">9.7%</td></tr>\n                  <tr><td class=\"t-2\">研发强度</td><td class=\"num\">5.1%</td></tr>\n                  <tr><td class=\"t-2\">应收周转天数</td><td class=\"num\">44 天</td></tr>\n                  <tr><td class=\"t-2\">存货周转天数</td><td class=\"num\">86 天</td></tr>\n                  <tr><td class=\"t-2\">股息率</td><td class=\"num\">1.24%</td></tr>\n                </tbody>\n              </table>\n            </div>\n          </div>\n\n          <div class=\"card\">\n            <div class=\"card-head\"><span class=\"card-title\">披露时点</span></div>\n            <div class=\"card-body\">\n              <div class=\"timeline\">\n                <div class=\"tl-item is-up\"><span class=\"tl-dot\"></span>\n                  <div class=\"tl-head\"><span class=\"tl-time\">2026-10-24</span><span class=\"tag tag-warn\">待披露</span></div>\n                  <div class=\"tl-title\">2026 年三季报</div>\n                  <div class=\"tl-body\">业绩预告区间：净利润同比 +32% ~ +45%</div>\n                </div>\n                <div class=\"tl-item is-brand\"><span class=\"tl-dot\"></span>\n                  <div class=\"tl-head\"><span class=\"tl-time\">2026-08-28</span><span class=\"tag tag-outline\">已披露</span></div>\n                  <div class=\"tl-title\">2026 年半年报</div>\n                  <div class=\"tl-body\">营收 +18.4% · 净利润 +18.4%</div>\n                </div>\n                <div class=\"tl-item\"><span class=\"tl-dot\"></span>\n                  <div class=\"tl-head\"><span class=\"tl-time\">2026-04-18</span><span class=\"tag tag-outline\">已披露</span></div>\n                  <div class=\"tl-title\">2025 年年报</div>\n                  <div class=\"tl-body\">营收 4,268.6 亿 · 净利润 622.8 亿</div>\n                </div>\n              </div>\n            </div>\n          </div>\n\n        </aside>\n      </div>";

    var D = window.SA_DATA, C = window.SA_CHARTS, F = C.F, fin = D.finance, q = D.financeQuarters;

    function growth(a, b) { return +(((b - a) / Math.abs(a)) * 100).toFixed(1); }

    /* 概览 KPI */
    function kpiRow(mode) {
      var data = mode === "year"
        ? [
            { k: "营业收入", v: fin.revenue[4].toFixed(1), u: "亿元", d: "+17.9%", up: true },
            { k: "净利润", v: fin.netProfit[4].toFixed(1), u: "亿元", d: "+22.8%", up: true },
            { k: "毛利率", v: fin.grossMargin[4].toFixed(1), u: "%", d: "+1.4pct", up: true },
            { k: "净利率", v: fin.netMargin[4].toFixed(1), u: "%", d: "+0.6pct", up: true },
            { k: "ROE", v: fin.roe[4].toFixed(1), u: "%", d: "+1.4pct", up: true },
            { k: "经营现金流", v: fin.ocf[4].toFixed(1), u: "亿元", d: "+16.9%", up: true },
            { k: "研发投入", v: fin.rd[4].toFixed(1), u: "亿元", d: "+17.3%", up: true },
            { k: "资产负债率", v: fin.assetLiability[4].toFixed(1), u: "%", d: "-2.2pct", up: true },
            { k: "每股收益", v: "11.96", u: "元", d: "+22.8%", up: true },
            { k: "每股净资产", v: "58.10", u: "元", d: "+14.4%", up: true },
            { k: "净现比", v: "1.63", u: "", d: "现金流优质", up: true },
            { k: "一致预期 PE", v: "16.9", u: "倍", d: "2026E", up: true }
          ]
        : [
            { k: "单季营收", v: q.revenue[5].toFixed(1), u: "亿元", d: "+18.4% 同比", up: true },
            { k: "单季净利", v: q.netProfit[5].toFixed(1), u: "亿元", d: "+20.8% 同比", up: true },
            { k: "单季毛利率", v: q.grossMargin[5].toFixed(1), u: "%", d: "+2.4pct 同比", up: true },
            { k: "单季净利率", v: q.netMargin[5].toFixed(1), u: "%", d: "+0.3pct 同比", up: true },
            { k: "环比营收", v: "+23.2%", u: "", d: "26Q2 vs 26Q1", up: true },
            { k: "环比净利", v: "+23.9%", u: "", d: "26Q2 vs 26Q1", up: true },
            { k: "26Q3E 营收", v: q.revenue[6].toFixed(1), u: "亿元", d: "预告中值", up: true },
            { k: "26Q3E 净利", v: q.netProfit[6].toFixed(1), u: "亿元", d: "同比 +56.7%", up: true },
            { k: "单季 ROE", v: "5.8", u: "%", d: "折年 23.2%", up: true },
            { k: "单季经营现金流", v: "286.4", u: "亿元", d: "净现比 1.46", up: true },
            { k: "单季研发", v: "58.6", u: "亿元", d: "+21.4% 同比", up: true },
            { k: "连续增长季数", v: "3", u: "季", d: "毛利率环比", up: true }
          ];
      document.getElementById("kpiRow").innerHTML = data.map(function (d) {
        return '<div class="kpi"><span class="kpi-label">' + d.k + '</span>' +
          '<span class="kpi-value is-sm">' + d.v + (d.u ? '<span class="fs-11 t-3"> ' + d.u + "</span>" : "") + "</span>" +
          '<span class="kpi-delta ' + (d.up ? "t-up" : "t-down") + '">' + d.d + "</span></div>";
      }).join("");
    }
    kpiRow("year");

    /* 营收利润图 */
    function revChart(mode) {
      var labels = mode === "year" ? fin.years : q.labels;
      var rev = mode === "year" ? fin.revenue : q.revenue;
      var np = mode === "year" ? fin.netProfit : q.netProfit;
      var gm = mode === "year" ? fin.grossMargin : q.grossMargin;
      document.getElementById("revChart").innerHTML = "";
      F.combo("#revChart", {
        labels: labels, unit: "亿元", unit2: "%",
        bars: [{ name: "营业收入", data: rev }],
        lines: [
          { name: "净利润", data: np, axis: 2, color: C.cv("--chart-4") },
          { name: "毛利率 %", data: gm, axis: 2, color: C.cv("--chart-2"), area: false }
        ]
      });
      C.rebuildAll();
    }
    revChart("year");

    SA.qsa("#segMode > *").forEach(function (el) {
      el.addEventListener("click", function () {
        SA.qsa("#segMode > *").forEach(function (o) { o.classList.toggle("is-active", o === el); });
        var m = el.getAttribute("data-v");
        kpiRow(m); revChart(m);
        SA.toast(m === "year" ? "已切换到年度口径" : "已切换到单季度口径", "info");
      });
    });

    /* 盈利能力 */
    F.combo("#profitChart", {
      labels: fin.years, unit: "%",
      bars: [{ name: "ROE", data: fin.roe }],
      lines: [
        { name: "净利率", data: fin.netMargin, axis: 2, color: C.cv("--chart-2") },
        { name: "研发强度 %", data: [5.9, 4.7, 4.6, 5.1, 5.1, 5.2], axis: 2, color: C.cv("--chart-3") }
      ],
      unit2: "%"
    });

    /* 现金流 */
    F.stacked("#cashChart", {
      labels: fin.years, unit: "亿元",
      series: [
        { name: "经营净额", data: fin.ocf, color: C.cv("--chart-6") },
        { name: "投资净额", data: [-486.2, -642.8, -726.4, -586.2, -526.2, -482.6] },
        { name: "筹资净额", data: [186.4, 242.6, -86.4, -124.2, -86.2, -64.8] }
      ]
    });

    /* 增速表 */
    var rows = [
      { k: "营业收入", v: q.revenue, unit: "亿元" },
      { k: "净利润", v: q.netProfit, unit: "亿元" },
      { k: "毛利率", v: q.grossMargin, unit: "%" },
      { k: "净利率", v: q.netMargin, unit: "%" },
      { k: "营收同比", v: q.yoy, unit: "%" },
      { k: "营收环比", v: q.qoq, unit: "%" }
    ];
    document.getElementById("growthBody").innerHTML = rows.map(function (r) {
      var cells = r.v.map(function (v) {
        var pct = r.unit === "%";
        var cls = v >= 0 ? (pct && r.k.indexOf("率") < 0 ? "t-up" : "") : "t-down";
        if (r.k.indexOf("率") >= 0) cls = "";
        return '<td class="num growth-cell ' + cls + '">' + (v >= 0 && r.k.indexOf("同比") >= 0 || r.k.indexOf("环比") >= 0 ? "+" : "") + v.toFixed(1) + (pct ? "%" : "") + "</td>";
      }).join("");
      var yoy = r.k.indexOf("营收同比") >= 0 ? "—" : "+" + growth(r.v[4], r.v[5]).toFixed(1) + "%";
      var qoq = r.k.indexOf("营收环比") >= 0 ? "—" : "+" + growth(r.v[5], r.v[6]).toFixed(1) + "%";
      return "<tr><td class=\"fw-600\">" + r.k + "</td>" + cells +
        '<td class="num ' + (yoy === "—" ? "t-flat" : "t-up") + '">' + yoy + "</td>" +
        '<td class="num ' + (qoq === "—" ? "t-flat" : "t-up") + '">' + qoq + "</td></tr>";
    }).join("");

    /* 同业财务 */
    document.getElementById("peerFin").innerHTML = D.industry.peers.map(function (p) {
      var isSelf = p.self;
      return '<tr data-chg="' + (p.pct >= 0 ? "up" : "down") + '" class="is-clickable" data-href="stock.html?code=' + p.code + '">' +
        '<td class="fw-600">' + p.name + (isSelf ? ' <span class="tag tag-brand">本股</span>' : "") + "</td>" +
        '<td class="num">' + p.revenue.toFixed(1) + "</td>" +
        '<td class="num ' + (p.netProfit >= 0 ? "" : "t-down") + '">' + p.netProfit.toFixed(1) + "</td>" +
        '<td class="num">' + p.grossMargin.toFixed(1) + "%</td>" +
        '<td class="num">' + (p.revenue ? (p.netProfit / p.revenue * 100).toFixed(1) : "—") + "%</td>" +
        '<td class="num ' + (p.roe >= 0 ? "t-up" : "t-down") + '">' + p.roe.toFixed(1) + "%</td>" +
        '<td class="num">' + (p.self ? "60.2" : (58 + Math.random() * 12).toFixed(1)) + "%</td>" +
        '<td class="num">' + (p.self ? "5.1" : (3 + Math.random() * 4).toFixed(1)) + "%</td>" +
        "</tr>";
    }).join("");

    /* 财务健康仪表 */
    F.gauge("#finGauge", { value: 82, max: 100, name: "综合财务评分", warnAt: 50, dangerAt: 70 });

    document.getElementById("btnExport").addEventListener("click", function () {
      SA.toast("已导出财务报表为 CSV（原型不产生文件）", "ok");
    });

    SA.startLiveTicker("#qLive", { interval: 3000 });
  }

  window.SA_PANELS = window.SA_PANELS || {};
  window.SA_PANELS.finance = { key: "finance", title: "财务", render: render };
})();
