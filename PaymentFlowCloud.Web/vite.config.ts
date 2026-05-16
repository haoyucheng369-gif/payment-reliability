import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    proxy: {
      // 本地开发时通过 Vite 代理 API，避免额外配置 CORS。
      '/payments': 'http://localhost:5147',
    },
  },
})
