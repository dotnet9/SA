import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { QueryClientProvider } from '@tanstack/react-query';
import { RouterProvider } from 'react-router';

// 引入顺序必须与原型 <head> 一致：tokens → tailwind → components（实施计划 §3.4 / §5.9）
import '@proto/tokens.css';
import '@proto/tailwind.css';
import '@proto/components.css';

// 应用补充样式放在最后，便于覆盖
import '@/styles/app.css';
// 移动端外壳样式（只补桌面壳没有的部分：粘性页头、底部 Tab、安全区）
import '@/styles/mobile.css';

import { router } from '@/app/router';
import { queryClient } from '@/lib/query';
import { AuthProvider } from '@/providers/AuthProvider';
import { RealtimeProvider } from '@/providers/RealtimeProvider';
import { ThemeProvider } from '@/providers/ThemeProvider';
import { ToastProvider } from '@/providers/ToastProvider';

const host = document.getElementById('root');
if (!host) {
  throw new Error('未找到挂载点 #root');
}

/**
 * 注册 Service Worker。
 *
 * 只在生产构建里注册：开发时 Vite 的资源名不带 hash，
 * Service Worker 的「缓存优先」会把旧模块缓存住，导致改了代码刷新不生效。
 * 生产构建下资源名带 hash，缓存是安全的，同时换来离线可用与 Web Push 接收能力。
 */
if (import.meta.env.PROD && 'serviceWorker' in navigator && window.isSecureContext) {
  window.addEventListener('load', () => {
    void navigator.serviceWorker.register('/sw.js', { scope: '/' });
  });
}

createRoot(host).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <ThemeProvider>
        <ToastProvider>
          <AuthProvider>
            {/* 实时行情是会话级的：放在 AuthProvider 内以便读取登录态，放在路由外以免切页断开 */}
            <RealtimeProvider>
              <RouterProvider router={router} />
            </RealtimeProvider>
          </AuthProvider>
        </ToastProvider>
      </ThemeProvider>
    </QueryClientProvider>
  </StrictMode>
);
