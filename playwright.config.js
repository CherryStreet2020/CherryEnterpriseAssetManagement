const { defineConfig } = require('@playwright/test');

module.exports = defineConfig({
  testDir: './tests',
  testMatch: '*.spec.js',
  timeout: 120000,
  workers: 1,
  retries: 1,
  globalSetup: require.resolve('./tests/_globalSetup.js'),
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
  // No `webServer` block: tests assume the long-running `Web Server` workflow
  // is already serving on :5000. Letting Playwright spawn its own dotnet
  // process under parallel suite execution previously caused cascading OOMs
  // because each suite would race to start a competing server.
  outputDir: 'proof/ui/playwright/test-results',
});
