# SA · 股析

本地股票分析工作台。数据全部来自公开免费接口，只做分析工具，不构成投资建议。

---

## 1. 技术栈

| 层 | 选型 |
| --- | --- |
| 后端 | .NET 11（ASP.NET Core Minimal API）、EF Core + SQLite、DuckDB（时序列存）、SignalR |
| 前端 | React 19、TypeScript 7、Vite 8、TanStack Query、ECharts 6、React Router 7 |
| 样式 | 原生 CSS + 设计令牌，**直接复用 `design/web/_shared` 下的原型三件套**，单一来源零漂移 |
| 测试 | xUnit（`dotnet test`），当前 432 项 |

前端样式不另起一套：`web/vite.config.ts` 把 `@proto` 指向 `design/web/_shared`，
改令牌即同时改应用与原型。

---

## 2. 快速开始（Windows）

双击 **`Run.bat`**，或在命令行运行：

```bat
Run.bat
```

脚本会依次完成六件事：

1. 检查运行环境（**.NET SDK 11+**、**Node.js 20+**，并校验主版本号）
2. 检查端口占用（默认后端 5180、前端 5173）
3. 编译后端
4. 构建前端产物（`npm run build`，含 TypeScript 类型检查）
5. 启动后端与静态服务器（各一个独立窗口）
6. 打印访问地址、登录账号与数据采集说明

### 访问地址

| 用途 | 地址 |
| --- | --- |
| 前端界面 | <http://localhost:5173/> |
| 移动端界面 | <http://localhost:5173/m> |
| 后端 API | <http://localhost:5180/> |
| 健康检查 | <http://localhost:5180/api/health> |

### 登录账号

| 用户名 | 密码 |
| --- | --- |
| `admin` | `Sa@2026Admin` |

- 首次启动用该密码**创建**管理员；`data/` 下已有数据库时，密码是你之前设置的那个（脚本不会重置已存在账号）。
- 改密码：改 `Run.bat` 顶部的 `ADMIN_PASSWORD`，或登录后在「个人设置」里改。
- 忘记密码：关闭全部服务，删除或改名 `data/sa.db` 后重新运行；或让管理员在后台「用户与权限」里重置。
- 免登录可用：市场概览、搜索、个股全部模块、价值研究、四种拓扑图、行业景气度。
  需登录：自选股、条件选股器、提醒规则、通知中心、个人设置、后台管理。

### 首次启动没有数据是正常的

本项目**没有演示数据兜底**，数据靠后台采集逐步补齐：

| 数据 | 就绪时间 |
| --- | --- |
| 市场概览 / 搜索 / 自选 | 约 1 分钟 |
| 个股趋势 / 财务 / 股权 / 研究数据 | 首次访问该股时按需补齐（几秒） |
| 全市场日线回补 | 后台分批进行，约 30–60 分钟 |

数据未就绪时页面显示「数据正在采集」并自动重试。

### 运行形态说明

`Run.bat` 走的是**线上形态**：静态产物 `web/dist` 由 5173 提供（根路径 `./` 直接访问），
它把 `/api` 与 `/hubs` 反代到后端 5180——与 nginx 的三个 location 是同一形态，
因此部署前可以用它在本机验证。前端全部走同源相对路径，没有跨域。

要**改前端代码即时热更新**，用开发模式（vite dev 同样带 `/api` 与 `/hubs` 代理）：

```bat
cd web
npm run dev
```

### 手动运行（不用 Run.bat）

```bat
dotnet run --project src\SA.Api          REM 后端，默认 http://localhost:5180
cd web && npm install && npm run dev     REM 前端，http://localhost:5173
```

### 常用命令

```bat
dotnet test SA.sln                       REM 全部测试（432 项）
dotnet run --project src\SA.Collector -- --probe all   REM 上游数据源探活（报告写到 data/logs）
cd web && npm run typecheck              REM 前端类型检查
cd web && npm run build                  REM 前端构建
node design\_build\check.js              REM 原型自洽检查
```

---

## 3. 部署（Linux + nginx，单域名）

### 3.1 构建产物

