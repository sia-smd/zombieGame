# Design System

Token-based design system for Zombie vs Human. **Never hardcode colors, spacing, radius, shadows, or animation values in components.**

## Architecture

```
src/styles/theme/
  tokens.css          # Master import
  variables.css       # Layout + safe area
  colors.css          # Semantic color slots (RGB channels)
  spacing.css         # xs → 4xl scale
  radius.css          # small → circle
  typography.css      # caption → display (fluid clamp)
  shadows.css         # sm, md, lg, glow variants
  animations.css      # ds-* animation utilities
  zindex.css          # Layering scale
  themes/
    dark.css          # Default game theme
    light.css         # Future light theme
```

## Theming

```ts
import { useTheme } from '@/composables/useTheme'

const { theme, isDark, setTheme, toggleTheme } = useTheme()
setTheme('light') // instant, no reload
```

`data-theme` on `<html>` drives CSS variables. Persisted in `localStorage` (`zvh_theme`).

## Tailwind semantic classes

| Token | Class examples |
|-------|----------------|
| Colors | `bg-primary`, `text-text-secondary`, `border-border/10` |
| Spacing | `p-md`, `gap-lg`, `px-xl` |
| Radius | `rounded-2xl`, `rounded-pill`, `rounded-circle` |
| Shadow | `shadow-card`, `shadow-glow-danger` |
| Type | `text-body`, `text-heading`, `font-display` |
| Z-index | `z-modal`, `z-toast` |
| Duration | `duration-fast`, `duration-slow` |

**Do not use** `text-red-500`, `bg-blue-600`, or raw pixel values.

## Components

### Buttons (`Button.vue`)
Variants: `primary`, `secondary`, `ghost`, `danger`, `success`, `outline`  
Props: `loading`, `disabled`, `block`, `icon`

### Cards (`Card.vue`)
Variants: `default`, `glass`, `outlined`, `popup`, `game`  
Props: `glow`, `hoverable`, `selected`, `interactive`

### Inputs (`Input/`)
`Input`, `PasswordInput`, `Textarea`, `SearchInput`, `NumberInput`, `OtpInput`

### Modals
`Dialog`, `Modal`, `ConfirmDialog`, `BottomSheet`

### Notifications
`Toast`, `Snackbar`, `Notification` (stack), `NotificationBubble`

### Game primitives
`HealthBar`, `ManaBar`, `Timer`, `Avatar`, `Badge`, `PlayerCard`, `RoomCard`, `InventorySlot`, `SkillCard`, `RoleBadge`, `ReadyBadge`

## Utility classes

- `.ds-panel` — standard game surface
- `.ds-title` — display heading style
- `.ds-input` — form input base
- `.animate-ds-fade-up`, `.animate-ds-float`, etc.

## TypeScript

Token unions in `src/types/design-tokens.ts`.
