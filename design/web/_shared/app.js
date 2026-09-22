/* ============================================================
   SA · 股析 — 交互与外壳（原型用，零依赖）
   主题 / 涨跌色 / 导航（按角色裁剪）/ 搜索 / 表格排序 / Tab
   卡片折叠 / 拖拽排序 / 对话框 / 抽屉 / Toast / 实时闪烁
   ============================================================ */
(function () {
  "use strict";

  var D = window.SA_DATA;
  var LS = {
    theme: "sa.theme",
    updown: "sa.updown",
    role: "sa.role",
    navMini: "sa.navmini",
    cardOrder: "sa.cardorder.",
    density: "sa.density"
  };

  function get(k, dflt) {
    try {
      var v = localStorage.getItem(k);
      return v === null ? dflt : v;
    } catch (e) { return dflt; }
  }
  function set(k, v) {
    try { localStorage.setItem(k, v); } catch (e) { /* 忽略 */ }
  }
  function qs(s, r) { return (r || document).querySelector(s); }
  function qsa(s, r) { return Array.prototype.slice.call((r || document).querySelectorAll(s)); }

  /* ============================================================
     1. 主题与涨跌色
     ============================================================ */
  function applyTheme() {
    var theme = get(LS.theme, "light");
    var updown = get(LS.updown, "red-up");
    var root = document.documentElement;
    root.setAttribute("data-theme", theme);
    root.setAttribute("data-updown", updown);
    document.dispatchEvent(new CustomEvent("sa:themechange", { detail: { theme: theme, updown: updown } }));
  }

  function toggleTheme() {
    set(LS.theme, get(LS.theme, "light") === "dark" ? "light" : "dark");
    applyTheme();
    var el = qs("#btnTheme");
    if (el) el.textContent = get(LS.theme, "light") === "dark" ? "☾" : "☀";
    toast("已切换到" + (get(LS.theme, "light") === "dark" ? "深色" : "浅色") + "主题", "info");
  }

  function toggleUpdown() {
    set(LS.updown, get(LS.updown, "red-up") === "red-up" ? "green-up" : "red-up");
    applyTheme();
    toast("涨跌色已切换为「" + (get(LS.updown, "red-up") === "red-up" ? "红涨绿跌" : "绿涨红跌") + "」", "info");
  }

  /* ============================================================
     2. 角色与权限
     ============================================================ */
  function currentRole() {
    var id = get(LS.role, "admin");
    var found = null;
    D.roles.forEach(function (r) { if (r.id === id) found = r; });
    return found || D.roles[0];
  }
  function setRole(id) { set(LS.role, id); }
  function can(fp) { return currentRole().fps.indexOf(fp) >= 0; }

  /* ============================================================
     3. 导航定义
     ============================================================ */
  var NAV = [
    { group: "总览", items: [
      { key: "market", text: "市场概览", icon: "▦", href: "market.html", fp: "market.view" },
      { key: "search", text: "股票搜索", icon: "⌕", href: "search-results.html", fp: "stock.search" },
      { key: "watchlist", text: "自选股盯盘", icon: "★", href: "watchlist.html", fp: "watchlist.view", badge: "3" }
    ] },
    { group: "个股分析矩阵", items: [
      { key: "stock", text: "个股总览", icon: "◉", href: "stock.html", fp: "stock.trend" },
      { key: "stock-trend", text: "趋势与价格结构", icon: "◪", href: "stock-trend.html", fp: "stock.trend" },
      { key: "stock-finance", text: "盈利与财务表现", icon: "▤", href: "stock-finance.html", fp: "stock.finance" },
      { key: "stock-equity", text: "投资与股权结构", icon: "⛓", href: "stock-equity.html", fp: "stock.equity" },
      { key: "stock-capital", text: "资金面与筹码", icon: "◐", href: "stock-capital.html", fp: "stock.capital" },
      { key: "stock-industry", text: "行业与同业对比", icon: "◫", href: "stock-industry.html", fp: "stock.industry" },
      { key: "stock-events", text: "事件时间线与影响", icon: "◈", href: "stock-events.html", fp: "stock.events" },
      { key: "stock-risk", text: "风险与舆情监控", icon: "⚠", href: "stock-risk.html", fp: "stock.risk" },
      { key: "stock-rating", text: "机构评级与预测", icon: "◎", href: "stock-rating.html", fp: "stock.rating" }
    ] },
    { group: "拓扑图", items: [
      { key: "topology", text: "四种拓扑图总览", icon: "⁂", href: "topology.html", fp: "topology.view" }
    ] },
    { group: "选股与提醒", items: [
      { key: "screener", text: "条件选股器", icon: "⚙", href: "screener.html", fp: "screener.use" },
      { key: "alerts", text: "提醒规则", icon: "◔", href: "alerts.html", fp: "alert.manage" },
      { key: "notifications", text: "通知中心", icon: "◍", href: "notifications.html", fp: "notify.view", badge: "4" }
    ] },
    { group: "系统", items: [
      { key: "settings", text: "个人设置", icon: "⚒", href: "settings.html", fp: null },
      { key: "admin-users", text: "用户管理", icon: "☰", href: "admin-users.html", fp: "admin.users" },
      { key: "admin-permissions", text: "角色与权限", icon: "⛨", href: "admin-permissions.html", fp: "admin.permissions" },
      { key: "admin-datasource", text: "数据源监控", icon: "◱", href: "admin-datasource.html", fp: "admin.datasource" },
      { key: "admin-security", text: "登录与安全", icon: "⛭", href: "admin-security.html", fp: "admin.security" }
    ] }
  ];

  function renderNav() {
    var host = qs("#saNav");
    if (!host) return;
    var page = document.body.getAttribute("data-page") || "";
    var role = currentRole();
    var html = "";
    var hidden = 0;

    NAV.forEach(function (g) {
      var items = g.items.filter(function (it) {
        if (it.fp === null) return true;
        return role.fps.indexOf(it.fp) >= 0;
      });
      hidden += g.items.length - items.length;
      if (!items.length) return;
      html += '<div class="sa-navgroup">' + g.group + "</div>";
      items.forEach(function (it) {
        var active = it.key === page || it.href === page;
        html += '<a class="sa-navitem' + (active ? " is-active" : "") + '" href="' + it.href + '" title="' + it.text + '">' +
          '<span class="ni-icon">' + it.icon + "</span>" +
          '<span class="ni-text">' + it.text + "</span>" +
          (it.badge ? '<span class="ni-badge">' + it.badge + "</span>" : "") +
          "</a>";
      });
    });

    html += '<div class="sa-navgroup" style="margin-top:12px">当前角色</div>' +
      '<div class="sa-navitem is-locked" title="切换角色见 角色与权限 页">' +
      '<span class="ni-icon">◑</span><span class="ni-text">' + role.name + "</span></div>";

    if (hidden > 0) {
      html += '<div style="padding:8px 12px;font-size:11px;color:var(--text-3);line-height:1.7">' +
        "已按「" + role.name + "」角色隐藏 " + hidden + " 个无权限菜单</div>";
    }
    host.innerHTML = html;
  }

  /* ============================================================
     4. 顶栏
     ============================================================ */
  function renderTopbar() {
    var host = qs("#saTop");
    if (!host) return;
    var role = currentRole();
    var isAdmin = role.id === "admin";
    var themeIcon = get(LS.theme, "light") === "dark" ? "☾" : "☀";

    host.innerHTML =
      '<div class="sa-search">' +
        '<span class="sa-search-icon">⌕</span>' +
        '<input class="sa-search-input" id="saSearchInput" type="search" autocomplete="off" placeholder="搜索股票代码 / 名称 / 拼音首字母，如 300750、宁德时代、ndsd">' +
        '<span class="sa-search-kbd"><kbd>⌘</kbd><kbd>K</kbd></span>' +
        '<div class="sa-search-panel" id="saSearchPanel"></div>' +
      "</div>" +
      '<div class="sa-topactions">' +
        '<span class="tag tag-outline hide-mobile" title="数据截止时间">' + D.meta.asOf + "</span>" +
        '<span class="tag tag-ok tag-dot hide-mobile" title="自选股 3 秒推送">实时</span>' +
        '<button class="icon-btn" id="btnUpdown" title="切换涨跌色（默认红涨绿跌）">⇅</button>' +
        '<button class="icon-btn" id="btnTheme" title="切换深浅主题">' + themeIcon + "</button>" +
        '<a class="icon-btn" href="notifications.html" title="通知中心">◍<span class="dot-badge">4</span></a>' +
        '<a class="user-chip" href="' + (isAdmin ? "admin-users.html" : "settings.html") + '" title="当前登录用户与角色">' +
          '<span class="avatar">' + role.name.charAt(0) + "</span>" +
          '<span class="fs-12 hide-mobile">' + role.name + "</span>" +
        "</a>" +
      "</div>";

    bindSearch();
    qs("#btnTheme").addEventListener("click", toggleTheme);
    qs("#btnUpdown").addEventListener("click", toggleUpdown);
  }

  /* ============================================================
     5. 全局搜索
     ============================================================ */
  function bindSearch() {
    var input = qs("#saSearchInput");
    var panel = qs("#saSearchPanel");
    if (!input || !panel) return;

    function render(kw) {
      var k = (kw || "").trim().toLowerCase();
      var list = D.stocks.filter(function (s) {
        if (!k) return true;
        return s.code.indexOf(k) >= 0 || s.name.toLowerCase().indexOf(k) >= 0 || s.py.indexOf(k) >= 0;
      });
      var hot = ["300750", "600519", "002594", "688256", "300308"];
      if (!k) list = hot.map(function (c) { return D.stockByCode[c]; });

      var html = '<div class="sa-search-group">' + (k ? "匹配 " + list.length + " 只" : "热门搜索") + "</div>";
      if (!list.length) {
        html += '<div style="padding:12px;color:var(--text-3);font-size:12px">没有匹配的股票</div>';
      }
      list.slice(0, 8).forEach(function (s) {
        var cls = s.pct >= 0 ? "is-up" : "is-down";
        html += '<a class="sa-search-item" href="stock.html?code=' + s.code + '">' +
          '<span class="stock-cell" style="min-width:0"><span class="sc-name">' + s.name + "</span>" +
          '<span class="sc-code">' + s.code + " · " + s.board + "</span></span>" +
          '<span class="tag tag-outline" style="margin-left:auto">' + s.industry + "</span>" +
          '<span class="chg ' + cls + '" style="width:74px;text-align:right">' + s.price.toFixed(2) +
          " <span>" + (s.pct >= 0 ? "+" : "") + s.pct.toFixed(2) + "%</span></span>" +
          "</a>";
      });
      html += '<div class="menu-sep"></div>' +
        '<a class="sa-search-item" href="search-results.html"><span class="t-3 fs-12">查看全部搜索结果 →</span></a>';
      panel.innerHTML = html;
    }

    function open() { panel.classList.add("is-open"); render(input.value); }
    function close() { panel.classList.remove("is-open"); }

    input.addEventListener("focus", open);
    input.addEventListener("input", function () { render(input.value); open(); });
    input.addEventListener("keydown", function (e) {
      if (e.key === "Enter") {
        var first = qs(".sa-search-item", panel);
        if (first) window.location.href = first.getAttribute("href");
      }
      if (e.key === "Escape") close();
    });
    document.addEventListener("click", function (e) {
      if (!panel.contains(e.target) && e.target !== input) close();
    });
    document.addEventListener("keydown", function (e) {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === "k") {
        e.preventDefault(); input.focus();
      } else if (e.key === "/" && document.activeElement.tagName !== "INPUT") {
        e.preventDefault(); input.focus();
      }
    });
  }

  /* ============================================================
     6. 表格排序 / 筛选
     ============================================================ */
  function bindTables(root) {
    qsa(".tbl thead th.is-sortable", root).forEach(function (th) {
      th.addEventListener("click", function () {
        var table = th.closest("table");
        var idx = Array.prototype.indexOf.call(th.parentNode.children, th);
        var asc = th.getAttribute("data-sort") !== "asc";
        qsa("th", th.parentNode).forEach(function (o) { o.removeAttribute("data-sort"); o.classList.remove("is-sorted"); });
        th.setAttribute("data-sort", asc ? "asc" : "desc");
        th.classList.add("is-sorted");
        var caret = qs(".sort-caret", th);
        if (caret) caret.textContent = asc ? "▲" : "▼";

        var body = qs("tbody", table);
        var rows = Array.prototype.slice.call(body.children);
        rows.sort(function (a, b) {
          var av = cellVal(a.children[idx]);
          var bv = cellVal(b.children[idx]);
          if (av === bv) return 0;
          if (typeof av === "number" && typeof bv === "number") return asc ? av - bv : bv - av;
          return asc ? String(av).localeCompare(String(bv), "zh") : String(bv).localeCompare(String(av), "zh");
        });
        rows.forEach(function (r) { body.appendChild(r); });
      });
    });

    function cellVal(td) {
      if (!td) return "";
      var raw = td.getAttribute("data-v");
      if (raw !== null) { var n = parseFloat(raw); return isNaN(n) ? raw : n; }
      var t = (td.textContent || "").trim().replace(/[,%倍亿元万亿+\s]/g, "");
      var num = parseFloat(t);
      return isNaN(num) ? t : num;
    }
  }

  /* ============================================================
     7. Tab / 分段控件
     ============================================================ */
  function bindTabs(root) {
    qsa("[data-tabs]", root).forEach(function (bar) {
      var targetSel = bar.getAttribute("data-tabs");
      qsa("[data-tab]", bar).forEach(function (btn) {
        btn.addEventListener("click", function () {
          qsa("[data-tab]", bar).forEach(function (o) { o.classList.remove("is-active"); });
          btn.classList.add("is-active");
          var key = btn.getAttribute("data-tab");
          if (targetSel) {
            qsa(targetSel).forEach(function (p) {
              p.classList.toggle("is-active", p.getAttribute("data-panel") === key);
            });
          }
          document.dispatchEvent(new CustomEvent("sa:tabchange", { detail: { key: key, bar: bar } }));
        });
      });
    });
    /* 分段控件同步 .is-active */
    qsa("[data-seg]", root).forEach(function (seg) {
      qsa("button, a, span", seg).forEach(function (b) {
        b.addEventListener("click", function () {
          qsa("button, a, span", seg).forEach(function (o) { o.classList.remove("is-active"); });
          b.classList.add("is-active");
        });
      });
    });
  }

  /* ============================================================
     8. 卡片折叠 / 拖拽排序
     ============================================================ */
  function bindCards(root) {
    qsa(".card.is-collapsible", root).forEach(function (card) {
      var head = qs(".card-head", card);
      if (!head) return;
      head.addEventListener("click", function (e) {
        if (e.target.closest("button, a, input, select, .no-collapse")) return;
        card.classList.toggle("is-collapsed");
      });
    });

    /* 拖拽排序（同一容器内的 .card[data-order-key]） */
    var dragging = null;
    qsa(".card[draggable='true']", root).forEach(function (card) {
      card.addEventListener("dragstart", function (e) {
        dragging = card;
        card.classList.add("is-dragging");
        e.dataTransfer.effectAllowed = "move";
      });
      card.addEventListener("dragend", function () {
        card.classList.remove("is-dragging");
        qsa(".card.is-droptarget", root).forEach(function (c) { c.classList.remove("is-droptarget"); });
        saveOrder(card.parentNode);
        dragging = null;
      });
      card.addEventListener("dragover", function (e) {
        if (!dragging || dragging === card || dragging.parentNode !== card.parentNode) return;
        e.preventDefault();
        card.classList.add("is-droptarget");
      });
      card.addEventListener("dragleave", function () { card.classList.remove("is-droptarget"); });
      card.addEventListener("drop", function (e) {
        if (!dragging || dragging === card || dragging.parentNode !== card.parentNode) return;
        e.preventDefault();
        card.classList.remove("is-droptarget");
        var rect = card.getBoundingClientRect();
        var after = e.clientY > rect.top + rect.height / 2;
        card.parentNode.insertBefore(dragging, after ? card.nextSibling : card);
      });
    });
  }

  function orderKey(container) {
    var first = qs("[data-order-key]", container);
    return first ? LS.cardOrder + first.getAttribute("data-order-key") : null;
  }
  function saveOrder(container) {
    var k = orderKey(container);
    if (!k) return;
    var ids = qsa("[data-order-key]", container).map(function (c) { return c.getAttribute("data-order-key"); });
    set(k, ids.join(","));
  }
  function restoreOrder(container) {
    var k = orderKey(container);
    if (!k) return;
    var saved = get(k, "");
    if (!saved) return;
    var ids = saved.split(",");
    ids.forEach(function (id) {
      var el = qs('[data-order-key="' + id + '"]', container);
      if (el) container.appendChild(el);
    });
  }

  /* ============================================================
     9. 对话框 / 抽屉 / 菜单
     ============================================================ */
  function openDialog(id) { var d = qs("#" + id); if (d) d.classList.add("is-open"); }
  function closeDialog(id) { var d = qs("#" + id); if (d) d.classList.remove("is-open"); }
  function openDrawer(id) { var d = qs("#" + id); if (d) d.classList.add("is-open"); }
  function closeDrawer(id) { var d = qs("#" + id); if (d) d.classList.remove("is-open"); }

  function bindOverlays(root) {
    qsa("[data-open]", root).forEach(function (b) {
      b.addEventListener("click", function (e) {
        e.preventDefault();
        var t = b.getAttribute("data-open");
        if (qs("#" + t)) openDialog(t);
        if (qs("#" + t) && qs("#" + t).classList.contains("drawer")) openDrawer(t);
        if (qs("#" + t) && qs("#" + t).classList.contains("sheet")) openDrawer(t);
      });
    });
    qsa("[data-close]", root).forEach(function (b) {
      b.addEventListener("click", function (e) {
        e.preventDefault();
        var t = b.getAttribute("data-close");
        closeDialog(t); closeDrawer(t);
      });
    });
    qsa(".overlay", root).forEach(function (ov) {
      ov.addEventListener("click", function (e) { if (e.target === ov) ov.classList.remove("is-open"); });
    });

    /* 下拉菜单 */
    qsa("[data-menu]", root).forEach(function (btn) {
      btn.addEventListener("click", function (e) {
        e.stopPropagation();
        var m = qs("#" + btn.getAttribute("data-menu"));
        if (!m) return;
        var open = m.classList.contains("is-open");
        qsa(".menu.is-open").forEach(function (o) { o.classList.remove("is-open"); });
        if (!open) {
          m.classList.add("is-open");
          var r = btn.getBoundingClientRect();
          m.style.top = r.bottom + 6 + "px";
          m.style.left = Math.max(8, Math.min(r.left, window.innerWidth - m.offsetWidth - 8)) + "px";
        }
      });
    });
    document.addEventListener("click", function () {
      qsa(".menu.is-open").forEach(function (o) { o.classList.remove("is-open"); });
    });
  }

  /* ============================================================
     10. Toast
     ============================================================ */
  function toast(msg, kind) {
    var wrap = qs(".toast-wrap");
    if (!wrap) {
      wrap = document.createElement("div");
      wrap.className = "toast-wrap";
      /* 移动端原型放进手机屏幕内，否则 fixed 定位会落在浏览器窗口上 */
      (qs(".phone-screen") || document.body).appendChild(wrap);
    }
    var el = document.createElement("div");
    el.className = "toast is-" + (kind || "info");
    el.innerHTML = msg;
    wrap.appendChild(el);
    setTimeout(function () {
      el.style.transition = "opacity 200ms";
      el.style.opacity = "0";
      setTimeout(function () { el.remove(); }, 220);
    }, 2600);
  }

  /* ============================================================
     11. 实时价格闪烁模拟
     ============================================================ */
  function startLiveTicker(selector, opts) {
    opts = opts || {};
    var interval = opts.interval || 3000;
    var els = qsa(selector);
    if (!els.length) return null;
    return setInterval(function () {
      els.forEach(function (el) {
        var base = parseFloat(el.getAttribute("data-price") || "0");
        if (!base) return;
        var drift = (Math.random() - 0.48) * base * 0.0016;
        var next = base + drift;
        el.setAttribute("data-price", next.toFixed(2));
        var pctEl = el.parentNode ? qs(".live-pct", el.parentNode) : null;
        var pctBase = pctEl ? parseFloat(pctEl.getAttribute("data-pct") || "0") : 0;
        var nextPct = pctBase + drift / base * 100;
        el.textContent = next.toFixed(2);
        el.classList.remove("flash-up", "flash-down");
        void el.offsetWidth;
        el.classList.add(drift >= 0 ? "flash-up" : "flash-down");
        if (pctEl) {
          pctEl.setAttribute("data-pct", nextPct.toFixed(2));
          pctEl.textContent = (nextPct >= 0 ? "+" : "") + nextPct.toFixed(2) + "%";
          pctEl.className = "live-pct chg " + (nextPct >= 0 ? "is-up" : "is-down");
        }
      });
    }, interval);
  }

  /* ============================================================
     12. 格式化
     ============================================================ */
  function fmt(n, digits) {
    if (n === null || n === undefined || isNaN(n)) return "—";
    return Number(n).toFixed(digits === undefined ? 2 : digits);
  }
  function fmtPct(n, digits) {
    if (n === null || n === undefined || isNaN(n)) return "—";
    return (n >= 0 ? "+" : "") + Number(n).toFixed(digits === undefined ? 2 : digits) + "%";
  }
  function fmtYi(n) {
    if (n === null || n === undefined || isNaN(n)) return "—";
    return Number(n).toFixed(2) + " 亿";
  }
  function chgClass(n) { return n > 0 ? "is-up" : n < 0 ? "is-down" : "is-flat"; }
  function chgText(n, digits) { return (n > 0 ? "+" : "") + Number(n).toFixed(digits === undefined ? 2 : digits); }

  /* ============================================================
     13. 个股页公共片段（模块页复用）
     ============================================================ */
  var MODULES = [
    { key: "stock", text: "矩阵总览", href: "stock.html" },
    { key: "stock-trend", text: "趋势与价格结构", href: "stock-trend.html" },
    { key: "stock-finance", text: "盈利与财务表现", href: "stock-finance.html" },
    { key: "stock-equity", text: "投资与股权结构", href: "stock-equity.html" },
    { key: "stock-capital", text: "资金面与筹码", href: "stock-capital.html" },
    { key: "stock-industry", text: "行业与同业对比", href: "stock-industry.html" },
    { key: "stock-events", text: "事件时间线与影响", href: "stock-events.html" },
    { key: "stock-risk", text: "风险与舆情监控", href: "stock-risk.html" },
    { key: "stock-rating", text: "机构评级与预测", href: "stock-rating.html" }
  ];

  /* 模块专注模式标签栏 */
  function moduleTabs(active) {
    return '<div class="tabbar is-pill">' + MODULES.map(function (m) {
      return '<a class="tab' + (m.key === active ? " is-active" : "") + '" href="' + m.href + '">' + m.text + "</a>";
    }).join("") + "</div>";
  }

  /* 个股紧凑行情条 */
  function quoteStrip() {
    var p = D.focusProfile;
    var cls = p.pct >= 0 ? "is-up" : "is-down";
    return '<div class="card is-accent"><div class="card-body is-tight row gap-5 wrap">' +
      '<div class="row gap-2 wrap">' +
        '<span class="fs-16 fw-700">' + p.name + "</span>" +
        '<span class="mono fs-11 t-3">' + p.code + "</span>" +
        '<span class="tag tag-outline">' + p.board + "</span>" +
        '<span class="tag tag-brand">' + p.industry + "</span>" +
      "</div>" +
      '<div class="row gap-3">' +
        '<span class="mono fs-22 fw-700 ' + (p.pct >= 0 ? "t-up" : "t-down") + '" id="qLive" data-price="' + p.price + '">' + p.price.toFixed(2) + "</span>" +
        '<span class="live-pct chg ' + cls + '" data-pct="' + p.pct + '">' + (p.pct >= 0 ? "+" : "") + p.chg.toFixed(2) + " " + (p.pct >= 0 ? "+" : "") + p.pct.toFixed(2) + "%</span>" +
      "</div>" +
      '<div class="row gap-4 wrap fs-11 t-3">' +
        "<span>今开 <b class=\"mono t-1\">" + p.open.toFixed(2) + "</b></span>" +
        "<span>最高 <b class=\"mono t-up\">" + p.high.toFixed(2) + "</b></span>" +
        "<span>最低 <b class=\"mono t-down\">" + p.low.toFixed(2) + "</b></span>" +
        "<span>成交额 <b class=\"mono t-1\">" + p.amount + " 亿</b></span>" +
        "<span>换手 <b class=\"mono t-1\">" + p.turnover + "%</b></span>" +
        "<span>量比 <b class=\"mono t-1\">" + p.volRatio + "</b></span>" +
        "<span>市值 <b class=\"mono t-1\">" + p.cap.toLocaleString() + " 亿</b></span>" +
        "<span>PE(TTM) <b class=\"mono t-1\">" + p.peTtm + "</b></span>" +
      "</div>" +
      '<div class="row gap-2" style="margin-left:auto">' +
        '<span class="hint">' + D.meta.asOf + "</span>" +
        '<span class="row gap-2"><span class="dot dot-live"></span><span class="hint">实时推送</span></span>' +
      "</div>" +
    "</div></div>";
  }

  /* ============================================================
     14. 页面初始化
     ============================================================ */
  function init() {
    applyTheme();
    renderNav();
    renderTopbar();
    bindTables();
    bindTabs();
    bindCards();
    bindOverlays();

    /* 个股页公共片段 */
    var mt = qs("#modTabs");
    if (mt) mt.innerHTML = moduleTabs(document.body.getAttribute("data-page"));
    var qsEl = qs("#quoteStrip");
    if (qsEl) qsEl.innerHTML = quoteStrip();

    qsa(".card-grid").forEach(restoreOrder);

    /* 任意元素加 data-href 即可整行跳转（避免内联 onclick 的引号问题） */
    document.addEventListener("click", function (e) {
      var hit = e.target.closest ? e.target.closest("[data-href]") : null;
      if (!hit) return;
      if (e.target.closest("a, button, input, select")) return;
      window.location.href = hit.getAttribute("data-href");
    });

    /* 演示数据角标 */
    if (!qs(".demo-badge") && !qs("#appScreen")) {
      var b = document.createElement("div");
      b.className = "demo-badge";
      b.innerHTML = "<b>演示数据</b> · 非实时 · 不构成投资建议";
      document.body.appendChild(b);
    }

    document.dispatchEvent(new CustomEvent("sa:ready"));
  }

  /* ============================================================
     14. 导出
     ============================================================ */
  window.SA = {
    get: get, set: set, qs: qs, qsa: qsa,
    applyTheme: applyTheme, toggleTheme: toggleTheme, toggleUpdown: toggleUpdown,
    currentRole: currentRole, setRole: setRole, can: can, renderNav: renderNav,
    openDialog: openDialog, closeDialog: closeDialog,
    openDrawer: openDrawer, closeDrawer: closeDrawer,
    toast: toast, startLiveTicker: startLiveTicker,
    fmt: fmt, fmtPct: fmtPct, fmtYi: fmtYi, chgClass: chgClass, chgText: chgText,
    LS: LS
  };

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", init);
  } else {
    init();
  }
})();
