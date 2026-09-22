/* ============================================================
   SA · 股析 — 个股面板：股权
   由 design/web/stock.html#equity 机械迁移而来（批次 3）。
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
      '<div class="fs-13 fw-600">' + name + " 的股权明细在演示版中未内置" +
      '</div><div class="fs-12 t-3 mt-2">原型只为 ' +
      ((D.stockByCode[FOCUS] && D.stockByCode[FOCUS].name) || FOCUS) + " " + FOCUS +
      ' 准备了完整的明细数据；其它标的可切到「概览」查看行情与估值。</div>' +
      '</div></div>';
  }

  function render(host, code) {
    if (code && code !== FOCUS) { fallback(host, code); return; }

    host.innerHTML = "      <div class=\"sa-split\">\n        <div class=\"col gap-4\">\n\n          <!-- 股权关系拓扑 -->\n          <div class=\"card is-accent\">\n            <div class=\"card-head\">\n              <span class=\"card-title\">股权关系拓扑</span>\n              <span class=\"card-sub\">边标持股比例 · 可拖拽</span>\n              <div class=\"card-tools\">\n                <span class=\"segmented\" id=\"segLayout\">\n                  <span class=\"is-active\" data-v=\"force\">力导向</span>\n                  <span data-v=\"circular\">环形</span>\n                </span>\n                <label class=\"check\"><input type=\"checkbox\" id=\"ckLabel\" checked> 显示持股比例</label>\n              </div>\n            </div>\n            <div class=\"card-body\">\n              <div id=\"eqGraph\" class=\"chart chart-2xl\"></div>\n              <div class=\"grid grid-4 mt-3\">\n                <div class=\"kpi\"><span class=\"kpi-label\">实际控制人</span><span class=\"kpi-value is-sm\">曾毓群</span><span class=\"kpi-delta t-3\">直接 22.42% + 间接 10.6%</span></div>\n                <div class=\"kpi\"><span class=\"kpi-label\">控股子公司</span><span class=\"kpi-value is-sm\">6 家</span><span class=\"kpi-delta t-3\">3 家合资控股</span></div>\n                <div class=\"kpi\"><span class=\"kpi-label\">参股公司</span><span class=\"kpi-value is-sm\">2 家</span><span class=\"kpi-delta t-3\">上游资源 + 智能座舱</span></div>\n                <div class=\"kpi\"><span class=\"kpi-label\">合资 / 技术授权</span><span class=\"kpi-value is-sm\">2 家</span><span class=\"kpi-delta t-3\">福特 LRS / Stellantis</span></div>\n              </div>\n            </div>\n          </div>\n\n          <!-- 股权穿透 -->\n          <div class=\"card\">\n            <div class=\"card-head\"><span class=\"card-title\">股权穿透路径</span><span class=\"card-sub\">实际控制人控制链</span></div>\n            <div class=\"card-body\">\n              <div id=\"penetration\" class=\"col gap-2\"></div>\n\n            </div>\n          </div>\n\n          <!-- 十大股东 -->\n          <div class=\"card\">\n            <div class=\"card-head\">\n              <span class=\"card-title\">十大股东与变动</span>\n              <span class=\"card-sub\">2026 年半年报 · 单位：亿股 / 亿元</span>\n              <div class=\"card-tools\"><span class=\"tag tag-up\">增持 4</span><span class=\"tag tag-down\">减持 2</span><span class=\"tag\">不变 4</span></div>\n            </div>\n            <div class=\"card-body is-flush\">\n              <div class=\"tbl-wrap\">\n                <table class=\"tbl is-comfort\">\n                  <thead>\n                    <tr><th style=\"width:36px\" class=\"num\">#</th><th>股东名称</th><th>类型</th><th class=\"num\">持股（亿股）</th><th class=\"num\">占比</th><th class=\"num\">变动</th><th class=\"num\">持股市值</th><th style=\"width:120px\">变动方向</th></tr>\n                  </thead>\n                  <tbody id=\"holdersBody\"></tbody>\n                </table>\n              </div>\n              <div class=\"tbl-foot\"><span>股东数据每交易日 19:30 从定期报告与公告抽取，持股数按最新披露口径</span></div>\n            </div>\n          </div>\n\n          <!-- 机构持仓 -->\n          <div class=\"grid grid-2\">\n            <div class=\"card\">\n              <div class=\"card-head\"><span class=\"card-title\">机构持仓趋势</span><span class=\"card-sub\">占流通股比例（%）</span></div>\n              <div class=\"card-body\"><div id=\"instChart\" class=\"chart chart-lg\"></div></div>\n            </div>\n            <div class=\"card\">\n              <div class=\"card-head\"><span class=\"card-title\">股东户数与集中度</span><span class=\"card-sub\">户数越少、户均持股越高，筹码越集中</span></div>\n              <div class=\"card-body\">\n                <div id=\"holderNum\" class=\"chart chart-md\"></div>\n                <div class=\"grid grid-2 mt-3\">\n                  <div class=\"kpi\"><span class=\"kpi-label\">股东户数</span><span class=\"kpi-value is-sm t-up\">18.6 万</span><span class=\"kpi-delta t-up\">↓ -6.4% 环比</span></div>\n                  <div class=\"kpi\"><span class=\"kpi-label\">户均持股</span><span class=\"kpi-value is-sm t-up\">236.4 万元</span><span class=\"kpi-delta t-up\">↑ +12.8%</span></div>\n                </div>\n              </div>\n            </div>\n          </div>\n\n          <!-- 质押 + 对外投资 -->\n          <div class=\"grid grid-3\">\n            <div class=\"card\">\n              <div class=\"card-head\"><span class=\"card-title\">股权质押</span><span class=\"card-sub\">控股股东及一致行动人</span></div>\n              <div class=\"card-body\">\n                <div id=\"pledgeGauge\" class=\"chart chart-sm\"></div>\n                <div class=\"col gap-2 mt-2\">\n                  <div class=\"row-between\"><span class=\"fs-12 t-2\">质押比例</span><span class=\"mono fs-12 t-up\">2.4%</span></div>\n                  <div class=\"row-between\"><span class=\"fs-12 t-2\">质押股数</span><span class=\"mono fs-12\">1.06 亿股</span></div>\n                  <div class=\"row-between\"><span class=\"fs-12 t-2\">质押笔数</span><span class=\"mono fs-12\">3 笔</span></div>\n                  <div class=\"row-between\"><span class=\"fs-12 t-2\">预警线估算</span><span class=\"mono fs-12 t-up\">约 148 元</span></div>\n                </div>\n\n              </div>\n            </div>\n\n            <div class=\"card span-2\">\n              <div class=\"card-head\">\n                <span class=\"card-title\">对外投资与并购记录</span>\n                <span class=\"card-sub\">近 12 个月 · 单位：亿元</span>\n                <div class=\"card-tools\"><span class=\"tag tag-outline\">合计 360.2 亿</span></div>\n              </div>\n              <div class=\"card-body is-flush\">\n                <div class=\"tbl-wrap\">\n                  <table class=\"tbl is-comfort\">\n                    <thead><tr><th style=\"width:96px\">日期</th><th>标的</th><th>类型</th><th class=\"num\">金额</th><th class=\"num\">持股</th><th>说明</th></tr></thead>\n                    <tbody id=\"invBody\"></tbody>\n                  </table>\n                </div>\n              </div>\n            </div>\n          </div>\n\n          <!-- 子公司清单 -->\n          <div class=\"card\">\n            <div class=\"card-head\">\n              <span class=\"card-title\">控股与参股公司清单</span>\n              <span class=\"card-sub\">含认缴金额与业务定位</span>\n              <div class=\"card-tools\"><button class=\"btn btn-sm btn-ghost\" id=\"btnExpand\">全部展开</button></div>\n            </div>\n            <div class=\"card-body\">\n              <div class=\"grid grid-2\" id=\"subList\"></div>\n            </div>\n          </div>\n\n        </div>\n\n        <aside class=\"col gap-4\">\n          <div class=\"card\">\n            <div class=\"card-head\"><span class=\"card-title\">控制权稳定性</span><span class=\"tag tag-ok\">稳定</span></div>\n            <div class=\"card-body col gap-3\">\n              <div>\n                <div class=\"row-between\"><span class=\"fs-12 t-2\">实际控制人合计控制</span><span class=\"mono fs-14 fw-600 t-up\">33.02%</span></div>\n                <div class=\"meter mt-2\"><i style=\"width:66%\"></i></div>\n              </div>\n              <div class=\"row-between\"><span class=\"fs-12 t-2\">一致行动人</span><span class=\"tag tag-outline\">宁波联合创新 10.6%</span></div>\n              <div class=\"row-between\"><span class=\"fs-12 t-2\">董事会席位</span><span class=\"mono fs-12\">9 席中控制 6 席</span></div>\n              <div class=\"row-between\"><span class=\"fs-12 t-2\">近一年控制权变更</span><span class=\"tag tag-ok\">无</span></div>\n              <div class=\"row-between\"><span class=\"fs-12 t-2\">要约收购风险</span><span class=\"tag tag-ok\">低</span></div>\n            </div>\n          </div>\n\n          <div class=\"card\">\n            <div class=\"card-head\"><span class=\"card-title\">股东结构速览</span></div>\n            <div class=\"card-body\">\n              <div id=\"holderDonut\" class=\"chart chart-sm\"></div>\n              <div class=\"col gap-2 mt-2\">\n                <div class=\"row-between\"><span class=\"fs-12 t-2\">个人股东</span><span class=\"mono fs-12\">38.6%</span></div>\n                <div class=\"row-between\"><span class=\"fs-12 t-2\">机构投资者</span><span class=\"mono fs-12\">25.2%</span></div>\n                <div class=\"row-between\"><span class=\"fs-12 t-2\">北向资金</span><span class=\"mono fs-12\">7.09%</span></div>\n                <div class=\"row-between\"><span class=\"fs-12 t-2\">国家队 / 社保</span><span class=\"mono fs-12\">4.0%</span></div>\n                <div class=\"row-between\"><span class=\"fs-12 t-2\">其他流通股</span><span class=\"mono fs-12\">25.1%</span></div>\n              </div>\n            </div>\n          </div>\n\n          <div class=\"card\">\n            <div class=\"card-head\"><span class=\"card-title\">值得关注</span></div>\n            <div class=\"card-body col gap-2\">\n              <div class=\"row gap-2\"><span class=\"tag tag-down\">减持</span><span class=\"fs-12 t-2\">董事李平减持计划进行中（≤0.1%）</span></div>\n              <div class=\"row gap-2\"><span class=\"tag tag-up\">增持</span><span class=\"fs-12 t-2\">北向连续 3 月增持，累计 +0.67pct</span></div>\n              <div class=\"row gap-2\"><span class=\"tag tag-brand\">新进</span><span class=\"fs-12 t-2\">兴全合润混合新进前十</span></div>\n              <div class=\"row gap-2\"><span class=\"tag tag-outline\">不变</span><span class=\"fs-12 t-2\">曾毓群、瑞庭投资持股未变动</span></div>\n            </div>\n            <div class=\"card-foot\">\n              <a class=\"t-brand\" href=\"stock.html#events\">查看相关事件 →</a>\n              <a class=\"t-brand\" href=\"stock.html#capital\">查看增减持明细 →</a>\n            </div>\n          </div>\n\n        </aside>\n      </div>";

    var D = window.SA_DATA, C = window.SA_CHARTS, F = C.F, eq = D.equity;

    var cats = [
      { name: "control", color: C.cv("--chart-4") },
      { name: "self", color: C.cv("--chart-1") },
      { name: "sub", color: C.cv("--chart-2") },
      { name: "assoc", color: C.cv("--chart-3") },
      { name: "partner", color: C.cv("--chart-7") }
    ];

    function drawGraph(layout, showLabel) {
      document.getElementById("eqGraph").innerHTML = "";
      F.graph("#eqGraph", {
        nodes: eq.graph.nodes.map(function (n) {
          return Object.assign({}, n, { size: n.id === "self" ? 64 : 42, short: n.name.length > 8 ? n.name.slice(0, 8) + "…" : n.name });
        }),
        links: eq.graph.links,
        categories: cats,
        layout: layout,
        circular: layout === "circular",
        showLinkLabel: showLabel,
        repulsion: 420,
        edgeLength: [90, 200]
      });
      C.rebuildAll();
    }
    drawGraph("force", true);

    SA.qsa("#segLayout > *").forEach(function (el) {
      el.addEventListener("click", function () {
        SA.qsa("#segLayout > *").forEach(function (o) { o.classList.toggle("is-active", o === el); });
        drawGraph(el.getAttribute("data-v"), document.getElementById("ckLabel").checked);
      });
    });
    document.getElementById("ckLabel").addEventListener("change", function () {
      drawGraph(document.querySelector("#segLayout .is-active").getAttribute("data-v"), this.checked);
    });

    /* 股权穿透 */
    document.getElementById("penetration").innerHTML = eq.penetration.map(function (p, i) {
      var w = Math.max(28, p.pct * 3);
      return '<div class="pen-row">' +
        '<span class="mono fs-11 t-3" style="width:56px">L' + p.level + "</span>" +
        '<div class="pen-bar" style="width:' + w + 'px"></div>' +
        '<span class="fs-13 fw-600">' + p.name + "</span>" +
        '<span class="mono fs-12 t-up">' + p.pct + "%</span>" +
        '<span class="hint" style="margin-left:auto">' + p.note + "</span>" +
        "</div>";
    }).join("");

    /* 十大股东 */
    document.getElementById("holdersBody").innerHTML = eq.topHolders.map(function (h) {
      var cls = h.chg > 0 ? "is-up" : h.chg < 0 ? "is-down" : "is-flat";
      var tag = h.chgType === "增持" ? "tag-up" : h.chgType === "减持" ? "tag-down" : "tag";
      var dir = h.chg === 0 ? '<span class="hint">—</span>'
        : '<span class="minibar" style="width:70px"><i style="width:' + Math.min(100, Math.abs(h.chg) * 160) + "%;background:var(--" + (h.chg > 0 ? "up" : "down") + ')"></i></span>';
      return "<tr>" +
        '<td class="num t-3">' + h.rank + "</td>" +
        '<td class="fw-600">' + h.name + "</td>" +
        '<td><span class="tag tag-outline">' + h.type + "</span></td>" +
        '<td class="num">' + h.shares.toFixed(2) + "</td>" +
        '<td class="num">' + h.pct.toFixed(2) + "%</td>" +
        '<td class="num ' + cls + '">' + (h.chg > 0 ? "+" : "") + h.chg.toFixed(2) + "</td>" +
        '<td class="num">' + h.value.toFixed(1) + " 亿</td>" +
        '<td><span class="tag ' + tag + '">' + h.chgType + "</span>" + dir + "</td>" +
        "</tr>";
    }).join("");

    /* 机构持仓 */
    F.combo("#instChart", {
      labels: eq.institutions.labels, unit: "%",
      lines: [
        { name: "公募基金", data: eq.institutions.fund, color: C.cv("--chart-1"), area: true },
        { name: "北向资金", data: eq.institutions.northbound, color: C.cv("--chart-2"), area: true },
        { name: "社保基金", data: eq.institutions.social, color: C.cv("--chart-6"), area: true },
        { name: "QFII", data: eq.institutions.qfii, color: C.cv("--chart-3"), area: true }
      ],
      bars: []
    });

    /* 股东户数 */
    F.combo("#holderNum", {
      labels: ["25Q1", "25Q2", "25Q3", "25Q4", "26Q1", "26Q2"],
      unit: "万户",
      bars: [{ name: "股东户数", data: [24.2, 23.6, 22.8, 21.4, 19.9, 18.6] }],
      lines: [{ name: "户均持股（万元）", data: [162, 168, 176, 186, 209, 236], axis: 2, color: C.cv("--chart-4") }],
      unit2: "万元"
    });

    /* 质押仪表 */
    F.gauge("#pledgeGauge", { value: eq.pledge.ratio, max: 50, suffix: "%", name: "质押比例", warnAt: 20, dangerAt: 40 });

    /* 对外投资 */
    document.getElementById("invBody").innerHTML = eq.investments.map(function (iv) {
      return '<tr class="is-clickable">' +
        '<td class="mono fs-11 t-3">' + iv.date + "</td>" +
        '<td class="fw-600">' + iv.target + "</td>" +
        '<td><span class="tag tag-outline">' + iv.type + "</span></td>" +
        '<td class="num">' + iv.amount.toFixed(1) + " 亿</td>" +
        '<td class="num">' + iv.stake + "</td>" +
        '<td class="ellipsis" style="max-width:320px">' + iv.note + "</td>" +
        "</tr>";
    }).join("");

    /* 子公司清单 */
    var catName = { control: "控股方", self: "本公司", sub: "控股子公司", assoc: "参股公司", partner: "合资 / 技术授权" };
    document.getElementById("subList").innerHTML = eq.graph.nodes.map(function (n) {
      var lk = eq.graph.links.filter(function (l) { return l.target === n.id; })[0];
      var ratio = lk ? lk.ratio : 0;
      var type = lk ? lk.type : "";
      return '<div class="fold">' +
        '<div class="fold-head">' +
          '<span class="tag ' + (n.cat === "self" ? "tag-brand" : "tag-outline") + '">' + (catName[n.cat] || n.cat) + "</span>" +
          '<span class="fw-600">' + n.name + "</span>" +
          (ratio ? '<span class="mono fs-12 t-up">' + ratio + "%</span>" : "") +
          (type ? '<span class="tag tag-outline">' + type + "</span>" : "") +
          '<span class="caret">▼</span>' +
        "</div>" +
        '<div class="fold-body hint">' + n.desc + "</div>" +
        "</div>";
    }).join("");

    SA.qsa(".fold-head").forEach(function (h) {
      h.addEventListener("click", function () { h.parentNode.classList.toggle("is-collapsed"); });
    });
    document.getElementById("btnExpand").addEventListener("click", function () {
      var anyOpen = SA.qsa("#subList .fold:not(.is-collapsed)").length > 0;
      SA.qsa("#subList .fold").forEach(function (f) { f.classList.toggle("is-collapsed", anyOpen); });
      this.textContent = anyOpen ? "全部展开" : "全部折叠";
    });

    /* 股东结构环形 */
    F.donut("#holderDonut", {
      center: ["36%", "50%"], radius: ["48%", "70%"],
      label: false,
      data: [
        { name: "个人股东", value: 38.6 },
        { name: "机构投资者", value: 25.2 },
        { name: "北向资金", value: 7.09 },
        { name: "国家队/社保", value: 4.0 },
        { name: "其他流通股", value: 25.11 }
      ]
    });

    SA.startLiveTicker("#qLive", { interval: 3000 });
  }

  window.SA_PANELS = window.SA_PANELS || {};
  window.SA_PANELS.equity = { key: "equity", title: "股权", render: render };
})();
