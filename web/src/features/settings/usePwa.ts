import { useCallback, useEffect, useState } from 'react';
import { apiDelete, apiGet, apiPost } from '@/lib/api';

/**
 * PWA 能力与 Web Push 订阅。
 *
 * 三条设计原则（都是为了让界面显示的状态与真实情况一致）：
 * 1. **不猜**：Service Worker 是否注册、推送是否已订阅，都从浏览器实际查询；
 * 2. **可解释**：不支持时报出具体原因（非安全上下文 / 不支持 Notification / 被拒绝），
 *    而不是笼统地显示「不可用」；
 * 3. **可恢复**：订阅与取消都能重试，失败时返回错误文案而不是静默。
 */

/** VAPID 公钥响应。 */
interface PushKeyResponse {
  publicKey: string;
}

/** 订阅状态。 */
export interface PushState {
  /** 浏览器是否支持 Service Worker。 */
  supportsServiceWorker: boolean;
  /** 是否处于安全上下文（HTTPS 或 localhost）。 */
  secureContext: boolean;
  /** Service Worker 是否已注册。 */
  serviceWorkerRegistered: boolean;
  /** 通知权限：granted / denied / default / unsupported。 */
  permission: string;
  /** 当前是否已向服务端订阅推送。 */
  subscribed: boolean;
  /** 端点的展示用主机名（已订阅时）。 */
  endpointHost: string | null;
}

/**
 * 把 base64url 转成 Uint8Array（pushManager.subscribe 要求二进制密钥）。
 *
 * 显式基于 ArrayBuffer 构造：TS 5.7+ 把 Uint8Array 泛型化后，
 * `new Uint8Array(length)` 的类型是 `Uint8Array<ArrayBufferLike>`，不能直接当作 BufferSource 传。
 */
function urlBase64ToUint8Array(base64Url: string): Uint8Array<ArrayBuffer> {
  const padding = '='.repeat((4 - (base64Url.length % 4)) % 4);
  const base64 = (base64Url + padding).replace(/-/g, '+').replace(/_/g, '/');
  const raw = window.atob(base64);
  const output = new Uint8Array(new ArrayBuffer(raw.length));

  for (let i = 0; i < raw.length; i++) {
    output[i] = raw.charCodeAt(i);
  }

  return output;
}

/**
 * 注册 Service Worker 并管理推送订阅。
 */
