import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';

// 引入顺序必须与原型 <head> 一致：tokens → tailwind → components（实施计划 §3.4 / §5.9）
import '@proto/tokens.css';
import '@proto/tailwind.css';
import '@proto/components.css';

// 应用补充样式放在最后，便于覆盖
import '@/styles/app.css';

import { App } from '@/App';

const host = document.getElementById('root');
if (!host) {
  throw new Error('未找到挂载点 #root');
}

createRoot(host).render(
  <StrictMode>
    <App />
  </StrictMode>
);