```bash
# 服务器上（需 .NET 11 SDK + Node 20+）
cd web && npm ci && npm run build                 # 产出 web/dist
cd .. && dotnet publish src/SA.Api -c Release -o /opt/sa/api

# 前端产物直接放到 nginx 站点目录，./ 即可访问
sudo mkdir -p /var/www/sa /var/lib/sa
sudo cp -r web/dist/* /var/www/sa/
```

`web/dist/` 已被 `.gitignore` 忽略，必须在服务器上构建或单独上传。

### 3.2 nginx

只需**三个 location**：静态根、`/api`、`/hubs`。前端全部走同源相对路径，**不需要 CORS**；
SPA 路由里没有 `/api`、`/hubs` 前缀，因此兜底不会吞掉接口请求。

`/etc/nginx/conf.d/sa.conf`：

```nginx
# WebSocket 升级头：用 map 而不是写死 "upgrade"，否则普通请求也会带上 Connection: upgrade
map $http_upgrade $connection_upgrade {
    default upgrade;
    ''      close;
}

server {
    listen 80;
    server_name sa.example.com;

    root /var/www/sa;
    index index.html;

    gzip on;
    gzip_types text/css application/javascript application/json application/manifest+json image/svg+xml;

    # PWA manifest 的 MIME（老版 mime.types 里没有）
    types { application/manifest+json webmanifest; }

    # 带哈希的构建产物：长缓存
    location /assets/ {
        expires 1y;
        add_header Cache-Control "public, immutable";
    }

    # 入口与 Service Worker 每次校验：否则用户拿到旧 index.html 指向已删除的 chunk
    location = /index.html { add_header Cache-Control "no-cache"; }
    location = /sw.js     { add_header Cache-Control "no-cache"; }

    # 后端 API。注意 proxy_pass 后面【不要】加斜杠
    location /api/ {
        proxy_pass http://127.0.0.1:5180;
        proxy_http_version 1.1;
        proxy_set_header Host              $host;
        proxy_set_header X-Real-IP         $remote_addr;
        proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_read_timeout 120s;      # 导出与全市场扫描可能较慢
    }

    # SignalR：必须带 Upgrade 且关缓冲，否则会退回长轮询或直接断开
    location /hubs/ {
        proxy_pass http://127.0.0.1:5180;
        proxy_http_version 1.1;
        proxy_set_header Upgrade    $http_upgrade;
        proxy_set_header Connection $connection_upgrade;
        proxy_set_header Host              $host;
        proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_buffering off;
        proxy_read_timeout 3600s;     # 长连接，别用默认 60s
        proxy_send_timeout 3600s;
    }

    # SPA 兜底：深链接（/stock/688110/value）必须回 index.html
    location / {
        try_files $uri $uri/ /index.html;
    }
}
```

```bash
sudo nginx -t && sudo systemctl reload nginx
```

**四个必须注意的点**

1. **`proxy_pass` 后面千万别加斜杠**。`proxy_pass http://127.0.0.1:5180;`（无斜杠）= 原样转发 `/api/xxx`，与后端路由一致。加了斜杠会剥掉 `/api` 前缀 → 全部 404。这是本配法最常见的坑。
2. **站点必须挂在域名根，不要挂子路径**（如 `/sa/`）。Service Worker 注册在 `/sw.js` 且 `scope: '/'`，挂子路径会注册失败，PWA 与 Web Push 直接不可用。
3. **`location /api/` 与 `/hubs/` 的末尾斜杠必须保留**。写成 `location /api` 会连 `/apifoo` 一起匹配。它们比 `location /` 更具体，因此顺序不影响匹配。
4. **`http2 on;` 需要 nginx 1.25+**。更早的版本要写成 `listen 443 ssl http2;`。

### 3.3 HTTPS（PWA 与推送的前提）

`/sw.js` 在非安全上下文下不会注册（`localhost` 除外），因此**只有 http 时 PWA 装不上、Web Push 收不到**。
要这两个功能，在 `server` 块上加 443 与证书，并让 80 跳转：

