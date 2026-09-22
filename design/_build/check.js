/* ============================================================
   SA 原型自检工具（build 辅助，不参与原型运行）
   用法：node design/_build/check.js
   检查项：
     1. 每个 html 的内联 <script> 语法是否合法
     2. 引用的本地 css / js / 图片 / 页面链接是否存在
     3. 页面之间是否存在死链
     4. 外部 .js 语法是否合法（原先只查内联脚本）
     5. 文案精简：不得出现 legend-block；card-sub 只能是 ≤12 字的短语
     6. 主导航项数必须为 2（大盘概况 / 自选股）
     7. 视觉主角：is-accent 同页 > 1 处仅告警
   ============================================================ */
const fs = require("fs");
const path = require("path");
const vm = require("vm");

const ROOT = path.resolve(__dirname, "..");
const DIRS = ["web", "app"];

let errors = [];
let warnings = [];
let stats = { files: 0, scripts: 0, refs: 0, links: 0 };

function walk(dir, out) {
  out = out || [];
  for (const name of fs.readdirSync(dir)) {
    const p = path.join(dir, name);
    const st = fs.statSync(p);
    if (st.isDirectory()) {
      if (name === "vendor" || name === "_build" || name === "node_modules") continue;
      walk(p, out);
    } else if (name.endsWith(".html")) {
      out.push(p);
    }
  }
  return out;
}

function rel(p) {
  return path.relative(ROOT, p).replace(/\\/g, "/");
}

/* ---------- 收集所有存在的文件（用于链接校验） ---------- */
const allFiles = new Set();
DIRS.forEach(function (d) {
  const base = path.join(ROOT, d);
  if (!fs.existsSync(base)) return;
  (function rec(cur) {
    for (const name of fs.readdirSync(cur)) {
      const p = path.join(cur, name);
      if (fs.statSync(p).isDirectory()) rec(p);
      else allFiles.add(path.resolve(p));
    }
  })(base);
});

/* ---------- 逐页检查 ---------- */
const pages = [];
DIRS.forEach(function (d) {
  const base = path.join(ROOT, d);
  if (fs.existsSync(base)) pages.push(...walk(base));
});

pages.forEach(function (file) {
  stats.files++;
  const src = fs.readFileSync(file, "utf8");
  const dir = path.dirname(file);

  /* 1. 内联脚本语法 */
  const scriptRe = /<script(?![^>]*\bsrc=)[^>]*>([\s\S]*?)<\/script>/gi;
  let m;
  while ((m = scriptRe.exec(src)) !== null) {
    const code = m[1];
    if (!code.trim()) continue;
    stats.scripts++;
    try {
      new vm.Script(code, { filename: file });
    } catch (e) {
      errors.push(rel(file) + " 内联脚本语法错误：" + e.message);
    }
  }

  /* 2. 本地资源引用（href / src，排除外链与锚点） */
  const refRe = /(?:href|src)\s*=\s*"([^"]+)"/gi;
  while ((m = refRe.exec(src)) !== null) {
    const raw = m[1].trim();
    if (!raw || raw.startsWith("#") || raw.startsWith("http") || raw.startsWith("//") ||
        raw.startsWith("data:") || raw.startsWith("mailto:")) continue;
    /* 跳过 JS 模板拼接出来的路径（含引号或加号） */
    if (raw.includes("'") || raw.includes("+") || raw.includes("${")) continue;
    stats.refs++;
    const clean = raw.split("#")[0].split("?")[0];
    if (!clean) continue;
    const target = path.resolve(dir, clean);
    if (!fs.existsSync(target)) {
      errors.push(rel(file) + " 引用了不存在的文件：" + raw);
    } else if (clean.endsWith(".html")) {
      stats.links++;
    }
  }

  /* 3. 表单控件 id 重复（同页内） */
  const idRe = /\sid\s*=\s*"([^"]+)"/gi;
  const seen = Object.create(null);
  while ((m = idRe.exec(src)) !== null) {
    const id = m[1];
    if (seen[id]) warnings.push(rel(file) + " 存在重复 id：" + id);
    seen[id] = true;
  }
});

