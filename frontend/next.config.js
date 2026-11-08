/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
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
