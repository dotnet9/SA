# SA · 股析 — 原型说明与索引

本目录是 **高保真可点击原型**，用于确认需求与视觉语言，**不含后端与前端工程代码**。
软件名取 stock analysis 首字母缩写 **SA**，中文名 **股析**。

## 如何预览

- **直接双击任意 `.html`** 即可打开（图表库与样式都在本地，无需 Node、无需起服务器、断网可用）。
- 建议浏览器：Chrome / Edge 最新版（原型使用 `backdrop-filter`、CSS 变量与 ECharts 6）。
- `design/app/` 的页面会在桌面浏览器里套一个 390×844 手机外框，右侧附跳转与主题切换工具。
- 全局快捷键：<kbd>⌘/Ctrl</kbd>+<kbd>K</kbd> 或 <kbd>/</kbd> 唤起顶栏搜索。
- Web 端左下角固定「演示数据 · 非实时 · 不构成投资建议」角标；移动端无浮动角标，每页页底有同义灰字说明；所有数值均为演示数据。

## 目录结构

```
design/
├── web/                      桌面端原型（23 页，1440 宽设计 + 响应式）
│   ├── index.html            登录
│   ├── styleguide.html       设计规范与组件库（视觉语言基准）
│   ├── market.html           市场概览（首页）
│   ├── search-results.html   搜索结果
│   ├── stock.html            个股总览（分析矩阵 · 卡片堆叠 + 标签专注）
│   ├── stock.html#trend      ① 趋势与价格结构
│   ├── stock.html#finance    ② 盈利与财务表现
│   ├── stock.html#equity     ③ 公司投资与股权结构
│   ├── stock.html#capital    ④ 资金面与筹码
│   ├── stock.html#industry   ⑤ 行业与同业对比
│   ├── stock.html#events     ⑥ 事件时间线与影响（四种拓扑图）
│   ├── stock.html#risk       ⑦ 风险与舆情监控
│   ├── stock.html#rating     ⑧ 机构评级与盈利预测
│   ├── stock.html#events         四种拓扑图集中对照
│   ├── watchlist.html        自选股盯盘
│   ├── screener.html         条件选股器
│   ├── alerts.html           提醒规则
│   ├── notifications.html    通知中心
│   ├── settings.html         个人设置
│   ├── admin-users.html      后台 · 用户管理
│   ├── admin-permissions.html 后台 · 角色与功能点开关矩阵
│   ├── admin-datasource.html 后台 · 数据源与采集监控
│   ├── admin-security.html   后台 · 登录与安全
│   └── _shared/              共享资产（见下）
└── app/                      移动端原型（19 页，390×844 竖屏）
    ├── index.html            登录
    ├── home.html             市场概览
    ├── search.html           搜索
    ├── stock.html            个股总览
    ├── stock.html#trend      趋势与价格结构
    ├── stock.html#finance    盈利与财务表现
    ├── stock.html#equity     投资与股权结构
    ├── stock.html#capital    资金面与筹码
    ├── stock.html#industry   行业与同业对比
    ├── stock.html#events     事件时间线与影响
    ├── stock.html#risk       风险与舆情监控
    ├── stock.html#rating     机构评级与预测
    ├── watchlist.html        自选股盯盘
    ├── screener.html         条件选股器
    ├── notifications.html    通知中心
    ├── alerts.html           提醒规则
    ├── notify-preview.html   提醒形态预览（通知栏 / 锁屏 / 震动）
    ├── settings.html         我的
    └── styleguide-mobile.html 移动端设计规范
```

## 共享资产（`design/web/_shared/`）

| 文件 | 作用 |
|---|---|
| `tokens.css` | 设计令牌：深浅主题、涨跌色切换、字体、间距、动画 |
| `components.css` | 手写组件样式：外壳 / 卡片 / 表格 / 表单 / 浮层 / 状态 / 手机外框 |
| `tailwind.css` | Tailwind v4 编译产物（仅布局工具类；缺失也不影响原型可用） |
| `app.js` | 主题与涨跌色、按角色裁剪导航、全局搜索、表格排序、Tab、卡片折叠与拖拽、Toast、实时闪烁 |
| `charts.js` | ECharts 6 主题注册与 16 种图表工厂（K 线 / 桑基 / 关系图 / 热力 / 筹码 / 泳道 …） |
| `data.js` | 全部演示数据（含确定性伪随机序列生成器，保证每次打开一致） |
| `mock-frame.js` | 移动端手机外框与底部 Tab 栏（仅 `design/app` 引用） |
| `vendor/echarts.min.js` | ECharts 6.1.0 本地副本（离线可用） |

移动端通过 `../web/_shared/...` 复用同一套令牌、组件、数据与图表封装，保证两端一致。

## 视觉语言要点

- **大气简约**：中性沉稳的底色 + 单一品牌色，**无装饰性渐变、无光晕**；卡片无描边、16px 圆角，层次靠底色差与留白表达。
- **深色为主 + 可切换浅色**；**红涨绿跌**为默认，可一键切为国际习惯（全站图表、表格、数字同步）。
- 数字一律等宽 + `tabular-nums` 右对齐，保证小数点竖直对齐；关键数字（行情价、目标价、命中数）放大做视觉锚点。
- **小屏聚焦**：移动端每页只有一个视觉主角，板块 ≤ 5 个，口径说明统一放页底灰字，不做逐卡片标注。
- 表格行左侧 2px 涨跌色细条；浮层统一玻璃拟态（`backdrop-filter: blur(20px)`，仅浮层使用）。
- 每个数据卡片右上角有弱化的「数据口径」问号（hover 恢复），悬浮显示字段口径与更新时间。
- 完整说明见 `web/styleguide.html`（13 节）与 `app/styleguide-mobile.html`（8 节）。

## 演示数据说明

- 主演示股：**宁德时代 300750**；股票池覆盖沪主板 / 深主板 / 创业板 / 科创板 / 北交所。
- 行情与财务取真实量级的演示数值；事件、因果链、告警、机构评级为**虚构但合理**的内容。
- 序列数据（K 线、资金流、持仓趋势）由固定种子的伪随机生成器产出，每次打开完全一致。

## 原型自检

```
# 静态检查：内联脚本语法 + 本地资源引用 + 页面死链
node design/_build/check.js

# 可选：重新生成 Tailwind 产物（缺失不影响原型）
cd design/_build && npm install && npm run build
```

## 已知边界（原型有意不做）

- 无真实后端、无真实登录鉴权、无真实数据抓取、无真实推送（通知为演示内容）。
- 事件与因果链**只做查询与浏览**，不做标注编辑界面（编辑在正式版由管理员另行提供）。
- 不含券商研报与营业部观点原文抓取，只汇总机构评级与盈利预测。
- 不含手机桌面小组件（iOS 纯 PWA 不支持，已用通知栏提醒 + 震动替代）。
- 各模块的「导出」按钮只提示导出结果，不产生文件。

后续实现方案见 `docs/`：需求规格、架构设计、概要设计、详细设计。