```nginx
server {
    listen 80;
    server_name sa.example.com;
    return 301 https://$host$request_uri;
}

server {
    listen 443 ssl;
    http2 on;
    server_name sa.example.com;

    ssl_certificate     /etc/letsencrypt/live/sa.example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/sa.example.com/privkey.pem;
    ssl_protocols       TLSv1.2 TLSv1.3;

    add_header Strict-Transport-Security "max-age=31536000" always;

    # ……下面与 3.2 的 location 完全相同……
}
```

### 3.4 systemd

`/etc/systemd/system/sa-api.service`：

```ini
[Unit]
Description=SA 股析 API
After=network-online.target

[Service]
WorkingDirectory=/opt/sa/api
ExecStart=/usr/bin/dotnet /opt/sa/api/SA.Api.dll
Restart=always
RestartSec=5

# 只监听回环，外网只能经 nginx 进入
Environment=ASPNETCORE_URLS=http://127.0.0.1:5180
Environment=ASPNETCORE_ENVIRONMENT=Production

# 数据目录必须用绝对路径：相对值 "data" 是按「向上找 SA.sln」解析的，
# 发布目录里没有 SA.sln，会落到启动时的 cwd —— 换工作目录就换了一份库
Environment=Sa__DataDirectory=/var/lib/sa

# nginx 与后端同机时无需配置：回环地址默认已被信任。
# 若 nginx 在另一台机器，把它的地址写进来（逗号分隔），否则拿不到正确的协议与来源 IP
# Environment=Sa__TrustedProxies=10.0.0.5

# 仅首次启动需要，建完 admin 就删掉这两行
Environment=Sa__Auth__AdminInitialPassword=改成强密码

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now sa-api
sudo journalctl -u sa-api -f
```

`/var/lib/sa` 需要可写，且**不要丢**：`sa.db`（用户 / 自选 / 元数据）、`parquet/`（时序数据）、
`keys/`（DataProtection 密钥环，丢了会导致已登录会话全部失效）都在这里。

### 3.5 部署形态：一个进程就够

`SA.Api` **已内嵌采集**（`AddSaCollect`），不必单独运行 `SA.Collector`。
`src/SA.Collector` 是独立的采集进程，只在「采集与 API 分开部署」时才需要。

---

## 4. 配置

配置来源优先级（高 → 低）：环境变量 → `appsettings.{Environment}.json` → `appsettings.json` → 代码默认值。

环境变量用**双下划线**代替冒号：`Sa:Auth:AccessTokenMinutes` → `Sa__Auth__AccessTokenMinutes`。

### 4.1 应用

| 键 | 默认值 | 说明 |
| --- | --- | --- |
| `Sa:DataDirectory` | `data` | 数据目录（SQLite + Parquet + 密钥环）。**部署时务必给绝对路径** |
| `Sa:TrustedProxies` | 空 | 额外信任的反向代理 IP。回环默认已信任；nginx 在另一台机器时必须配置 |

### 4.2 认证（`Sa:Auth`）

| 键 | 默认值 | 说明 |
| --- | --- | --- |
| `AccessTokenMinutes` | `30` | 访问令牌有效期（分钟） |
| `RefreshTokenHours` | `72` | 刷新令牌有效期（小时） |
| `MaxSessionsPerUser` | `5` | 每用户最多同时在线会话数 |
| `LockThreshold` | `5` | 连续失败几次锁定账号 |
| `LockMinutes` | `15` | 锁定时长（分钟） |
| `PasswordMinLength` | `10` | 密码最小长度 |
| `PasswordExpireDays` | `90` | 密码过期天数 |
| `RequireTotp` | `false` | 是否强制双因素 |
| `SigningKey` | 空 | 令牌签名密钥。留空则首启自动生成并存到 `keys/` |
| `AdminInitialPassword` | 空 | 初始管理员密码，**仅首次建号时使用** |

### 4.3 采集（`Sa:Collector`）

