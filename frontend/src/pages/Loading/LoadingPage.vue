<script setup lang="ts">
import { onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import MobileFrame from '@/components/layout/MobileFrame/MobileFrame.vue'
import Spinner from '@/components/common/Spinner/Spinner.vue'

const route = useRoute()
const router = useRouter()
const { t } = useI18n()

onMounted(() => {
  const matchId = typeof route.query.matchId === 'string' ? route.query.matchId : undefined
  if (matchId) {
    void router.replace({ name: 'lobby', params: { id: matchId } })
    return
  }
  void router.replace('/rooms')
})
</script>

<template>
  <MobileFrame fullscreen>
    <div class="flex min-h-screen flex-col items-center justify-center gap-4">
      <Spinner size="lg" :label="t('loading.summoning')" />
    </div>
  </MobileFrame>
</template>
