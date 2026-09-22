/* ============================================================
   SA · 股析 — 个股区控制器（原型用，零依赖）
   职责：
     1. 渲染个股区骨架（个股条 + 9 个 Tab + 面板容器）
     2. 面板注册表（panels/<key>.js 各自注册到 window.SA_PANELS）
     3. 懒渲染：只渲染当前 Tab —— ECharts 在隐藏容器里初始化会得到 0×0 画布
     4. URL 同步：?code=xxx#tab，浏览器前进后退可用
   大盘概况页、自选股页、个股页共用这一份实现，不存在两套。
   ============================================================ */
(function () {
  "use strict";

  var D = window.SA_DATA;

  /* 9 个 Tab：key 与 _shared/panels/<key>.js 的 key 一一对应 */
  var TABS = [
    { key: "overview", text: "概览" },
    { key: "trend", text: "趋势" },
    { key: "finance", text: "财务" },
    { key: "equity", text: "股权" },
    { key: "capital", text: "资金" },
    { key: "industry", text: "行业" },
    { key: "events", text: "事件" },
    { key: "risk", text: "风险" },
    { key: "rating", text: "评级" }
  ];

  window.SA_PANELS = window.SA_PANELS || {};
  window.SA_PANE_TABS = TABS;

  function qs(s, r) { return (r || document).querySelector(s); }

  function stockOf(code) {
    if (D.allStockByCode && D.allStockByCode[code]) return D.allStockByCode[code];
    if (D.stockByCode && D.stockByCode[code]) return D.stockByCode[code];
    return null;
  }

  function fmtPct(n) {
    return (n >= 0 ? "+" : "") + Number(n).toFixed(2) + "%";
  }

  /* 个股条：身份 + 现价涨跌 + 三项关键指标（不放 8 个指标，避免「没有重点」） */
  function headHtml(s) {
    if (!s) return '<div class="stock-pane-head"><span class="t-3 fs-12">未找到该股票</span></div>';
    var cls = s.pct >= 0 ? "is-up" : "is-down";
    var amount = (s.cap * s.turnover / 100);
    return '<div class="stock-pane-head">' +
      '<button class="stock-back" data-pane-back="1">← 返回列表</button>' +
      '<div class="row gap-2 wrap" style="min-width:0">' +
        '<span class="fs-16 fw-700">' + s.name + "</span>" +
        '<span class="mono fs-11 t-3">' + s.code + "</span>" +
        '<span class="tag tag-outline">' + s.board + "</span>" +
        '<span class="tag tag-brand">' + s.industry + "</span>" +
      "</div>" +
      '<div class="row gap-3" style="margin-left:auto">' +
        '<span class="mono fs-20 fw-700 ' + (s.pct >= 0 ? "t-up" : "t-down") + '" id="qLive" data-price="' + s.price + '">' + s.price.toFixed(2) + "</span>" +
        '<span class="live-pct chg ' + cls + '" data-pct="' + s.pct + '">' + fmtPct(s.pct) + "</span>" +
      "</div>" +
      '<div class="row gap-4 fs-11 t-3">' +
        '<span>成交 <b class="mono t-1">' + amount.toFixed(1) + " 亿</b></span>" +
        '<span>换手 <b class="mono t-1">' + s.turnover.toFixed(2) + "%</b></span>" +
        '<span>PE <b class="mono t-1">' + (s.pe > 0 ? s.pe.toFixed(1) : "—") + "</b></span>" +
      "</div>" +
    "</div>";
  }

  function tabbarHtml(active) {
    return '<div class="tabbar is-pill" data-tabs="#stockPaneBody" data-hash="true">' +
      TABS.map(function (t) {
        return '<a class="tab' + (t.key === active ? " is-active" : "") + '" href="#' + t.key +
          '" data-tab="' + t.key + '">' + t.text + "</a>";
      }).join("") +
    "</div>";
  }

  /* 骨架：个股条 + Tab 条 + 面板容器（面板一次只渲染一个） */
  function skeletonHtml() {
    return '<div id="stockPaneHead"></div>' +
      '<div class="stock-tabs">' + tabbarHtml(null) + "</div>" +
      '<div id="stockPaneBody">' +
        TABS.map(function (t) {
          return '<div data-panel="' + t.key + '"></div>';
        }).join("") +
      "</div>";
  }

  var state = { mounted: null, listEl: null, code: null, rendered: {} };

  /* 把个股区挂进页面；listSel 是列表容器，打开个股时隐藏它 */
  function mount(hostSel, listSel) {
    var host = qs(hostSel);
    if (!host) return null;
    if (state.mounted !== host) {
      host.innerHTML = skeletonHtml();
      state.mounted = host;
      state.rendered = {};
      /* 复用 app.js 的 Tab 绑定（含 hash 双向同步与 sa:tabshow 事件） */
      if (window.SA && window.SA.bindTabs) window.SA.bindTabs(host);
      var back = qs("[data-pane-back]", host);
      if (back) back.addEventListener("click", function () { close(); });
      /* 懒渲染：切到哪个 Tab 才渲染哪个 */
      document.addEventListener("sa:tabshow", function (e) {
        if (e.detail && e.detail.bar && host.contains(e.detail.bar)) showTab(e.detail.key);
      });
    }
    state.listEl = listSel ? qs(listSel) : null;
    return host;
  }

  /* 渲染某个面板：已渲染过则保留 DOM（切回不重画，只触发 resize） */
  function showTab(key) {
    var body = qs("#stockPaneBody");
    if (!body) return;
    var host = qs('[data-panel="' + key + '"]', body);
    if (!host) return;

    if (!state.rendered[key]) {
      var panel = window.SA_PANELS[key];
      if (panel && typeof panel.render === "function") {
        panel.render(host, state.code);
      } else {
        host.innerHTML = '<div class="card"><div class="card-body fs-12 t-3">该面板暂不可用</div></div>';
      }
      state.rendered[key] = true;
    } else {
      /* 已渲染过：通知面板做一次 resize（图表在隐藏时尺寸为 0） */
      document.dispatchEvent(new CustomEvent("sa:panelresize", { detail: { key: key } }));
    }
  }

  function writeUrl(code, tab) {
    var next = window.location.pathname + (code ? "?code=" + code : "") + (tab ? "#" + tab : "");
    try { history.replaceState(null, "", next); } catch (e) { /* file:// 下忽略 */ }
  }

  /* 打开某只股票：隐藏列表、显示个股区 */
  function open(code, tab) {
    var s = stockOf(code);
    if (!s) { if (window.SA) window.SA.toast("未找到该股票：" + code, "info"); return; }

    state.code = code;
    state.rendered = {};
    var head = qs("#stockPaneHead");
    if (head) head.innerHTML = headHtml(s);

    /* 重建 Tab 条（重置为传入的 Tab），并重新绑定 */
    var tabsHost = qs(".stock-tabs");
    if (tabsHost) {
      tabsHost.innerHTML = tabbarHtml(tab || TABS[0].key);
      if (window.SA && window.SA.bindTabs && state.mounted) window.SA.bindTabs(state.mounted);
    }

    if (state.listEl) state.listEl.classList.add("text-none");
    if (state.mounted) state.mounted.classList.add("is-active");

    showTab(tab || TABS[0].key);
    writeUrl(code, tab || TABS[0].key);
  }

  /* 返回列表 */
  function close() {
    if (state.listEl) state.listEl.classList.remove("text-none");
    if (state.mounted) state.mounted.classList.remove("is-active");
    state.code = null;
    state.rendered = {};
    writeUrl(null, null);
  }

  /* 从 URL 还原：?code=xxx#tab */
  function restoreFromUrl() {
    var m = /[?&]code=([^&#]+)/.exec(window.location.search || "");
    if (!m) return false;
    var code = decodeURIComponent(m[1]);
    var tab = (window.location.hash || "").replace("#", "") || TABS[0].key;
    open(code, tab);
    return true;
  }

  window.SA_PANE = {
    mount: mount,
    open: open,
    close: close,
    headHtml: headHtml,
    TABS: TABS,
    restoreFromUrl: restoreFromUrl
  };
})();
