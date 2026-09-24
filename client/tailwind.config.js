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
        // Brightened deliberately. These are read from behind the counter, several metres away, at
        // a glance - the previous shades were picked to sit quietly in a dark theme, which is the
        // opposite of what this particular grid is for.
        //
        // maintenance is its own token now. It used to borrow pc.offline, so a machine taken out of
        // service for a fortnight looked identical to one that had simply lost power.
        //
        // awaitingsetup is its own token too, for the same reason: a PC record that has never
        // been claimed by a physical machine used to render Idle (bright blue, "FREE") - visually
        // identical to a real, working, available seat - because the endpoint behind the Settings
        // "Add PC" form wrote Idle straight into a brand-new row. Grey rather than another bright
        // hue on purpose: this state is not something to walk over and use, unlike every other
        // color in this block.
        pc: {
          idle: '#00baff',        // bright blue   - free
          active: '#00e676',      // bright green  - playing
          reserved: '#5b21b6',    // dark purple   - booked
          awaiting: '#ffffff',    // white         - waiting to be billed
          offline: '#ff2d55',     // bright red    - shut down
          maintenance: '#ffd400', // bright yellow - out of service
          awaitingsetup: '#94a3b8', // grey        - never set up, not bookable
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
