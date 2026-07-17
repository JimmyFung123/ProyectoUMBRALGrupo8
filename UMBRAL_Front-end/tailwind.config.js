/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{js,ts,jsx,tsx}'],
  theme: {
    extend: {
      fontFamily: {
        sans: ['Inter', 'system-ui', '-apple-system', 'Segoe UI', 'Roboto', 'sans-serif'],
        mono: ['JetBrains Mono', 'ui-monospace', 'Consolas', 'monospace'],
      },
      colors: {
        // ── Surfaces ────────────────────────────────────────────────────────
        surface: {
          base:    '#f8fafc', // page background
          subtle:  '#f1f5f9', // muted panel
          card:    '#ffffff',
          inset:   '#fafbff', // very faint tinted panel inside a card
        },
        // ── Text ────────────────────────────────────────────────────────────
        ink: {
          DEFAULT: '#0f172a', // primary text
          soft:    '#334155',
          muted:   '#64748b',
          subtle:  '#94a3b8',
          inverse: '#f8fafc',
        },
        // ── Brand (indigo) ──────────────────────────────────────────────────
        brand: {
          50:  '#eef2ff',
          100: '#e0e7ff',
          200: '#c7d2fe',
          500: '#6366f1',
          600: '#4f46e5',
          700: '#4338ca',
        },
        // ── Semantic ────────────────────────────────────────────────────────
        success: { 50: '#ecfdf5', 500: '#10b981', 600: '#059669', 700: '#047857' },
        warning: { 50: '#fffbeb', 500: '#f59e0b', 600: '#d97706', 700: '#b45309' },
        danger:  { 50: '#fff1f2', 500: '#f43f5e', 600: '#e11d48', 700: '#be123c' },
        info:    { 50: '#f0f9ff', 500: '#0ea5e9', 600: '#0284c7', 700: '#0369a1' },
      },
      borderRadius: {
        DEFAULT: '6px',
        md: '8px',
        lg: '12px',
        xl: '16px',
      },
      boxShadow: {
        card:     '0 1px 2px rgba(15,23,42,0.04), 0 1px 3px rgba(15,23,42,0.06)',
        elevated: '0 4px 12px rgba(15,23,42,0.08), 0 2px 4px rgba(15,23,42,0.04)',
        floating: '0 12px 32px rgba(15,23,42,0.18)',
      },
      ringWidth: { DEFAULT: '2px' },
    },
  },
  plugins: [],
};
