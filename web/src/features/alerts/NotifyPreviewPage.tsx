import { useState } from 'react';
import { Link } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { EmptyState } from '@/components/ui/States';
import { useToast } from '@/providers/ToastProvider';
import { fetchNotifications, type Notification } from '@/features/alerts/api';

/**
 * 提醒形态预览。
 *
 * 结构与 `design/app/notify-preview.html` 对应：站内通知 → 浏览器通知栏 → 锁屏通知 → 震动 →
 * 明确不做的部分。
 *
 * <b>两处与原型不同的处理（页面都写明了）</b>：
 * 1. 预览内容优先用<b>真实的最近一条通知</b>；没有通知时才给一条示例并标注「示例」，
 *    避免把演示文案当成真实提醒；
 * 2. 浏览器通知栏与锁屏通知在原型里是纯样式示意，本实现同样只是样式示意，但会显示
 *    浏览器的真实通知权限状态，并指向设置页的推送订阅入口（订阅后页面关闭也能收到通知）。
 */
export function NotifyPreviewPage() {
  const toast = useToast();
  const [permission, setPermission] = useState<string>(() =>
    typeof window !== 'undefined' && 'Notification' in window ? Notification.permission : 'unsupported'
  );

  const { data } = useQuery({
    queryKey: ['notifications', false],
    queryFn: () => fetchNotifications(false, 5),
    staleTime: 60_000
  });

  const real: Notification | undefined = data?.items[0];

  /** 预览用的内容：真实通知优先，否则退回示例（并标注）。 */
  const preview = real
    ? { title: real.title, body: real.body.replace(/\n/g, ' · '), time: real.createdAt.slice(11, 16), isSample: false }
    : { title: '宁德时代 上穿 265.00 元', body: '现价 268.42，+4.86% · 主力净流入 18.62 亿', time: '14:32', isSample: true };

  const tryVibrate = () => {
    if (typeof navigator !== 'undefined' && 'vibrate' in navigator) {
      const supported = navigator.vibrate?.(200) ?? false;
      toast.toast(supported ? '已触发一次 200ms 震动' : '当前设备不支持震动', supported ? 'ok' : 'warn');
      return;
    }

    toast.toast('当前环境不支持 Web Vibration API（iOS Safari 不支持）', 'warn');
  };

  const tryNotify = async () => {
    if (!('Notification' in window)) {
      toast.toast('当前环境不支持浏览器通知', 'warn');
      return;
    }

    if (Notification.permission !== 'granted') {
      const result = await Notification.requestPermission();
      setPermission(result);
      if (result !== 'granted') {
        toast.toast('未获得通知权限，已改为站内提示', 'warn');
        return;
      }
    }

    try {
      new Notification('股析 SA · 测试通知', { body: preview.body });
      toast.toast('已发送一条浏览器通知', 'ok');
    } catch {
      toast.toast('浏览器拒绝了通知，已改为站内提示', 'warn');
    }
  };

  return (
    <>
      <div className="sa-pagehead">
        <div>
          <div className="breadcrumb">
            <Link to="/notifications">通知中心</Link>
            <span className="sep">/</span>
            <span>提醒形态预览</span>
          </div>
          <h1>提醒形态预览</h1>
          <div className="sub">
            站内通知 / 浏览器通知栏 / 锁屏通知三种样式对照 + 震动说明 · 用于确认提醒在手机上的真实观感
          </div>
        </div>
        <div className="sa-pagehead-actions">
          <span className={`tag ${permission === 'granted' ? 'tag-up' : 'tag-outline'}`}>
            通知权限：{permission === 'granted' ? '已授权' : permission === 'denied' ? '已拒绝' : permission === 'unsupported' ? '环境不支持' : '未授权'}
          </span>
        </div>
      </div>
      <div className="grid grid-2">
        <div className="col gap-3">
          {/* ① 站内通知 */}
          <div className="card">
            <div className="card-head">
              <span className="card-title">① 站内通知（通知中心）</span>
              <span className="card-sub">{real ? '最近一条真实通知' : '示例内容'}</span>
            </div>
            <div className="card-body">
              <div className="mock-banner" style={{ borderLeft: '3px solid var(--up)' }}>
                <div className="grow">
                  <div className="row gap-2">
                    <span className="fs-13 fw-600">{preview.title}</span>
                    <span className="mono fs-10 t-3" style={{ marginLeft: 'auto' }}>
                      {preview.time}
                    </span>
                  </div>
                  <div className="fs-11 t-2" style={{ marginTop: 4, lineHeight: 1.7 }}>
                    {preview.body}
                  </div>
                  <div className="row gap-2 mt-2">
                    <span className="tag tag-outline">提醒触发</span>
                    <span className="fs-10 t-3">
                      {real?.code ? `关联标的 ${real.code}` : '示例'}
                    </span>
                  </div>
                </div>
              </div>
              <div className="chart-note">
                站内通知在应用内展示，有未读角标，不依赖任何系统权限，所有设备都可用。
                {data?.unread ? `当前有 ${data.unread} 条未读。` : ''}
              </div>
            </div>
          </div>

          {/* ② 浏览器通知栏 */}
          <div className="card">
            <div className="card-head">
              <span className="card-title">② 浏览器通知栏</span>
              <span className="card-sub">样式示意</span>
            </div>
            <div className="card-body">
              <div className="mock-banner">
                <div className="app-ico">SA</div>
                <div className="grow">
                  <div className="row gap-2">
                    <span className="fs-12 fw-600">股析 SA</span>
                    <span className="fs-10 t-3" style={{ marginLeft: 'auto' }}>
                      现在
                    </span>
                  </div>
                  <div className="fs-12 fw-600" style={{ marginTop: 2 }}>
                    {preview.title}
                  </div>
                  <div className="fs-11 t-2" style={{ lineHeight: 1.6 }}>
                    {preview.body}
                  </div>
                </div>
              </div>
              {/* chart-note 是 display:flex 的短标注容器：含行内标记的长文本必须包在一个子元素里，
                  否则每段行内文本都会变成一个 flex item，间距与换行会错乱（实测） */}
              <div className="chart-note">
                <span>
                  需要 HTTPS + 通知权限授权。推送通道已在<b>设置页</b>提供（注册 Service Worker → 申请权限 → 订阅），
                  订阅后页面关闭也能收到通知；未订阅时只有页面打开期间（经 SignalR）会提示。
                  <br />
                  iOS 还必须先在 Safari 中「添加到主屏幕」并从主屏图标启动，才能申请通知权限。
                </span>
              </div>
              <div className="row gap-2 mt-3">
                <button
                  type="button"
                  className="btn btn-outline btn-sm"
                  onClick={() => void tryNotify()}
                >
                  发一条测试通知
                </button>
              </div>
            </div>
          </div>
        </div>
        <div className="col gap-3">
          {/* ③ 锁屏通知 */}
          <div className="card">
            <div className="card-head">
              <span className="card-title">③ 锁屏通知</span>
              <span className="card-sub">样式示意</span>
            </div>
            <div className="card-body">
              <div className="mock-lock">
                <div className="row-between" style={{ marginBottom: 10 }}>
                  <span className="mono fs-11 t-3">{preview.time}</span>
                  <span className="fs-11 t-3">{new Date().toLocaleDateString('zh-CN')}</span>
                </div>
                <div className="mock-banner" style={{ background: 'rgba(24,33,49,.7)' }}>
                  <div className="app-ico">SA</div>
                  <div className="grow">
                    <div className="row gap-2">
                      <span className="fs-12 fw-600">股析 SA</span>
                      <span className="fs-10 t-3" style={{ marginLeft: 'auto' }}>
                        现在
                      </span>
                    </div>
                    <div className="fs-12 fw-600" style={{ marginTop: 2 }}>
                      {preview.title}
                    </div>
                    <div className="fs-11 t-2" style={{ lineHeight: 1.6 }}>
                      {preview.body}
                    </div>
                  </div>
                </div>
                <div className="fs-11 t-3 mt-3" style={{ textAlign: 'center' }}>
                  上滑查看 · 长按管理通知
                </div>
              </div>
              <div className="chart-note">
                锁屏样式由系统决定，应用只能提供标题与正文；不同厂商的 Android 皮肤差异较大。
              </div>
            </div>
          </div>

          {/* ④ 震动 */}
          <div className="card">
            <div className="card-head">
              <span className="card-title">④ 震动</span>
              <span className="card-sub">Web Vibration API</span>
            </div>
            <div className="card-body">
              <div className="row gap-3">
                <span className="vibrate-demo">
                  <i /><i /><i /><i /><i />
                </span>
                <div className="grow">
                  <div className="fs-12 fw-600">触发时短震动提示</div>
                  <div className="hint" style={{ lineHeight: 1.7 }}>
                    使用 Web Vibration API，需安全上下文与用户手势触发。
                  </div>
                </div>
              </div>
              <div className="row gap-2 mt-3">
                <button type="button" className="btn btn-outline btn-sm" onClick={tryVibrate}>
                  试一下震动
                </button>
              </div>
              <div className="chart-note">
                <span>
                  <b>iOS 限制</b>：Safari 不支持 Web Vibration API，iPhone 上无法通过网页触发震动；
                  Android（Chrome / Edge）支持。
                </span>
              </div>
            </div>
          </div>

          {/* ⑤ 明确不做 */}
          <div className="card">
            <div className="card-head">
              <span className="card-title">⑤ 明确不做</span>
              <span className="card-sub">避免给出做不到的预期</span>
            </div>
            <div className="card-body col gap-2 fs-12 t-2">
              <div>· 桌面小组件：需要原生壳或第三方 App，本项目是响应式 Web。</div>
              <div>· 邮件通知：需要邮件服务与模板，未接入。</div>
              <div className="legend-block mt-3">
                以上两项在设置页都标注为「未接入」或「未注册」，不显示为可用状态——
                给出做不到的开关比不给开关更容易误导。
              </div>
            </div>
          </div>
        </div>
      </div>

      {!real ? (
        <div className="mt-4">
          <EmptyState
            title="当前没有真实通知可预览"
            hint="上面展示的是示例内容。新建一条提醒规则并等它触发后，这里会显示最近一条真实通知。"
            action={
              <Link className="btn btn-sm btn-primary" to="/alerts">
                去建提醒规则
              </Link>
            }
          />
        </div>
      ) : null}
    </>
  );
}
