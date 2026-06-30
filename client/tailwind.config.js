/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  darkMode: 'class',
  theme: {
    extend: {
      // ═══════════════════════════════════════════
      // Apple Esports — Gaming Café Design System
      // Colors extracted from UI prototype
      // ═══════════════════════════════════════════
      colors: {
        // Background hierarchy
        bg: {
          DEFAULT: '#0a0d14',
          2: '#0f1420',
          3: '#141926',
          4: '#1a2035',
        },
        // Borders
        border: {
          DEFAULT: '#1e2840',
          2: '#253050',
        },
        // Accent (primary brand color — red)
        accent: {
          DEFAULT: '#dc2626',
          dark: '#b91c1c',
          dim: 'rgba(220, 38, 38, 0.08)',
          glow: 'rgba(220, 38, 38, 0.15)',
        },
        // Status colors from SOP PC states
        neon: {
          blue: '#4da6ff',
          'blue-dim': 'rgba(77, 166, 255, 0.08)',
          orange: '#ff8c42',
          'orange-dim': 'rgba(255, 140, 66, 0.08)',
          red: '#ff4d6d',
          'red-dim': 'rgba(255, 77, 109, 0.08)',
          purple: '#9b72ff',
          'purple-dim': 'rgba(155, 114, 255, 0.08)',
          green: '#22d3a6',
          'green-dim': 'rgba(34, 211, 166, 0.08)',
        },
        // PC State Colors (SOP §7.1)
        pc: {
          idle: '#4da6ff', // blue
          active: '#22d3a6', // green
          reserved: '#eab308', // yellow
          awaiting: '#ff8c42', // orange
          offline: '#dc2626', // red
        },
        // Text hierarchy
        text: {
          DEFAULT: '#e8eaf0',
          2: '#8892a8',
          3: '#4a5568',
        },
      },
      fontFamily: {
        heading: ['Rajdhani', 'sans-serif'],
        mono: ['DM Mono', 'monospace'],
        body: ['Inter', 'sans-serif'],
      },
      borderRadius: {
        sm: '8px',
        md: '12px',
        lg: '16px',
      },
      animation: {
        'pulse-slow': 'pulse 3s cubic-bezier(0.4, 0, 0.6, 1) infinite',
        'blink': 'blink 1.4s infinite',
        'await-pulse': 'awaitPulse 2s infinite',
      },
      keyframes: {
        blink: {
          '0%, 100%': { opacity: 1 },
          '50%': { opacity: 0.3 },
        },
        awaitPulse: {
          '0%, 100%': { borderColor: '#ff8c42' },
          '50%': { borderColor: '#ff8c42', boxShadow: '0 0 8px rgba(255, 140, 66, 0.08)' },
        },
      },
    },
  },
  plugins: [
    require('@tailwindcss/forms'),
  ],
}
