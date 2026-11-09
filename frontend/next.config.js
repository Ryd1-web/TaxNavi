/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
  // The Docker image sets NEXT_OUTPUT=standalone; other builds (Netlify, local) use the default output.
  output: process.env.NEXT_OUTPUT === 'standalone' ? 'standalone' : undefined,
  // Lets CI or a second build run without touching the dev server's .next folder.
  distDir: process.env.NEXT_DIST_DIR || '.next',
  // Friendly aliases for routes people commonly type or bookmark.
  async redirects() {
    return [
      { source: '/tax-calculator', destination: '/calculate', permanent: true },
      { source: '/settings', destination: '/dashboard/settings', permanent: true },
    ];
  },
  env: {
    NEXT_PUBLIC_API_URL: process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000/api",
  },
}

module.exports = nextConfig
