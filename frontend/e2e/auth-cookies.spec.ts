import { test, expect, mockApi } from './helpers'

test.describe('P0 cookie session', () => {
  test('guest login does not persist JWTs in localStorage', async ({ page }) => {
    await page.addInitScript(() => {
      localStorage.setItem('zvh_lang', 'en')
    })
    await mockApi(page)
    await page.goto('/login')
    await page.getByRole('button', { name: /continue as guest/i }).click()
    await expect(page).toHaveURL(/\/home/)
    await expect(page.getByRole('heading', { name: /lobby/i })).toBeVisible()

    const stored = await page.evaluate(() => ({
      auth: localStorage.getItem('zvh_auth'),
      access: localStorage.getItem('zvh_access_token'),
      refresh: localStorage.getItem('zvh_refresh_token'),
    }))
    expect(stored.auth).toBe('1')
    expect(stored.access).toBeNull()
    expect(stored.refresh).toBeNull()
  })

  test('unsigned visitors are sent to login', async ({ page }) => {
    await page.addInitScript(() => {
      localStorage.setItem('zvh_lang', 'en')
    })
    await mockApi(page)
    await page.goto('/home')
    await expect(page).toHaveURL('http://127.0.0.1:5173/')

  })
})
