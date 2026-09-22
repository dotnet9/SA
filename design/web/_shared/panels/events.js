/* ============================================================
   SA · 股析 — 个股面板：事件
   由 design/web/stock.html#events 机械迁移而来（批次 3）。
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
      '<div class="fs-13 fw-600">' + name + " 的事件明细在演示版中未内置" +
      '</div><div class="fs-12 t-3 mt-2">原型只为 ' +
      ((D.stockByCode[FOCUS] && D.stockByCode[FOCUS].name) || FOCUS) + " " + FOCUS +
      ' 准备了完整的明细数据；其它标的可切到「概览」查看行情与估值。</div>' +
      '</div></div>';
  }

  function render(host, code) {
    if (code && code !== FOCUS) { fallback(host, code); return; }

    host.innerHTML = "      <div class=\"sa-split\">\n        <div class=\"col gap-4\">\n\n          <!-- 四种拓扑图 -->\n          <div class=\"card is-accent\">\n            <div class=\"card-head\">\n              <span class=\"card-title\">四种拓扑图</span>\n              <span class=\"card-sub\">节点可拖拽 · 画布可缩放</span>\n              <div class=\"card-tools\">\n                <span class=\"tag tag-outline\">近 90 天</span>\n                <a class=\"btn btn-sm btn-ghost\" href=\"stock.html#events\">集中对照页 →</a>\n              </div>\n            </div>\n            <div class=\"card-body\">\n              <div class=\"graph-tabbar\" data-tabs=\".graph-panel\">\n                <div class=\"graph-tab is-active\" data-tab=\"causal\"><span class=\"gt-num\">1</span>事件因果网络</div>\n                <div class=\"graph-tab\" data-tab=\"equity\"><span class=\"gt-num\">2</span>公司股权关系</div>\n                <div class=\"graph-tab\" data-tab=\"chain\"><span class=\"gt-num\">3</span>产业链传导</div>\n                <div class=\"graph-tab\" data-tab=\"topdown\"><span class=\"gt-num\">4</span>自上而下影响传导</div>\n              </div>\n\n              <!-- ① 事件因果网络 -->\n              <div class=\"graph-panel is-active\" data-panel=\"causal\">\n                <div class=\"chart-toolbar\">\n                  <label class=\"check\"><input type=\"checkbox\" id=\"ckUp\" checked> 只看利好</label>\n                  <label class=\"check\"><input type=\"checkbox\" id=\"ckDown\" checked> 只看利空</label>\n                  <span class=\"hint\">实线 = 正向传导 · 虚线 = 负向压制 · 线宽 = 影响强度</span>\n                  <span class=\"hint\" style=\"margin-left:auto\">共 12 个事件节点 · 13 条传导关系</span>\n                </div>\n                <div id=\"causalGraph\" class=\"chart chart-2xl\"></div>\n                <div class=\"grid grid-3 mt-3\">\n\n\n\n                </div>\n              </div>\n\n              <!-- ② 公司股权关系 -->\n              <div class=\"graph-panel\" data-panel=\"equity\">\n                <div class=\"chart-toolbar\">\n                  <span class=\"hint\">聚焦与事件相关方：控股方、控股子公司、参股公司与合资伙伴</span>\n                  <a class=\"btn btn-sm btn-ghost\" style=\"margin-left:auto\" href=\"stock.html#equity\">进入完整股权分析 →</a>\n                </div>\n                <div id=\"equityGraph\" class=\"chart chart-2xl\"></div>\n\n              </div>\n\n              <!-- ③ 产业链传导 -->\n              <div class=\"graph-panel\" data-panel=\"chain\">\n                <div class=\"chart-toolbar\">\n                  <span class=\"hint\">事件化的产业链：上游价格变动如何传导到本公司与下游</span>\n                  <a class=\"btn btn-sm btn-ghost\" style=\"margin-left:auto\" href=\"stock.html#industry\">进入完整行业分析 →</a>\n                </div>\n                <div id=\"chainSankey\" class=\"chart chart-2xl\"></div>\n\n              </div>\n\n              <!-- ④ 自上而下传导 -->\n              <div class=\"graph-panel\" data-panel=\"topdown\">\n                <div class=\"chart-toolbar\">\n                  <span class=\"hint\">宏观 / 政策 → 行业 → 个股 的传导链路与影响强度</span>\n                  <span class=\"hint\" style=\"margin-left:auto\">共 12 个节点 · 10 条传导关系</span>\n                </div>\n                <div id=\"topdownSankey\" class=\"chart chart-2xl\"></div>\n                <div class=\"grid grid-3 mt-3\">\n\n\n\n                </div>\n              </div>\n            </div>\n          </div>\n\n          <!-- 时间轴泳道 -->\n          <div class=\"card\">\n            <div class=\"card-head\">\n              <span class=\"card-title\">事件时间轴泳道</span>\n              <span class=\"card-sub\">按主体分泳道</span>\n              <div class=\"card-tools\">\n                <span class=\"tag tag-up\">利好</span><span class=\"tag tag-down\">利空</span><span class=\"tag\">中性</span>\n              </div>\n            </div>\n            <div class=\"card-body\">\n              <div id=\"laneChart\" class=\"chart chart-xl\"></div>\n\n            </div>\n          </div>\n\n          <!-- 事件类型统计 -->\n          <div class=\"grid grid-2\">\n            <div class=\"card\">\n              <div class=\"card-head\"><span class=\"card-title\">事件类型与影响方向统计</span><span class=\"card-sub\">近 90 天</span></div>\n              <div class=\"card-body\">\n                <div id=\"typeChart\" class=\"chart chart-lg\"></div>\n\n              </div>\n            </div>\n            <div class=\"card\">\n              <div class=\"card-head\"><span class=\"card-title\">事件强度分布</span><span class=\"card-sub\">气泡大小 = 影响强度</span></div>\n              <div class=\"card-body\">\n                <div id=\"strengthChart\" class=\"chart chart-lg\"></div>\n              </div>\n            </div>\n          </div>\n\n          <!-- 事件表 -->\n          <div class=\"card\">\n            <div class=\"card-head\">\n              <span class=\"card-title\">事件明细表</span>\n              <span class=\"card-sub\">规则引擎自动抽取 · 影响方向与强度为人工标注</span>\n              <div class=\"card-tools\">\n                <select class=\"select input-sm\" id=\"fType\" style=\"width:120px\"></select>\n                <select class=\"select input-sm\" id=\"fImpact\" style=\"width:110px\">\n                  <option value=\"all\">全部方向</option>\n                  <option value=\"up\">仅利好</option>\n                  <option value=\"down\">仅利空</option>\n                </select>\n                <button class=\"btn btn-sm btn-outline\" id=\"btnExport\">导出</button>\n              </div>\n            </div>\n            <div class=\"card-body is-flush\">\n              <div class=\"tbl-wrap\">\n                <table class=\"tbl is-comfort\" id=\"evTbl\">\n                  <thead>\n                    <tr>\n                      <th class=\"is-sortable\" style=\"width:96px\">日期 <span class=\"sort-caret\">▼</span></th>\n                      <th style=\"width:88px\">类型</th>\n                      <th>事件标题</th>\n                      <th style=\"width:88px\">影响方向</th>\n                      <th class=\"is-sortable\" style=\"width:130px\">影响强度 <span class=\"sort-caret\">▼</span></th>\n                      <th style=\"width:130px\">来源</th>\n                      <th style=\"width:190px\">涉及方</th>\n                      <th style=\"width:64px\" class=\"col-actions\">操作</th>\n                    </tr>\n                  </thead>\n                  <tbody id=\"evBody\"></tbody>\n                </table>\n              </div>\n              <div class=\"tbl-foot\">\n                <span id=\"evCount\"></span>\n                <span class=\"hint\" style=\"margin-left:auto\">共 21 条 · 显示前 12 条</span>\n              </div>\n            </div>\n          </div>\n\n        </div>\n\n        <aside class=\"col gap-4\">\n          <div class=\"card\">\n            <div class=\"card-head\"><span class=\"card-title\">事件影响速览</span><span class=\"tag tag-up\">净偏多</span></div>\n            <div class=\"card-body col gap-3\">\n              <div class=\"grid grid-2\">\n                <div class=\"kpi\"><span class=\"kpi-label\">利好事件</span><span class=\"kpi-value is-sm t-up\">14</span><span class=\"kpi-delta t-3\">平均强度 62</span></div>\n                <div class=\"kpi\"><span class=\"kpi-label\">利空事件</span><span class=\"kpi-value is-sm t-down\">7</span><span class=\"kpi-delta t-3\">平均强度 48</span></div>\n              </div>\n              <div>\n                <div class=\"row-between\"><span class=\"fs-12 t-2\">情绪温度（事件加权）</span><span class=\"mono fs-12 t-up\">74 / 100</span></div>\n                <div class=\"meter is-ok mt-2\"><i style=\"width:74%\"></i></div>\n              </div>\n\n            </div>\n          </div>\n\n          <div class=\"card\">\n            <div class=\"card-head\"><span class=\"card-title\">因果链路 Top 3</span></div>\n            <div class=\"card-body col gap-3\">\n              <div class=\"fold\">\n                <div class=\"fold-head\"><span class=\"tag tag-up\">+74</span>业绩超预期 → 目标价上调<span class=\"caret\">▼</span></div>\n                <div class=\"fold-body hint\">业绩预告净利润同比预增 32%–45%，3 家机构在当日上调目标价，一致目标价由 302 升至 320 元。</div>\n              </div>\n              <div class=\"fold is-collapsed\">\n                <div class=\"fold-head\"><span class=\"tag tag-up\">+72</span>储能大单 → 业绩超预期<span class=\"caret\">▼</span></div>\n                <div class=\"fold-body hint\">海外储能框架协议 12GWh，储能毛利率高于动力电池，直接抬升整体盈利水平。</div>\n              </div>\n              <div class=\"fold is-collapsed\">\n                <div class=\"fold-head\"><span class=\"tag tag-down\">-48</span>价格战 → 毛利率承压<span class=\"caret\">▼</span></div>\n                <div class=\"fold-body hint\">二线厂商报价下调 8%，行业价格中枢下移；公司凭借高毛利储能与海外订单对冲，毛利率仍环比改善。</div>\n              </div>\n            </div>\n          </div>\n\n          <div class=\"card\">\n            <div class=\"card-head\"><span class=\"card-title\">关键时点</span></div>\n            <div class=\"card-body\">\n              <div class=\"timeline\">\n                <div class=\"tl-item is-warn\"><span class=\"tl-dot\"></span>\n                  <div class=\"tl-head\"><span class=\"tl-time\">2026-10-24</span><span class=\"tag tag-warn\">待发生</span></div>\n                  <div class=\"tl-title\">三季报正式披露</div>\n                  <div class=\"tl-body\">业绩预告区间中值对应净利 +38.5%</div>\n                </div>\n                <div class=\"tl-item is-brand\"><span class=\"tl-dot\"></span>\n                  <div class=\"tl-head\"><span class=\"tl-time\">2026-10-15</span><span class=\"tag tag-outline\">预计</span></div>\n                  <div class=\"tl-title\">固态电池路线图正式稿发布</div>\n                  <div class=\"tl-body\">征求意见稿已于 09-18 发布，正式稿预计 10 月落地</div>\n                </div>\n                <div class=\"tl-item is-down\"><span class=\"tl-dot\"></span>\n                  <div class=\"tl-head\"><span class=\"tl-time\">2026-11-30</span><span class=\"tag tag-outline\">预计</span></div>\n                  <div class=\"tl-title\">李平减持计划到期</div>\n                  <div class=\"tl-body\">计划减持不超过 0.1%，剩余可减持约 0.06%</div>\n                </div>\n              </div>\n            </div>\n          </div>\n\n        </aside>\n      \n        <div class=\"card mt-4\">\n          <div class=\"card-head\"><span class=\"card-title\">四种拓扑的差异对照</span><span class=\"card-sub\">选型参考</span></div>\n          <div class=\"card-body is-flush\">\n            <div class=\"tbl-wrap\">\n              <table class=\"tbl is-comfort\">\n              <thead>\n              <tr><th style=\"width:170px\">维度</th><th>① 事件因果网络</th><th>② 公司股权关系</th><th>③ 产业链传导</th><th>④ 自上而下传导</th></tr>\n              </thead>\n              <tbody>\n              <tr><td class=\"fw-600\">节点含义</td><td>事件</td><td>法律主体</td><td>产业链环节 / 公司</td><td>宏观 / 政策 / 行业 / 个股</td></tr>\n              <tr><td class=\"fw-600\">边含义</td><td>因果传导（可负）</td><td>持股 / 控制</td><td>成本占比 / 供货占比</td><td>影响强度</td></tr>\n              <tr><td class=\"fw-600\">有向性</td><td>有向（因 → 果）</td><td>有向（上 → 下）</td><td>有向（上 → 下）</td><td>有向（上 → 下）</td></tr>\n              <tr><td class=\"fw-600\">层级结构</td><td>无固定层级</td><td>三层（控股方 / 本公司 / 参控股）</td><td>三层（上游 / 中游 / 下游）</td><td>三层（宏观 / 行业 / 个股）</td></tr>\n              <tr><td class=\"fw-600\">数据来源</td><td>规则引擎抽取 + 人工标注</td><td>定期报告合并范围 + 公告</td><td>行业研究与公开统计</td><td>政策文件 + 宏观数据 + 人工标注</td></tr>\n              <tr><td class=\"fw-600\">更新频率</td><td>每交易日 3 次</td><td>每日 19:30</td><td>季度 / 事件驱动</td><td>事件驱动</td></tr>\n              <tr><td class=\"fw-600\">主要用途</td><td>催化剂追踪、事件驱动交易</td><td>关联交易、控制权、风险传染</td><td>成本与需求弹性、同业定位</td><td>主题投资、政策跟踪</td></tr>\n              <tr><td class=\"fw-600\">局限</td><td>依赖人工标注，覆盖有限</td><td>披露滞后，层级可能不全</td><td>占比为估算值，非披露值</td><td>强度为主观评分</td></tr>\n              </tbody>\n              </table>\n            </div>\n          </div>\n        </div>\n</div>";

    var D = window.SA_DATA, C = window.SA_CHARTS, F = C.F, ev = D.events;

    var catColor = {
      policy: C.cv("--chart-1"), finance: C.cv("--chart-6"), order: C.cv("--chart-2"),
      operate: C.cv("--chart-8"), supply: C.cv("--chart-4"), fund: C.cv("--chart-3"),
      price: C.cv("--chart-7"), capacity: C.cv("--chart-2"), rating: C.cv("--chart-1"),
      compete: C.cv("--chart-5"), regulatory: C.cv("--chart-5")
    };
    var catName = {
      policy: "政策", finance: "业绩", order: "订单", operate: "经营", supply: "成本",
      fund: "资金", price: "价格", capacity: "产能", rating: "评级", compete: "竞争"
    };

    /* ① 因果网络 */
    function drawCausal() {
      var upOn = document.getElementById("ckUp").checked;
      var downOn = document.getElementById("ckDown").checked;
      var nodes = ev.causal.nodes.filter(function (n) { return n.impact === "up" ? upOn : downOn; });
      var ids = nodes.map(function (n) { return n.id; });
      var links = ev.causal.links.filter(function (l) {
        return ids.indexOf(l.source) >= 0 && ids.indexOf(l.target) >= 0;
      });
      document.getElementById("causalGraph").innerHTML = "";
      F.graph("#causalGraph", {
        nodes: nodes.map(function (n) {
          return {
            id: n.id, name: n.name.length > 9 ? n.name.slice(0, 9) + "…" : n.name, cat: n.cat, size: 34 + n.strength / 5,
            desc: "类型：" + n.type + " · 日期：" + n.date + " · 方向：" + (n.impact === "up" ? "利好" : "利空") + " · 强度：" + n.strength
          };
        }),
        links: links,
        categories: Object.keys(catColor).map(function (k) { return { name: k, color: catColor[k] }; }),
        repulsion: 460, edgeLength: [110, 220], showLinkLabel: false, curveness: 0.18
      });
      C.rebuildAll();
    }
    drawCausal();
    document.getElementById("ckUp").addEventListener("change", drawCausal);
    document.getElementById("ckDown").addEventListener("change", drawCausal);

    /* ② 股权关系 */
    F.graph("#equityGraph", {
      nodes: D.equity.graph.nodes.map(function (n) {
        return Object.assign({}, n, {
          size: n.id === "self" ? 60 : 40,
          short: n.name.length > 7 ? n.name.slice(0, 7) + "…" : n.name
        });
      }),
      links: D.equity.graph.links,
      categories: [
        { name: "control", color: C.cv("--chart-4") }, { name: "self", color: C.cv("--chart-1") },
        { name: "sub", color: C.cv("--chart-2") }, { name: "assoc", color: C.cv("--chart-3") },
        { name: "partner", color: C.cv("--chart-7") }
      ],
      repulsion: 440, edgeLength: [100, 210]
    });

    /* ③ 产业链 */
    F.sankey("#chainSankey", {
      nodes: D.industry.chain.nodes.map(function (n) {
        return {
          name: n.name,
          level: n.stage === "上游" ? 0 : n.stage === "中游" ? 1 : 2,
          color: n.stage === "上游" ? C.cv("--chart-4") : n.stage === "中游" ? C.cv("--chart-1") : C.cv("--chart-2"),
          note: n.note + " · 景气度 " + n.boom
        };
      }),
      links: D.industry.chain.links
    });

    /* ④ 自上而下 */
    F.sankey("#topdownSankey", {
      nodes: ev.topdown.nodes.map(function (n) {
        return {
          name: n.name, level: n.level,
          color: n.impact === "up" ? C.cv("--chart-1") : C.cv("--chart-5"),
          note: "影响方向 " + (n.impact === "up" ? "利好" : "利空") + " · 强度 " + n.strength
        };
      }),
      links: ev.topdown.links
    });

    /* 泳道 */
    F.lanes("#laneChart", { lanes: ev.lanes });

    /* 类型统计 */
    F.stacked("#typeChart", {
      labels: ev.typeStats.map(function (t) { return t.type; }),
      unit: "条",
      series: [
        { name: "利好", data: ev.typeStats.map(function (t) { return t.up; }), color: C.cv("--up") },
        { name: "利空", data: ev.typeStats.map(function (t) { return t.down; }), color: C.cv("--down") },
        { name: "中性", data: ev.typeStats.map(function (t) { return t.neutral; }), color: C.cv("--flat") }
      ]
    });

    /* 强度散点 */
    F.bubble("#strengthChart", { data: ev.table.map(function (t, i) { return { name: t.type, pe: i + 1, roe: t.strength, cap: t.strength * 20 }; }), xName: "序号", yName: "影响强度" });

    /* 事件表 */
    var types = ["all"].concat(ev.typeStats.map(function (t) { return t.type; }));
    document.getElementById("fType").innerHTML = types.map(function (t) {
      return '<option value="' + t + '">' + (t === "all" ? "全部类型" : t) + "</option>";
    }).join("");

    function renderTable() {
      var ft = document.getElementById("fType").value;
      var fi = document.getElementById("fImpact").value;
      var rows = ev.table.filter(function (t) {
        return (ft === "all" || t.type === ft) && (fi === "all" || t.impact === fi);
      });
      document.getElementById("evCount").textContent = "筛选结果 " + rows.length + " 条";
      document.getElementById("evBody").innerHTML = rows.map(function (t, i) {
        var isUp = t.impact === "up";
        var barColor = isUp ? "var(--up)" : "var(--down)";
        return '<tr data-chg="' + (isUp ? "up" : "down") + '">' +
          '<td class="mono fs-11 t-3">' + t.date + "</td>" +
          '<td><span class="tag tag-outline">' + t.type + "</span></td>" +
          '<td class="fw-500">' + t.title + "</td>" +
          '<td><span class="tag ' + (isUp ? "tag-up" : "tag-down") + '">' + (isUp ? "利好" : "利空") + "</span></td>" +
          '<td><span class="ev-strength"><span class="mono fs-12" data-v="' + t.strength + '">' + t.strength + "</span>" +
            '<span class="bar"><i style="width:' + t.strength + "%;background:" + barColor + '"></i></span></span></td>' +
          "<td class=\"fs-11 t-3\">" + t.source + "</td>" +
          '<td class="fs-11">' + t.parties + "</td>" +
          '<td class="col-actions"><button class="btn btn-sm btn-ghost evDetail" data-i="' + ev.table.indexOf(t) + '">详情</button></td>' +
          "</tr>";
      }).join("");
      bindDetail();
    }

    function bindDetail() {
      SA.qsa(".evDetail").forEach(function (b) {
        b.addEventListener("click", function () {
          var t = ev.table[parseInt(b.getAttribute("data-i"), 10)];
          var isUp = t.impact === "up";
          document.getElementById("evTitle").textContent = t.title;
          document.getElementById("evDetail").innerHTML =
            '<div class="row gap-2 wrap">' +
              '<span class="tag ' + (isUp ? "tag-up" : "tag-down") + '">' + (isUp ? "利好" : "利空") + "</span>" +
              '<span class="tag tag-outline">' + t.type + "</span>" +
              '<span class="tag tag-outline">' + t.date + "</span>" +
              '<span class="tag tag-accent">强度 ' + t.strength + "</span>" +
            "</div>" +
            '<div class="kpi"><span class="kpi-label">来源</span><span class="kpi-value is-sm">' + t.source + "</span></div>" +
            '<div class="kpi"><span class="kpi-label">涉及方</span><span class="kpi-value is-sm">' + t.parties + "</span></div>" +
            '<div><div class="label">原文摘要</div><div class="legend-block mt-2">' +
              t.title + "。该事件由规则引擎从" + t.source + "自动抽取并入库，" +
              "影响方向标注为「" + (isUp ? "利好" : "利空") + "」，强度评分 " + t.strength + " / 100。" +
              "（原型中不展示公告原文全文，正式版提供原文链接与摘要。）</div></div>" +
            '<div><div class="label">传导关系</div>' +
              '<div class="col gap-2 mt-2">' +
                ev.causal.links.filter(function (l) { return l.note.indexOf(t.type) >= 0 || Math.abs(l.strength) > 60; }).slice(0, 3).map(function (l) {
                  return '<div class="row gap-2"><span class="tag ' + (l.strength >= 0 ? "tag-up" : "tag-down") + '">' +
                    (l.strength >= 0 ? "+" : "") + l.strength + "</span>" +
                    '<span class="fs-12 t-2">' + l.note + "</span></div>";
                }).join("") +
              "</div></div>" +
            '<button class="btn btn-outline btn-block" onclick="SA.closeDrawer(\'dlgEvent\')">关闭</button>';
          SA.openDrawer("dlgEvent");
        });
      });
    }
    renderTable();
    document.getElementById("fType").addEventListener("change", renderTable);
    document.getElementById("fImpact").addEventListener("change", renderTable);
    document.getElementById("btnExport").addEventListener("click", function () {
      SA.toast("已导出事件明细为 CSV（原型不产生文件）", "ok");
    });

    SA.startLiveTicker("#qLive", { interval: 3000 });
  }

  window.SA_PANELS = window.SA_PANELS || {};
  window.SA_PANELS.events = { key: "events", title: "事件", render: render };
})();
