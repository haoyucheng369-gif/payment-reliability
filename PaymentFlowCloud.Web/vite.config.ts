import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      // 本地前端通过 Vite proxy 调 API，避免开发阶段额外配置 CORS。
      '/payments': 'http://localhost:5147',
    },
  },
})
