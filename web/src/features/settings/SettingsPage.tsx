import { useEffect, useState } from 'react';
import { Link } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { useToast } from '@/providers/ToastProvider';
import { useTheme } from '@/providers/ThemeProvider';
import { useRealtime } from '@/providers/RealtimeProvider';
import { useAuth } from '@/providers/AuthProvider';
import { readBool, readJson, readString, StorageKeys, writeBool, writeJson, writeString } from '@/lib/storage';
import { fetchWatchlist } from '@/features/watchlist/api';
import { fetchAlertRules } from '@/features/alerts/api';
import { fetchNotifications } from '@/features/alerts/api';

/**
 * 个人设置。
 *
 * 结构与 `design/web/settings.html` 对应：外观 → 刷新与推送 → 指标参数 → 通知偏好 → PWA 安装，
 * 右侧为账号、我的数据、数据口径与关于。
 *
 * 三处与原型不同的处理，都是「不写假的」：
 * 1. PWA 区不显示「HTTPS 已就绪 / Service Worker 已注册」的绿色标签——本轮未实现 Service Worker，
 *    因此只展示浏览器的真实能力（安全上下文、通知权限）并说明安装前置条件；
 * 2. 「邮件通知」标注未接入并禁用，不给出可点的开关；
 * 3. 数据口径区写本实现的实际口径（全市场扫描周期、北向不提供等），而不是原型里的演示文案。
 */
