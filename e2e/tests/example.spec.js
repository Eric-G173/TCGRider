import { test, expect } from '@playwright/test';
 
// NOTE: each test below gets its own isolated browser context (separate
// localStorage, separate client ID) — "Jungle" being already synced
// GLOBALLY means the sync step is instant either way, but each test still
// has to independently browse to it, since tracker state is per-client.
 
test.describe('TCGRider core flows', () => {
  test.beforeEach(async ({ page }) => {
    // Surface any real browser-side errors directly in the test output —
    // if webkit is hitting something chromium/firefox tolerate
    // differently, this is the most direct way to actually see it.
    page.on('pageerror', (err) => console.log('BROWSER ERROR:', err.message));
    page.on('console', (msg) => {
      if (msg.type() === 'error') console.log('CONSOLE ERROR:', msg.text());
    });
 
    await page.goto('/');
 
    await page.getByRole('button', { name: '+ New Tracker' }).click();
    await page.getByTestId('game-link-Pokémon').click();
 
    // "Jungle" — one of the original 1999 Pokemon sets, chosen
    // deliberately for stability, unlikely to ever be renamed or removed.
    await page.getByRole('button', { name: 'Jungle' }).click();
 
    const sidebar = page.getByRole('complementary');
    const trackerButton = sidebar.getByRole('button', { name: 'Jungle' });
    await trackerButton.waitFor({ state: 'visible', timeout: 30000 });
    await trackerButton.click();
 
    // Every test that uses this setup needs real cards actually loaded
    // before it starts — wait for that here, once, rather than in each test.
    await expect(page.locator('[data-card-id]').first()).toBeVisible({ timeout: 15000 });
  });
 
  test('user can browse, add a tracker, and see its cards', async ({ page }) => {
    const cards = page.locator('[data-card-id]');
    expect(await cards.count()).toBeGreaterThan(0);
  });
 
  test('user can toggle a card as collected', async ({ page }) => {
    const collectButton = page.getByRole('button', { name: 'Collect' }).first();
    await collectButton.click();
 
    // The button's own accessible name changes once collected — this is
    // the actual, meaningful confirmation the toggle really happened,
    // not just that a click occurred.
    const collectedButton = page.getByRole('button', { name: 'Collected' }).first();
    await expect(collectedButton).toBeVisible();
 
    // Full round-trip — confirm it toggles back too, not just one direction.
    await collectedButton.click();
    await expect(page.getByRole('button', { name: 'Collect' }).first()).toBeVisible();
  });
});