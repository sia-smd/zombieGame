import { computed, type MaybeRefOrGetter, toValue } from 'vue'
import { useI18n } from 'vue-i18n'
import { DayEventType } from '@/types/enums'
import { getDayEventImage, getDayEventPublicImage } from '@/utils/imageAssets'

export function useDayEventPresentation(event: MaybeRefOrGetter<DayEventType | null | undefined>) {
  const { t } = useI18n()

  const dayEvent = computed(() => toValue(event) ?? DayEventType.NormalDay)
  const image = computed(() => getDayEventImage(dayEvent.value))
  const fallbackImage = computed(() => getDayEventPublicImage(dayEvent.value))
  const title = computed(() => {
    switch (dayEvent.value) {
      case DayEventType.SunnyDay:
        return t('dayEvent.sunnyTitle')
      case DayEventType.Storm:
        return t('dayEvent.stormTitle')
      default:
        return t('dayEvent.normalTitle')
    }
  })
  const hint = computed(() => {
    switch (dayEvent.value) {
      case DayEventType.SunnyDay:
        return t('dayEvent.sunny')
      case DayEventType.Storm:
        return t('dayEvent.storm')
      default:
        return t('dayEvent.normal')
    }
  })

  return { dayEvent, image, fallbackImage, title, hint }
}
