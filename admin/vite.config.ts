import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    // Same-origin API: the session cookie works without CORS. Production uses a reverse proxy the same way.
    proxy: {
      '/api': {
        target: 'http://localhost:5300',
        rewrite: (path) => path.replace(/^\/api/, ''),
      },
    },
  },
})
