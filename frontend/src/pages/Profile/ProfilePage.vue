<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import MobileFrame from '@/components/layout/MobileFrame/MobileFrame.vue'
import PageBackdrop from '@/components/layout/PageBackdrop/PageBackdrop.vue'
import WoodPanel from '@/components/layout/WoodPanel/WoodPanel.vue'
import BottomBar from '@/components/layout/BottomBar/BottomBar.vue'
import IconButton from '@/components/common/IconButton/IconButton.vue'
import CircularAvatarCropper from '@/components/profile/CircularAvatarCropper/CircularAvatarCropper.vue'
import {
  ArrowLeftIcon,
  BeakerIcon,
  CheckIcon,
  DocumentCheckIcon,
  HeartIcon,
  PencilIcon,
  TrophyIcon,
  UserIcon,
} from '@heroicons/vue/24/outline'
import { useAuthStore } from '@/stores/auth.store'
import { useSettingsStore } from '@/stores/settings.store'
import { profileService, type AchievementProgress } from '@/services/profile.service'
import { resolveAvatarUrl } from '@/utils/avatarAssets'
import { useLocaleFormat } from '@/composables/useGameLabels'
import { images } from '@/assets/images'
import { formatIranMobile } from '@/utils/mobile'

const router = useRouter()
const auth = useAuthStore()
const settings = useSettingsStore()
const { t } = useI18n()
const { formatLocaleNumber } = useLocaleFormat()

const loading = ref(true)
const savingAll = ref(false)
const name = ref('')
const username = ref('')
const mobile = ref('')
const verifyCode = ref('')
const pendingMobile = ref(false)
const password = ref('')
const currentPassword = ref('')
const avatarPreview = ref<string | null>(null)
const cropFile = ref<File | null>(null)
const cropperOpen = ref(false)
const fileInput = ref<HTMLInputElement | null>(null)
const activeTab = ref<'profile' | 'medals'>('profile')
const medals = ref<AchievementProgress[]>([])
const medalsLoading = ref(false)

const wins = computed(() => auth.profile?.statistics.wins ?? auth.guestProfile?.wins ?? 0)
const losses = computed(() => auth.profile?.statistics.losses ?? auth.guestProfile?.losses ?? 0)
const avatarUrl = computed(() => avatarPreview.value ?? resolveAvatarUrl(auth.profile) ?? images.avatars.default)

onMounted(async () => {
  loading.value = true
  try {
    await auth.loadProfile()
    syncFromProfile()
    await loadMedals()
  } catch (e: unknown) {
    settings.reportError(e)
  } finally {
    loading.value = false
  }
})

async function loadMedals() {
  medalsLoading.value = true
  try {
    const progress = await profileService.getAchievements()
    medals.value = progress.achievements
  } catch (e: unknown) {
    settings.reportError(e)
  } finally {
    medalsLoading.value = false
  }
}

function medalIcon(statKey: string) {
  if (statKey === 'heals') return HeartIcon
  if (statKey === 'poisons') return BeakerIcon
  return TrophyIcon
}

function syncFromProfile() {
  const p = auth.profile
  if (!p) return
  name.value = p.name
  username.value = p.username
  mobile.value = formatIranMobile(p.phoneNumber ?? p.pendingPhoneNumber ?? '')
  pendingMobile.value = !!p.pendingPhoneNumber && !p.mobileVerified
}

function openAvatarPicker() {
  fileInput.value?.click()
}

function onAvatarSelected(e: Event) {
  const file = (e.target as HTMLInputElement).files?.[0]
  if (!file) return
  cropFile.value = file
  cropperOpen.value = true
  ;(e.target as HTMLInputElement).value = ''
}

async function onAvatarCropped(base64: string) {
  cropperOpen.value = false
  cropFile.value = null
  try {
    const updated = await profileService.uploadAvatar(base64)
    auth.applyProfile(updated)
    avatarPreview.value = `data:image/jpeg;base64,${base64}`
    settings.pushToast('success', t('profileEdit.avatarSaved'))
  } catch (e: unknown) {
    settings.reportError(e)
  }
}

