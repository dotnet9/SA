/* ============================================================
   SA · 股析 — 个股面板：资金
   由 design/web/stock.html#capital 机械迁移而来（批次 3）。
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
      '<div class="fs-13 fw-600">' + name + " 的资金明细在演示版中未内置" +
      '</div><div class="fs-12 t-3 mt-2">原型只为 ' +
      ((D.stockByCode[FOCUS] && D.stockByCode[FOCUS].name) || FOCUS) + " " + FOCUS +
      ' 准备了完整的明细数据；其它标的可切到「概览」查看行情与估值。</div>' +
      '</div></div>';
  }

  function render(host, code) {
    if (code && code !== FOCUS) { fallback(host, code); return; }

    host.innerHTML = "      <div class=\"sa-split\">\n        <div class=\"col gap-4\">\n\n          <!-- 主力资金概览 -->\n          <div class=\"card is-accent\">\n            <div class=\"card-head\">\n              <span class=\"card-title\">主力资金概览</span>\n              <span class=\"card-sub\">超大单 + 大单口径 · 单位：亿元</span>\n              <div class=\"card-tools\">\n                <span class=\"segmented\" id=\"segRange\">\n                  <span class=\"is-active\" data-v=\"9\">近 9 日</span>\n                  <span data-v=\"20\">近 20 日</span>\n                  <span data-v=\"60\">近 60 日</span>\n                </span>\n                <button class=\"btn btn-sm btn-ghost\" id=\"btnExport\">导出</button>\n              </div>\n            </div>\n            <div class=\"card-body\">\n              <div class=\"grid grid-4\">\n                <div class=\"flow-tile\"><div class=\"ft-k\">今日净流入</div><div class=\"ft-v t-up\">+18.62</div><div class=\"hint\">超大单 +11.42 · 大单 +7.20</div></div>\n                <div class=\"flow-tile\"><div class=\"ft-k\">近 5 日累计</div><div class=\"ft-v t-up\">+38.14</div><div class=\"hint\">连续 3 日净流入</div></div>\n                <div class=\"flow-tile\"><div class=\"ft-k\">近 20 日累计</div><div class=\"ft-v t-up\">+52.86</div><div class=\"hint\">电池行业第 1</div></div>\n                <div class=\"flow-tile\"><div class=\"ft-k\">近 60 日累计</div><div class=\"ft-v t-up\">+28.42</div><div class=\"hint\">先流出后回流</div></div>\n              </div>\n              <div id=\"mainFlowChart\" class=\"chart chart-lg mt-4\"></div>\n\n            </div>\n          </div>\n\n          <!-- 资金分层 + 北向 -->\n          <div class=\"grid grid-2\">\n            <div class=\"card\">\n              <div class=\"card-head\"><span class=\"card-title\">资金分层结构</span><span class=\"card-sub\">超大单 / 大单 / 中单 / 小单净额（亿元）</span></div>\n              <div class=\"card-body\">\n                <div id=\"layerChart\" class=\"chart chart-lg\"></div>\n                <table class=\"tbl mt-3\">\n                  <thead><tr><th>分层</th><th class=\"num\">今日</th><th class=\"num\">近 5 日</th><th class=\"num\">近 20 日</th></tr></thead>\n                  <tbody>\n                    <tr><td>超大单（≥100 万股或 ≥200 万元）</td><td class=\"num t-up\">+11.42</td><td class=\"num t-up\">+26.84</td><td class=\"num t-up\">+38.62</td></tr>\n                    <tr><td>大单（20–100 万股）</td><td class=\"num t-up\">+7.20</td><td class=\"num t-up\">+11.30</td><td class=\"num t-up\">+14.24</td></tr>\n                    <tr><td>中单（4–20 万股）</td><td class=\"num t-down\">-5.86</td><td class=\"num t-down\">-12.42</td><td class=\"num t-down\">-18.64</td></tr>\n                    <tr><td>小单（&lt;4 万股）</td><td class=\"num t-down\">-12.76</td><td class=\"num t-down\">-25.72</td><td class=\"num t-down\">-34.22</td></tr>\n                  </tbody>\n                </table>\n              </div>\n            </div>\n\n            <div class=\"card\">\n              <div class=\"card-head\">\n                <span class=\"card-title\">北向资金持股</span>\n                <span class=\"card-sub\">陆股通持股比例与股数</span>\n                <div class=\"card-tools\"><span class=\"tag tag-up\">7.09% · 连续 3 月增持</span></div>\n              </div>\n              <div class=\"card-body\">\n                <div id=\"northChart\" class=\"chart chart-lg\"></div>\n                <div class=\"grid grid-3 mt-3\">\n                  <div class=\"kpi\"><span class=\"kpi-label\">持股比例</span><span class=\"kpi-value is-sm\">7.09%</span><span class=\"kpi-delta t-up\">↑ +0.07pct 环比</span></div>\n                  <div class=\"kpi\"><span class=\"kpi-label\">持股股数</span><span class=\"kpi-value is-sm\">3.12 亿股</span><span class=\"kpi-delta t-up\">↑ +0.03 亿股</span></div>\n                  <div class=\"kpi\"><span class=\"kpi-label\">持股市值</span><span class=\"kpi-value is-sm\">838.0 亿元</span><span class=\"kpi-delta t-3\">按最新收盘价</span></div>\n                </div>\n              </div>\n            </div>\n          </div>\n\n          <!-- 两融 -->\n          <div class=\"card\">\n            <div class=\"card-head\">\n              <span class=\"card-title\">融资融券</span>\n              <span class=\"card-sub\">余额与买入额（亿元）</span>\n              <div class=\"card-tools\"><span class=\"tag tag-outline\">融资余额占流通市值 2.32%</span></div>\n            </div>\n            <div class=\"card-body\">\n              <div id=\"marginChart\" class=\"chart chart-md\"></div>\n              <div class=\"grid grid-4 mt-3\">\n                <div class=\"kpi\"><span class=\"kpi-label\">融资余额</span><span class=\"kpi-value is-sm\">242.4 亿</span><span class=\"kpi-delta t-up\">↑ +6.0% 环比</span></div>\n                <div class=\"kpi\"><span class=\"kpi-label\">融资买入额（当日）</span><span class=\"kpi-value is-sm\">31.2 亿</span><span class=\"kpi-delta t-up\">↑ +18.2%</span></div>\n                <div class=\"kpi\"><span class=\"kpi-label\">融券余额</span><span class=\"kpi-value is-sm\">1.52 亿</span><span class=\"kpi-delta t-down\">↑ +4.1% 看空增</span></div>\n                <div class=\"kpi\"><span class=\"kpi-label\">融资买入占成交额</span><span class=\"kpi-value is-sm\">16.7%</span><span class=\"kpi-delta t-3\">杠杆资金活跃</span></div>\n              </div>\n            </div>\n          </div>\n\n          <!-- 筹码分布 -->\n          <div class=\"grid grid-2\">\n            <div class=\"card\">\n              <div class=\"card-head\">\n                <span class=\"card-title\">筹码分布</span>\n                <span class=\"card-sub\">价格 → 筹码占比（%）· 现价上方为套牢盘</span>\n              </div>\n              <div class=\"card-body\">\n                <div id=\"chipChart\" class=\"chart chart-xl\"></div>\n              </div>\n            </div>\n            <div class=\"card\">\n              <div class=\"card-head\"><span class=\"card-title\">筹码结构与含义</span></div>\n              <div class=\"card-body col gap-3\">\n                <div class=\"grid grid-2\">\n                  <div class=\"kpi\"><span class=\"kpi-label\">平均成本</span><span class=\"kpi-value is-sm\">246.80 元</span><span class=\"kpi-delta t-3\">现价高于成本 8.7%</span></div>\n                  <div class=\"kpi\"><span class=\"kpi-label\">获利盘比例</span><span class=\"kpi-value is-sm t-up\">72.4%</span><span class=\"kpi-delta t-3\">抛压相对可控</span></div>\n                  <div class=\"kpi\"><span class=\"kpi-label\">90% 成本区间</span><span class=\"kpi-value is-sm\">198 – 288</span><span class=\"kpi-delta t-3\">集中度 0.244</span></div>\n                  <div class=\"kpi\"><span class=\"kpi-label\">70% 成本区间</span><span class=\"kpi-value is-sm\">216 – 274</span><span class=\"kpi-delta t-3\">集中度 0.186</span></div>\n                </div>\n                <div>\n                  <div class=\"row-between\"><span class=\"fs-12 t-2\">筹码集中度（越低越集中）</span><span class=\"mono fs-12\">0.186</span></div>\n                  <div class=\"meter is-ok mt-2\"><i style=\"width:82%\"></i></div>\n                  <div class=\"hint mt-1\">近 3 个月集中度由 0.286 降至 0.186，筹码持续集中</div>\n                </div>\n\n              </div>\n            </div>\n          </div>\n\n          <!-- 龙虎榜 -->\n          <div class=\"card\">\n            <div class=\"card-head\">\n              <span class=\"card-title\">龙虎榜</span>\n              <span class=\"card-sub\">近 3 个月上榜 4 次 · 单位：亿元</span>\n              <div class=\"card-tools\"><span class=\"tag tag-up\">净买入 3 次</span><span class=\"tag tag-down\">净卖出 1 次</span></div>\n            </div>\n            <div class=\"card-body is-flush\">\n              <div class=\"tbl-wrap\">\n                <table class=\"tbl is-comfort\">\n                  <thead><tr><th style=\"width:96px\">日期</th><th>上榜原因</th><th class=\"num\">买入</th><th class=\"num\">卖出</th><th class=\"num\">净额</th><th class=\"num\">席位</th><th style=\"width:80px\" class=\"col-actions\">明细</th></tr></thead>\n                  <tbody id=\"dtBody\"></tbody>\n                </table>\n              </div>\n            </div>\n          </div>\n\n          <!-- 大宗交易 + 增减持 -->\n          <div class=\"grid grid-2\">\n            <div class=\"card\">\n              <div class=\"card-head\"><span class=\"card-title\">大宗交易</span><span class=\"card-sub\">近 3 个月 · 折价率相对当日收盘价</span></div>\n              <div class=\"card-body is-flush\">\n                <div class=\"tbl-wrap\">\n                  <table class=\"tbl is-comfort\">\n                    <thead><tr><th style=\"width:96px\">日期</th><th class=\"num\">成交价</th><th class=\"num\">成交量（万股）</th><th class=\"num\">成交额</th><th class=\"num\">折价率</th><th>买方</th></tr></thead>\n                    <tbody id=\"blockBody\"></tbody>\n                  </table>\n                </div>\n              </div>\n            </div>\n\n            <div class=\"card\">\n              <div class=\"card-head\"><span class=\"card-title\">股东增减持</span><span class=\"card-sub\">近 6 个月公告</span></div>\n              <div class=\"card-body\">\n                <div class=\"timeline\">\n                  <div id=\"holderTimeline\"></div>\n                </div>\n              </div>\n            </div>\n          </div>\n\n        </div>\n\n        <aside class=\"col gap-4\">\n          <div class=\"card\">\n            <div class=\"card-head\"><span class=\"card-title\">资金面评分</span><span class=\"tag tag-up\">强势</span></div>\n            <div class=\"card-body\">\n              <div id=\"flowGauge\" class=\"chart chart-sm\"></div>\n              <div class=\"col gap-2 mt-2\">\n                <div class=\"row-between\"><span class=\"fs-12 t-2\">主力资金</span><span class=\"mono fs-12 t-up\">92</span></div>\n                <div class=\"row-between\"><span class=\"fs-12 t-2\">北向资金</span><span class=\"mono fs-12 t-up\">84</span></div>\n                <div class=\"row-between\"><span class=\"fs-12 t-2\">杠杆资金</span><span class=\"mono fs-12 t-up\">76</span></div>\n                <div class=\"row-between\"><span class=\"fs-12 t-2\">筹码结构</span><span class=\"mono fs-12 t-up\">82</span></div>\n                <div class=\"row-between\"><span class=\"fs-12 t-2\">龙虎榜</span><span class=\"mono fs-12 t-up\">78</span></div>\n              </div>\n            </div>\n          </div>\n\n          <div class=\"card\">\n            <div class=\"card-head\"><span class=\"card-title\">同行业资金流排名</span><span class=\"card-sub\">电池行业 · 今日主力净流入</span></div>\n            <div class=\"card-body is-flush\">\n              <table class=\"tbl\">\n                <tbody>\n                  <tr data-chg=\"up\"><td class=\"fw-600\">宁德时代 <span class=\"tag tag-brand\">本股</span></td><td class=\"num t-up\">+18.62 亿</td><td class=\"num t-3\">1</td></tr>\n                  <tr data-chg=\"up\"><td>亿纬锂能</td><td class=\"num t-up\">+4.26 亿</td><td class=\"num t-3\">2</td></tr>\n                  <tr data-chg=\"up\"><td>贝特瑞</td><td class=\"num t-up\">+0.86 亿</td><td class=\"num t-3\">3</td></tr>\n                  <tr data-chg=\"down\"><td>国轩高科</td><td class=\"num t-down\">-1.24 亿</td><td class=\"num t-3\">4</td></tr>\n                  <tr data-chg=\"down\"><td>欣旺达</td><td class=\"num t-down\">-2.16 亿</td><td class=\"num t-3\">5</td></tr>\n                  <tr data-chg=\"down\"><td>孚能科技</td><td class=\"num t-down\">-0.86 亿</td><td class=\"num t-3\">6</td></tr>\n                </tbody>\n              </table>\n              <div class=\"tbl-foot\"><a class=\"t-brand\" href=\"stock.html#industry\">查看完整行业对比 →</a></div>\n            </div>\n          </div>\n\n          <div class=\"card\">\n            <div class=\"card-head\"><span class=\"card-title\">资金与价格背离</span></div>\n            <div class=\"card-body\">\n              <div id=\"diverge\" class=\"chart chart-sm\"></div>\n\n            </div>\n          </div>\n\n        </aside>\n      </div>";

    var D = window.SA_DATA, C = window.SA_CHARTS, F = C.F, cap = D.capital;

    /* 主力资金 */
    function drawFlow(n) {
      document.getElementById("mainFlowChart").innerHTML = "";
      var labels = cap.mainFlow.labels.slice(-n);
      var net = cap.mainFlow.net.slice(-n);
      F.combo("#mainFlowChart", {
        labels: labels, unit: "亿元",
        bars: [{
          name: "主力净流入",
          data: net,
          color: { type: "linear", x: 0, y: 0, x2: 0, y2: 1, colorStops: [{ offset: 0, color: C.cv("--chart-1") }, { offset: 1, color: "transparent" }] }
        }],
        lines: [{ name: "累计", data: net.reduce(function (a, b) { var o = []; var s = 0; return net.map(function (v) { s += v; return +s.toFixed(2); }); }, []), area: true, color: C.cv("--chart-2") }]
      });
      C.rebuildAll();
    }
    drawFlow(9);

    SA.qsa("#segRange > *").forEach(function (el) {
      el.addEventListener("click", function () {
        SA.qsa("#segRange > *").forEach(function (o) { o.classList.toggle("is-active", o === el); });
        drawFlow(parseInt(el.getAttribute("data-v"), 10));
        SA.toast("已切换到" + el.textContent.trim() + "（原型中展示同一演示序列）", "info");
      });
    });

    /* 资金分层 */
    F.posneg("#layerChart", { labels: cap.mainFlow.labels, data: cap.mainFlow.superLarge });
    document.querySelector("#layerChart").setAttribute("data-series", "superLarge");

    /* 北向 */
    F.combo("#northChart", {
      labels: cap.northbound.labels, unit: "%",
      bars: [{ name: "持股比例", data: cap.northbound.ratio }],
      lines: [{ name: "持股股数（亿股）", data: cap.northbound.shares, axis: 2, color: C.cv("--chart-4") }],
      unit2: "亿股"
    });

    /* 两融 */
    F.combo("#marginChart", {
      labels: cap.margin.labels, unit: "亿元",
      bars: [{ name: "融资余额", data: cap.margin.balance }],
      lines: [
        { name: "融资买入额", data: cap.margin.buy, axis: 2, color: C.cv("--chart-4") },
        { name: "融券余额", data: cap.margin.short, axis: 2, color: C.cv("--chart-5") }
      ],
      unit2: "亿元"
    });

    /* 筹码 */
    F.chips("#chipChart", { data: cap.chips, current: D.focusProfile.price });

    /* 龙虎榜 */
    document.getElementById("dtBody").innerHTML = cap.dragonTiger.map(function (d) {
      var cls = d.net >= 0 ? "t-up" : "t-down";
      return '<tr data-chg="' + (d.net >= 0 ? "up" : "down") + '">' +
        '<td class="mono fs-11 t-3">' + d.date + "</td>" +
        '<td>' + d.reason + "</td>" +
        '<td class="num t-up">' + d.buy.toFixed(2) + "</td>" +
        '<td class="num t-down">' + d.sell.toFixed(2) + "</td>" +
        '<td class="num ' + cls + '">' + (d.net >= 0 ? "+" : "") + d.net.toFixed(2) + "</td>" +
        '<td class="num">' + d.seats + "</td>" +
        '<td class="col-actions"><button class="btn btn-sm btn-ghost" data-open="dlgSeats">查看</button></td>' +
        "</tr>";
    }).join("");

    /* 大宗 */
    document.getElementById("blockBody").innerHTML = cap.blockTrades.map(function (b) {
      return "<tr>" +
        '<td class="mono fs-11 t-3">' + b.date + "</td>" +
        '<td class="num">' + b.price.toFixed(2) + "</td>" +
        '<td class="num">' + b.volume.toFixed(1) + "</td>" +
        '<td class="num">' + b.amount.toFixed(2) + " 亿</td>" +
        '<td class="num t-down">' + b.discount.toFixed(2) + "%</td>" +
        "<td>" + b.buyer + "</td>" +
        "</tr>";
    }).join("");

    /* 增减持时间轴 */
    document.getElementById("holderTimeline").innerHTML = cap.holderChanges.map(function (h) {
      var isUp = h.type === "增持";
      return '<div class="tl-item ' + (isUp ? "is-up" : "is-down") + '"><span class="tl-dot"></span>' +
        '<div class="tl-head"><span class="tl-time">' + h.date + "</span>" +
          '<span class="tag ' + (isUp ? "tag-up" : "tag-down") + '">' + h.type + "</span>" +
          '<span class="tag tag-outline">' + h.progress + "</span></div>" +
        '<div class="tl-title">' + h.holder + " · " + h.shares + " 万股（" + h.pct + "%）</div>" +
        '<div class="tl-body">金额约 ' + h.amount + " 亿元 · " + h.note + "</div>" +
        "</div>";
    }).join("");

    /* 评分 */
    F.gauge("#flowGauge", { value: 84, max: 100, name: "资金面综合评分", warnAt: 50, dangerAt: 70 });

    /* 背离 */
    F.combo("#diverge", {
      labels: cap.mainFlow.labels,
      unit: "亿元",
      bars: [{ name: "主力净流入", data: cap.mainFlow.net }],
      lines: [{ name: "价格", data: [244.2, 238.6, 246.8, 252.4, 248.6, 258.4, 262.8, 256.2, 268.4], axis: 2, color: C.cv("--chart-4") }],
      unit2: "元"
    });

    document.getElementById("btnExport").addEventListener("click", function () {
      SA.toast("已导出资金流明细为 CSV（原型不产生文件）", "ok");
    });

    SA.startLiveTicker("#qLive", { interval: 3000 });
  }

  window.SA_PANELS = window.SA_PANELS || {};
  window.SA_PANELS.capital = { key: "capital", title: "资金", render: render };
})();
