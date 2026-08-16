import { defineStore } from 'pinia'
import { ref } from 'vue'
import { gameService } from '@/services/game.service'

/** Queue/matchmaking only — live play uses the room store + RoomHub. */
export const useGameStore = defineStore('game', () => {
  const isInQueue = ref(false)
  const queueCount = ref(0)

  async function joinQueue() {
    const res = await gameService.joinQueue()
    isInQueue.value = res.queued
    return res
  }

  async function refreshQueueStatus() {
    const status = await gameService.getQueueStatus()
    isInQueue.value = status.inQueue
    queueCount.value = status.queueCount
  }

  function reset() {
    isInQueue.value = false
    queueCount.value = 0
  }

  return {
    isInQueue,
    queueCount,
    joinQueue,
    refreshQueueStatus,
    reset,
  }
})
