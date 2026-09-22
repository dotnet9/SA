/* ============================================================
   SA · 股析 — 个股面板：风险
   由 design/web/stock.html#risk 机械迁移而来（批次 3）。
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
      '<div class="fs-13 fw-600">' + name + " 的风险明细在演示版中未内置" +
      '</div><div class="fs-12 t-3 mt-2">原型只为 ' +
      ((D.stockByCode[FOCUS] && D.stockByCode[FOCUS].name) || FOCUS) + " " + FOCUS +
      ' 准备了完整的明细数据；其它标的可切到「概览」查看行情与估值。</div>' +
      '</div></div>';
  }

  function render(host, code) {
    if (code && code !== FOCUS) { fallback(host, code); return; }

    host.innerHTML = "      <div class=\"sa-split\">\n        <div class=\"col gap-4\">\n\n          <!-- 概览 -->\n          <div class=\"card is-accent\">\n            <div class=\"card-head\">\n              <span class=\"card-title\">风险概览</span>\n              <span class=\"card-sub\">风险矩阵 · 告警 · 舆情情绪</span>\n              <div class=\"card-tools\">\n                <span class=\"segmented\" id=\"segRange\">\n                  <span class=\"is-active\" data-v=\"90\">近 90 天</span>\n                  <span data-v=\"180\">近 180 天</span>\n                  <span data-v=\"365\">近 1 年</span>\n                </span>\n                <button class=\"btn btn-sm btn-ghost\" id=\"btnExport\">导出</button>\n              </div>\n            </div>\n            <div class=\"card-body\">\n              <div class=\"grid grid-6\">\n                <div class=\"kpi\"><span class=\"kpi-label\">最高风险等级</span><span class=\"kpi-value is-sm t-danger\">4 / 5</span><span class=\"kpi-delta t-3\">价格战 / 原材料</span></div>\n                <div class=\"kpi\"><span class=\"kpi-label\">高风险项</span><span class=\"kpi-value is-sm t-danger\">4</span><span class=\"kpi-delta t-3\">等级 ≥ 4</span></div>\n                <div class=\"kpi\"><span class=\"kpi-label\">中等风险项</span><span class=\"kpi-value is-sm t-warn\">5</span><span class=\"kpi-delta t-3\">等级 3</span></div>\n                <div class=\"kpi\"><span class=\"kpi-label\">待关注告警</span><span class=\"kpi-value is-sm t-warn\">8</span><span class=\"kpi-delta t-3\">近 6 个月</span></div>\n                <div class=\"kpi\"><span class=\"kpi-label\">舆情情绪得分</span><span class=\"kpi-value is-sm t-up\">74</span><span class=\"kpi-delta t-up\">↑ +6 环比</span></div>\n                <div class=\"kpi\"><span class=\"kpi-label\">负面占比</span><span class=\"kpi-value is-sm t-up\">9%</span><span class=\"kpi-delta t-up\">↓ -3pct</span></div>\n              </div>\n            </div>\n          </div>\n\n          <!-- 风险矩阵 -->\n          <div class=\"card\">\n            <div class=\"card-head\">\n              <span class=\"card-title\">风险矩阵</span>\n              <span class=\"card-sub\">横轴严重度 · 纵轴概率</span>\n              <div class=\"card-tools\">\n                <span class=\"risk-lv risk-lv-1\"><i></i>1</span>\n                <span class=\"risk-lv risk-lv-2\"><i></i>2</span>\n                <span class=\"risk-lv risk-lv-3\"><i></i>3</span>\n                <span class=\"risk-lv risk-lv-4\"><i></i>4</span>\n                <span class=\"risk-lv risk-lv-5\"><i></i>5</span>\n              </div>\n            </div>\n            <div class=\"card-body\">\n              <div id=\"riskMatrix\" class=\"chart chart-xl\"></div>\n\n            </div>\n          </div>\n\n          <!-- 风险雷达 + 舆情 -->\n          <div class=\"grid grid-2\">\n            <div class=\"card\">\n              <div class=\"card-head\"><span class=\"card-title\">六维风险雷达</span><span class=\"card-sub\">数值越高风险越大（0–100）</span></div>\n              <div class=\"card-body\">\n                <div id=\"riskRadar\" class=\"chart chart-lg\"></div>\n\n              </div>\n            </div>\n            <div class=\"card\">\n              <div class=\"card-head\">\n                <span class=\"card-title\">舆情情绪趋势</span>\n                <span class=\"card-sub\">正负面新闻条数占比与情绪得分</span>\n                <div class=\"card-tools\"><span class=\"tag tag-up\">情绪转好</span></div>\n              </div>\n              <div class=\"card-body\">\n                <div id=\"sentiment\" class=\"chart chart-lg\"></div>\n                <div class=\"grid grid-3 mt-3\">\n                  <div class=\"kpi\"><span class=\"kpi-label\">正面</span><span class=\"kpi-value is-sm t-up\">66%</span><span class=\"kpi-delta t-up\">↑ +8pct</span></div>\n                  <div class=\"kpi\"><span class=\"kpi-label\">负面</span><span class=\"kpi-value is-sm t-down\">9%</span><span class=\"kpi-delta t-up\">↓ -3pct</span></div>\n                  <div class=\"kpi\"><span class=\"kpi-label\">中性</span><span class=\"kpi-value is-sm t-flat\">25%</span><span class=\"kpi-delta t-3\">—</span></div>\n                </div>\n              </div>\n            </div>\n          </div>\n\n          <!-- 舆情热词 -->\n          <div class=\"card\">\n            <div class=\"card-head\">\n              <span class=\"card-title\">舆情热词</span>\n              <span class=\"card-sub\">词大小 = 出现权重 · 颜色 = 情绪倾向</span>\n              <div class=\"card-tools\"><span class=\"hint\">数据源：公开新闻与资讯，每交易日 20:30 计算</span></div>\n            </div>\n            <div class=\"card-body\">\n              <div class=\"wordcloud\" id=\"hotWords\"></div>\n              <div class=\"grid grid-2 mt-4\">\n                <div>\n                  <div class=\"fs-12 fw-600 t-up\" style=\"margin-bottom:8px\">利好热词</div>\n                  <div id=\"posBar\" class=\"chart chart-sm\"></div>\n                </div>\n                <div>\n                  <div class=\"fs-12 fw-600 t-down\" style=\"margin-bottom:8px\">利空热词</div>\n                  <div id=\"negBar\" class=\"chart chart-sm\"></div>\n                </div>\n              </div>\n            </div>\n          </div>\n\n          <!-- 告警清单 -->\n          <div class=\"card\">\n            <div class=\"card-head\">\n              <span class=\"card-title\">告警清单</span>\n              <span class=\"card-sub\">风险项明细</span>\n              <div class=\"card-tools\">\n                <select class=\"select input-sm\" id=\"fLevel\" style=\"width:120px\">\n                  <option value=\"all\">全部等级</option>\n                  <option value=\"4\">仅高风险（≥4）</option>\n                  <option value=\"3\">仅中风险（=3）</option>\n                  <option value=\"2\">仅低风险（≤2）</option>\n                </select>\n              </div>\n            </div>\n            <div class=\"card-body\">\n              <div id=\"alertList\"></div>\n            </div>\n          </div>\n\n          <!-- 关联方风险传导 -->\n          <div class=\"card\">\n            <div class=\"card-head\">\n              <span class=\"card-title\">关联方风险传导</span>\n              <span class=\"card-sub\">子公司 / 参股公司 / 合资伙伴的风险敞口</span>\n            </div>\n            <div class=\"card-body\">\n              <div id=\"relatedRisk\" class=\"chart chart-lg\"></div>\n\n            </div>\n          </div>\n\n        </div>\n\n        <aside class=\"col gap-4\">\n          <div class=\"card\">\n            <div class=\"card-head\"><span class=\"card-title\">风险综合评分</span><span class=\"tag tag-warn\">中等</span></div>\n            <div class=\"card-body\">\n              <div id=\"riskGauge\" class=\"chart chart-sm\"></div>\n              <div class=\"hint\">综合评分越低越安全；本公司 34 / 100，行业均值 42 / 100</div>\n              <div class=\"col gap-2 mt-3\">\n                <div class=\"row-between\"><span class=\"fs-12 t-2\">财务风险</span><span class=\"mono fs-12 t-up\">18</span></div>\n                <div class=\"row-between\"><span class=\"fs-12 t-2\">经营风险</span><span class=\"mono fs-12 t-warn\">32</span></div>\n                <div class=\"row-between\"><span class=\"fs-12 t-2\">治理风险</span><span class=\"mono fs-12 t-up\">16</span></div>\n                <div class=\"row-between\"><span class=\"fs-12 t-2\">市场风险</span><span class=\"mono fs-12 t-down\">58</span></div>\n                <div class=\"row-between\"><span class=\"fs-12 t-2\">合规风险</span><span class=\"mono fs-12 t-up\">22</span></div>\n                <div class=\"row-between\"><span class=\"fs-12 t-2\">舆情风险</span><span class=\"mono fs-12 t-up\">26</span></div>\n              </div>\n            </div>\n          </div>\n\n          <div class=\"card\">\n            <div class=\"card-head\"><span class=\"card-title\">最需关注的三件事</span></div>\n            <div class=\"card-body col gap-3\">\n              <div class=\"fold\">\n                <div class=\"fold-head\"><span class=\"risk-lv risk-lv-4\"><i></i></span>行业价格战<span class=\"caret\">▼</span></div>\n                <div class=\"fold-body hint\">二线厂商报价下调 8%，若持续将压制 2027 年毛利率。应对：储能与海外高毛利业务占比提升。</div>\n              </div>\n              <div class=\"fold is-collapsed\">\n                <div class=\"fold-head\"><span class=\"risk-lv risk-lv-4\"><i></i></span>原材料价格波动<span class=\"caret\">▼</span></div>\n                <div class=\"fold-body hint\">碳酸锂自低点回升 6.4%，直接抬升正极成本。应对：参股洛阳钼业锁定资源，长协比例 62%。</div>\n              </div>\n              <div class=\"fold is-collapsed\">\n                <div class=\"fold-head\"><span class=\"risk-lv risk-lv-4\"><i></i></span>海外贸易壁垒<span class=\"caret\">▼</span></div>\n                <div class=\"fold-body hint\">欧盟终裁加征关税，美国 IRA 本地化要求。应对：西班牙与匈牙利本地化建厂，技术授权模式（福特 LRS）。</div>\n              </div>\n            </div>\n          </div>\n\n          <div class=\"card\">\n            <div class=\"card-head\"><span class=\"card-title\">告警订阅</span></div>\n            <div class=\"card-body col gap-2\">\n              <label class=\"check\"><input type=\"checkbox\" checked> 新增监管函 / 问询函</label>\n              <label class=\"check\"><input type=\"checkbox\" checked> 股东减持计划披露</label>\n              <label class=\"check\"><input type=\"checkbox\" checked> 股权质押比例变动超 1pct</label>\n              <label class=\"check\"><input type=\"checkbox\" checked> 负面舆情占比超 20%</label>\n              <label class=\"check\"><input type=\"checkbox\"> 商誉减值计提</label>\n              <label class=\"check\"><input type=\"checkbox\"> 涉及重大诉讼</label>\n              <a class=\"btn btn-outline btn-block mt-3\" href=\"alerts.html\">管理提醒规则 →</a>\n            </div>\n          </div>\n\n        </aside>\n      </div>";

    var D = window.SA_DATA, C = window.SA_CHARTS, F = C.F, rk = D.risk;

    /* 矩阵：x = 严重度(1-5)，y = 概率(5→1 自上而下) */
    var grid = [];
    for (var y = 0; y < 5; y++) {
      for (var x = 0; x < 5; x++) {
        var sev = x + 1, prob = 5 - y;
        var items = rk.matrix.filter(function (m) { return m.severity === sev && m.probability === prob; });
        grid.push({
          x: x, y: y,
          level: items.length ? Math.max.apply(null, items.map(function (m) { return m.level; })) : Math.max(1, Math.round(sev * prob / 5)),
          risks: items.map(function (m) { return m.type + "：" + m.note; })
        });
      }
    }
    F.heatmap("#riskMatrix", {
      xLabels: ["1 极低", "2 低", "3 中", "4 高", "5 极高"],
      yLabels: ["概率 5 极高", "4 高", "3 中", "2 低", "1 极低"],
      data: grid
    });

    /* 雷达 */
    F.radar("#riskRadar", { indicators: rk.radar.indicators, series: rk.radar.series, radius: "64%" });

    /* 舆情 */
    F.combo("#sentiment", {
      labels: rk.sentiment.labels, unit: "条",
      bars: [
        { name: "正面", data: rk.sentiment.positive, color: C.cv("--up") },
        { name: "负面", data: rk.sentiment.negative, color: C.cv("--down") },
        { name: "中性", data: rk.sentiment.neutral, color: C.cv("--flat") }
      ],
      lines: [{ name: "情绪得分", data: rk.sentiment.score, axis: 2, color: C.cv("--chart-2") }],
      unit2: "分"
    });

    /* 热词 */
    document.getElementById("hotWords").innerHTML = rk.hotWords.map(function (w) {
      var size = 11 + Math.round(w.weight / 12);
      var cls = w.tone === "up" ? "is-up" : w.tone === "down" ? "is-down" : "";
      return '<span class="wc ' + cls + '" style="font-size:' + size + 'px">' + w.word +
        ' <span class="mono" style="opacity:.6;font-size:10px">' + w.weight + "</span></span>";
    }).join("");

    var pos = rk.hotWords.filter(function (w) { return w.tone === "up"; });
    var neg = rk.hotWords.filter(function (w) { return w.tone === "down"; });
    F.hbar("#posBar", { data: pos.map(function (w) { return { name: w.word, pct: w.weight / 12 }; }) });
    F.hbar("#negBar", { data: neg.map(function (w) { return { name: w.word, pct: -w.weight / 12 }; }) });

    /* 告警清单 */
    function lvColor(l) {
      return l >= 5 ? "#ef4444" : l === 4 ? "#f97316" : l === 3 ? "#eab308" : l === 2 ? "#84cc16" : "#22c55e";
    }
    function renderAlerts() {
      var f = document.getElementById("fLevel").value;
      var rows = rk.alerts.filter(function (a) {
        if (f === "all") return true;
        var l = parseInt(f, 10);
        return f === "4" ? a.level >= 4 : f === "3" ? a.level === 3 : a.level <= 2;
      });
      document.getElementById("alertList").innerHTML = rows.length ? rows.map(function (a) {
        return '<div class="alert-row">' +
          '<div class="alert-lv" style="background:' + lvColor(a.level) + '">' + a.level + "</div>" +
          '<div class="grow">' +
            '<div class="row gap-2 wrap">' +
              '<span class="tag tag-outline">' + a.type + "</span>" +
              '<span class="fs-13 fw-600">' + a.title + "</span>" +
              '<span class="mono fs-11 t-3" style="margin-left:auto">' + a.date + "</span>" +
            "</div>" +
            '<div class="fs-12 t-2 mt-1" style="margin-top:4px;line-height:1.7">' + a.detail + "</div>" +
            '<div class="hint mt-1" style="margin-top:4px">来源：' + a.source + "</div>" +
          "</div>" +
          '<div class="col gap-1" style="flex:0 0 auto">' +
            '<span class="risk-lv risk-lv-' + a.level + '"><i></i>' + (a.level >= 4 ? "高" : a.level === 3 ? "中" : "低") + "</span>" +
          "</div>" +
        "</div>";
      }).join("") : '<div class="empty"><div class="em-icon">◌</div><div class="fs-13">该筛选条件下暂无告警</div></div>';
    }
    renderAlerts();
    document.getElementById("fLevel").addEventListener("change", renderAlerts);

    /* 关联方风险 */
    F.hbar("#relatedRisk", {
      data: [
        { name: "Stellantis 欧洲合资", pct: 82 },
        { name: "邦普循环（回收）", pct: 64 },
        { name: "时代储能（海外）", pct: 58 },
        { name: "时代广汽", pct: 42 },
        { name: "时代上汽", pct: 38 },
        { name: "四川时代", pct: 28 },
        { name: "宁德新能源", pct: 22 },
        { name: "时代智能", pct: 18 }
      ]
    });

    F.gauge("#riskGauge", { value: 34, max: 100, name: "综合风险评分", warnAt: 30, dangerAt: 60 });

    SA.qsa(".fold-head").forEach(function (h) {
      h.addEventListener("click", function () { h.parentNode.classList.toggle("is-collapsed"); });
    });
    document.getElementById("btnExport").addEventListener("click", function () {
      SA.toast("已导出风险与告警清单为 CSV（原型不产生文件）", "ok");
    });

    SA.startLiveTicker("#qLive", { interval: 3000 });
  }

  window.SA_PANELS = window.SA_PANELS || {};
  window.SA_PANELS.risk = { key: "risk", title: "风险", render: render };
})();
