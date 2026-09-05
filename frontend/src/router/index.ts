import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import { authSession } from '@/services/api'
import { useAuthStore } from '@/stores/auth.store'

const routes: RouteRecordRaw[] = [
  {
    path: '/',
    name: 'splash',
    component: () => import('@/pages/Splash/SplashPage.vue'),
    meta: { public: true },
  },
  {
    path: '/login',
    name: 'login',
    component: () => import('@/pages/Login/LoginPage.vue'),
    meta: { public: true },
  },
  {
    path: '/home',
    name: 'home',
    component: () => import('@/pages/Home/HomePage.vue'),
  },
  {
    path: '/rooms',
    name: 'rooms',
    component: () => import('@/pages/RoomBrowser/RoomBrowserPage.vue'),
  },
  {
    path: '/rooms/create',
    name: 'create-room',
    component: () => import('@/pages/CreateRoom/CreateRoomPage.vue'),
  },
  {
    path: '/room/:id',
    name: 'lobby',
    component: () => import('@/pages/Lobby/LobbyPage.vue'),
    props: true,
  },
  {
    path: '/loading',
    name: 'loading',
    component: () => import('@/pages/Loading/LoadingPage.vue'),
    meta: { public: true },
  },
  {
    path: '/reveal-role/:id',
    name: 'reveal-role',
    component: () => import('@/pages/RevealRole/RevealRolePage.vue'),
    props: true,
  },
  {
    path: '/game/:id',
    name: 'game',
    component: () => import('@/pages/GameRoom/GameRoomPage.vue'),
    props: true,
  },
  {
    path: '/battle/:id',
    name: 'battle',
    component: () => import('@/pages/Game/GamePage.vue'),
    props: true,
  },
  {
    path: '/battle-summary/:id',
    name: 'battle-summary',
    component: () => import('@/pages/BattleSummary/BattleSummaryPage.vue'),
    props: true,
  },
  {
    path: '/voting/:id',
    name: 'voting',
    component: () => import('@/pages/Voting/VotingPage.vue'),
    props: true,
  },
  {
    path: '/match-result/:id',
    name: 'match-result',
    component: () => import('@/pages/MatchResult/MatchResultPage.vue'),
    props: true,
  },
  {
    path: '/profile',
    name: 'profile',
    component: () => import('@/pages/Profile/ProfilePage.vue'),
  },
  {
    path: '/collection',
    name: 'collection',
    component: () => import('@/pages/Collection/CollectionPage.vue'),
  },
  {
    path: '/friends',
    name: 'friends',
    component: () => import('@/pages/Friends/FriendsPage.vue'),
  },
  {
    path: '/leaderboard',
    name: 'leaderboard',
    component: () => import('@/pages/Leaderboard/LeaderboardPage.vue'),
  },
  {
    path: '/settings',
    name: 'settings',
    component: () => import('@/pages/Settings/SettingsPage.vue'),
  },
  {
    path: '/shop',
    name: 'shop',
    component: () => import('@/pages/Shop/ShopPage.vue'),
  },
  { path: '/:pathMatch(.*)*', redirect: '/home' },
]

export const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes,
  scrollBehavior: () => ({ top: 0 }),
})

router.beforeEach(async (to) => {
  if (to.meta.public) return true
  if (authSession.isSignedIn()) {
    const auth = useAuthStore()
    auth.hydrateFromStorage()
    if (!auth.profile) await auth.loadProfile().catch(() => undefined)
    return true
  }
  return { name: 'splash' }
})
