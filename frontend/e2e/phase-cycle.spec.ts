import { RoomPhase } from '../src/types/enums'
import {
  test,
  expect,
  mockApi,
  seedSignedIn,
  seedE2eRoom,
  MATCH_ID,
} from './helpers'

const steps: Array<{ phase: RoomPhase; path: string; heading: RegExp }> = [
  { phase: RoomPhase.Lobby, path: `/room/${MATCH_ID}`, heading: /waiting room/i },
  { phase: RoomPhase.OpponentSelection, path: `/game/${MATCH_ID}`, heading: /e2e room/i },
  { phase: RoomPhase.CardBattle, path: `/battle/${MATCH_ID}`, heading: /private battle/i },
  { phase: RoomPhase.BattleResult, path: `/battle-summary/${MATCH_ID}`, heading: /battle results/i },
  { phase: RoomPhase.Voting, path: `/voting/${MATCH_ID}`, heading: /voting/i },
  { phase: RoomPhase.Finished, path: `/match-result/${MATCH_ID}`, heading: /humans win/i },
]

test.describe('P3 phase cycle', () => {
  test('live room routes follow the seeded match phase', async ({ page }) => {
    await seedSignedIn(page)
    await mockApi(page)
    await page.goto('/home')
    await expect(page.getByRole('heading', { name: /lobby/i })).toBeVisible()

    for (const step of steps) {
      await seedE2eRoom(page, step.phase)
      await page.goto(step.path)
      await expect(page).toHaveURL(new RegExp(step.path.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')))
      await expect(page.getByRole('heading', { name: step.heading }).first()).toBeVisible({
        timeout: 15_000,
      })
    }
  })
})