async function saveUsername() {
  try {
    const updated = await profileService.updateUsername(username.value.trim())
    auth.applyProfile(updated)
    settings.pushToast('success', t('profileEdit.usernameSaved'))
  } catch (e: unknown) {
    settings.reportError(e)
  }
}

async function saveMobile() {
  try {
    const number = formatIranMobile(mobile.value)
    const res = await profileService.addMobile(number)
    if (res.verificationRequired) {
      pendingMobile.value = true
      settings.pushToast('info', t('profileEdit.codeSent'))
    } else {
      await auth.loadProfile()
      syncFromProfile()
      settings.pushToast('success', t('profileEdit.mobileSaved'))
    }
  } catch (e: unknown) {
    settings.reportError(e)
  }
}

async function verifyMobile() {
  try {
    await profileService.verifyMobile(formatIranMobile(mobile.value), verifyCode.value.trim())
    pendingMobile.value = false
    verifyCode.value = ''
    await auth.loadProfile()
    syncFromProfile()
    settings.pushToast('success', t('profileEdit.mobileSaved'))
  } catch (e: unknown) {
    settings.reportError(e)
  }
}

async function savePassword() {
  try {
    await profileService.changePassword(
      password.value,
      auth.profile?.hasPassword ? currentPassword.value : undefined,
    )
    password.value = ''
    currentPassword.value = ''
    settings.pushToast('success', t('profileEdit.passwordSaved'))
  } catch (e: unknown) {
    settings.reportError(e)
  }
}

async function saveAll() {
  savingAll.value = true
  try {
    await profileService.updateProfile({ name: name.value.trim() })
    await profileService.updateUsername(username.value.trim())
    if (password.value) {
      await profileService.changePassword(
        password.value,
        auth.profile?.hasPassword ? currentPassword.value : undefined,
      )
    }
    if (pendingMobile.value && verifyCode.value) {
      await profileService.verifyMobile(formatIranMobile(mobile.value), verifyCode.value.trim())
    } else if (formatIranMobile(mobile.value) && formatIranMobile(mobile.value) !== formatIranMobile(auth.profile?.phoneNumber ?? '')) {
      await saveMobile()
    }
    await auth.loadProfile()
    syncFromProfile()
    settings.pushToast('success', t('profileEdit.saved'))
  } catch (e: unknown) {
    settings.reportError(e)
  } finally {
    savingAll.value = false
  }
}
</script>

