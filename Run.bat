@echo off
setlocal EnableDelayedExpansion

rem ============================================================
rem  股析 SA · 一键启动脚本
rem
rem  做四件事：
rem    1. 检查运行环境（.NET SDK 11+、Node.js 20+）并校验版本
rem    2. 检查端口占用
rem    3. 编译后端、必要时安装前端依赖
rem    4. 分别启动后端与前端，并打印全部访问地址与账号
rem
rem  停止：关闭弹出的两个服务窗口即可（或在本窗口按 Ctrl+C 后手动关闭）。
rem
rem  ！！改这个文件时必须保持两个格式要求，实测缺一不可 ！！
rem
rem    1) 换行必须是 CRLF。
rem       用 LF 保存时 cmd 会把每行当成残缺命令，满屏
rem       「'xxx' 不是内部或外部命令」。
rem
rem    2) 编码必须是 GBK（中文 Windows 的 OEM 代码页 936），
rem       且脚本内不要出现 chcp 切换代码页。
rem       原因：cmd 是按「当前代码页」计算行起始字节偏移的。
rem       一旦在文件中间执行 chcp，后续行的偏移就按旧代码页算，
rem       中文行会被从中间截断——报错长得像语法错误，其实是编码问题。
rem       已实测：UTF-8 无 BOM 会截断；UTF-8 带 BOM 除截断外还会
rem       让第一行 @echo off 失效。所以这里用 GBK + 不切代码页。
rem
rem       若你用 VS Code 等编辑器改过本文件，请在右下角把编码改回
rem       GB2312/GBK 再保存（编辑器默认存 UTF-8，存完就会重现上面的截断）。
rem ============================================================

title 股析 SA - 一键启动

rem ---- 可调参数（改这里即可换端口与初始密码）------------------
set "API_PORT=5180"
set "WEB_PORT=5173"
rem 首次运行会用它创建 admin；改这个值不会影响已存在的账号。
rem 这是「本机自用」的便利默认值，若把服务暴露到局域网/公网，请先改掉它。
set "ADMIN_PASSWORD=Sa@2026Admin"
rem ---- ---------------------------------------------------------

rem %~dp0 带结尾反斜杠，而 "路径\" 会把引号转义掉，因此先去掉结尾反斜杠
set "ROOT=%~dp0"
set "ROOT=%ROOT:~0,-1%"
set "DATA_DIR=%ROOT%\data"
set "WEB_DIR=%ROOT%\web"

cd /d "%ROOT%"

echo.
echo ============================================================
echo   股析 SA · 本地股票分析工作台
echo ============================================================
echo.

rem ---------- 1. 环境检查 ----------
rem 只判断「命令存在」不够：装了 .NET 8 却要编 net11.0，报错会淹没在几百行编译输出里。
rem 因此这里真的读出版本号并比较主版本号。
where dotnet >nul 2>nul
if errorlevel 1 (
    echo [错误] 未找到 dotnet 命令。
    echo        请先安装 .NET SDK 11 或更高版本：https://dotnet.microsoft.com/download
    goto :fail
)

where node >nul 2>nul
if errorlevel 1 (
    echo [错误] 未找到 node 命令。
    echo        请先安装 Node.js 20 或更高版本：https://nodejs.org/
    goto :fail
)

call :checkdotnet
if errorlevel 1 goto :fail
call :checknode
if errorlevel 1 goto :fail
echo [1/4] 环境检查通过：.NET SDK !DOTNET_MAJOR! / Node !NODE_VER!
echo.

rem ---------- 2. 端口占用检查 ----------
call :checkport %API_PORT% "后端 API"
if errorlevel 1 goto :fail
call :checkport %WEB_PORT% "前端"
if errorlevel 1 goto :fail
echo [2/4] 端口 %API_PORT% 与 %WEB_PORT% 均可用
echo.