export function SettingsPage() {
  const toast = useToast();
  const theme = useTheme();
  const realtime = useRealtime();
  const { me } = useAuth();

  /* --- 外观 --- */
  const [numFont, setNumFont] = useState<'mono' | 'ui'>(() => (readString(StorageKeys.NumFont, 'mono') === 'ui' ? 'ui' : 'mono'));

  /* --- 刷新与推送 --- */
  const [homePath, setHomePath] = useState(() => readString(StorageKeys.HomePath, '/market'));
  const [flashOnPush, setFlashOnPush] = useState(() => readBool(StorageKeys.FlashOnPush, true));
  const [showMarketTime, setShowMarketTime] = useState(() => readBool(StorageKeys.ShowMarketTime, true));

  /* --- 指标参数 --- */
  const [maPeriods, setMaPeriods] = useState(() => readString(StorageKeys.MaPeriods, '5,10,20,60'));
  const [adjust, setAdjust] = useState(() => readString(StorageKeys.Adjust, 'qfq'));
  const [macdParams, setMacdParams] = useState(() => readString(StorageKeys.MacdParams, '12,26,9'));
  const [fundFlowCaliber, setFundFlowCaliber] = useState(() => readString(StorageKeys.FundFlowCaliber, 'super+large'));

  /* --- 通知偏好 --- */
  const [channels, setChannels] = useState<Record<string, boolean>>(() =>
    readJson(StorageKeys.NotifyChannels, { site: true, browser: true, vibrate: true, mail: false })
  );
  const [dnd, setDnd] = useState(() =>
    readJson(StorageKeys.DoNotDisturb, { enabled: true, from: '23:00', to: '08:30', dailyDigest: true })
  );

  /** 浏览器通知权限（真实值，不是原型里的固定文案）。 */
  const [notifyPermission, setNotifyPermission] = useState<string>('unsupported');

  useEffect(() => {
    setNotifyPermission(typeof window !== 'undefined' && 'Notification' in window ? Notification.permission : 'unsupported');
  }, []);

  /** 数字字体：通过根节点属性切换，样式在 app.css 里按属性选择。 */
  useEffect(() => {
    document.documentElement.setAttribute('data-numfont', numFont);
    writeString(StorageKeys.NumFont, numFont);
  }, [numFont]);

  /* --- 右侧：我的数据（真实计数） --- */
  const { data: watchlist } = useQuery({ queryKey: ['watchlist'], queryFn: fetchWatchlist, staleTime: 60_000 });
  const { data: alerts } = useQuery({ queryKey: ['alerts'], queryFn: fetchAlertRules, staleTime: 60_000 });
  const { data: notifications } = useQuery({
    queryKey: ['notifications', true],
    queryFn: () => fetchNotifications(true),
    staleTime: 60_000
  });

  const save = () => {
    writeString(StorageKeys.HomePath, homePath);
    writeBool(StorageKeys.FlashOnPush, flashOnPush);
    writeBool(StorageKeys.ShowMarketTime, showMarketTime);
    writeString(StorageKeys.MaPeriods, maPeriods);
    writeString(StorageKeys.Adjust, adjust);
    writeString(StorageKeys.MacdParams, macdParams);
    writeString(StorageKeys.FundFlowCaliber, fundFlowCaliber);
    writeJson(StorageKeys.NotifyChannels, channels);
    writeJson(StorageKeys.DoNotDisturb, dnd);
    toast.toast('设置已保存到本机', 'ok');
  };

  const reset = () => {
    theme.setTheme('dark');
    theme.setUpdown('red-up');
    theme.setDensity('compact');
    setNumFont('mono');
    setHomePath('/market');
    setFlashOnPush(true);
    setShowMarketTime(true);
    setMaPeriods('5,10,20,60');
    setAdjust('qfq');
    setMacdParams('12,26,9');
    setFundFlowCaliber('super+large');
    setChannels({ site: true, browser: true, vibrate: true, mail: false });
    setDnd({ enabled: true, from: '23:00', to: '08:30', dailyDigest: true });
    writeString(StorageKeys.NumFont, 'mono');
    writeString(StorageKeys.HomePath, '/market');
    toast.toast('已恢复默认设置', 'info');
  };

  const requestBrowserPermission = async () => {
    if (!('Notification' in window)) {
      toast.toast('当前环境不支持浏览器通知', 'warn');
      return;
    }

    const permission = await Notification.requestPermission();
    setNotifyPermission(permission);
    toast.toast(
      permission === 'granted' ? '浏览器通知已授权' : '未获得通知权限，仅保留站内通知',
      permission === 'granted' ? 'ok' : 'warn'
    );
  };

  const activeAlerts = alerts?.rules.filter((rule) => rule.enabled).length ?? 0;

  return (
    <>
      <div className="sa-pagehead">
        <div>
          <div className="breadcrumb">
            <Link to="/market">市场概览</Link>
            <span className="sep">/</span>
            <span>个人设置</span>
          </div>
          <h1>个人设置</h1>
          <div className="sub">外观、刷新频率、指标参数、通知偏好 · 保存在本机浏览器，改动即时生效</div>
        </div>
        <div className="sa-pagehead-actions">
          <button type="button" className="btn btn-outline btn-sm" onClick={reset}>
            恢复默认
          </button>
          <button type="button" className="btn btn-primary btn-sm" onClick={save}>
            保存设置
          </button>
        </div>
      </div>

      <div className="sa-split">
        <div className="col gap-4">
          {/* 外观 */}
          <div className="card is-accent">
            <div className="card-head">
              <span className="card-title">外观</span>
              <span className="card-sub">全站生效</span>
            </div>
            <div className="card-body grid grid-2">
              <div className="field">
                <label className="label">主题</label>
                <div className="segmented">
                  <span className={theme.theme === 'dark' ? 'is-active' : undefined} onClick={() => theme.setTheme('dark')}>
                    深色（默认）
                  </span>
                  <span className={theme.theme === 'light' ? 'is-active' : undefined} onClick={() => theme.setTheme('light')}>
                    浅色
                  </span>
                </div>
                <span className="hint">深色为默认主题，适合长时间盯盘；浅色在强光环境下更易读。</span>
              </div>

              <div className="field">
                <label className="label">涨跌颜色</label>
                <div className="segmented">
                  <span className={theme.updown === 'red-up' ? 'is-active' : undefined} onClick={() => theme.setUpdown('red-up')}>
                    红涨绿跌（A 股习惯）
                  </span>
                  <span className={theme.updown === 'green-up' ? 'is-active' : undefined} onClick={() => theme.setUpdown('green-up')}>
                    绿涨红跌（国际习惯）
                  </span>
                </div>
                <span className="hint">影响所有图表、表格与数字颜色，切换后立即重建图表。</span>
              </div>

              <div className="field">
                <label className="label">表格密度</label>
                <div className="segmented">
                  <span className={theme.density === 'compact' ? 'is-active' : undefined} onClick={() => theme.setDensity('compact')}>
                    紧凑（行高 32px）
                  </span>
                  <span
                    className={theme.density === 'comfortable' ? 'is-active' : undefined}
                    onClick={() => theme.setDensity('comfortable')}
                  >
                    舒适（行高 40px）
                  </span>
                </div>
              </div>

              <div className="field">
                <label className="label">数字字体</label>
                <div className="segmented">
                  <span className={numFont === 'mono' ? 'is-active' : undefined} onClick={() => setNumFont('mono')}>
                    等宽（推荐）
                  </span>
                  <span className={numFont === 'ui' ? 'is-active' : undefined} onClick={() => setNumFont('ui')}>
                    跟随界面字体
                  </span>
                </div>
                <span className="hint">等宽数字可让小数点竖直对齐，便于纵向扫读。</span>
              </div>
            </div>
          </div>

          {/* 刷新与推送 */}
          <div className="card">
            <div className="card-head">
              <span className="card-title">刷新与推送</span>
              <span className="card-sub">仅自选股盘中实时</span>
            </div>
            <div className="card-body grid grid-2">
              <div className="field">
                <label className="label">自选股推送间隔</label>
                <div className="segmented">
                  {[3, 5, 10].map((seconds) => (
                    <span
                      key={seconds}
                      className={realtime.intervalSeconds === seconds ? 'is-active' : undefined}
                      onClick={() => realtime.setIntervalSeconds(seconds)}
                    >
                      {seconds} 秒{seconds === 3 ? '（推荐）' : ''}
                    </span>
                  ))}
                </div>
                <span className="hint">
                  服务端经 SignalR 推送，仅推送自选股。
                  当前连接状态：{realtime.status === 'connected' ? `已连接（订阅 ${realtime.subscribed.length} 只）` : '未连接'}。
                </span>
              </div>

              <div className="field">
                <label className="label">默认首页</label>
                <select className="input input-sm" value={homePath} onChange={(event) => setHomePath(event.target.value)}>
                  <option value="/market">市场概览</option>
                  <option value="/watchlist">自选股盯盘</option>
                  <option value="/screener">条件选股器</option>
                  <option value="/notifications">通知中心</option>
                </select>
                <span className="hint">登录后跳转到该页面。</span>
              </div>

              <div className="field">
                <label className="label">数字变动闪烁</label>
                <label className="check">
                  <input type="checkbox" checked={flashOnPush} onChange={(event) => setFlashOnPush(event.target.checked)} />
                  推送到达时高亮 600ms
                </label>
                <span className="hint">仅改变底色，不做位移，避免列表跳动。</span>
              </div>

              <div className="field">
                <label className="label">行情时间戳</label>
                <label className="check">
                  <input type="checkbox" checked={showMarketTime} onChange={(event) => setShowMarketTime(event.target.checked)} />
                  在顶栏常驻显示「最后更新」时间
                </label>
              </div>
            </div>
          </div>

          {/* 指标参数 */}
          <div className="card">
            <div className="card-head">
              <span className="card-title">指标参数</span>
              <span className="card-sub">本机展示与分析口径</span>
            </div>
            <div className="card-body grid grid-2">
              <div className="field">
                <label className="label">均线周期</label>
                <div className="row gap-2">
                  {maPeriods.split(',').map((value, index) => (
                    <input
                      key={index}
                      className="input input-num input-sm"
                      style={{ width: 64 }}
                      value={value}
                      onChange={(event) => {
                        const next = maPeriods.split(',');
                        next[index] = event.target.value;
                        setMaPeriods(next.join(','));
                      }}
                    />
                  ))}
                </div>
                <span className="hint">日线默认 5/10/20/60。</span>
              </div>

              <div className="field">
                <label className="label">复权方式</label>
                <div className="segmented">
                  <span className={adjust === 'qfq' ? 'is-active' : undefined} onClick={() => setAdjust('qfq')}>
                    前复权
                  </span>
                  <span className={adjust === 'hfq' ? 'is-active' : undefined} onClick={() => setAdjust('hfq')}>
                    后复权
                  </span>
                  <span className={adjust === 'none' ? 'is-active' : undefined} onClick={() => setAdjust('none')}>
                    不复权
                  </span>
                </div>
                <span className="hint">
                  当前服务的日线与指标统一按<b>前复权</b>入库，因此这里的选择只影响你的阅读偏好记录，
                  不改变服务端已算好的数值（避免同一份指标出现两套口径）。
                </span>
              </div>

              <div className="field">
                <label className="label">MACD 参数</label>
                <div className="row gap-2">
                  {macdParams.split(',').map((value, index) => (
                    <input
                      key={index}
                      className="input input-num input-sm"
                      style={{ width: 64 }}
                      value={value}
                      onChange={(event) => {
                        const next = macdParams.split(',');
                        next[index] = event.target.value;
                        setMacdParams(next.join(','));
                      }}
                    />
                  ))}
                </div>
                <span className="hint">服务端按 12/26/9 计算，此处仅记录偏好。</span>
              </div>

              <div className="field">
                <label className="label">主力资金口径</label>
                <select
                  className="input input-sm"
                  value={fundFlowCaliber}
                  onChange={(event) => setFundFlowCaliber(event.target.value)}
                >
                  <option value="super+large">超大单 + 大单（服务端口径）</option>
                  <option value="super">仅超大单</option>
                </select>
                <span className="hint">
                  服务端按「超大单 + 大单」计算主力净额（与上游定义一致），此处仅记录偏好。
                </span>
              </div>
            </div>
          </div>

          {/* 通知偏好 */}
          <div className="card">
            <div className="card-head">
              <span className="card-title">通知偏好</span>
              <div className="card-tools">
                <span className={`tag ${notifyPermission === 'granted' ? 'tag-up' : 'tag-outline'}`}>
                  浏览器通知：{notifyPermission === 'granted' ? '已授权' : notifyPermission === 'denied' ? '已拒绝' : notifyPermission === 'unsupported' ? '环境不支持' : '未授权'}
                </span>
              </div>
            </div>
            <div className="card-body grid grid-2">
              <div className="field">
                <label className="label">通知渠道</label>
                <div className="col gap-2">
                  <label className="check">
                    <input
                      type="checkbox"
                      checked={channels.site ?? true}
                      onChange={(event) => setChannels({ ...channels, site: event.target.checked })}
                    />
                    站内通知中心
                  </label>
                  <label className="check">
                    <input
                      type="checkbox"
                      checked={channels.browser ?? false}
                      onChange={(event) => setChannels({ ...channels, browser: event.target.checked })}
                    />
                    浏览器通知栏
                  </label>
                  <label className="check">
                    <input
                      type="checkbox"
                      checked={channels.vibrate ?? false}
                      onChange={(event) => setChannels({ ...channels, vibrate: event.target.checked })}
                    />
                    震动（移动端）
                  </label>
                  <label className="check">
                    <input type="checkbox" checked={false} disabled />
                    邮件（未接入）
                  </label>
                </div>
                <button type="button" className="btn btn-sm btn-outline mt-3" onClick={() => void requestBrowserPermission()}>
                  请求浏览器通知权限
                </button>
              </div>

              <div className="field">
                <label className="label">免打扰</label>
                <div className="row gap-2" style={{ alignItems: 'center' }}>
                  <input
                    className="input input-sm"
                    style={{ width: 88 }}
                    value={dnd.from}
                    onChange={(event) => setDnd({ ...dnd, from: event.target.value })}
                  />
                  <span className="hint">至</span>
                  <input
                    className="input input-sm"
                    style={{ width: 88 }}
                    value={dnd.to}
                    onChange={(event) => setDnd({ ...dnd, to: event.target.value })}
                  />
                  <label className="check">
                    <input
                      type="checkbox"
                      checked={dnd.enabled ?? false}
                      onChange={(event) => setDnd({ ...dnd, enabled: event.target.checked })}
                    />
                    启用
                  </label>
                </div>
                <span className="hint">
                  免打扰期间仅保留站内通知（服务端仍会记录，不丢失）。
                  浏览器通知与震动由浏览器与设备控制，本机设置只决定是否请求。
                </span>
              </div>
            </div>
            <div className="card-foot">
              <Link className="t-brand" to="/alerts">
                管理提醒规则 →
              </Link>
              <Link className="t-brand" to="/notifications">
                查看通知中心 →
              </Link>
              <Link className="t-brand" to="/notify-preview">
                查看提醒形态预览 →
              </Link>
            </div>
          </div>

          {/* PWA：只展示真实能力，不写「已就绪」 */}
          <div className="card">
            <div className="card-head">
              <span className="card-title">安装与通知能力</span>
              <span className="card-sub">手机端获得通知栏提醒的前提</span>
            </div>
            <div className="card-body grid grid-2">
              <div className="col gap-3">
                <div className="row gap-2 wrap">
                  <span className={`tag ${window.isSecureContext ? 'tag-up' : 'tag-warn'}`}>
                    安全上下文：{window.isSecureContext ? '满足' : '不满足'}
                  </span>
                  <span className="tag tag-outline">Service Worker：未注册</span>
                </div>
                <div className="fold">
                  <div className="fold-head">
                    Android（Chrome / Edge）<span className="caret">▼</span>
                  </div>
                  <div className="fold-body hint">
                    本轮未实现 Service Worker 与 Web Push，因此浏览器不会弹出「添加到主屏幕」提示，
                    也无法在页面关闭后收到通知。当前的实时提醒只在页面打开时经 SignalR 送达。
                  </div>
                </div>
                <div className="fold is-collapsed">
                  <div className="fold-head">
                    iOS（Safari）<span className="caret">▼</span>
                  </div>
                  <div className="fold-body hint">
                    同上：iOS 的 Web Push 依赖 Service Worker + 主屏启动，本轮不提供。
                  </div>
                </div>
              </div>
              <div className="col gap-3">
                <div className="kpi">
                  <span className="kpi-label">推送通道</span>
                  <span className="kpi-value is-sm">
                    {realtime.status === 'connected' ? 'SignalR 已连接' : 'SignalR 未连接'}
                  </span>
                  <span className="kpi-delta t-3">仅在页面打开期间有效</span>
                </div>
                <button type="button" className="btn btn-outline" onClick={() => void requestBrowserPermission()}>
                  请求通知权限
                </button>
                <div className="legend-block">
                  <b>为什么需要 HTTPS</b>
                  <br />
                  Service Worker、Web Push、通知栏 API、震动 API 都只在安全上下文（HTTPS 或 localhost）下可用。
                  本机调试用 http://localhost 即可，手机访问必须走域名 HTTPS。
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* 右侧信息栏 */}
        <aside className="col gap-4">
          <div className="card">
            <div className="card-head">
              <span className="card-title">账号</span>
            </div>
            <div className="card-body col gap-3">
              <div className="row gap-3">
                <span className="avatar" style={{ width: 38, height: 38, fontSize: 14 }}>
                  {(me?.nickname ?? me?.username ?? '—').slice(0, 1)}
                </span>
                <div className="grow">
                  <div className="fs-14 fw-600">{me?.nickname ?? '—'}</div>
                  <div className="hint">{me?.username ?? '—'}</div>
                </div>
              </div>
              <div className="row-between">
                <span className="fs-12 t-2">角色</span>
                <span className="tag tag-brand">{me?.roleName ?? '—'}</span>
              </div>
              <div className="row-between">
                <span className="fs-12 t-2">权限功能点</span>
                <span className="mono fs-12">{me?.functionPoints.length ?? 0} 项</span>
              </div>
              <div className="row-between">
                <span className="fs-12 t-2">数据范围</span>
                <span className="mono fs-12">{me?.dataScope === 'watchlist' ? '仅自选股' : '全市场'}</span>
              </div>
              <div className="row-between">
                <span className="fs-12 t-2">二次验证</span>
                <span className={`tag ${me?.totpEnabled ? 'tag-up' : 'tag-outline'}`}>
                  {me?.totpEnabled ? '已开启' : me?.totpRequired ? '未开启（强制）' : '未开启'}
                </span>
              </div>
              <Link className="btn btn-outline btn-block" to="/admin/security">
                安全设置与会话
              </Link>
            </div>
          </div>

          <div className="card">
            <div className="card-head">
              <span className="card-title">我的数据</span>
            </div>
            <div className="card-body col gap-2">
              <div className="row-between">
                <span className="fs-12 t-2">自选股</span>
                <Link className="mono fs-12 t-brand" to="/watchlist">
                  {watchlist?.items.length ?? 0} 只
                </Link>
              </div>
              <div className="row-between">
                <span className="fs-12 t-2">提醒规则</span>
                <Link className="mono fs-12 t-brand" to="/alerts">
                  {alerts?.total ?? 0} 条（{activeAlerts} 启用）
                </Link>
              </div>
              <div className="row-between">
                <span className="fs-12 t-2">未读通知</span>
                <Link className="mono fs-12 t-brand" to="/notifications">
                  {notifications?.unread ?? 0} 条
                </Link>
              </div>
            </div>
          </div>

          <div className="card">
            <div className="card-head">
              <span className="card-title">数据口径</span>
            </div>
            <div className="card-body">
              <div className="legend-block">
                <b>行情</b>：东方财富公开接口；<b>全市场</b>按扫描周期整体刷新（端点单页上限 100 行，一轮约 60 次请求），非逐笔。
                <br />
                <b>自选股</b>：多标的快照端点经 SignalR 推送，间隔可选 3/5/10 秒；失败自动降级到腾讯备源。
                <br />
                <b>日线</b>：前复权，落本地 Parquet；指标（MA / MACD / KDJ / RSI / BOLL）由服务端批量计算。
                <br />
                <b>财务 / 股权</b>：东财公开报表，季频；<b>资金流与两融</b>：逐日。
                <br />
                <b>行业分类</b>：东财行业（申万一级无公开直取源）。
                <br />
                <b>北向资金</b>：公开接口已不再提供逐日净买入，本实现不提供该数值，也不以估算替代。
              </div>
            </div>
          </div>

          <div className="card">
            <div className="card-head">
              <span className="card-title">关于</span>
            </div>
            <div className="card-body col gap-2">
              <div className="row-between">
                <span className="fs-12 t-2">服务端</span>
                <span className="fs-12">ASP.NET Core Web API</span>
              </div>
              <div className="row-between">
                <span className="fs-12 t-2">前端</span>
                <span className="fs-12">React + ECharts</span>
              </div>
              <div className="row-between">
                <span className="fs-12 t-2">数据存储</span>
                <span className="fs-12">SQLite + Parquet</span>
              </div>
              <div className="row-between">
                <span className="fs-12 t-2">移动端</span>
                <span className="fs-12">响应式 Web</span>
              </div>
            </div>
          </div>
        </aside>
      </div>

      <div className="legend-block mt-4">
        <b>说明</b>
        <br />
        本页大部分设置保存在浏览器本地（<code>sa.*</code> 键），换设备或清缓存后会恢复默认；
        推送间隔、数据范围、权限等功能性设置由服务端按账号生效。
        <br />
        「复权方式」与「MACD 参数」记录的是阅读偏好：服务端已按前复权与 12/26/9 统一计算，
        本地不做第二套计算，避免同一份指标出现两个口径。
      </div>
    </>
  );
}
