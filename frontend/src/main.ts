import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import { router } from './router'
import { setupI18n } from './i18n'
import { useLanguageStore } from './stores/language.store'
import { useThemeStore } from './stores/theme.store'
import { useAuthStore } from './stores/auth.store'
import { useSettingsStore } from './stores/settings.store'
import { registerAuthFailureHandler } from './services/auth-failure'
import { registerSW } from 'virtual:pwa-register'
import './styles/tailwind.css'

async function bootstrap() {
  const app = createApp(App)
  const pinia = createPinia()
  app.use(pinia)

  const i18n = await setupI18n()
  app.use(i18n)

  await useLanguageStore(pinia).init()
  useThemeStore(pinia).init()
  const auth = useAuthStore(pinia)
  const settings = useSettingsStore(pinia)
  const updateSW = registerSW({
    immediate: true,
    onNeedRefresh() {
      settings.showAppUpdate(async () => {
        await updateSW(true)
        window.location.reload()
      })
    },
  })

  registerAuthFailureHandler(async () => {
    await auth.clearClientSession()
    settings.closeAlert()
    if (router.currentRoute.value.name !== 'login') {
      await router.replace({ name: 'login' })
    }
  })

  app.config.errorHandler = (error) => {
    console.error('[vue]', error)
    settings.reportError(error)
  }
  window.addEventListener('unhandledrejection', (event) => {
    console.error('[unhandledrejection]', event.reason)
    settings.reportError(event.reason)
  })

  app.use(router)
  app.mount('#app')
}

bootstrap().catch((error: unknown) => {
  console.error('[bootstrap]', error)
  const root = document.querySelector<HTMLElement>('#app')
  if (root) {
    root.setAttribute('role', 'alert')
    root.textContent = 'Unable to start the application. Please reload the page.'
  }
})
