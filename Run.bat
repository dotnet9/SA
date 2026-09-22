@echo off
setlocal EnableDelayedExpansion

rem ============================================================
rem  股析 SA · 一键启动脚本（线上形态：单域名）
rem
rem  形态等价于 nginx 部署，便于部署前在本机先验证：
rem    静态产物 web\dist 由静态服务器在 5173 提供（根路径 ./ 直接访问），
rem    它把 /api 与 /hubs 反代到后端 5180。前端全部走同源相对路径，
rem    因此没有跨域，也不需要在后端开 CORS。
rem
rem  做六件事：
rem    1. 检查运行环境（.NET SDK 11+、Node.js 20+）并校验版本
rem    2. 检查端口占用
rem    3. 编译后端
rem    4. 构建前端产物（npm run build）
rem    5. 启动后端与静态服务器（各一个窗口）
rem    6. 打印全部访问地址与账号
rem
rem  停止：关闭弹出的两个服务窗口即可（或在本窗口按 Ctrl+C 后手动关闭）。
rem
rem  要「改前端代码即时热更新」请用 npm run dev（vite dev 同样带 /api 与 /hubs 代理）；
rem  本脚本走的是构建产物，改完前端代码需要重新运行本脚本才会生效。
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

rem ---- 可调参数（改这里即可换端口） ----
set "API_PORT=5180"
set "WEB_PORT=5173"
rem ---- ---------------------------------------------------------

rem %~dp0 带结尾反斜杠，而 "路径\" 会把引号转义掉，因此先去掉结尾反斜杠
set "ROOT=%~dp0"
set "ROOT=%ROOT:~0,-1%"
set "DATA_DIR=%ROOT%\data"
set "WEB_DIR=%ROOT%\web"
set "DIST_DIR=%WEB_DIR%\dist"

cd /d "%ROOT%"

echo.
echo ============================================================
echo   股析 SA · 本地股票分析工作台（线上形态预览）
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
echo [1/6] 环境检查通过：.NET SDK !DOTNET_MAJOR! / Node !NODE_VER!
echo.

rem ---------- 2. 端口占用检查 ----------
call :checkport %API_PORT% "后端 API"
if errorlevel 1 goto :fail
call :checkport %WEB_PORT% "前端静态服务"
if errorlevel 1 goto :fail
echo [2/6] 端口 %API_PORT% 与 %WEB_PORT% 均可用
echo.

rem ---------- 3. 编译后端 ----------
echo [3/6] 正在编译后端（首次编译需要一两分钟）...
dotnet build "%ROOT%\src\SA.Api\SA.Api.csproj" -c Debug --nologo -v quiet
if errorlevel 1 (
    echo.
    echo [错误] 后端编译失败，请向上滚动查看具体错误。
    goto :fail
)
echo       后端编译完成。
echo.

rem ---------- 4. 构建前端产物 ----------
rem 线上形态服务的是构建产物而不是 vite dev，因此这一步是必须的：
rem dist 不存在时 npm run preview 会直接失败。
if not exist "%WEB_DIR%\node_modules" (
    echo [4/6] 首次运行，正在安装前端依赖（npm install，可能需要几分钟）...
    pushd "%WEB_DIR%"
    call npm install --no-audit --no-fund
    if errorlevel 1 (
        popd
        echo.
        echo [错误] 前端依赖安装失败，请检查网络与 npm 配置。
        goto :fail
    )
    popd
)

echo [4/6] 正在构建前端产物（含 TypeScript 类型检查，首次约 1 分钟）...
pushd "%WEB_DIR%"
call npm run build
if errorlevel 1 (
    popd
    echo.
    echo [错误] 前端构建失败，请向上滚动查看具体错误（类型检查不通过也会走到这里）。
    goto :fail
)
popd

if not exist "%DIST_DIR%\index.html" (
    echo.
    echo [错误] 构建完成但没有找到 %DIST_DIR%\index.html。
    goto :fail
)
echo       前端产物已就绪：%DIST_DIR%
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
echo   前端与接口同源：界面在 %WEB_PORT%，/api 与 /hubs 由它反代到 %API_PORT%，
echo   与线上 nginx 的三个 location 是同一形态（部署前可用本脚本验证）。
echo.
echo   无需登录：所有功能直接可用。自选股与提醒设置存在本机，换设备不会同步。
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

rem ---------- 5a. 启动后端（独立窗口，便于单独看采集日志）----------
rem 用 start /D 指定工作目录，避免在带引号的命令串里再嵌 cd 造成引号转义问题
start "SA 后端 API (端口 %API_PORT%)" /D "%ROOT%" cmd /k "set "ASPNETCORE_URLS=http://localhost:%API_PORT%"&&set "Sa__DataDirectory=%DATA_DIR%"&&dotnet run --project src\SA.Api --no-build"

call :waitport %API_PORT% 90
if errorlevel 1 (
    echo [警告] 等待后端超时。请查看「SA 后端 API」窗口的日志；
    echo        常见原因是端口被占用、数据库被其它进程锁住，或上游接口不通。
    echo        前端仍会启动，但接口会报错。
) else (
    echo       后端已就绪，正在启动前端静态服务...
)
echo.

rem ---------- 5b. 启动前端静态服务 ----------
rem npm run preview 服务 web\dist 并把 /api 与 /hubs 反代到后端，
rem 与线上 nginx 同形态：前端走同源相对路径，不需要后端开 CORS。
start "SA 前端 (端口 %WEB_PORT%)" /D "%WEB_DIR%" cmd /k "npm run preview"

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
set "MAX=%~2"
set "MAX=%MAX:"=%"
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
