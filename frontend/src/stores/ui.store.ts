import { defineStore } from 'pinia'
import { computed, ref } from 'vue'

export const useUiStore = defineStore('ui', () => {
  const pendingWrites = ref(0)
  const suppressBusy = ref(false)

  const isBusy = computed(() => !suppressBusy.value && pendingWrites.value > 0)

  function beginWrite() {
    pendingWrites.value += 1
  }

  function endWrite() {
    pendingWrites.value = Math.max(0, pendingWrites.value - 1)
  }

  function setSuppressBusy(value: boolean) {
    suppressBusy.value = value
  }

  return {
    pendingWrites,
    suppressBusy,
    isBusy,
    beginWrite,
    endWrite,
    setSuppressBusy,
  }
})
