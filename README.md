# SA · 股析（Stock Analysis）

输入一个股票名称或代码，聚合公开披露资料，形成一张**分析矩阵**：趋势与价格结构、盈利与财务、
投资与股权、资金与筹码、行业与同业、事件与影响、风险与舆情、机构评级与预测。
以表格与图形化方式呈现，并用四种拓扑图回答「谁影响谁」，服务于趋势投资场景下的「一眼得到有用信息」。

## 当前进度

| 阶段 | 状态 | 产物 |
|---|---|---|
| 需求确认 | 已完成 | `docs/需求规格.md` |
| 设计文档 | 已完成 | `docs/架构设计.md`、`docs/概要设计.md`、`docs/详细设计.md` |
| 高保真原型 | 已完成 | `design/web/`（23 页）、`design/app/`（19 页），见 `design/README.md` |
| 服务端与前端实现 | 待开始 | ASP.NET Core Web API + React + PWA |

## 快速预览原型

直接双击 `design/web/index.html` 或 `design/app/home.html` 即可，无需起服务器、断网可用。

- 主演示股：宁德时代 `300750`
- 右下角固定标注「演示数据 · 非实时 · 不构成投资建议」
- 顶栏 <kbd>⌘/Ctrl</kbd>+<kbd>K</kbd> 唤起全局搜索
- 设计规范见 `design/web/styleguide.html` 与 `design/app/styleguide-mobile.html`

## 文档

| 文档 | 内容 |
|---|---|
| [`docs/需求规格.md`](docs/需求规格.md) | 目标、角色、功能需求（FR 编号）、非功能需求、数据需求、权限、界面要求、验收标准、范围外 |
| [`docs/架构设计.md`](docs/架构设计.md) | 关键决策与理由、C4 上下文/容器/组件视图、技术选型、数据架构、实时推送、部署、安全、风险 |
| [`docs/概要设计.md`](docs/概要设计.md) | 解决方案结构、前端结构、关键流程、接口清单、数据模型概要、关键技术方案、原型对应关系 |
| [`docs/详细设计.md`](docs/详细设计.md) | 通用约定与错误码、DTO、SQLite DDL、Parquet 分区、指标算法、采集与推送协议、授权实现、前端组件与图表契约、配置项、测试要点 |
| [`design/README.md`](design/README.md) | 原型索引、共享资产、视觉语言、自检方式、已知边界 |

## 技术栈（确定）

- 服务端：ASP.NET Core Web API（.NET 10）
- Web 前端：React + Vite + TypeScript + Tailwind + shadcn/ui + Apache ECharts 6
- 移动端：**响应式 Web + PWA**（不做独立 App）
- 存储：SQLite（元数据）+ DuckDB/Parquet（时序）
- 采集：纯 C# 直连公开 HTTP 数据源（东方财富 / 新浪 / 腾讯 / 交易所 / 巨潮资讯），不引入 Python
- 实时：SignalR，仅自选股 3 秒推送

## 原型自检

```
node design/_build/check.js          # 内联脚本语法 + 本地资源引用 + 页面死链
```
