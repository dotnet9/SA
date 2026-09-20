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

import { router } from '@/app/router';
import { queryClient } from '@/lib/query';
import { AuthProvider } from '@/providers/AuthProvider';
import { ThemeProvider } from '@/providers/ThemeProvider';
import { ToastProvider } from '@/providers/ToastProvider';

const host = document.getElementById('root');
if (!host) {
  throw new Error('未找到挂载点 #root');
}

createRoot(host).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <ThemeProvider>
        <ToastProvider>
          <AuthProvider>
            <RouterProvider router={router} />
          </AuthProvider>
        </ToastProvider>
      </ThemeProvider>
    </QueryClientProvider>
  </StrictMode>
);
