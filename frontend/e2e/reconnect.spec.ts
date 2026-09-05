import { test, expect, mockApi, seedSignedIn } from './helpers'

test.describe('P3 reconnect', () => {
  test('shows the connection-lost dialog and accepts retry', async ({ page }) => {
    await seedSignedIn(page)
    await mockApi(page)
    await page.goto('/home')
    await expect(page.getByRole('heading', { name: /lobby/i })).toBeVisible()

    await page.evaluate(() => {
      window.dispatchEvent(new Event('zvh-e2e-disconnect'))
    })

    await expect(page.getByRole('heading', { name: /connection lost/i })).toBeVisible()
    await page.getByRole('button', { name: /^retry$/i }).click()
    await expect(page.getByRole('heading', { name: /connection lost/i })).toHaveCount(0)
    await expect(page.getByRole('heading', { name: /lobby/i })).toBeVisible()
  })
})
