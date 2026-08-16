<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useLanguageStore } from '@/stores/language.store'
import type { SupportedLocale } from '@/i18n/helpers/language'
import { CheckIcon, ChevronDownIcon, LanguageIcon } from '@heroicons/vue/24/outline'

const { t } = useI18n()
const languageStore = useLanguageStore()
const open = ref(false)
const root = ref<HTMLElement | null>(null)

const current = computed(() =>
  languageStore.availableLanguages.find((l) => l.code === languageStore.language),
)

function toggle() {
  open.value = !open.value
}

async function select(code: SupportedLocale) {
  await languageStore.changeLanguage(code)
  open.value = false
}

function onClickOutside(e: MouseEvent) {
  if (root.value && !root.value.contains(e.target as Node)) open.value = false
}

onMounted(() => document.addEventListener('click', onClickOutside))
onUnmounted(() => document.removeEventListener('click', onClickOutside))
</script>

<template>
  <div ref="root" class="relative">
    <button
      type="button"
      class="flex h-11 items-center gap-2 rounded-2xl border border-white/10 bg-black/30 px-3 text-sm text-fog transition hover:border-gold/30"
      :aria-label="t('language.select')"
      :aria-expanded="open"
      @click.stop="toggle"
    >
      <LanguageIcon class="h-5 w-5 text-gold" />
      <span class="text-base">{{ current?.flag }}</span>
      <span class="max-w-[5rem] truncate font-medium">{{ current?.nativeName }}</span>
      <ChevronDownIcon
        class="h-4 w-4 text-mist transition"
        :class="open ? 'rotate-180' : ''"
      />
    </button>

    <Transition name="dropdown">
      <div
        v-if="open"
        class="absolute end-0 z-50 mt-2 min-w-[11rem] overflow-hidden rounded-2xl border border-white/10 bg-abyss shadow-card"
      >
        <button
          v-for="lang in languageStore.availableLanguages"
          :key="lang.code"
          type="button"
          class="flex w-full items-center gap-3 px-4 py-3 text-start text-sm transition hover:bg-white/5"
          :class="lang.code === languageStore.language ? 'bg-gold/10 text-gold' : 'text-fog'"
          @click="select(lang.code)"
        >
          <span class="text-lg">{{ lang.flag }}</span>
          <span class="flex-1 font-medium">{{ lang.nativeName }}</span>
          <CheckIcon
            v-if="lang.code === languageStore.language"
            class="h-4 w-4 shrink-0 text-gold"
          />
        </button>
      </div>
    </Transition>
  </div>
</template>

<style scoped>
.dropdown-enter-active,
.dropdown-leave-active {
  transition: opacity 0.15s ease, transform 0.15s ease;
}
.dropdown-enter-from,
.dropdown-leave-to {
  opacity: 0;
  transform: translateY(-6px);
}
</style>