<template>
  <MobileFrame fullscreen>
    <PageBackdrop :src="images.backgrounds.login" :overlay-opacity="0.55" class="flex min-h-screen flex-col">
      <div class="relative z-10 flex min-h-screen flex-col">
        <header class="flex items-center px-4 pt-[calc(0.75rem+var(--safe-top))]">
          <IconButton :icon="ArrowLeftIcon" :label="t('common.back')" @click="router.back()" />
        </header>

        <main class="flex flex-1 flex-col overflow-y-auto px-4 pb-4 pt-2">
          <h1 class="profile-title text-center font-display text-3xl uppercase tracking-widest">
            {{ t('profileEdit.title') }}
          </h1>

          <div class="profile-avatar-row mx-auto mt-3 flex items-center justify-center gap-3">
            <div class="profile-stat-badge profile-stat-badge--win">
              <p class="text-[10px] font-semibold uppercase tracking-wide text-white/90">{{ t('profileEdit.wins') }}</p>
              <p class="text-xl font-bold text-white">{{ formatLocaleNumber(wins) }}</p>
            </div>

            <div class="profile-avatar-wrap relative">
              <img
                :src="avatarUrl"
                :alt="name"
                class="profile-avatar-img h-28 w-28 rounded-full object-cover"
              />
              <button
                type="button"
                class="profile-edit-btn absolute bottom-0 end-0 flex h-9 w-9 items-center justify-center rounded-full"
                :aria-label="t('profileEdit.changeAvatar')"
                @click="openAvatarPicker"
              >
                <PencilIcon class="h-4 w-4 text-white" />
              </button>
              <input ref="fileInput" type="file" accept="image/*" class="hidden" @change="onAvatarSelected" />
            </div>

            <div class="profile-stat-badge profile-stat-badge--loss">
              <p class="text-[10px] font-semibold uppercase tracking-wide text-white/90">{{ t('profileEdit.losses') }}</p>
              <p class="text-xl font-bold text-white">{{ formatLocaleNumber(losses) }}</p>
            </div>
          </div>

          <div class="profile-tabs mx-auto mt-4 flex w-full max-w-sm gap-2">
            <button
              type="button"
              class="profile-tab"
              :class="{ 'profile-tab--active': activeTab === 'profile' }"
              @click="activeTab = 'profile'"
            >
              <UserIcon class="h-5 w-5" />
              {{ t('profileEdit.tabProfile') }}
            </button>
            <button
              type="button"
              class="profile-tab"
              :class="{ 'profile-tab--active': activeTab === 'medals' }"
              @click="activeTab = 'medals'"
            >
              <TrophyIcon class="h-5 w-5" />
              {{ t('profileEdit.tabMedals') }}
            </button>
          </div>

          <template v-if="activeTab === 'profile'">
          <WoodPanel
            class="profile-panel mx-auto mt-3 w-full max-w-sm space-y-2 p-3"
          >
            <section class="space-y-0.5 text-start">
              <label class="text-sm font-semibold text-white">{{ t('profileEdit.name') }}</label>
              <div class="profile-field" :style="{ backgroundImage: `url(${images.ui.inputFrameBig})` }">
                <input v-model="name" type="text" maxlength="20" class="profile-input" />
              </div>
            </section>

            <section class="space-y-0.5 text-start">
              <label class="text-sm font-semibold text-white">{{ t('profileEdit.username') }}</label>
              <div class="profile-field profile-field--action" :style="{ backgroundImage: `url(${images.ui.inputFrameBig})` }">
                <span class="text-white/70">@</span>
                <input v-model="username" type="text" maxlength="20" class="profile-input" />
                <button type="button" class="profile-save-icon" :aria-label="t('common.save')" @click="saveUsername">
                  <DocumentCheckIcon class="h-5 w-5" />
                </button>
              </div>
            </section>

            <section class="space-y-0.5 text-start">
              <label class="text-sm font-semibold text-white">{{ t('profileEdit.mobile') }}</label>
              <div class="profile-field profile-field--action" :style="{ backgroundImage: `url(${images.ui.inputFrameBig})` }">
                <input
                  v-model="mobile"
                  type="tel"
                  inputmode="numeric"
                  maxlength="11"
                  class="profile-input"
                  :placeholder="t('profileEdit.mobilePlaceholder')"
                />
                <button type="button" class="profile-save-icon" :aria-label="t('common.save')" @click="saveMobile">
                  <DocumentCheckIcon class="h-5 w-5" />
                </button>
              </div>
              <div v-if="pendingMobile" class="flex gap-2">
                <div class="profile-field flex-1" :style="{ backgroundImage: `url(${images.ui.inputFrameBig})` }">
                  <input v-model="verifyCode" type="text" maxlength="6" class="profile-input" :placeholder="t('profileEdit.verifyCode')" />
                </div>
                <button type="button" class="profile-mini-btn" @click="verifyMobile">{{ t('profileEdit.verify') }}</button>
              </div>
            </section>

            <section class="space-y-0.5 text-start">
              <label class="text-sm font-semibold text-white">{{ t('profileEdit.password') }}</label>
              <div class="profile-field profile-field--action" :style="{ backgroundImage: `url(${images.ui.inputFrameBig})` }">
                <input v-model="password" type="password" class="profile-input" :placeholder="t('profileEdit.passwordPlaceholder')" />
                <button type="button" class="profile-save-icon" :aria-label="t('common.save')" @click="savePassword">
                  <DocumentCheckIcon class="h-5 w-5" />
                </button>
              </div>
              <div
                v-if="auth.profile?.hasPassword"
                class="profile-field mt-1"
                :style="{ backgroundImage: `url(${images.ui.inputFrameBig})` }"
              >
                <input
                  v-model="currentPassword"
                  type="password"
                  class="profile-input"
                  :placeholder="t('profileEdit.currentPassword')"
                />
              </div>
            </section>
          </WoodPanel>

          <button
            type="button"
            class="profile-action profile-action--save mx-auto mt-3 w-full max-w-sm"
            :disabled="savingAll || loading"
            @click="saveAll"
          >
            <CheckIcon class="h-6 w-6" />
            {{ t('profileEdit.save') }}
          </button>
          </template>

          <WoodPanel v-else class="profile-panel mx-auto mt-3 w-full max-w-sm p-3">
            <p v-if="medalsLoading" class="py-6 text-center text-sm text-white/70">{{ t('common.loading') }}</p>
            <p v-else-if="!medals.length" class="py-6 text-center text-sm text-white/70">
              {{ t('profileEdit.medalsEmpty') }}
            </p>
            <ul v-else class="medal-grid">
              <li
                v-for="medal in medals"
                :key="medal.code"
                class="medal-card"
                :class="medal.unlocked ? 'medal-card--on' : 'medal-card--off'"
              >
                <component
                  :is="medalIcon(medal.statKey)"
                  class="medal-icon"
                />
                <p class="medal-title">{{ medal.title }}</p>
                <p class="medal-desc">{{ medal.description }}</p>
                <p class="medal-progress">
                  {{ t('profileEdit.medalProgress', { current: formatLocaleNumber(medal.currentValue), threshold: formatLocaleNumber(medal.threshold) }) }}
                </p>
              </li>
            </ul>
          </WoodPanel>
        </main>

        <BottomBar />
      </div>
    </PageBackdrop>

    <CircularAvatarCropper
      :visible="cropperOpen"
      :file="cropFile"
      @confirm="onAvatarCropped"
      @cancel="cropperOpen = false"
    />
  </MobileFrame>
