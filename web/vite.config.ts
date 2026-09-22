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
  },

  /*
   * 预览（`npm run preview`）服务构建产物，用来在本机复现「线上单域名」形态：
   * 静态文件 + /api 与 /hubs 反代到后端，等价于 nginx 的三个 location。
   *
   * Vite 4+ 的 preview.proxy 默认继承 server.proxy，这里仍显式写一遍——
   * 不依赖版本默认行为，也让「预览与开发走同一套代理」在配置里看得见。
   */
  preview: {
    port: 5173,
    strictPort: true,
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
