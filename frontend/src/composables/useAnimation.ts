import { computed, onMounted, onScopeDispose, ref, toValue, watch, type MaybeRefOrGetter } from 'vue'
import gsap from 'gsap'
import { useSettingsStore } from '@/stores/settings.store'

export function applyReducedMotionPreference(enabled: boolean) {
  document.documentElement.classList.toggle('reduce-motion', enabled)
  gsap.globalTimeline.timeScale(enabled ? 0 : 1)
}

export function useGsapFade(target: () => HTMLElement | null, delay = 0) {
  onMounted(() => {
    const el = target()
    if (!el) return
    const reduced = useSettingsStore().reducedMotion
    if (reduced) {
      gsap.set(el, { opacity: 1, y: 0 })
      return
    }
    gsap.fromTo(el, { opacity: 0, y: 24 }, { opacity: 1, y: 0, duration: 0.5, delay, ease: 'power2.out' })
  })
}

export function useCountdown(
  endsAt: MaybeRefOrGetter<string | null | undefined>,
  active: MaybeRefOrGetter<boolean> = true,
) {
  const nowMs = ref(Date.now())
  let timer: ReturnType<typeof setInterval> | null = null

  function tick() {
    nowMs.value = Date.now()
  }

  const secondsLeft = computed<number | null>(() => {
    if (!toValue(active)) return null
    const end = toValue(endsAt)
    if (!end) return null
    const endMs = new Date(end).getTime()
    if (!Number.isFinite(endMs)) return null
    return Math.max(0, Math.ceil((endMs - nowMs.value) / 1000))
  })
  const clock = computed(() => {
    const seconds = secondsLeft.value
    return seconds === null ? '--:--' : formatTimer(seconds)
  })

  watch([() => toValue(endsAt), () => toValue(active)], tick, { immediate: true })

  onMounted(() => {
    tick()
    timer = setInterval(() => {
      if (toValue(active)) tick()
    }, 250)
  })

  onScopeDispose(() => {
    if (timer) clearInterval(timer)
  })

  return { secondsLeft, remaining: secondsLeft, clock }
}

export function formatTimer(seconds: number): string {
  const m = Math.floor(seconds / 60)
  const s = seconds % 60
  return `${m}:${s.toString().padStart(2, '0')}`
}