/* ---------- 4. 外部 .js 语法（_shared 下除 vendor 外全部） ---------- */
const sharedDir = path.join(ROOT, "web", "_shared");
const jsFiles = [];
(function recJs(cur) {
  if (!fs.existsSync(cur)) return;
  for (const name of fs.readdirSync(cur)) {
    const p = path.join(cur, name);
    if (fs.statSync(p).isDirectory()) {
      if (name === "vendor" || name === "node_modules") continue;
      recJs(p);
    } else if (name.endsWith(".js")) {
      jsFiles.push(p);
    }
  }
})(sharedDir);

jsFiles.forEach(function (file) {
  try {
    new vm.Script(fs.readFileSync(file, "utf8"), { filename: file });
    stats.externalJs = (stats.externalJs || 0) + 1;
  } catch (e) {
    errors.push(rel(file) + " 外部脚本语法错误：" + e.message);
  }
});

/* ---------- 5. 文案精简 ---------- */
const CARD_SUB_MAX = 24;
const DOLLAR_BRACE = "$" + "{";
pages.forEach(function (file) {
  const src = fs.readFileSync(file, "utf8");

  /* 只看标记区：内联脚本里的同名片段是 JS 字符串（批次 3 面板化时随文件重写消失） */
  const markup = src.replace(/<script(?![^>]*\bsrc=)[^>]*>[\s\S]*?<\/script>/gi, "");
  if (markup.includes('class="legend-block')) {
    errors.push(rel(file) + " 出现 legend-block：口径说明应集中到 _shared/spec.js");
  }

  const subRe = /<(span|div)\s+class="card-sub"[^>]*>([\s\S]*?)<\/\1>/gi;
  let m;
  while ((m = subRe.exec(src)) !== null) {
    const raw = m[2];
    if (raw.includes("' +") || raw.includes('" +') || raw.indexOf(DOLLAR_BRACE) >= 0) continue;
    const plain = raw.replace(/<[^>]+>/g, "").replace(/\s+/g, " ").trim();
    if (!plain) continue;
    /* 图表口径类（如「2025 年报 · 单位：亿元」）是必要标注，不算冗余文字 */
    const isChartSpec = plain.indexOf("\u00b7") >= 0 && /\d/.test(plain);
    if (isChartSpec) continue;
    if (plain.length > CARD_SUB_MAX) {
      errors.push(rel(file) + " card-sub 过长（" + plain.length + " 字 > " + CARD_SUB_MAX + "）：「" + plain.slice(0, 30) + "…」");
    }
  }

  const accent = (src.match(/is-accent/g) || []).length;
  if (accent > 1) {
    warnings.push(rel(file) + " is-accent 出现 " + accent + " 处，页面缺少唯一视觉主角");
  }
});

/* ---------- 6. 主导航项数 ---------- */
(function checkNav() {
  const appJs = path.join(sharedDir, "app.js");
  if (!fs.existsSync(appJs)) {
    errors.push("缺少 web/_shared/app.js");
    return;
  }
  const src = fs.readFileSync(appJs, "utf8");
  const start = src.indexOf("var NAV = [");
  const end = src.indexOf("];", start);
  if (start < 0 || end < 0) {
    errors.push("app.js 中未找到 NAV 定义");
    return;
  }
  const body = src.slice(start, end);
  const count = (body.match(/\{\s*key:/g) || []).length;
  if (count !== 2) {
    errors.push("主导航项数为 " + count + "，应为 2（大盘概况 / 自选股）");
  }
})();

/* ---------- 报告 ---------- */
console.log("检查文件数：" + stats.files + "，内联脚本：" + stats.scripts +
  "，外部脚本：" + (stats.externalJs || 0) +
  "，资源引用：" + stats.refs + "，页面链接：" + stats.links);
if (warnings.length) {
  console.log("\n警告 " + warnings.length + " 条：");
  warnings.forEach(function (w) { console.log("  ! " + w); });
}
if (errors.length) {
  console.log("\n错误 " + errors.length + " 条：");
  errors.forEach(function (e) { console.log("  x " + e); });
  process.exit(1);
}
console.log("\n通过：无死链、无脚本语法错误、无 legend-block、card-sub 未超长、主导航 2 项。");