rem ---------- 3. 编译后端 ----------
echo [3/4] 正在编译后端（首次编译需要一两分钟）...
dotnet build "%ROOT%\src\SA.Api\SA.Api.csproj" -c Debug --nologo -v quiet
if errorlevel 1 (
    echo.
    echo [错误] 后端编译失败，请向上滚动查看具体错误。
    goto :fail
)
echo       后端编译完成。
echo.

rem ---------- 4. 准备前端依赖 ----------
if not exist "%WEB_DIR%\node_modules" (
    echo [4/4] 首次运行，正在安装前端依赖（npm install，可能需要几分钟）...
    pushd "%WEB_DIR%"
    call npm install --no-audit --no-fund
    if errorlevel 1 (
        popd
        echo.
        echo [错误] 前端依赖安装失败，请检查网络与 npm 配置。
        goto :fail
    )
    popd
) else (
    echo [4/4] 前端依赖已就绪。
)
echo.

rem ---------- 打印访问信息 ----------
echo ============================================================
echo   正在启动，请稍候（后端约 5 秒内响应；采集在后台继续跑）
echo ============================================================
echo.
echo   前端界面      http://localhost:%WEB_PORT%/
echo   移动端界面    http://localhost:%WEB_PORT%/m
echo   后端 API      http://localhost:%API_PORT%/
echo   健康检查      http://localhost:%API_PORT%/api/health
echo.
echo   ┌── 登录账号 ──────────────────────────────────────────
echo   │  用户名      admin
echo   │  密码        %ADMIN_PASSWORD%
echo   │
echo   │  首次启动用上面的密码创建管理员。
echo   │  若 data 目录下已有数据库，密码是你之前用的那个
echo   │  （本脚本不会重置已存在账号的密码）。
echo   └─────────────────────────────────────────────────────
echo.
echo   免登录可见：市场概览、搜索、个股全部模块、四种拓扑图、行业景气度
echo   需登录使用：自选股、条件选股器、提醒规则、通知中心、个人设置、后台管理
echo.

rem 已有数据库时明确提示：脚本无法知道旧密码，给出可操作的处理方式
if exist "%DATA_DIR%\sa.db" (
    echo   [提示] 检测到既有数据库：%DATA_DIR%\sa.db
    echo          管理员密码沿用你之前设置的那个，不是上面这句。
    echo          忘记密码时：
    echo            a^) 关闭全部服务，删除或改名 data\sa.db 后重新运行本脚本
    echo            b^) 或让管理员在后台「用户与权限」里重置密码
    echo.
)

echo   首次启动需要先采集数据，属正常现象：
echo     - 市场概览 / 搜索 / 自选    约 1 分钟内可用
echo     - 个股趋势 / 财务 / 股权    首次访问该股时按需补齐（几秒）
echo     - 全市场日线回补            后台分批进行，约 30-60 分钟
echo   数据未就绪时页面显示「数据正在采集」并自动重试。
echo.
echo   数据目录      %DATA_DIR%
echo.
echo ============================================================
echo.

rem ---------- 启动后端（独立窗口，便于单独看采集日志）----------
rem 用 start /D 指定工作目录，避免在带引号的命令串里再嵌 cd 造成引号转义问题
start "SA 后端 API (端口 %API_PORT%)" /D "%ROOT%" cmd /k "set ASPNETCORE_URLS=http://localhost:%API_PORT% && set Sa__DataDirectory=%DATA_DIR% && set Sa__Auth__AdminInitialPassword=%ADMIN_PASSWORD% && dotnet run --project src\SA.Api --no-build"

call :waitport %API_PORT% 90
if errorlevel 1 (
    echo [警告] 等待后端超时。请查看「SA 后端 API」窗口的日志；
    echo        常见原因是端口被占用、数据库被其它进程锁住，或上游接口不通。
    echo        前端仍会启动，但接口会报错。
) else (
    echo       后端已就绪，正在启动前端...
)
echo.