</template>

<style scoped>
.profile-title {
  color: rgb(var(--color-game-accent-rgb));
  text-shadow: 0 0 2px rgb(var(--color-on-game-rgb)), 0 3px 0 rgb(0 0 0 / 0.8);
}

.profile-panel {
  border-radius: 1.25rem;
  background-repeat: repeat;
  background-size: 180px auto;
  border: 2px solid rgb(var(--color-game-border-rgb) / 0.75);
  box-shadow: inset 0 0 0 1px rgb(255 255 255 / 0.06);
}

.profile-field {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  width: 100%;
  min-height: 3.75rem;
  padding: 0.35rem 0.75rem;
  background-repeat: no-repeat;
  background-position: center;
  background-size: 100% 100%;
}

.profile-field--action {
  padding-inline-end: 0.35rem;
}

.profile-input {
  width: 100%;
  border: 0;
  background: transparent;
  border-radius: 0;
  color: rgb(var(--color-on-game-rgb));
  font-size: 0.9rem;
  font-weight: 600;
  outline: none;
  padding: 0.4rem 0.5rem;
}

.profile-input::placeholder {
  color: rgb(255 255 255 / 0.45);
}

.profile-save-icon {
  display: inline-flex;
  height: 2rem;
  width: 2rem;
  flex-shrink: 0;
  align-items: center;
  justify-content: center;
  border-radius: 0.75rem;
  color: rgb(var(--color-game-cta-start-rgb));
}

.profile-avatar-row {
  width: 100%;
  max-width: 20rem;
}

.profile-avatar-wrap {
  flex-shrink: 0;
  padding: 4px;
  border-radius: 9999px;
  background: linear-gradient(
    180deg,
    rgb(var(--color-game-cta-start-rgb)) 0%,
    rgb(var(--color-game-cta-end-rgb)) 100%
  );
  box-shadow:
    0 0 0 3px rgb(255 255 255 / 0.85),
    0 6px 16px rgb(0 0 0 / 0.45);
}

.profile-avatar-img {
  display: block;
  background: rgb(var(--color-overlay-rgb) / 0.85);
  box-shadow: inset 0 0 0 2px rgb(0 0 0 / 0.25);
}

.profile-stat-badge {
  min-width: 4.25rem;
  border-radius: 0.9rem;
  padding: 0.45rem 0.55rem;
  text-align: center;
  border: 2px solid rgb(255 255 255 / 0.25);
  box-shadow: 0 4px 0 rgb(0 0 0 / 0.3);
}

