import { useEffect, useState } from 'react';
import { Link } from 'react-router';
import { useMutation, useQuery } from '@tanstack/react-query';
import { useToast } from '@/providers/ToastProvider';
import { useTheme } from '@/providers/ThemeProvider';
import { useRealtime } from '@/providers/RealtimeProvider';
import { apiGet, apiPut } from '@/lib/api';
import { errorText } from '@/lib/errorText';
import { readBool, readJson, readString, StorageKeys, writeBool, writeJson, writeString } from '@/lib/storage';
import { fetchAlertRules, fetchNotifications, type NotifySettings } from '@/features/alerts/api';
import { useWatchlist } from '@/features/watchlist/WatchlistProvider';
import { cacheClear, cacheStats } from '@/lib/idb-cache';
import { usePwa } from './usePwa';

/**
 * 个人设置。
 *
 * 结构与 `design/web/settings.html` 对应：外观 → 刷新与推送 → 指标参数 → 通知偏好 → 安装与通知能力，
 * 右侧为账号、我的数据、数据口径与关于。
 *
 * 三处与原型不同、且都是「不写假的」：
 * 1. 安装与通知能力区展示<b>浏览器与服务端的真实状态</b>（Service Worker 是否注册、
 *    推送是否已订阅、通知权限），并提供可用的订阅/取消/测试按钮；
 * 2. 「邮件通知」标注未接入并禁用，不给出可点的开关；
 * 3. 数据口径区写本实现的实际口径（全市场扫描周期、北向不提供等），而不是原型里的演示文案。
 *
 * 另外：<b>免打扰与推送开关存在服务端</b>（而不是只存浏览器本地）——
 * 浏览器关掉时前端没有机会判断「现在该不该响」，免打扰必须在服务端执行才有意义。
 */
