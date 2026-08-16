import splash from './splash.png'
import bgLogin from './backgrounds/bg-login.webp'
import bgHome from './backgrounds/bg-home.webp'
import bgVoting from './backgrounds/bg-voting.webp'
import logoSmall from './brand/logo-small.png'
import cardBack from './cards/card-back.png'
import cardShield from './cards/card-shield.png'
import cardHeal from './cards/card-heal.png'
import cardShotgun from './cards/card-shotgun.png'
import cardVisitor from './cards/card-visitor.png'
import cardZombiePoison from './cards/card-zombie-poison.png'
import cardPass from './cards/card-pass.png'
import eventNormalDay from './event/event-normal-day.png'
import eventSunnyDay from './event/event-sunny-day.png'
import eventStorm from './event/event-storm.png'
import roleHuman from './roles/role-human.png'
import roleZombie from './roles/role-zombie.png'
import rolePowerZombie from './roles/role-power-zombie.png'
import defaultAvatar from './avatars/default-01.png'
import coin from './ui/coin.png'
import iconMatchmaking from './ui/icon-matchmaking.png'
import iconQuickMatch from './ui/icon-quick-match.png'
import inputFrameBig from './ui/input-frame-big.png'
import inputFrameSmall from './ui/input-frame-small.png'
import wood03 from './ui/wood03.webp'
import wood02 from './ui/wood02.webp'

export const images = {
  splash,
  backgrounds: {
    login: bgLogin,
    home: bgHome,
    voting: bgVoting,
  },
  brand: {
    logoSmall,
  },
  cards: {
    back: cardBack,
    shield: cardShield,
    heal: cardHeal,
    shotgun: cardShotgun,
    visitor: cardVisitor,
    /** @deprecated alias — use visitor */
    humanRole: cardVisitor,
    zombiePoison: cardZombiePoison,
    pass: cardPass,
  },
  events: {
    normalDay: eventNormalDay,
    sunnyDay: eventSunnyDay,
    storm: eventStorm,
  },
  roles: {
    human: roleHuman,
    zombie: roleZombie,
    powerZombie: rolePowerZombie,
  },
  avatars: {
    default: defaultAvatar,
  },
  ui: {
    coin,
    matchmaking: iconMatchmaking,
    quickMatch: iconQuickMatch,
    inputFrameBig,
    inputFrameSmall,
    wood03,
    wood02,
  },
} as const

export default images
