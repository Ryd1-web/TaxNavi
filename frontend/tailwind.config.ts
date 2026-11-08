import type { Config } from 'tailwindcss';

// Semantic tokens resolve to CSS variables (see globals.css) so light and dark themes share one set of classes.
const token = (name: string) => `rgb(var(--${name}) / <alpha-value>)`;

const config: Config = {
  content: ['./src/**/*.{js,ts,jsx,tsx,mdx}'],
  darkMode: 'class',
  theme: {
    extend: {
      colors: {
        // Primary brand: deep "naira" green — trust, money, growth.
        brand: {
          50: '#ecf8f3',
          100: '#d1efe2',
          200: '#a6dfc7',
          300: '#6fc8a6',
          400: '#3aa982',
          500: '#1a8c67',
          600: '#0f7b5a',
          700: '#0b6b4f',
          800: '#0a5440',
          900: '#083f31',
          950: '#04261d',
        },
        // Premium accent, carried over from the original TaxNavi amber.
        gold: {
          50: '#fdf8ec',
          100: '#f9edcc',
          200: '#f3dc9c',
          300: '#ecc976',
          400: '#e2b55a',
          500: '#d6a446',
          600: '#b8862c',
          700: '#946823',
          800: '#6f4e1c',
        },
        ink: {
          DEFAULT: token('ink'),
          muted: token('ink-muted'),
          subtle: token('ink-subtle'),
        },
        surface: {
          DEFAULT: token('surface'),
          subtle: token('surface-subtle'),
          muted: token('surface-muted'),
        },
        line: {
          DEFAULT: token('line'),
          strong: token('line-strong'),
        },
        // Always-dark panel (premium card, trust band, tooltips) in both themes.
        night: token('night'),
        // Validated chart palette per theme (see components/tax/IncomeBreakdownBar).
        chart: {
          sheltered: token('chart-sheltered'),
          kept: token('chart-kept'),
          tax: token('chart-tax'),
        },
      },
      fontFamily: {
        sans: [
          'Inter',
          'ui-sans-serif',
          'system-ui',
          '-apple-system',
          'Segoe UI',
          'Roboto',
          'Helvetica Neue',
          'Arial',
          'sans-serif',
        ],
      },
      fontSize: {
        '2xs': ['0.6875rem', { lineHeight: '1rem' }],
      },
      boxShadow: {
        card: '0 1px 2px 0 rgb(15 23 42 / 0.04), 0 1px 3px 0 rgb(15 23 42 / 0.04)',
        raised: '0 1px 2px rgb(15 23 42 / 0.04), 0 8px 24px -6px rgb(15 23 42 / 0.10)',
        focus: '0 0 0 3px rgb(15 123 90 / 0.25)',
      },
      borderRadius: {
        xl: '0.75rem',
        '2xl': '1rem',
      },
      keyframes: {
        'fade-in': { from: { opacity: '0', transform: 'translateY(4px)' }, to: { opacity: '1', transform: 'none' } },
        shimmer: { '100%': { transform: 'translateX(100%)' } },
      },
      animation: {
        'fade-in': 'fade-in 200ms ease-out both',
      },
    },
  },
  plugins: [],
};

export default config;