| 键 | 默认值 | 说明 |
| --- | --- | --- |
| `Enabled` | `true` | 是否启用采集 |
| `QuoteIntervalSeconds` | `3` | 行情快照刷新间隔 |
| `FullScanIntervalSeconds` | `60` | 全市场扫描间隔 |
| `DailyRefreshTime` | `15:40` | 每日日线回补时间 |
| `MaxRetry` | `3` | 单次请求最大重试次数 |
| `BackoffBaseMs` | `1000` | 退避基数（1s → 2s → 4s） |
| `MaxConcurrency` | `4` | 同时出网的请求数上限 |
| `PerHostMinIntervalMs` | `700` | **按域名**的最小请求间隔。取代了早期的全局令牌桶 |
| `TimeoutSeconds` | `20` | 单次请求超时 |
| `DegradeAfterFailures` | `3` | 连续失败多少次后降级到备源 |
| `CooldownMinutes` | `5` | 数据源冷却时长 |
| `BreakerThreshold` | `3` | **按域名**连续失败多少次后熔断 |
| `BreakerCooldownSeconds` | `60` | 熔断冷却时长；到期放行一次试探 |
| `UserAgents` | 7 条 | 轮换用的 UA 池（为空则用内置兜底 UA） |
| `EnableTencentFallback` | `true` | 是否启用腾讯备源 |
| `CollectOnNonTradingDay` | `true` | 非交易日是否也采集 |
| `OverviewCacheSeconds` | `60` | 市场概览缓存时长 |

### 4.4 反向代理下的协议识别

`Program.cs` 启用了 `ForwardedHeaders`（只信任回环与 `Sa:TrustedProxies`），
因此 nginx 终止 TLS 后应用仍能正确识别 `https`，刷新令牌 Cookie 会带上 `Secure` 标记。

`X-Forwarded-For` / `X-Forwarded-Proto` 是客户端可伪造的头，所以**不无差别信任**：
默认的 `KnownNetworks` 会放宽到整个内网段，这里改为清空后只加回环 + 显式配置的代理。

---

## 5. 目录结构

```
SA.sln
├─ Run.bat                     一键启动（线上形态预览）
├─ src/
│  ├─ SA.Domain/               实体与领域规则（无外部依赖）
│  ├─ SA.Application/          用例服务、DTO 契约
│  ├─ SA.Infrastructure/       采集适配器、存储、EF、DuckDB
│  ├─ SA.Api/                  Minimal API 入口（内嵌采集）
│  └─ SA.Collector/            独立采集进程（可选）
├─ tests/                      xUnit 测试
├─ web/                        React 前端（产物在 web/dist）
├─ design/                     原型与详细设计（design/web/_shared 是样式单一来源）
└─ data/                       运行时数据（已 gitignore）
   ├─ sa.db                    SQLite：用户、自选、元数据
   ├─ parquet/                 时序数据
   └─ keys/                    DataProtection 密钥环
```

---

## 6. 数据来源

全部为公开免费接口，**不依赖任何需要 API Key 的服务**：

| 数据 | 来源 |
| --- | --- |
| 行情快照、指数、行业板块、涨跌停、两融、资金流 | 东方财富 `push2` / `push2delay` |
| 全市场基本面（100+ 因子） | 东方财富 `RPT_F10_FINANCE_MAINFINADATA` |
| 日线 / 周线 / 月线 | 腾讯财经（主源）→ 东方财富（备源） |
| 主营构成、股本结构、限售解禁 | 东方财富 F10 |
| 公告、研报 | 东方财富 `np-anotice-stock` / `reportapi` |

**降级与防风控**：按域名限速 + 按域名熔断 + UA 轮换 + 指数退避重试 + 多源回退。
降级顺序不是「谁更权威」而是「谁当前真的可用、信息损失最小」，具体取舍见
`SourceRegistry` 的注释。

**不做的**：不做公告正文解析（不抽关键词、不做情绪判断、不做事件自动归类）；
不提供目标价（公开研报接口的 `indvAimPriceT` / `indvAimPriceL` 实测全为空字符串）；
不做投资建议、不做评分排序。

---

## 7. 说明

- 本项目是**分析工具**，不构成投资建议，不承诺数据准确性。
- 数据来自第三方公开接口，其可用性、字段口径与限流策略随时可能变化。
- 上游接口变更会导致对应模块降级或显示「数据正在采集」，属预期行为而非崩溃。
