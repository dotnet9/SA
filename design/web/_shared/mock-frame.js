/* ============================================================
   SA · 股析 — 移动端原型外壳（手机外框 + 底部 Tab 栏）
   design/app/*.html 引用本文件，自动把页面内容包进手机外框，
   并在桌面浏览器里提供页面跳转、主题切换等预览工具。
   ============================================================ */
(function () {
  "use strict";

  /* 页面清单（供预览工具的下拉跳转使用）。
     8 个个股模块已并入 stock.html 的 Tab，不再单列。 */
  var PAGES = [
    { key: "index", text: "登录", href: "index.html" },
    { key: "home", text: "大盘概况", href: "home.html" },
    { key: "search", text: "搜索", href: "search.html" },
    { key: "stock", text: "个股（9 个 Tab）", href: "stock.html" },
    { key: "watchlist", text: "自选股", href: "watchlist.html" },
    { key: "more", text: "更多", href: "more.html" },
    { key: "screener", text: "条件选股器", href: "screener.html" },
    { key: "notifications", text: "通知中心", href: "notifications.html" },
    { key: "alerts", text: "提醒规则", href: "alerts.html" },
    { key: "notify-preview", text: "提醒形态预览", href: "notify-preview.html" },
    { key: "settings", text: "我的", href: "settings.html" },
    { key: "styleguide-mobile", text: "移动端规范", href: "styleguide-mobile.html" }
  ];

  /* 底部 Tab：3 项。与桌面端一致（大盘、自选为主，其余收进「更多」）。 */
  var TABS = [
    { key: "home", text: "大盘", icon: "▦", href: "home.html" },
    { key: "watchlist", text: "自选", icon: "★", href: "watchlist.html" },
    { key: "more", text: "更多", icon: "☰", href: "more.html" }
  ];

  function qs(s, r) { return (r || document).querySelector(s); }

  function clock() {
    var d = new Date();
    return String(d.getHours()).padStart(2, "0") + ":" + String(d.getMinutes()).padStart(2, "0");
  }

  /* 需要搬进手机屏幕的浮层类元素（否则会以 fixed 定位落在浏览器窗口上） */
  var OVERLAY_SEL = ".sheet, .overlay, .drawer, .menu, .toast-wrap, .demo-badge";

  function build() {
    var screen = qs("#appScreen");
    if (!screen) return;
    var pageKey = document.body.getAttribute("data-page") || "";
    var title = document.body.getAttribute("data-title") || "";
    var desc = document.body.getAttribute("data-desc") || "";
    var actions = qs("#appActions");
    var noTabs = document.body.hasAttribute("data-no-tabs");

    /* 先把页面节点取出来（保持引用，后面整体搬迁） */
    var content = Array.prototype.slice.call(screen.childNodes);

    /* 组装手机外框 */
    var stage = document.createElement("div");
    stage.className = "phone-stage";

    var phone = document.createElement("div");
    phone.className = "phone";
    phone.innerHTML =
      '<div class="phone-notch"></div>' +
      '<div class="phone-screen">' +
        '<div class="phone-status"><span>' + clock() + "</span>" +
        '<span style="letter-spacing:2px">▮▮▮ ᯤ ▰</span></div>' +
        '<div class="phone-body" id="phoneBody"></div>' +
        (actions ? '<div id="phoneActions"></div>' : "") +
        (noTabs ? "" : '<div class="app-tabbar" id="phoneTabs"></div>') +
      "</div>";

    var caption = document.createElement("div");
    caption.className = "phone-caption";
    caption.innerHTML = "<h3>" + title + "</h3><p>" + desc + "</p>" +
      '<div style="margin-top:10px;font-size:11px;color:var(--text-3)">390 × 844 · iOS/Android 通用竖屏</div>';

    var wrap = document.createElement("div");
    wrap.style.cssText = "display:flex;flex-direction:column;align-items:center";
    wrap.appendChild(phone);
    wrap.appendChild(caption);
    stage.appendChild(wrap);
    stage.appendChild(buildTools(pageKey));

    document.body.appendChild(stage);

    /* 搬迁真实节点而非复制 innerHTML：
       复制会丢掉已绑定的事件监听与 ECharts 实例（画布会变成空白） */
    var body = qs("#phoneBody");
    content.forEach(function (n) { body.appendChild(n); });
    screen.remove();

    if (actions) {
      qs("#phoneActions").appendChild(actions);
      actions.style.display = "flex";
    }
    if (!noTabs) {
      qs("#phoneTabs").innerHTML = TABS.map(function (t) {
        /* 个股页与「更多」下的子页都把「更多」标为当前 */
        var active = t.key === pageKey ||
          (t.key === "more" && ["stock", "screener", "notifications", "alerts", "settings", "more"].indexOf(pageKey) >= 0);
        return '<a class="app-tab' + (active ? " is-active" : "") + '" href="' + t.href + '">' +
          '<span class="at-icon">' + t.icon + "</span>" + t.text +
          (t.badge ? '<span class="at-badge">' + t.badge + "</span>" : "") + "</a>";
      }).join("");
    }

    /* 把页面自带的浮层与 Toast 容器搬进手机屏幕，使其在框内定位 */
    var phoneScreen = qs(".phone-screen");
    Array.prototype.slice.call(document.body.querySelectorAll(OVERLAY_SEL)).forEach(function (el) {
      if (!phoneScreen.contains(el)) phoneScreen.appendChild(el);
    });

    /* 状态栏时间每分钟刷新 */
    setInterval(function () {
      var el = qs(".phone-status span");
      if (el) el.textContent = clock();
    }, 30000);
  }

  function buildTools(pageKey) {
    var tools = document.createElement("div");
    tools.style.cssText = "width:300px;display:flex;flex-direction:column;gap:12px";
    tools.innerHTML =
      '<div class="card is-accent">' +
        '<div class="card-head"><span class="card-title">移动端原型预览</span></div>' +
        '<div class="card-body col gap-3">' +
          '<div class="hint">' +
            "与 Web 端共用同一套令牌、组件、数据与图表封装。" +
          "</div>" +
          '<div class="field"><label class="label">跳转到页面</label>' +
            '<select class="select" id="pageJump">' +
              PAGES.map(function (p) {
                return '<option value="' + p.href + '"' + (p.key === pageKey ? " selected" : "") + ">" + p.text + "</option>";
              }).join("") +
            "</select></div>" +
          '<div class="row gap-2">' +
            '<button class="btn btn-outline grow" id="tTheme">切换主题</button>' +
            '<button class="btn btn-outline grow" id="tUpdown">切换涨跌色</button>' +
          "</div>" +
          '<a class="btn btn-ghost" href="../web/index.html">← 打开 Web 端原型</a>' +
        "</div>" +
      "</div>" +
      '<div class="concept-note">' +
        "<b>说明</b>：桌面小组件（手机主屏 Widget）iOS 上纯 PWA 无法实现，" +
        "已改为通知栏提醒 + 震动，见「提醒形态预览」页。" +
      "</div>";

    setTimeout(function () {
      var jump = qs("#pageJump", tools);
      if (jump) jump.addEventListener("change", function () { window.location.href = jump.value; });
      var t = qs("#tTheme", tools);
      if (t) t.addEventListener("click", function () { window.SA.toggleTheme(); });
      var u = qs("#tUpdown", tools);
      if (u) u.addEventListener("click", function () { window.SA.toggleUpdown(); });
    }, 0);

    return tools;
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", build);
  } else {
    build();
  }
})();
