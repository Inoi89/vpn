import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': {
        target: process.env.VITE_PROXY_TARGET ?? 'https://localhost:7001',
        changeOrigin: true,
        secure: false,
      },
      '/hubs': {
        target: process.env.VITE_PROXY_TARGET ?? 'https://localhost:7001',
        changeOrigin: true,
        secure: false,
        ws: true,
      },
    },
  },
})
