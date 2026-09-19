/* ============================================================
   SA 原型自检工具（build 辅助，不参与原型运行）
   用法：node design/_build/check.js
   检查项：
     1. 每个 html 的内联 <script> 语法是否合法
     2. 引用的本地 css / js / 图片 / 页面链接是否存在
     3. 页面之间是否存在死链
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

/* ---------- 报告 ---------- */
console.log("检查文件数：" + stats.files + "，内联脚本：" + stats.scripts +
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
console.log("\n通过：无死链、无内联脚本语法错误。");
