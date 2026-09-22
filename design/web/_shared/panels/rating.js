/* ============================================================
   SA · 股析 — 个股面板：评级
   由 design/web/stock.html#rating 机械迁移而来（批次 3）。
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
      '<div class="fs-13 fw-600">' + name + " 的评级明细在演示版中未内置" +
      '</div><div class="fs-12 t-3 mt-2">原型只为 ' +
      ((D.stockByCode[FOCUS] && D.stockByCode[FOCUS].name) || FOCUS) + " " + FOCUS +
      ' 准备了完整的明细数据；其它标的可切到「概览」查看行情与估值。</div>' +
      '</div></div>';
  }

  function render(host, code) {
    if (code && code !== FOCUS) { fallback(host, code); return; }

    host.innerHTML = "      <div class=\"sa-split\">\n        <div class=\"col gap-4\">\n\n          <!-- 概览 -->\n          <div class=\"card is-accent\">\n            <div class=\"card-head\">\n              <span class=\"card-title\">评级概览</span>\n              <span class=\"card-sub\">近 12 个月 · 35 家机构给出评级</span>\n              <div class=\"card-tools\">\n                <span class=\"segmented\" id=\"segRange\">\n                  <span class=\"is-active\" data-v=\"12\">近 12 个月</span>\n                  <span data-v=\"3\">近 3 个月</span>\n                  <span data-v=\"1\">近 1 个月</span>\n                </span>\n                <button class=\"btn btn-sm btn-ghost\" id=\"btnExport\">导出</button>\n              </div>\n            </div>\n            <div class=\"card-body\">\n              <div class=\"grid grid-3\">\n                <div class=\"consensus-card\">\n                  <div class=\"hint\">一致目标价</div>\n                  <div class=\"mono fs-28 fw-700 t-up\">320.00 <span class=\"fs-12 t-3\">元</span></div>\n                  <div class=\"upside-band mt-3\"><span class=\"m\" style=\"left:19.2%\"></span></div>\n                  <div class=\"row-between mt-2\">\n                    <span class=\"hint\">现价 268.42</span>\n                    <span class=\"mono fs-12 t-up\">+19.2% 上行空间</span>\n                  </div>\n                  <div class=\"hint mt-2\">区间 252 – 320 元 · 12 家机构</div>\n                </div>\n                <div class=\"consensus-card\">\n                  <div class=\"hint\">评级分布</div>\n                  <div class=\"row gap-2 mt-2 wrap\">\n                    <span class=\"rating-badge rb-buy\">买入 18</span>\n                    <span class=\"rating-badge rb-add\">增持 12</span>\n                    <span class=\"rating-badge rb-neutral\">中性 4</span>\n                    <span class=\"rating-badge rb-reduce\">减持 1</span>\n                  </div>\n                  <div class=\"mt-3\">\n                    <div class=\"row-between\"><span class=\"fs-12 t-2\">看多占比（买入 + 增持）</span><span class=\"mono fs-12 t-up\">85.7%</span></div>\n                    <div class=\"meter is-ok mt-2\"><i style=\"width:86%\"></i></div>\n                  </div>\n                  <div class=\"hint mt-2\">近 3 个月新增 6 家覆盖，评级上调 5 家、下调 1 家</div>\n                </div>\n                <div class=\"consensus-card\">\n                  <div class=\"hint\">一致预期（2026E）</div>\n                  <div class=\"grid grid-2 mt-2\">\n                    <div><div class=\"hint\">EPS</div><div class=\"mono fs-18 fw-600\">15.86</div></div>\n                    <div><div class=\"hint\">净利（亿元）</div><div class=\"mono fs-18 fw-600 t-up\">786.2</div><div class=\"hint t-up\">+26.2%</div></div>\n                    <div><div class=\"hint\">营收（亿元）</div><div class=\"mono fs-14\">5,120.4</div></div>\n                    <div><div class=\"hint\">预测 PE</div><div class=\"mono fs-14 t-up\">16.9</div></div>\n                  </div>\n                  <div class=\"hint mt-2\">覆盖机构 35 家 · 一致预期由各家预测中位数得出</div>\n                </div>\n              </div>\n            </div>\n          </div>\n\n          <!-- 评级历史 -->\n          <div class=\"card\">\n            <div class=\"card-head\">\n              <span class=\"card-title\">评级分布与平均目标价走势</span>\n              <span class=\"card-sub\">柱 = 机构数 · 线 = 目标价</span>\n            </div>\n            <div class=\"card-body\">\n              <div id=\"ratingHistory\" class=\"chart chart-lg\"></div>\n\n            </div>\n          </div>\n\n          <!-- 目标价散点 -->\n          <div class=\"card\">\n            <div class=\"card-head\">\n              <span class=\"card-title\">机构目标价分布</span>\n              <span class=\"card-sub\">纵轴 = 目标价（元）</span>\n              <div class=\"card-tools\">\n                <span class=\"tag tag-up\">买入</span><span class=\"tag tag-brand\">增持</span>\n                <span class=\"tag\">中性</span><span class=\"tag tag-down\">减持</span>\n              </div>\n            </div>\n            <div class=\"card-body\">\n              <div id=\"targetChart\" class=\"chart chart-xl\"></div>\n              <div class=\"grid grid-4 mt-3\">\n                <div class=\"kpi\"><span class=\"kpi-label\">最高目标价</span><span class=\"kpi-value is-sm t-up\">320.00</span><span class=\"kpi-delta t-3\">中金公司</span></div>\n                <div class=\"kpi\"><span class=\"kpi-label\">最低目标价</span><span class=\"kpi-value is-sm t-down\">252.00</span><span class=\"kpi-delta t-3\">某券商（中性）</span></div>\n                <div class=\"kpi\"><span class=\"kpi-label\">中位目标价</span><span class=\"kpi-value is-sm\">308.00</span><span class=\"kpi-delta t-3\">12 家机构</span></div>\n                <div class=\"kpi\"><span class=\"kpi-label\">目标价标准差</span><span class=\"kpi-value is-sm\">22.4</span><span class=\"kpi-delta t-3\">分歧度中等</span></div>\n              </div>\n            </div>\n          </div>\n\n          <!-- 一致预期 -->\n          <div class=\"grid grid-2\">\n            <div class=\"card\">\n              <div class=\"card-head\"><span class=\"card-title\">一致预期（未来 3 年）</span><span class=\"card-sub\">EPS / 营收 / 净利 / 预测 PE</span></div>\n              <div class=\"card-body\">\n                <div id=\"consensusChart\" class=\"chart chart-lg\"></div>\n                <table class=\"tbl mt-3\">\n                  <thead><tr><th>年度</th><th class=\"num\">EPS（元）</th><th class=\"num\">营收（亿元）</th><th class=\"num\">净利（亿元）</th><th class=\"num\">净利增速</th><th class=\"num\">预测 PE</th><th class=\"num\">覆盖机构</th></tr></thead>\n                  <tbody id=\"consensusBody\"></tbody>\n                </table>\n              </div>\n            </div>\n            <div class=\"card\">\n              <div class=\"card-head\"><span class=\"card-title\">预测 PE 带</span><span class=\"card-sub\">按一致预期 EPS 计算的动态 PE</span></div>\n              <div class=\"card-body\">\n                <div id=\"peBand\" class=\"chart chart-md\"></div>\n                <div class=\"grid grid-3 mt-3\">\n                  <div class=\"kpi\"><span class=\"kpi-label\">当前 PE（TTM）</span><span class=\"kpi-value is-sm\">24.8</span><span class=\"kpi-delta t-3\">静态口径</span></div>\n                  <div class=\"kpi\"><span class=\"kpi-label\">2026E PE</span><span class=\"kpi-value is-sm t-up\">16.9</span><span class=\"kpi-delta t-up\">↓ 31.9%</span></div>\n                  <div class=\"kpi\"><span class=\"kpi-label\">2028E PE</span><span class=\"kpi-value is-sm t-up\">11.4</span><span class=\"kpi-delta t-up\">↓ 54.0%</span></div>\n                </div>\n\n              </div>\n            </div>\n          </div>\n\n          <!-- 盈利预测调整 -->\n          <div class=\"card\">\n            <div class=\"card-head\">\n              <span class=\"card-title\">盈利预测调整</span>\n              <span class=\"card-sub\">近 6 个月一致预期变动 · 上调为正向信号</span>\n            </div>\n            <div class=\"card-body\">\n              <div id=\"reviseChart\" class=\"chart chart-lg\"></div>\n\n            </div>\n          </div>\n\n          <!-- 评级明细表 -->\n          <div class=\"card\">\n            <div class=\"card-head\">\n              <span class=\"card-title\">评级明细</span>\n              <span class=\"card-sub\">点击表头排序</span>\n              <div class=\"card-tools\">\n                <select class=\"select input-sm\" id=\"fRating\" style=\"width:120px\">\n                  <option value=\"all\">全部评级</option>\n                  <option value=\"买入\">仅买入</option>\n                  <option value=\"增持\">仅增持</option>\n                  <option value=\"中性\">仅中性</option>\n                </select>\n                <span class=\"tag tag-outline\">共 12 条</span>\n              </div>\n            </div>\n            <div class=\"card-body is-flush\">\n              <div class=\"tbl-wrap\">\n                <table class=\"tbl is-comfort\">\n                  <thead>\n                    <tr>\n                      <th>机构</th><th>分析师</th>\n                      <th class=\"is-sortable\" style=\"width:100px\">日期 <span class=\"sort-caret\">▼</span></th>\n                      <th style=\"width:88px\">评级</th>\n                      <th class=\"is-sortable\" style=\"width:100px\">目标价 <span class=\"sort-caret\">▼</span></th>\n                      <th class=\"num\" style=\"width:96px\">前值</th>\n                      <th class=\"num\" style=\"width:100px\">距现价</th>\n                      <th style=\"width:100px\">变动</th>\n                      <th style=\"width:96px\" class=\"col-actions\">操作</th>\n                    </tr>\n                  </thead>\n                  <tbody id=\"ratingBody\"></tbody>\n                </table>\n              </div>\n              <div class=\"tbl-foot\">\n                <span>数据来源：公开机构评级汇总，每交易日 20:00 更新；本页不含研报全文，仅评级与目标价</span>\n              </div>\n            </div>\n          </div>\n\n        </div>\n\n        <aside class=\"col gap-4\">\n          <div class=\"card\">\n            <div class=\"card-head\"><span class=\"card-title\">机构共识强度</span><span class=\"tag tag-up\">强</span></div>\n            <div class=\"card-body\">\n              <div id=\"consensusGauge\" class=\"chart chart-sm\"></div>\n              <div class=\"col gap-2 mt-2\">\n                <div class=\"row-between\"><span class=\"fs-12 t-2\">看多占比</span><span class=\"mono fs-12 t-up\">85.7%</span></div>\n                <div class=\"row-between\"><span class=\"fs-12 t-2\">目标价上行空间</span><span class=\"mono fs-12 t-up\">+19.2%</span></div>\n                <div class=\"row-between\"><span class=\"fs-12 t-2\">近 3 月评级上调</span><span class=\"mono fs-12 t-up\">5 家</span></div>\n                <div class=\"row-between\"><span class=\"fs-12 t-2\">近 3 月评级下调</span><span class=\"mono fs-12 t-down\">1 家</span></div>\n                <div class=\"row-between\"><span class=\"fs-12 t-2\">覆盖机构数</span><span class=\"mono fs-12\">35 家</span></div>\n              </div>\n            </div>\n          </div>\n\n          <div class=\"card\">\n            <div class=\"card-head\"><span class=\"card-title\">最新评级变动</span></div>\n            <div class=\"card-body\">\n              <div class=\"timeline\">\n                <div class=\"tl-item is-up\"><span class=\"tl-dot\"></span>\n                  <div class=\"tl-head\"><span class=\"tl-time\">2026-09-18</span><span class=\"tag tag-up\">上调</span></div>\n                  <div class=\"tl-title\">中金公司：买入，目标价 300 → 320 元</div>\n                  <div class=\"tl-body\">理由：储能订单超预期，上调 2027 年盈利预测 6%</div>\n                </div>\n                <div class=\"tl-item is-up\"><span class=\"tl-dot\"></span>\n                  <div class=\"tl-head\"><span class=\"tl-time\">2026-09-18</span><span class=\"tag tag-up\">上调</span></div>\n                  <div class=\"tl-title\">华泰证券：买入，目标价 296 → 312 元</div>\n                  <div class=\"tl-body\">理由：毛利率连续三季回升，盈利质量改善</div>\n                </div>\n                <div class=\"tl-item is-up\"><span class=\"tl-dot\"></span>\n                  <div class=\"tl-head\"><span class=\"tl-time\">2026-09-18</span><span class=\"tag tag-up\">上调</span></div>\n                  <div class=\"tl-title\">中信证券：增持，目标价 288 → 305 元</div>\n                  <div class=\"tl-body\">理由：固态电池路线图明确技术路线，龙头受益</div>\n                </div>\n                <div class=\"tl-item is-down\"><span class=\"tl-dot\"></span>\n                  <div class=\"tl-head\"><span class=\"tl-time\">2026-08-16</span><span class=\"tag tag-down\">下调</span></div>\n                  <div class=\"tl-title\">某券商：增持 → 中性，目标价 286 → 252 元</div>\n                  <div class=\"tl-body\">理由：行业竞争加剧与海外政策不确定性</div>\n                </div>\n              </div>\n            </div>\n          </div>\n\n          <div class=\"card\">\n            <div class=\"card-head\"><span class=\"card-title\">与估值模块联动</span></div>\n            <div class=\"card-body col gap-2\">\n              <div class=\"row-between\"><span class=\"fs-12 t-2\">当前 PE（TTM）</span><span class=\"mono fs-12\">24.8</span></div>\n              <div class=\"row-between\"><span class=\"fs-12 t-2\">行业 PE 中位数</span><span class=\"mono fs-12\">26.8</span></div>\n              <div class=\"row-between\"><span class=\"fs-12 t-2\">相对行业</span><span class=\"mono fs-12 t-up\">折价 7.5%</span></div>\n              <div class=\"row-between\"><span class=\"fs-12 t-2\">PE 历史分位</span><span class=\"mono fs-12 t-up\">38%</span></div>\n              <a class=\"btn btn-outline btn-block mt-2\" href=\"stock.html#industry\">看行业估值对比 →</a>\n              <a class=\"btn btn-ghost btn-block\" href=\"stock.html#finance\">看财务与预测明细 →</a>\n            </div>\n          </div>\n\n        </aside>\n      </div>";

    var D = window.SA_DATA, C = window.SA_CHARTS, F = C.F, rt = D.ratings;
    var price = D.focusProfile.price;

    document.getElementById("ratingHistory").id = "ratingHistory";
    F.ratingHistory("#ratingHistory");

    /* 目标价散点 */
    var sorted = rt.targetPrices.slice().sort(function (a, b) { return a.date < b.date ? -1 : 1; });
    F.targetBand("#targetChart", {
      labels: sorted.map(function (t) { return t.date + " " + t.org; }),
      data: sorted,
      current: price
    });

    /* 一致预期 */
    F.combo("#consensusChart", {
      labels: rt.consensus.years, unit: "亿元", unit2: "元",
      bars: [
        { name: "营业收入", data: rt.consensus.revenue },
        { name: "净利润", data: rt.consensus.netProfit }
      ],
      lines: [{ name: "EPS（元）", data: rt.consensus.eps, axis: 2, color: C.cv("--chart-4") }]
    });

    document.getElementById("consensusBody").innerHTML = rt.consensus.years.map(function (y, i) {
      var growth = i === 0 ? "+26.2%" : "+" + (((rt.consensus.netProfit[i] - rt.consensus.netProfit[i - 1]) / rt.consensus.netProfit[i - 1]) * 100).toFixed(1) + "%";
      return "<tr>" +
        '<td class="fw-600">' + y + "</td>" +
        '<td class="num">' + rt.consensus.eps[i].toFixed(2) + "</td>" +
        '<td class="num">' + rt.consensus.revenue[i].toFixed(1) + "</td>" +
        '<td class="num t-up">' + rt.consensus.netProfit[i].toFixed(1) + "</td>" +
        '<td class="num t-up">' + growth + "</td>" +
        '<td class="num t-up">' + rt.consensus.pe[i].toFixed(1) + "</td>" +
        '<td class="num">' + rt.consensus.analystCount[i] + " 家</td>" +
        "</tr>";
    }).join("");

    /* 预测 PE 带 */
    F.combo("#peBand", {
      labels: ["PE(TTM)", "2026E", "2027E", "2028E"],
      unit: "倍",
      bars: [{ name: "PE", data: [24.8, 16.9, 13.8, 11.4] }],
      lines: [{ name: "行业 PE 中位数", data: [26.8, 26.8, 26.8, 26.8], axis: 2, color: C.cv("--chart-4") }],
      unit2: "倍"
    });

    /* 预测调整 */
    F.combo("#reviseChart", {
      labels: ["26-04", "26-05", "26-06", "26-07", "26-08", "26-09"],
      unit: "亿元",
      bars: [{ name: "2026E 一致预期净利", data: [712, 726, 738, 748, 762, 786] }],
      lines: [
        { name: "2027E 一致预期净利", data: [862, 878, 896, 912, 936, 962], color: C.cv("--chart-2") },
        { name: "月度上修幅度 %", data: [0.6, 1.9, 1.7, 1.4, 1.9, 3.1], axis: 2, color: C.cv("--chart-4") }
      ],
      unit2: "%"
    });

    /* 明细表 */
    function renderRatings() {
      var f = document.getElementById("fRating").value;
      var rows = rt.targetPrices.filter(function (t) { return f === "all" || t.rating === f; });
      document.getElementById("ratingBody").innerHTML = rows.map(function (t) {
        var badge = t.rating === "买入" ? "rb-buy" : t.rating === "增持" ? "rb-add" : t.rating === "中性" ? "rb-neutral" : "rb-reduce";
        var chg = t.target - t.prev;
        var chgTag = chg > 0 ? "tag-up" : chg < 0 ? "tag-down" : "tag";
        return "<tr>" +
          '<td class="fw-600">' + t.org + "</td>" +
          '<td class="t-2">' + t.analyst + "</td>" +
          '<td class="mono fs-11 t-3" data-v="' + t.date + '">' + t.date + "</td>" +
          '<td><span class="rating-badge ' + badge + '">' + t.rating + "</span></td>" +
          '<td class="num fw-600" data-v="' + t.target + '">' + t.target.toFixed(2) + "</td>" +
          '<td class="num t-3">' + t.prev.toFixed(2) + "</td>" +
          '<td class="num ' + (t.upside >= 0 ? "t-up" : "t-down") + '">' + (t.upside >= 0 ? "+" : "") + t.upside.toFixed(1) + "%</td>" +
          '<td><span class="tag ' + chgTag + '">' + (chg > 0 ? "↑ +" : chg < 0 ? "↓ " : "— ") + Math.abs(chg).toFixed(0) + "</span></td>" +
          '<td class="col-actions"><button class="btn btn-sm btn-ghost orgBtn" data-org="' + t.org + '">明细</button></td>' +
          "</tr>";
      }).join("");
      bindOrg();
    }

    function bindOrg() {
      SA.qsa(".orgBtn").forEach(function (b) {
        b.addEventListener("click", function () {
          var org = b.getAttribute("data-org");
          var t = rt.targetPrices.filter(function (x) { return x.org === org; })[0];
          document.getElementById("orgTitle").textContent = org + " · 预测明细";
          document.getElementById("orgBody").innerHTML =
            '<div class="row gap-2 wrap">' +
              '<span class="rating-badge ' + (t.rating === "买入" ? "rb-buy" : t.rating === "增持" ? "rb-add" : "rb-neutral") + '">' + t.rating + "</span>" +
              '<span class="tag tag-outline">' + t.date + "</span>" +
              '<span class="tag tag-accent">目标价 ' + t.target + " 元</span>" +
            "</div>" +
            '<div class="kpi"><span class="kpi-label">分析师</span><span class="kpi-value is-sm">' + t.analyst + "</span></div>" +
            '<div><div class="label">该机构预测（亿元 / 元）</div>' +
              '<table class="tbl is-comfort mt-2"><thead><tr><th>年度</th><th class="num">营收</th><th class="num">净利</th><th class="num">EPS</th></tr></thead><tbody>' +
                '<tr><td>2026E</td><td class="num">' + (5120 + Math.round(Math.random() * 60)) + '</td><td class="num">' + (786 + Math.round(Math.random() * 20 - 10)) + '</td><td class="num">' + (15.86 + (Math.random() * 0.6 - 0.3)).toFixed(2) + "</td></tr>" +
                '<tr><td>2027E</td><td class="num">' + (6186 + Math.round(Math.random() * 80)) + '</td><td class="num">' + (962 + Math.round(Math.random() * 30 - 15)) + '</td><td class="num">' + (19.42 + (Math.random() * 0.8 - 0.4)).toFixed(2) + "</td></tr>" +
                '<tr><td>2028E</td><td class="num">' + (7268 + Math.round(Math.random() * 100)) + '</td><td class="num">' + (1172 + Math.round(Math.random() * 40 - 20)) + '</td><td class="num">' + (23.64 + (Math.random() * 1.0 - 0.5)).toFixed(2) + "</td></tr>" +
              "</tbody></table></div>" +
            '<div class="legend-block"><b>说明</b>：上表为该机构公开预测的演示数据。' +
              "本页不展示研报正文，仅汇总评级、目标价与预测数值。</div>" +
            '<button class="btn btn-outline btn-block" onclick="SA.closeDrawer(\'dlgOrg\')">关闭</button>';
          SA.openDrawer("dlgOrg");
        });
      });
    }
    renderRatings();
    document.getElementById("fRating").addEventListener("change", renderRatings);

    F.gauge("#consensusGauge", { value: 86, max: 100, name: "机构共识强度", warnAt: 40, dangerAt: 65 });

    document.getElementById("btnExport").addEventListener("click", function () {
      SA.toast("已导出评级明细为 CSV（原型不产生文件）", "ok");
    });
    SA.qsa("#segRange > *").forEach(function (el) {
      el.addEventListener("click", function () {
        SA.qsa("#segRange > *").forEach(function (o) { o.classList.toggle("is-active", o === el); });
        SA.toast("已切换到" + el.textContent.trim() + "（原型中展示同一演示数据）", "info");
      });
    });

    SA.startLiveTicker("#qLive", { interval: 3000 });
  }

  window.SA_PANELS = window.SA_PANELS || {};
  window.SA_PANELS.rating = { key: "rating", title: "评级", render: render };
})();
