/* ============================================================
   SA · 股析 — 个股面板：趋势
   由 design/web/stock.html#trend 机械迁移而来（批次 3）。
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
      '<div class="fs-13 fw-600">' + name + " 的趋势明细在演示版中未内置" +
      '</div><div class="fs-12 t-3 mt-2">原型只为 ' +
      ((D.stockByCode[FOCUS] && D.stockByCode[FOCUS].name) || FOCUS) + " " + FOCUS +
      ' 准备了完整的明细数据；其它标的可切到「概览」查看行情与估值。</div>' +
      '</div></div>';
  }

  function render(host, code) {
    if (code && code !== FOCUS) { fallback(host, code); return; }

    host.innerHTML = "      <div class=\"sa-split\">\n        <div class=\"col gap-4\">\n\n          <!-- 工具栏 + 主图 -->\n          <div class=\"card is-accent\">\n            <div class=\"card-head\">\n              <span class=\"card-title\">K 线 · 均线 · 成交量 · MACD</span>\n              <span class=\"card-sub\">十字光标联动 · 滚轮缩放 · 拖动平移</span>\n              <div class=\"card-tools\">\n                <span class=\"segmented\" id=\"segPeriod\">\n                  <span class=\"is-active\" data-v=\"day\">日 K</span>\n                  <span data-v=\"week\">周 K</span>\n                  <span data-v=\"month\">月 K</span>\n                </span>\n                <span class=\"segmented\" id=\"segAdj\">\n                  <span class=\"is-active\" data-v=\"qfq\">前复权</span>\n                  <span data-v=\"hfq\">后复权</span>\n                  <span data-v=\"none\">不复权</span>\n                </span>\n                <button class=\"btn btn-sm btn-ghost\" id=\"btnExport\">导出</button>\n              </div>\n            </div>\n            <div class=\"card-body\">\n              <div class=\"chart-toolbar\">\n                <span class=\"label\" style=\"width:auto\">指标叠加</span>\n                <label class=\"check\"><input type=\"checkbox\" id=\"ckMA\" checked> 均线 MA5/10/20/60</label>\n                <label class=\"check\"><input type=\"checkbox\" id=\"ckVol\" checked> 成交量</label>\n                <label class=\"check\"><input type=\"checkbox\" id=\"ckMacd\" checked> MACD</label>\n                <label class=\"check\"><input type=\"checkbox\" id=\"ckKdj\"> KDJ</label>\n                <span class=\"hint\" style=\"margin-left:auto\">演示数据 · 非实时</span>\n              </div>\n              <div id=\"mainKline\" class=\"chart chart-2xl\"></div>\n              <div class=\"legend-inline mt-3\">\n                <span class=\"li\"><span class=\"sw\" style=\"background:var(--chart-1)\"></span>MA5</span>\n                <span class=\"li\"><span class=\"sw\" style=\"background:var(--chart-2)\"></span>MA10</span>\n                <span class=\"li\"><span class=\"sw\" style=\"background:var(--chart-3)\"></span>MA20</span>\n                <span class=\"li\"><span class=\"sw\" style=\"background:var(--chart-4)\"></span>MA60</span>\n                <span class=\"li\"><span class=\"sw\" style=\"background:var(--up)\"></span>阳线 / 涨</span>\n                <span class=\"li\"><span class=\"sw\" style=\"background:var(--down)\"></span>阴线 / 跌</span>\n                <span class=\"hint\" style=\"margin-left:auto\">前复权 · 共 240 个交易日 · 展示最近 120 日</span>\n              </div>\n            </div>\n          </div>\n\n          <!-- 相对强弱 -->\n          <div class=\"grid grid-2\">\n            <div class=\"card\">\n              <div class=\"card-head\">\n                <span class=\"card-title\">相对沪深300 强弱</span>\n                <span class=\"card-sub\">累计超额收益（%）</span>\n                <div class=\"card-tools\">\n                  <span class=\"segmented\"><span>近 20 日</span><span class=\"is-active\">近 60 日</span><span>近 120 日</span></span>\n                </div>\n              </div>\n              <div class=\"card-body\">\n                <div id=\"relStrength\" class=\"chart chart-md\"></div>\n\n              </div>\n            </div>\n\n            <div class=\"card\">\n              <div class=\"card-head\"><span class=\"card-title\">价格结构定位</span><span class=\"card-sub\">当前价所处位置</span></div>\n              <div class=\"card-body col gap-4\">\n                <div>\n                  <div class=\"row-between\"><span class=\"fs-12 t-2\">近 3 年价格分位</span><span class=\"mono fs-14 fw-600\">72%</span></div>\n                  <div class=\"tempbar mt-3\"><span class=\"marker\" style=\"left:72%\"></span></div>\n                  <div class=\"tempbar-legend\"><span>168.2（最低）</span><span>214.6（50%）</span><span>296.4（最高）</span></div>\n                </div>\n                <div class=\"grid grid-2\">\n                  <div class=\"kpi\"><span class=\"kpi-label\">距 52 周高点</span><span class=\"kpi-value is-sm t-down\">-9.44%</span><span class=\"kpi-delta t-3\">296.40</span></div>\n                  <div class=\"kpi\"><span class=\"kpi-label\">距 52 周低点</span><span class=\"kpi-value is-sm t-up\">+59.57%</span><span class=\"kpi-delta t-3\">168.20</span></div>\n                  <div class=\"kpi\"><span class=\"kpi-label\">站上 MA20</span><span class=\"kpi-value is-sm t-up\">+12.6%</span><span class=\"kpi-delta t-3\">MA20 = 238.4</span></div>\n                  <div class=\"kpi\"><span class=\"kpi-label\">站上年线 MA250</span><span class=\"kpi-value is-sm t-up\">+18.4%</span><span class=\"kpi-delta t-3\">MA250 = 226.7</span></div>\n                </div>\n                <div class=\"row gap-2 wrap\">\n                  <span class=\"tag tag-up tag-lg\">均线多头排列</span>\n                  <span class=\"tag tag-up tag-lg\">放量突破年线</span>\n                  <span class=\"tag tag-outline tag-lg\">箱体上沿 271.2</span>\n                  <span class=\"tag tag-outline tag-lg\">箱体下沿 238.4</span>\n                  <span class=\"tag tag-warn tag-lg\">RSI 72 偏高</span>\n                </div>\n              </div>\n            </div>\n          </div>\n\n          <!-- 趋势判定 -->\n          <div class=\"card\">\n            <div class=\"card-head\"><span class=\"card-title\">趋势判定摘要</span><span class=\"card-sub\">规则引擎按均线 / 量能 / 指标自动生成</span></div>\n            <div class=\"card-body\">\n              <div class=\"grid grid-4\">\n                <div class=\"kpi\"><span class=\"kpi-label\">短期（5 日）</span><span class=\"kpi-value is-sm t-up\">多头</span><span class=\"kpi-delta t-3\">+9.86%</span></div>\n                <div class=\"kpi\"><span class=\"kpi-label\">中期（20 日）</span><span class=\"kpi-value is-sm t-up\">多头</span><span class=\"kpi-delta t-3\">+22.40%</span></div>\n                <div class=\"kpi\"><span class=\"kpi-label\">长期（120 日）</span><span class=\"kpi-value is-sm t-up\">多头</span><span class=\"kpi-delta t-3\">+38.62%</span></div>\n                <div class=\"kpi\"><span class=\"kpi-label\">量能状态</span><span class=\"kpi-value is-sm t-up\">温和放量</span><span class=\"kpi-delta t-3\">量比 1.86</span></div>\n                <div class=\"kpi\"><span class=\"kpi-label\">MACD</span><span class=\"kpi-value is-sm t-up\">金叉</span><span class=\"kpi-delta t-3\">09-16 · 柱线转正</span></div>\n                <div class=\"kpi\"><span class=\"kpi-label\">KDJ</span><span class=\"kpi-value is-sm t-warn\">超买</span><span class=\"kpi-delta t-3\">J 值 96.4</span></div>\n                <div class=\"kpi\"><span class=\"kpi-label\">波动率（20 日）</span><span class=\"kpi-value is-sm\">38.6%</span><span class=\"kpi-delta t-3\">高于行业中位</span></div>\n                <div class=\"kpi\"><span class=\"kpi-label\">支撑 / 压力</span><span class=\"kpi-value is-sm\">238 / 271</span><span class=\"kpi-delta t-3\">MA20 / 前高</span></div>\n              </div>\n\n            </div>\n          </div>\n\n        </div>\n\n        <aside class=\"col gap-4\">\n          <div class=\"card\">\n            <div class=\"card-head\"><span class=\"card-title\">关键价位</span></div>\n            <div class=\"card-body col gap-2\">\n              <div class=\"row-between\"><span class=\"fs-12 t-2\">前高压力</span><span class=\"mono fs-12 t-down\">296.40</span></div>\n              <div class=\"row-between\"><span class=\"fs-12 t-2\">箱体上沿</span><span class=\"mono fs-12 t-down\">271.24</span></div>\n              <div class=\"row-between\"><span class=\"fs-12 t-2\">现价</span><span class=\"mono fs-13 fw-600 t-up\">268.42</span></div>\n              <div class=\"row-between\"><span class=\"fs-12 t-2\">MA20 支撑</span><span class=\"mono fs-12 t-up\">238.40</span></div>\n              <div class=\"row-between\"><span class=\"fs-12 t-2\">年线支撑</span><span class=\"mono fs-12 t-up\">226.70</span></div>\n              <div class=\"row-between\"><span class=\"fs-12 t-2\">前低支撑</span><span class=\"mono fs-12 t-up\">168.20</span></div>\n              <div class=\"meter is-ok mt-2\"><i style=\"width:64%\"></i></div>\n              <div class=\"hint\">价格位于箱体 64% 位置，接近上沿</div>\n            </div>\n          </div>\n\n          <div class=\"card\">\n            <div class=\"card-head\"><span class=\"card-title\">量价关系</span></div>\n            <div class=\"card-body\">\n              <div id=\"volPrice\" class=\"chart chart-sm\"></div>\n              <div class=\"col gap-2 mt-3\">\n                <div class=\"row-between\"><span class=\"fs-12 t-2\">5 日均量</span><span class=\"mono fs-12\">48.2 万手</span></div>\n                <div class=\"row-between\"><span class=\"fs-12 t-2\">20 日均量</span><span class=\"mono fs-12\">42.6 万手</span></div>\n                <div class=\"row-between\"><span class=\"fs-12 t-2\">今日 / 5 日均量</span><span class=\"mono fs-12 t-up\">1.30 倍</span></div>\n                <div class=\"row-between\"><span class=\"fs-12 t-2\">换手率分位</span><span class=\"mono fs-12\">58%</span></div>\n              </div>\n            </div>\n          </div>\n\n          <div class=\"card\">\n            <div class=\"card-head\"><span class=\"card-title\">历史分位对照</span></div>\n            <div class=\"card-body is-flush\">\n              <table class=\"tbl\">\n                <thead><tr><th>区间</th><th class=\"num\">涨幅</th><th class=\"num\">分位</th></tr></thead>\n                <tbody>\n                  <tr><td>近 5 日</td><td class=\"num t-up\">+9.86%</td><td class=\"num\">86%</td></tr>\n                  <tr><td>近 20 日</td><td class=\"num t-up\">+22.40%</td><td class=\"num\">92%</td></tr>\n                  <tr><td>近 60 日</td><td class=\"num t-up\">+32.64%</td><td class=\"num\">88%</td></tr>\n                  <tr><td>近 120 日</td><td class=\"num t-up\">+38.62%</td><td class=\"num\">84%</td></tr>\n                  <tr><td>近 250 日</td><td class=\"num t-up\">+42.86%</td><td class=\"num\">78%</td></tr>\n                </tbody>\n              </table>\n              <div class=\"tbl-foot\"><span>分位 = 在全市场同期涨幅中的百分位</span></div>\n            </div>\n          </div>\n\n          <div class=\"card\">\n            <div class=\"card-head\"><span class=\"card-title\">相关模块</span></div>\n            <div class=\"card-body col gap-2\">\n              <a class=\"btn btn-outline btn-block\" href=\"stock.html#capital\">看资金是否配合 →</a>\n              <a class=\"btn btn-outline btn-block\" href=\"stock.html#events\">看驱动事件 →</a>\n              <a class=\"btn btn-outline btn-block\" href=\"stock.html#rating\">看机构目标价 →</a>\n              <a class=\"btn btn-outline btn-block\" href=\"alerts.html\">按均线设提醒 →</a>\n            </div>\n          </div>\n        </aside>\n      </div>";

    var D = window.SA_DATA, C = window.SA_CHARTS, F = C.F;
    var opts = { ma: [5, 10, 20, 60], volume: true, macd: true, kdj: false, zoomStart: 45 };
    var bars = D.focusKline.slice(-120);

    function draw() {
      F.kline("#mainKline", bars, opts);
    }
    draw();

    /* 周期切换 */
    SA.qsa("#segPeriod > *").forEach(function (el) {
      el.addEventListener("click", function () {
        var v = el.getAttribute("data-v");
        bars = v === "day" ? D.focusKline.slice(-120) : v === "week" ? D.focusWeekly.slice(-100) : D.focusMonthly.slice(-60);
        SA.qsa("#mainKline").forEach(function (n) { n.innerHTML = ""; });
        C.rebuildAll();
        SA.toast("已切换到" + el.textContent.trim() + "（演示数据）", "info");
      });
    });

    /* 指标开关 */
    function refresh() {
      var dom = document.getElementById("mainKline");
      dom.innerHTML = "";
      F.kline("#mainKline", bars, opts);
      C.rebuildAll();
    }
    document.getElementById("ckMA").addEventListener("change", function () {
      opts.ma = this.checked ? [5, 10, 20, 60] : [];
      refresh();
    });
    document.getElementById("ckVol").addEventListener("change", function () { opts.volume = this.checked; refresh(); });
    document.getElementById("ckMacd").addEventListener("change", function () {
      opts.macd = this.checked;
      if (this.checked) opts.kdj = false, document.getElementById("ckKdj").checked = false;
      refresh();
    });
    document.getElementById("ckKdj").addEventListener("change", function () {
      opts.kdj = this.checked;
      if (this.checked) opts.macd = false, document.getElementById("ckMacd").checked = false;
      refresh();
    });

    document.getElementById("btnExport").addEventListener("click", function () {
      SA.toast("已导出 120 个交易日的 K 线与指标为 CSV（原型不产生文件）", "ok");
    });

    /* 相对强弱 */
    var rel = D.focusKline.slice(-60).map(function (b, i) { return +((i / 60 * 21.2) + (Math.sin(i / 4) * 1.6)).toFixed(2); });
    F.area("#relStrength", { labels: D.focusKline.slice(-60).map(function (b) { return b.d.slice(5); }), data: rel, unit: "%" });

    /* 量价关系 */
    F.combo("#volPrice", {
      labels: D.focusKline.slice(-20).map(function (b) { return b.d.slice(5); }),
      unit: "万手",
      bars: [{ name: "成交量", data: D.focusKline.slice(-20).map(function (b) { return +(b.v / 10000).toFixed(2); }) }],
      lines: [{ name: "收盘价", data: D.focusKline.slice(-20).map(function (b) { return b.c; }), axis: 2, color: C.cv("--chart-4") }],
      unit2: "元",
      rotate: 30
    });

    SA.startLiveTicker("#qLive", { interval: 3000 });
  }

  window.SA_PANELS = window.SA_PANELS || {};
  window.SA_PANELS.trend = { key: "trend", title: "趋势", render: render };
})();
