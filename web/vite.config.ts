import { fileURLToPath, URL } from 'node:url';
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// 原型目录：样式（tokens/tailwind/components）与图表基础（charts.js）的唯一来源。
const protoDir = fileURLToPath(new URL('../design/web/_shared', import.meta.url));

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
      // 直接引原型 CSS，避免复制产生的视觉漂移（实施计划 §2 决策 3）
      '@proto': protoDir
    }
  },
  server: {
    port: 5173,
    strictPort: true,
    fs: {
      // 允许读取 web/ 之外的 design/web/_shared
      allow: [fileURLToPath(new URL('..', import.meta.url))]
    },
    proxy: {
      '/api': {
        target: 'http://localhost:5180',
        changeOrigin: true
      },
      '/hubs': {
        target: 'http://localhost:5180',
        changeOrigin: true,
        ws: true
      }
    }
  }
});
