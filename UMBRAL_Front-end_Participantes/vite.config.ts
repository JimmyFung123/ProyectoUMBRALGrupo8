import { defineConfig } from 'vite'
import react, { reactCompilerPreset } from '@vitejs/plugin-react'
import babel from '@rolldown/plugin-babel'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
    babel({ presets: [reactCompilerPreset()] })
  ],
  server: {
    port: 5174,
    host: true,
    // Tunnel-friendly: serve everything from a single origin.
    // /api      → SessionService
    // /team-api → TeamService (rewritten to /api so the back doesn't need to change)
    proxy: {
      '/api': {
        target: 'http://localhost:5092',
        changeOrigin: true,
      },
      '/team-api': {
        target: 'http://localhost:5095',
        changeOrigin: true,
        rewrite: (path: string) => path.replace(/^\/team-api/, '/api'),
      },
    },
    // Allow cloudflared / ngrok tunnel hosts
    allowedHosts: true,
  },
})
