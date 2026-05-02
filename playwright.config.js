const { defineConfig } = require('@playwright/test');

module.exports = defineConfig({
  testDir: './tests',
  testMatch: '*.spec.js',
  timeout: 60000,
  workers: 1,
  retries: 1,
  reporter: [
    ['html', { outputFolder: 'proof/ui/playwright/playwright-report', open: 'never' }],
  ],
  use: {
    baseURL: 'http://127.0.0.1:5000',
    trace: 'retain-on-failure',
    screenshot: 'on',
    headless: true,
    viewport: { width: 1440, height: 900 },
    launchOptions: {
      args: ['--no-sandbox', '--disable-setuid-sandbox', '--disable-dev-shm-usage', '--disable-gpu']
    },
  },
  outputDir: 'proof/ui/playwright/test-results',
});