export function usePwa() {
  const [state, setState] = useState<PushState>({
    supportsServiceWorker: typeof navigator !== 'undefined' && 'serviceWorker' in navigator,
    secureContext: typeof window !== 'undefined' ? window.isSecureContext : false,
    serviceWorkerRegistered: false,
    permission: typeof window !== 'undefined' && 'Notification' in window ? Notification.permission : 'unsupported',
    subscribed: false,
    endpointHost: null
  });

  const [busy, setBusy] = useState(false);

  /** 从浏览器与服务端查询真实状态。 */
  const refresh = useCallback(async () => {
    const supportsSw = typeof navigator !== 'undefined' && 'serviceWorker' in navigator;
    const permission = typeof window !== 'undefined' && 'Notification' in window ? Notification.permission : 'unsupported';

    let registered = false;
    let subscribed = false;
    let host: string | null = null;

    if (supportsSw && window.isSecureContext) {
      const registration = await navigator.serviceWorker.getRegistration('/');
      registered = Boolean(registration);

      if (registration && permission === 'granted') {
        const subscription = await registration.pushManager.getSubscription();
        subscribed = Boolean(subscription);
        host = subscription ? safeHost(subscription.endpoint) : null;
      }
    }

    setState({
      supportsServiceWorker: supportsSw,
      secureContext: window.isSecureContext,
      serviceWorkerRegistered: registered,
      permission,
      subscribed,
      endpointHost: host
    });
  }, []);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  /**
   * 注册 Service Worker。
   *
   * 只在安全上下文下注册：Service Worker 在 http 的非 localhost 下会被浏览器直接拒绝，
   * 提前判断可以给出「需要 HTTPS」而不是一个含糊的注册失败。
   */
  const registerServiceWorker = useCallback(async (): Promise<string | null> => {
    if (!state.supportsServiceWorker) {
      return '当前浏览器不支持 Service Worker';
    }

    if (!window.isSecureContext) {
      return '需要 HTTPS 或 localhost 才能注册 Service Worker';
    }

    try {
      await navigator.serviceWorker.register('/sw.js', { scope: '/' });
      await refresh();
      return null;
    } catch (error) {
      return error instanceof Error ? error.message : 'Service Worker 注册失败';
    }
  }, [state.supportsServiceWorker, refresh]);

  /**
   * 订阅浏览器推送。
   */
  const subscribe = useCallback(async (): Promise<string | null> => {
    if (!state.supportsServiceWorker) {
      return '当前浏览器不支持 Service Worker，无法订阅推送';
    }

    if (!window.isSecureContext) {
      return '需要 HTTPS 或 localhost 才能订阅推送';
    }

    if (!('Notification' in window)) {
      return '当前浏览器不支持通知 API';
    }

    setBusy(true);
    try {
      const registrationError = await registerServiceWorker();
      if (registrationError) {
        return registrationError;
      }

      const permission = await Notification.requestPermission();
      if (permission !== 'granted') {
        return '未获得通知权限（可在浏览器地址栏的站点设置里重新允许）';
      }

      const registration = await navigator.serviceWorker.getRegistration('/');
      if (!registration) {
        return 'Service Worker 尚未就绪，请稍后重试';
      }

      const key = await apiGet<PushKeyResponse>('/api/notifications/push/key');

      const subscription = await registration.pushManager.subscribe({
        // 浏览器要求订阅时必须声明 userVisibleOnly
        userVisibleOnly: true,
        applicationServerKey: urlBase64ToUint8Array(key.publicKey)
      });

      const json = subscription.toJSON();
      await apiPost<number>('/api/notifications/push/subscribe', {
        endpoint: subscription.endpoint,
        keys: { p256dh: json.keys?.p256dh ?? null, auth: json.keys?.auth ?? null }
      });

      await refresh();
      return null;
    } catch (error) {
      return error instanceof Error ? error.message : '订阅推送失败';
    } finally {
      setBusy(false);
    }
  }, [state.supportsServiceWorker, registerServiceWorker, refresh]);

  /** 取消订阅（同时从服务端删除该端点）。 */
  const unsubscribe = useCallback(async (): Promise<string | null> => {
    setBusy(true);
    try {
      const registration = await navigator.serviceWorker.getRegistration('/');
      const subscription = await registration?.pushManager.getSubscription();

      if (subscription) {
        await apiDelete<number>('/api/notifications/push/subscribe', { endpoint: subscription.endpoint });
        await subscription.unsubscribe();
      }

      await refresh();
      return null;
    } catch (error) {
      return error instanceof Error ? error.message : '取消订阅失败';
    } finally {
      setBusy(false);
    }
  }, [refresh]);

  /** 发一条本地测试通知（验证浏览器侧链路，不经过服务端）。 */
  const sendTestNotification = useCallback(async (): Promise<string | null> => {
    if (!('Notification' in window)) {
      return '当前浏览器不支持通知 API';
    }

    if (Notification.permission !== 'granted') {
      return '尚未获得通知权限';
    }

    const registration = await navigator.serviceWorker.getRegistration('/');

    // 已注册 SW 时走 SW 通知（样式与真实推送一致）；否则退回页面级通知
    if (registration) {
      await registration.showNotification('股析 SA · 测试通知', {
        body: '如果你看到这条通知，说明浏览器推送链路可用。',
        icon: '/icon-192.png'
      });
      return null;
    }

    new Notification('股析 SA · 测试通知', { body: '页面级通知（尚未注册 Service Worker）。' });
    return null;
  }, []);

  return { state, busy, refresh, registerServiceWorker, subscribe, unsubscribe, sendTestNotification };
}

/** 取端点主机名（端点是长 URL，界面只需要展示来源）。 */
function safeHost(endpoint: string): string {
  try {
    return new URL(endpoint).host;
  } catch {
    return '未知端点';
  }
}