export function SettingsPage() {
  const toast = useToast();
  const theme = useTheme();
  const realtime = useRealtime();
  const watchlist = useWatchlist();
  const pwa = usePwa();

  /* 本地数据卡：缓存条数与占用（IndexedDB 的统计） */
  const [cacheLabel, setCacheLabel] = useState('—');
  useEffect(() => {
    void cacheStats().then((s) => {
      setCacheLabel(s.count === 0 ? '空' : `${s.count} 项 · ${Math.round(s.bytes / 1024)} KB`);
    });
  }, []);

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
  // 服务端的免打扰与推送开关：浏览器关掉时前端无从判断，因此这份配置必须存在服务端
  const { data: serverNotify } = useQuery({
    queryKey: ['notify', 'settings'],
    queryFn: () => apiGet<NotifySettings>('/api/notifications/settings')
  });

  const [dnd, setDnd] = useState({ enabled: true, from: '23:00', to: '08:30', keepInbox: true });

  // 首次拿到服务端配置后填充表单；之后以本地编辑为准（避免覆盖用户正在改的内容）
  const [dndLoaded, setDndLoaded] = useState(false);
  useEffect(() => {
    if (!serverNotify || dndLoaded) {
      return;
    }

    setDnd({
      enabled: serverNotify.dndEnabled,
      from: serverNotify.dndFrom,
      to: serverNotify.dndTo,
      keepInbox: serverNotify.dndKeepInbox
    });
    setDndLoaded(true);
  }, [serverNotify, dndLoaded]);

  const notifyMutation = useMutation({
    mutationFn: (value: { enabled: boolean; from: string; to: string; keepInbox: boolean; pushEnabled: boolean }) =>
      apiPut<number>('/api/notifications/settings', {
        dndEnabled: value.enabled,
        dndFrom: value.from,
        dndTo: value.to,
        dndKeepInbox: value.keepInbox,
        pushEnabled: value.pushEnabled
      }),
    onSuccess: () => toast.toast('提醒偏好已保存到服务端（免打扰在服务端生效）', 'ok'),
    onError: (mutationError) => toast.toast(errorText(mutationError), 'error')
  });

  /** 渠道开关（本地）：站内与震动是浏览器侧行为，浏览器推送由服务端开关控制。 */
  const [channels, setChannels] = useState<Record<string, boolean>>(() =>
    readJson(StorageKeys.NotifyChannels, { site: true, vibrate: true, mail: false })
  );

  /** 数字字体：通过根节点属性切换，样式在 app.css 里按属性选择。 */
  useEffect(() => {
    document.documentElement.setAttribute('data-numfont', numFont);
    writeString(StorageKeys.NumFont, numFont);
  }, [numFont]);

  /* --- 右侧：我的数据（真实计数） --- */
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

    // 免打扰与推送开关存服务端：它们决定「服务端要不要发推送」，存本地等于没生效
    notifyMutation.mutate({
      enabled: dnd.enabled,
      from: dnd.from,
      to: dnd.to,
      keepInbox: dnd.keepInbox,
      pushEnabled: true
    });
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
    setChannels({ site: true, vibrate: true, mail: false });
    setDnd({ enabled: true, from: '23:00', to: '08:30', keepInbox: true });
    writeString(StorageKeys.NumFont, 'mono');
    writeString(StorageKeys.HomePath, '/market');
    toast.toast('已恢复默认设置（服务端偏好需点「保存设置」）', 'info');
  };

  /** 订阅浏览器推送（走 usePwa：注册 SW → 申请权限 → pushManager.subscribe → 存服务端）。 */
  const subscribePush = async () => {
    const error = await pwa.subscribe();
    toast.toast(error ?? '已订阅浏览器推送（页面关闭后也能收到提醒）', error ? 'warn' : 'ok');
  };

  const unsubscribePush = async () => {
    const error = await pwa.unsubscribe();
    toast.toast(error ?? '已取消浏览器推送订阅', error ? 'warn' : 'ok');
  };

  const sendTestPush = async () => {
    const error = await pwa.sendTestNotification();
    toast.toast(error ?? '已发送一条测试通知（由浏览器本地发出）', error ? 'warn' : 'ok');
  };

  const { data: alerts } = useQuery({ queryKey: ['alerts'], queryFn: fetchAlertRules, staleTime: 60_000 });
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
                <span className={`tag ${pwa.state.permission === 'granted' ? 'tag-up' : 'tag-outline'}`}>
                  浏览器通知：{describePermission(pwa.state.permission)}
                </span>
                <span className={`tag ${pwa.state.subscribed ? 'tag-up' : 'tag-outline'}`}>
                  推送订阅：{pwa.state.subscribed ? `已订阅（${pwa.state.endpointHost ?? '—'}）` : '未订阅'}
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
                    站内通知中心（不依赖任何权限，始终可用）
                  </label>
                  <label className="check">
                    <input
                      type="checkbox"
                      checked={channels.vibrate ?? false}
                      onChange={(event) => setChannels({ ...channels, vibrate: event.target.checked })}
                    />
                    震动（移动端；iOS Safari 不支持）
                  </label>
                  <label className="check">
                    <input type="checkbox" checked={false} disabled />
                    邮件（未接入）
                  </label>
                </div>
                <span className="hint">
                  浏览器推送由下方的「安装与通知能力」区管理：它需要 Service Worker 与通知权限，
                  不能只用一个复选框表示。
                </span>
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

          {/* 安装与通知能力：状态全部来自浏览器与服务端的真实查询 */}
          <div className="card is-accent">
            <div className="card-head">
              <span className="card-title">安装与通知能力</span>
              <span className="card-sub">先注册 Service Worker，再订阅推送</span>
              <div className="card-tools">
                <button type="button" className="btn btn-sm btn-ghost" onClick={() => void pwa.refresh()}>
                  重新检测
                </button>
              </div>
            </div>
            <div className="card-body grid grid-2">
              <div className="col gap-3">
                <div className="row gap-2 wrap">
                  <span className={`tag ${pwa.state.secureContext ? 'tag-up' : 'tag-warn'}`}>
                    安全上下文：{pwa.state.secureContext ? '满足' : '不满足（需 HTTPS 或 localhost）'}
                  </span>
                  <span className={`tag ${pwa.state.serviceWorkerRegistered ? 'tag-up' : 'tag-outline'}`}>
                    Service Worker：{pwa.state.serviceWorkerRegistered ? '已注册' : '未注册'}
                  </span>
                  <span className={`tag ${pwa.state.subscribed ? 'tag-up' : 'tag-outline'}`}>
                    推送订阅：{pwa.state.subscribed ? '已订阅' : '未订阅'}
                  </span>
                </div>

                <div className="col gap-2">
                  {!pwa.state.serviceWorkerRegistered ? (
                    <button
                      type="button"
                      className="btn btn-outline"
                      disabled={pwa.busy || !pwa.state.secureContext}
                      onClick={async () => {
                        const error = await pwa.registerServiceWorker();
                        toast.toast(error ?? 'Service Worker 已注册（支持离线打开应用外壳）', error ? 'warn' : 'ok');
                      }}
                    >
                      注册 Service Worker
                    </button>
                  ) : null}

                  {pwa.state.serviceWorkerRegistered && !pwa.state.subscribed ? (
                    <button type="button" className="btn btn-primary" disabled={pwa.busy} onClick={() => void subscribePush()}>
                      订阅浏览器推送
                    </button>
                  ) : null}

                  {pwa.state.subscribed ? (
                    <div className="row gap-2 wrap">
                      <button type="button" className="btn btn-outline btn-sm" disabled={pwa.busy} onClick={() => void sendTestPush()}>
                        发送测试通知
                      </button>
                      <button type="button" className="btn btn-ghost btn-sm" disabled={pwa.busy} onClick={() => void unsubscribePush()}>
                        取消订阅
                      </button>
                    </div>
                  ) : null}
                </div>

                <div className="fold">
                  <div className="fold-head">
                    Android（Chrome / Edge）与 iOS（Safari）<span className="caret">▼</span>
                  </div>
                  <div className="fold-body hint">
                    <b>Android</b>：注册 Service Worker 并订阅后，页面关闭也能收到通知栏提醒；
                    Chrome 还会在满足安装条件时提供「添加到主屏幕」。
                    <br />
                    <b>iOS</b>：Web Push 要求先在 Safari 里「添加到主屏幕」，再从主屏图标启动页面订阅；
                    在 Safari 标签页里请求权限会被系统忽略。
                  </div>
                </div>
              </div>

              <div className="col gap-3">
                <div className="kpi">
                  <span className="kpi-label">实时通道（页面打开时）</span>
                  <span className="kpi-value is-sm">
                    {realtime.status === 'connected' ? 'SignalR 已连接' : 'SignalR 未连接'}
                  </span>
                  <span className="kpi-delta t-3">
                    页面内即时到达；关闭页面后由浏览器推送接力
                  </span>
                </div>
                <div className="legend-block">
                  <b>两条通道的分工</b>
                  <br />
                  SignalR 负责页面打开时的秒级到达（自选行情与提醒）；
                  浏览器推送（Web Push）负责页面关闭后的到达。
                  免打扰期间服务端只写站内通知、不发起推送，因此「不打扰」不等于「丢消息」。
                  <br />
                  <b>为什么需要 HTTPS</b>：Service Worker、Web Push、通知与震动 API 都只在安全上下文下可用；
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
              <span className="card-title">本地数据</span>
              <span className="card-sub">存在这台浏览器里</span>
            </div>
            <div className="card-body col gap-3">
              <div className="row-between">
                <span className="fs-12 t-2">自选股</span>
                <span className="mono fs-12">{watchlist.codes.length} 只</span>
              </div>
              <div className="row-between">
                <span className="fs-12 t-2">提醒规则</span>
                <span className="mono fs-12">{alerts?.total ?? 0} 条</span>
              </div>
              <div className="row-between">
                <span className="fs-12 t-2">接口缓存</span>
                <span className="mono fs-12">{cacheLabel}</span>
              </div>
              <div className="hint" style={{ lineHeight: 1.7 }}>
                本应用不需要登录，自选股、提醒规则与接口缓存都保存在这台浏览器里：
                换浏览器或清除站点数据会丢失，也不会在多设备之间同步。
              </div>
              <button
                type="button"
                className="btn btn-outline btn-block"
                onClick={() => {
                  void cacheClear().then(() => {
                    setCacheLabel('已清除');
                    toast.toast('本地接口缓存已清除', 'info');
                  });
                }}
              >
                清除本地接口缓存
              </button>
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
                  {watchlist.codes.length} 只
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
        <b>免打扰与推送开关存在服务端</b>：浏览器关掉时前端无从判断「现在该不该响」，
        因此这两个开关必须在服务端执行才有意义（页面上点「保存设置」即写入服务端）。
        <br />
        「复权方式」与「MACD 参数」记录的是阅读偏好：服务端已按前复权与 12/26/9 统一计算，
        本地不做第二套计算，避免同一份指标出现两个口径。
      </div>
    </>
  );
}

/** 通知权限的中文描述。 */
function describePermission(permission: string): string {
  switch (permission) {
    case 'granted':
      return '已授权';
    case 'denied':
      return '已拒绝';
    case 'unsupported':
      return '环境不支持';
    default:
      return '未授权';
  }
}
