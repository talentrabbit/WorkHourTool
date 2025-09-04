import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue()],
  server: {
    // bind to all interfaces so other machines can reach the dev server
    host: '0.0.0.0',
    port: 5173,
    strictPort: true,
    // HMR should tell clients to connect to the server hostname
    hmr: {
      host: 'shai571a',
      protocol: 'ws',
      port: 5173
    },
    // proxy API requests to the backend so axios('/api/...') works in dev
    proxy: {
      '/api': {
        target: 'http://shai571a:5063',
        changeOrigin: true,
        secure: false
      }
    }
  }
})
