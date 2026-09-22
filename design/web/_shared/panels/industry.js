/* ============================================================
   SA · 股析 — 个股面板：行业
   由 design/web/stock.html#industry 机械迁移而来（批次 3）。
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
      '<div class="fs-13 fw-600">' + name + " 的行业明细在演示版中未内置" +
      '</div><div class="fs-12 t-3 mt-2">原型只为 ' +
      ((D.stockByCode[FOCUS] && D.stockByCode[FOCUS].name) || FOCUS) + " " + FOCUS +
      ' 准备了完整的明细数据；其它标的可切到「概览」查看行情与估值。</div>' +
      '</div></div>';
  }

  function render(host, code) {
    if (code && code !== FOCUS) { fallback(host, code); return; }

    host.innerHTML = "      <div class=\"sa-split\">\n        <div class=\"col gap-4\">\n\n          <!-- 产业链传导拓扑 -->\n          <div class=\"card is-accent\">\n            <div class=\"card-head\">\n              <span class=\"card-title\">产业链传导拓扑</span>\n              <span class=\"card-sub\">带宽 = 成本占比 · 可拖拽</span>\n              <div class=\"card-tools\">\n                <span class=\"segmented\" id=\"segChain\">\n                  <span class=\"is-active\" data-v=\"sankey\">桑基传导图</span>\n                  <span data-v=\"cards\">环节卡片</span>\n                </span>\n              </div>\n            </div>\n            <div class=\"card-body\">\n              <div class=\"graph-panel is-active\" data-panel=\"sankey\">\n                <div id=\"chainSankey\" class=\"chart chart-2xl\"></div>\n              </div>\n              <div class=\"graph-panel\" data-panel=\"cards\">\n                <div class=\"grid grid-3\" id=\"chainCards\"></div>\n              </div>\n\n            </div>\n          </div>\n\n          <!-- 行业景气与估值 -->\n          <div class=\"grid grid-3\">\n            <div class=\"card\">\n              <div class=\"card-head\"><span class=\"card-title\">行业景气度</span><span class=\"card-sub\">电池 · 0–100</span></div>\n              <div class=\"card-body\">\n                <div id=\"boomGauge\" class=\"chart chart-sm\"></div>\n                <div class=\"col gap-2 mt-2\">\n                  <div class=\"row-between\"><span class=\"fs-12 t-2\">开工率</span><span class=\"mono fs-12 t-up\">82%</span></div>\n                  <div class=\"row-between\"><span class=\"fs-12 t-2\">订单能见度</span><span class=\"mono fs-12 t-up\">2.4 季度</span></div>\n                  <div class=\"row-between\"><span class=\"fs-12 t-2\">库存天数</span><span class=\"mono fs-12 t-up\">38 天（健康）</span></div>\n                  <div class=\"row-between\"><span class=\"fs-12 t-2\">产品价格趋势</span><span class=\"mono fs-12 t-up\">企稳回升</span></div>\n                </div>\n              </div>\n            </div>\n            <div class=\"card\">\n              <div class=\"card-head\"><span class=\"card-title\">行业估值分位</span><span class=\"card-sub\">电池行业 PE 历史分位</span></div>\n              <div class=\"card-body\">\n                <div id=\"indPe\" class=\"chart chart-sm\"></div>\n                <div class=\"col gap-2 mt-2\">\n                  <div class=\"row-between\"><span class=\"fs-12 t-2\">行业 PE</span><span class=\"mono fs-12\">26.8</span></div>\n                  <div class=\"row-between\"><span class=\"fs-12 t-2\">历史中位数</span><span class=\"mono fs-12\">32.4</span></div>\n                  <div class=\"row-between\"><span class=\"fs-12 t-2\">当前分位</span><span class=\"mono fs-12 t-up\">38%</span></div>\n                  <div class=\"row-between\"><span class=\"fs-12 t-2\">本股 PE</span><span class=\"mono fs-12 t-up\">22.4（折价 16%）</span></div>\n                </div>\n              </div>\n            </div>\n            <div class=\"card\">\n              <div class=\"card-head\"><span class=\"card-title\">市占率格局</span><span class=\"card-sub\">全球动力电池装机份额</span></div>\n              <div class=\"card-body\">\n                <div id=\"shareDonut\" class=\"chart chart-sm\"></div>\n                <div class=\"col gap-2 mt-2\">\n                  <div class=\"row-between\"><span class=\"fs-12 t-2\">宁德时代</span><span class=\"mono fs-12 t-up\">36.8%</span></div>\n                  <div class=\"row-between\"><span class=\"fs-12 t-2\">比亚迪弗迪</span><span class=\"mono fs-12\">15.2%</span></div>\n                  <div class=\"row-between\"><span class=\"fs-12 t-2\">中创新航</span><span class=\"mono fs-12\">6.8%</span></div>\n                  <div class=\"row-between\"><span class=\"fs-12 t-2\">其他</span><span class=\"mono fs-12\">41.2%</span></div>\n                </div>\n              </div>\n            </div>\n          </div>\n\n          <!-- 同业对比表 -->\n          <div class=\"card\">\n            <div class=\"card-head\">\n              <span class=\"card-title\">同业对比</span>\n              <span class=\"card-sub\">2025 年报口径 · 单位：亿元 · 点击表头排序</span>\n              <div class=\"card-tools\">\n                <span class=\"tag tag-outline\">已选 3 家对比</span>\n                <button class=\"btn btn-sm btn-outline\" data-open=\"dlgPeer\">调整对比公司</button>\n              </div>\n            </div>\n            <div class=\"card-body is-flush\">\n              <div class=\"tbl-wrap\">\n                <table class=\"tbl is-comfort\" id=\"peerTbl\">\n                  <thead>\n                    <tr>\n                      <th class=\"is-sortable\">公司 <span class=\"sort-caret\">▼</span></th>\n                      <th class=\"is-sortable\">营收 <span class=\"sort-caret\">▼</span></th>\n                      <th class=\"is-sortable\">净利润 <span class=\"sort-caret\">▼</span></th>\n                      <th class=\"is-sortable\">毛利率 <span class=\"sort-caret\">▼</span></th>\n                      <th class=\"is-sortable\">ROE <span class=\"sort-caret\">▼</span></th>\n                      <th class=\"is-sortable\">PE <span class=\"sort-caret\">▼</span></th>\n                      <th class=\"is-sortable\">PB <span class=\"sort-caret\">▼</span></th>\n                      <th class=\"is-sortable\">总市值 <span class=\"sort-caret\">▼</span></th>\n                      <th class=\"is-sortable\">涨跌幅 <span class=\"sort-caret\">▼</span></th>\n                      <th style=\"width:90px\">估值</th>\n                    </tr>\n                  </thead>\n                  <tbody id=\"peerBody\"></tbody>\n                </table>\n              </div>\n              <div class=\"tbl-foot\">\n                <span>数据口径：2025 年年报 + 最新收盘价；PE 为静态 PE，亏损公司显示「亏损」</span>\n                <a class=\"t-brand\" href=\"stock.html#finance\" style=\"margin-left:auto\">看完整财务对比 →</a>\n              </div>\n            </div>\n          </div>\n\n          <!-- 雷达 + 散点 -->\n          <div class=\"grid grid-2\">\n            <div class=\"card\">\n              <div class=\"card-head\">\n                <span class=\"card-title\">多指标雷达对比</span>\n                <span class=\"card-sub\">最多同时对比 5 家</span>\n                <div class=\"card-tools\">\n                  <span class=\"segmented\" id=\"segRadar\">\n                    <span class=\"is-active\" data-v=\"3\">3 家</span>\n                    <span data-v=\"6\">6 家</span>\n                  </span>\n                </div>\n              </div>\n              <div class=\"card-body\">\n                <div id=\"peerRadar\" class=\"chart chart-lg\"></div>\n                <div class=\"chart-legend mt-2\" id=\"radarLegend\"></div>\n              </div>\n            </div>\n            <div class=\"card\">\n              <div class=\"card-head\"><span class=\"card-title\">估值 × 盈利散点</span><span class=\"card-sub\">气泡大小 = 总市值 · 红点为当前股</span></div>\n              <div class=\"card-body\">\n                <div id=\"peerBubble\" class=\"chart chart-lg\"></div>\n\n              </div>\n            </div>\n          </div>\n\n          <!-- 产业链环节明细 -->\n          <div class=\"card\">\n            <div class=\"card-head\">\n              <span class=\"card-title\">产业链环节明细</span>\n              <span class=\"card-sub\">各环节景气度、公司参与度与代表标的</span>\n            </div>\n            <div class=\"card-body is-flush\">\n              <div class=\"tbl-wrap\">\n                <table class=\"tbl is-comfort\">\n                  <thead><tr><th style=\"width:88px\">环节</th><th>细分</th><th style=\"width:120px\" class=\"num\">景气度</th><th class=\"num\">公司参与度</th><th>说明</th><th style=\"width:170px\">代表标的</th></tr></thead>\n                  <tbody id=\"chainBody\"></tbody>\n                </table>\n              </div>\n            </div>\n          </div>\n\n        </div>\n\n        <aside class=\"col gap-4\">\n          <div class=\"card\">\n            <div class=\"card-head\"><span class=\"card-title\">行业地位</span><span class=\"tag tag-up\">龙头</span></div>\n            <div class=\"card-body col gap-3\">\n              <div class=\"row-between\"><span class=\"fs-12 t-2\">行业排名（按市值）</span><span class=\"mono fs-12 t-up\">1 / 86</span></div>\n              <div class=\"row-between\"><span class=\"fs-12 t-2\">全球市占率</span><span class=\"mono fs-12 t-up\">36.8%</span></div>\n              <div class=\"row-between\"><span class=\"fs-12 t-2\">国内市占率</span><span class=\"mono fs-12 t-up\">42.6%</span></div>\n              <div class=\"row-between\"><span class=\"fs-12 t-2\">营收行业占比</span><span class=\"mono fs-12\">34.2%</span></div>\n              <div class=\"row-between\"><span class=\"fs-12 t-2\">净利润行业占比</span><span class=\"mono fs-12 t-up\">48.6%</span></div>\n              <div class=\"row-between\"><span class=\"fs-12 t-2\">研发投入行业占比</span><span class=\"mono fs-12\">31.4%</span></div>\n              <div>\n                <div class=\"row-between\"><span class=\"fs-12 t-2\">行业定价权</span><span class=\"mono fs-12 t-up\">强</span></div>\n                <div class=\"meter mt-2\"><i style=\"width:88%\"></i></div>\n              </div>\n            </div>\n          </div>\n\n          <div class=\"card\">\n            <div class=\"card-head\"><span class=\"card-title\">上下游依赖</span></div>\n            <div class=\"card-body col gap-2\">\n              <div class=\"row-between\"><span class=\"fs-12 t-2\">前五大客户占比</span><span class=\"mono fs-12 t-warn\">42.4%</span></div>\n              <div class=\"row-between\"><span class=\"fs-12 t-2\">前五大供应商占比</span><span class=\"mono fs-12 t-warn\">38.6%</span></div>\n              <div class=\"row-between\"><span class=\"fs-12 t-2\">海外收入占比</span><span class=\"mono fs-12 t-up\">32.8%</span></div>\n              <div class=\"row-between\"><span class=\"fs-12 t-2\">储能收入占比</span><span class=\"mono fs-12 t-up\">26.4%</span></div>\n\n            </div>\n          </div>\n\n          <div class=\"card\">\n            <div class=\"card-head\"><span class=\"card-title\">同业涨跌排行</span><span class=\"card-sub\">电池行业今日</span></div>\n            <div class=\"card-body\"><div id=\"peerBar\" class=\"chart chart-md\"></div></div>\n          </div>\n\n        </aside>\n      </div>";

    var D = window.SA_DATA, C = window.SA_CHARTS, F = C.F, ind = D.industry;

    /* 桑基 */
    F.sankey("#chainSankey", {
      nodes: ind.chain.nodes.map(function (n, i) {
        var color = n.stage === "上游" ? C.cv("--chart-4") : n.stage === "中游" ? C.cv("--chart-1") : C.cv("--chart-2");
        return { name: n.name, level: n.stage === "上游" ? 0 : n.stage === "中游" ? 1 : 2, color: color, note: n.note + (n.share ? " · 市占率 " + n.share + "%" : "") + " · 景气度 " + n.boom };
      }),
      links: ind.chain.links,
      categories: [
        { depth: 0, itemStyle: { color: C.cv("--chart-4") } },
        { depth: 1, itemStyle: { color: C.cv("--chart-1") } },
        { depth: 2, itemStyle: { color: C.cv("--chart-2") } }
      ]
    });

    /* 环节卡片 */
    document.getElementById("chainCards").innerHTML = ["上游", "中游", "下游"].map(function (stage) {
      var nodes = ind.chain.nodes.filter(function (n) { return n.stage === stage; });
      return '<div class="col gap-2"><div class="fs-13 fw-600">' + stage + " · " + nodes.length + " 个环节</div>" +
        nodes.map(function (n) {
          return '<div class="chain-node' + (n.name === "宁德时代" ? " is-self" : "") + '">' +
            '<div class="row-between"><span class="fs-12 fw-600">' + n.name + "</span>" +
            '<span class="mono fs-11 ' + (n.boom >= 65 ? "t-up" : n.boom >= 50 ? "t-2" : "t-down") + '">' + n.boom + "</span></div>" +
            '<div class="hint">' + n.note + (n.share ? " · 市占率 " + n.share + "%" : "") + "</div>" +
            '<div class="boom-bar"><i style="width:' + n.boom + '%"></i></div>' +
            "</div>";
        }).join("") + "</div>";
    }).join("");

    SA.qsa("#segChain > *").forEach(function (el) {
      el.addEventListener("click", function () {
        SA.qsa("#segChain > *").forEach(function (o) { o.classList.toggle("is-active", o === el); });
        SA.qsa(".graph-panel").forEach(function (p) {
          p.classList.toggle("is-active", p.getAttribute("data-panel") === el.getAttribute("data-v"));
        });
      });
    });

    /* 景气 / 估值 / 市占 */
    F.gauge("#boomGauge", { value: 68, max: 100, name: "电池行业景气度", warnAt: 40, dangerAt: 60 });
    F.area("#indPe", { labels: Array.from({ length: 36 }, function (_, i) { return "M" + (i + 1); }), data: ind.valuation.industryPe.history, unit: "倍" });
    F.donut("#shareDonut", {
      center: ["38%", "50%"], radius: ["48%", "70%"], label: false,
      data: [
        { name: "宁德时代", value: 36.8, color: C.cv("--chart-1") },
        { name: "比亚迪弗迪", value: 15.2, color: C.cv("--chart-2") },
        { name: "中创新航", value: 6.8, color: C.cv("--chart-3") },
        { name: "亿纬锂能", value: 4.6, color: C.cv("--chart-4") },
        { name: "其他", value: 36.6, color: C.cv("--flat") }
      ]
    });

    /* 同业表 */
    document.getElementById("peerBody").innerHTML = ind.peers.map(function (p) {
      var cls = p.pct >= 0 ? "t-up" : "t-down";
      var val = p.self ? '<span class="tag tag-up">低估</span>'
        : p.pe === 0 ? '<span class="tag tag-warn">亏损</span>'
        : p.pe < 22 ? '<span class="tag tag-up">低估</span>'
        : p.pe < 30 ? '<span class="tag">合理</span>' : '<span class="tag tag-down">偏高</span>';
      return '<tr data-chg="' + (p.pct >= 0 ? "up" : "down") + '" class="is-clickable" data-href="stock.html?code=' + p.code + '">' +
        '<td class="fw-600">' + p.name + (p.self ? ' <span class="tag tag-brand">本股</span>' : "") + "</td>" +
        '<td class="num">' + p.revenue.toFixed(1) + "</td>" +
        '<td class="num ' + (p.netProfit >= 0 ? "" : "t-down") + '">' + p.netProfit.toFixed(1) + "</td>" +
        '<td class="num">' + p.grossMargin.toFixed(1) + "%</td>" +
        '<td class="num ' + (p.roe >= 0 ? "t-up" : "t-down") + '">' + p.roe.toFixed(1) + "%</td>" +
        '<td class="num">' + (p.pe ? p.pe.toFixed(1) : "亏损") + "</td>" +
        '<td class="num">' + p.pb.toFixed(2) + "</td>" +
        '<td class="num">' + p.cap.toLocaleString() + "</td>" +
        '<td class="num ' + cls + '">' + (p.pct >= 0 ? "+" : "") + p.pct.toFixed(2) + "%</td>" +
        "<td>" + val + "</td></tr>";
    }).join("");

    /* 雷达 */
    function drawRadar(n) {
      document.getElementById("peerRadar").innerHTML = "";
      F.radar("#peerRadar", {
        indicators: ind.peerRadar.indicators,
        series: ind.peerRadar.series.slice(0, n === 3 ? 3 : 3).concat(n === 6 ? [
          { name: "欣旺达", values: [14.2, 3.9, 9.8, 14.6, 3.2, 2.4] },
          { name: "贝特瑞", values: [21.4, 8.4, 12.8, 18.2, 4.1, 3.1] },
          { name: "国轩高科", values: [16.8, 4.2, 6.4, 22.4, 4.8, 3.6] }
        ] : []),
        radius: "62%"
      });
      C.rebuildAll();
    }
    drawRadar(3);
    SA.qsa("#segRadar > *").forEach(function (el) {
      el.addEventListener("click", function () {
        SA.qsa("#segRadar > *").forEach(function (o) { o.classList.toggle("is-active", o === el); });
        drawRadar(parseInt(el.getAttribute("data-v"), 10));
      });
    });

    /* 散点 */
    F.bubble("#peerBubble", { data: ind.valuation.peersScatter });

    /* 产业链明细表 */
    document.getElementById("chainBody").innerHTML = ind.chain.nodes.map(function (n) {
      var boom = n.boom;
      var lv = boom >= 70 ? "t-up" : boom >= 55 ? "t-2" : "t-down";
      var peers = n.stage === "上游" ? "天齐锂业 / 赣锋锂业 / 恩捷股份"
        : n.stage === "中游" ? "宁德时代 / 比亚迪 / 亿纬锂能" : "比亚迪 / 阳光电源 / 特斯拉";
      return "<tr>" +
        '<td><span class="tag tag-outline">' + n.stage + "</span></td>" +
        '<td class="fw-600">' + n.name + "</td>" +
        '<td class="num ' + lv + '">' + boom + " <span class=\"minibar\"><i style=\"width:" + boom + '%"></i></span></td>' +
        '<td class="num">' + (n.share ? n.share + "%" : "—") + "</td>" +
        '<td class="ellipsis" style="max-width:300px">' + n.note + "</td>" +
        "<td>" + peers + "</td></tr>";
    }).join("");

    /* 同业涨跌排行 */
    F.hbar("#peerBar", {
      data: ind.peers.filter(function (p) { return !p.self; }).concat([{ name: "国轩高科", pct: 1.86, flow: 0.42 }, { name: "鹏辉能源", pct: -0.64, flow: -0.28 }])
    });

    SA.startLiveTicker("#qLive", { interval: 3000 });
  }

  window.SA_PANELS = window.SA_PANELS || {};
  window.SA_PANELS.industry = { key: "industry", title: "行业", render: render };
})();
