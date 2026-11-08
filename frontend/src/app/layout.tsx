import type { Metadata, Viewport } from 'next';
import { Suspense } from 'react';
import NavigationProgress from '@/components/layout/NavigationProgress';
import { AuthProvider } from '@/context/AuthContext';
import { ThemeProvider, themeInitScript } from '@/context/ThemeContext';
import '@/styles/globals.css';

export const metadata: Metadata = {
  title: {
    default: 'TaxNavi — Understand your Nigerian taxes',
    template: '%s · TaxNavi',
  },
  description:
    'Calculate your Nigerian taxes, discover eligible deductions and stay compliant. Built for employees, freelancers and SMEs under the Nigeria Tax Act 2025.',
};

export const viewport: Viewport = {
  width: 'device-width',
  initialScale: 1,
  themeColor: [
    { media: '(prefers-color-scheme: light)', color: '#ffffff' },
    { media: '(prefers-color-scheme: dark)', color: '#0b1220' },
  ],
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    // The init script sets the theme class before hydration, so React must not flag the difference.
    <html lang="en-NG" suppressHydrationWarning>
      <head>
        <script dangerouslySetInnerHTML={{ __html: themeInitScript }} />
      </head>
      <body>
        <ThemeProvider>
          <Suspense fallback={null}>
            <NavigationProgress />
          </Suspense>
          <AuthProvider>{children}</AuthProvider>
        </ThemeProvider>
      </body>
    </html>
  );
}