.profile-stat-badge--win {
  background: linear-gradient(
    180deg,
    rgb(var(--color-game-cta-start-rgb)) 0%,
    rgb(var(--color-game-cta-end-rgb)) 100%
  );
}

.profile-stat-badge--loss {
  background: linear-gradient(
    180deg,
    rgb(var(--color-room-mood-critical-rgb)) 0%,
    rgb(var(--color-danger-rgb)) 100%
  );
}

.profile-edit-btn {
  background: linear-gradient(
    180deg,
    rgb(var(--color-game-cta-start-rgb)) 0%,
    rgb(var(--color-game-cta-end-rgb)) 100%
  );
  border: 2px solid rgb(255 255 255 / 0.7);
  box-shadow: 0 3px 0 rgb(0 0 0 / 0.35);
}

.profile-mini-btn {
  border-radius: 0.9rem;
  background: linear-gradient(180deg, rgb(var(--color-game-wood-start-rgb)) 0%, rgb(var(--color-game-wood-end-rgb)) 100%);
  border: 2px solid rgb(140 95 55 / 0.85);
  color: rgb(var(--color-on-game-rgb));
  padding: 0 0.9rem;
  font-size: 0.8rem;
  font-weight: 700;
}

.profile-action {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
  min-height: 3.5rem;
  font-size: 1.05rem;
  border-radius: var(--radius-pill);
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  box-shadow:
    0 8px 0 rgb(0 0 0 / 0.35),
    0 12px 24px rgb(34 197 94 / 0.3);
}

.profile-action--save {
  background: linear-gradient(
    180deg,
    rgb(var(--color-game-cta-start-rgb)) 0%,
    rgb(var(--color-game-cta-end-rgb)) 100%
  );
  color: rgb(var(--color-on-game-rgb));
  border: 3px solid rgb(220 255 180 / 0.85);
}

.profile-action:disabled {
  opacity: 0.6;
}

.profile-tabs {
  display: flex;
  gap: 0.5rem;
}

.profile-tab {
  flex: 1;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.35rem;
  min-height: 2.6rem;
  border-radius: 0.9rem;
  border: 2px solid rgb(140 95 55 / 0.85);
  background: linear-gradient(180deg, rgb(var(--color-game-wood-start-rgb)) 0%, rgb(var(--color-game-wood-end-rgb)) 100%);
  color: rgb(255 255 255 / 0.7);
  font-size: 0.85rem;
  font-weight: 800;
}

.profile-tab--active {
  color: rgb(var(--color-on-game-rgb));
  border-color: rgb(220 255 180 / 0.85);
  background: linear-gradient(
    180deg,
    rgb(var(--color-game-cta-start-rgb)) 0%,
    rgb(var(--color-game-cta-end-rgb)) 100%
  );
  box-shadow: 0 0 12px rgb(124 252 0 / 0.35);
}

.medal-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0.65rem;
}

.medal-card {
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
  gap: 0.25rem;
  border-radius: 0.9rem;
  padding: 0.75rem 0.5rem;
  border: 2px solid rgb(255 255 255 / 0.18);
}

.medal-icon {
  width: 2.1rem;
  height: 2.1rem;
}

.medal-title {
  font-size: 0.78rem;
  font-weight: 800;
  line-height: 1.2;
}

.medal-desc,
.medal-progress {
  font-size: 0.65rem;
  line-height: 1.3;
}

.medal-card--on {
  background: linear-gradient(180deg, rgb(var(--color-secondary-rgb)) 0%, rgb(122 78 0) 100%);
  color: rgb(var(--color-on-game-rgb));
  box-shadow: 0 0 14px rgb(255 210 80 / 0.45);
}

.medal-card--on .medal-desc,
.medal-card--on .medal-progress {
  color: rgb(255 255 255 / 0.85);
}

.medal-card--off {
  background: rgb(20 12 8 / 0.7);
  color: rgb(180 180 180 / 0.7);
  filter: grayscale(1) brightness(0.65);
}

.medal-card--off .medal-desc,
.medal-card--off .medal-progress {
  color: rgb(180 180 180 / 0.55);
}
</style>