rem ---------- 启动前端 ----------
start "SA 前端 (端口 %WEB_PORT%)" /D "%WEB_DIR%" cmd /k "npm run dev"

call :waitport %WEB_PORT% 90
if errorlevel 1 (
    echo [警告] 等待前端超时，请查看「SA 前端」窗口的日志。
) else (
    echo       前端已就绪，正在打开浏览器...
    start "" "http://localhost:%WEB_PORT%/"
)

echo.
echo 两个服务已在独立窗口中运行：关闭对应窗口即停止该服务。
echo.
echo   后端窗口  SA 后端 API (端口 %API_PORT%)
echo   前端窗口  SA 前端 (端口 %WEB_PORT%)
echo.
echo 本窗口可以直接关闭。
echo.
pause
exit /b 0

rem ============================================================
rem  子过程
rem ============================================================

rem 检查 .NET SDK 主版本 >= 11（项目目标框架是 net11.0）。
rem 取 dotnet --version 的首段数字即可：那是最新安装的 SDK 版本。
:checkdotnet
set "DOTNET_MAJOR="
for /f "tokens=1 delims=." %%v in ('dotnet --version 2^>nul') do set "DOTNET_MAJOR=%%v"
if not defined DOTNET_MAJOR (
    echo [错误] 无法读取 dotnet 版本（dotnet --version 无输出）。
    exit /b 1
)
if !DOTNET_MAJOR! lss 11 (
    echo [错误] .NET SDK 版本过低：本项目需要 11 或更高。
    echo        当前完整版本：
    dotnet --version
    echo        下载地址：https://dotnet.microsoft.com/download
    exit /b 1
)
exit /b 0

rem 检查 Node.js 主版本 >= 20（Vite 8 的最低要求）
:checknode
set "NODE_VER="
for /f "tokens=*" %%v in ('node --version 2^>nul') do set "NODE_VER=%%v"
if not defined NODE_VER (
    echo [错误] 无法读取 node 版本（node --version 无输出）。
    exit /b 1
)
rem node --version 形如 v25.2.1，去掉前缀 v 后取主版本号比较
set "NODE_MAJOR=!NODE_VER:~1!"
for /f "tokens=1 delims=." %%v in ("!NODE_MAJOR!") do set "NODE_MAJOR=%%v"
if !NODE_MAJOR! lss 20 (
    echo [错误] Node.js 版本过低：检测到 !NODE_VER!，本项目需要 20 或更高。
    echo        下载地址：https://nodejs.org/
    exit /b 1
)
exit /b 0

rem 检查端口是否已被占用（占用则提示并返回 1）
:checkport
set "PORT=%~1"
set "LABEL=%~2"
netstat -ano | findstr /r /c:"LISTENING" | findstr /c:":%PORT% " >nul
if not errorlevel 1 (
    echo [错误] 端口 %PORT%（%LABEL%）已被占用。
    echo        请先关闭占用它的程序，或修改本脚本顶部的端口参数。
    echo        查看占用进程：netstat -ano ^| findstr :%PORT%
    exit /b 1
)
exit /b 0

rem 等待端口开始监听（每秒探测一次，最多等 %2 秒）。
rem 延时用 ping 而不是 timeout：timeout 需要可用的标准输入，
rem 脚本一旦被重定向运行（如 > log 2>&1）就会报
rem 「ERROR: Input redirection is not supported」并立刻返回，等待逻辑形同虚设。
:waitport
set "PORT=%~1"
set /a "MAX=%~2"
set /a "WAITED=0"
:waitloop
ping -n 2 127.0.0.1 >nul
netstat -ano | findstr /r /c:"LISTENING" | findstr /c:":%PORT% " >nul
if not errorlevel 1 exit /b 0
set /a "WAITED+=1"
if !WAITED! lss %MAX% goto :waitloop
exit /b 1

:fail
echo.
echo 启动未完成。
pause
exit /b 1
