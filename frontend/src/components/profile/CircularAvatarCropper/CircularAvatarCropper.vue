<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'

const props = defineProps<{
  visible: boolean
  file: File | null
}>()

const emit = defineEmits<{
  confirm: [base64: string]
  cancel: []
}>()

const canvasRef = ref<HTMLCanvasElement | null>(null)
const scale = ref(1)
const offsetX = ref(0)
const offsetY = ref(0)
const dragging = ref(false)
const dragStart = ref({ x: 0, y: 0 })
const image = ref<HTMLImageElement | null>(null)
const viewportSize = 280
const outputSize = 256

const imageLoaded = computed(() => !!image.value)

function loadImage(file: File | null) {
  image.value = null
  scale.value = 1
  offsetX.value = 0
  offsetY.value = 0
  if (!file) return

  const img = new Image()
  img.onload = () => {
    image.value = img
    const fit = Math.max(viewportSize / img.width, viewportSize / img.height)
    scale.value = fit
    offsetX.value = (viewportSize - img.width * fit) / 2
    offsetY.value = (viewportSize - img.height * fit) / 2
    draw()
  }
  img.src = URL.createObjectURL(file)
}

function draw() {
  const canvas = canvasRef.value
  const img = image.value
  if (!canvas || !img) return

  const ctx = canvas.getContext('2d')
  if (!ctx) return

  canvas.width = viewportSize
  canvas.height = viewportSize
  ctx.clearRect(0, 0, viewportSize, viewportSize)
  ctx.save()
  ctx.beginPath()
  ctx.arc(viewportSize / 2, viewportSize / 2, viewportSize / 2 - 2, 0, Math.PI * 2)
  ctx.clip()
  ctx.drawImage(img, offsetX.value, offsetY.value, img.width * scale.value, img.height * scale.value)
  ctx.restore()
  ctx.strokeStyle = 'rgba(255,255,255,0.85)'
  ctx.lineWidth = 3
  ctx.beginPath()
  ctx.arc(viewportSize / 2, viewportSize / 2, viewportSize / 2 - 2, 0, Math.PI * 2)
  ctx.stroke()
}

function onPointerDown(e: PointerEvent) {
  dragging.value = true
  dragStart.value = { x: e.clientX - offsetX.value, y: e.clientY - offsetY.value }
}

function onPointerMove(e: PointerEvent) {
  if (!dragging.value) return
  offsetX.value = e.clientX - dragStart.value.x
  offsetY.value = e.clientY - dragStart.value.y
  draw()
}

function onPointerUp() {
  dragging.value = false
}

function onWheel(e: WheelEvent) {
  e.preventDefault()
  const delta = e.deltaY > 0 ? -0.05 : 0.05
  scale.value = Math.min(4, Math.max(0.2, scale.value + delta))
  draw()
}

function exportCircularAvatar(): string | null {
  const img = image.value
  if (!img) return null

  const exportCanvas = document.createElement('canvas')
  exportCanvas.width = outputSize
  exportCanvas.height = outputSize
  const ctx = exportCanvas.getContext('2d')
  if (!ctx) return null

  const ratio = outputSize / viewportSize
  ctx.beginPath()
  ctx.arc(outputSize / 2, outputSize / 2, outputSize / 2, 0, Math.PI * 2)
  ctx.clip()
  ctx.drawImage(
    img,
    offsetX.value * ratio,
    offsetY.value * ratio,
    img.width * scale.value * ratio,
    img.height * scale.value * ratio,
  )

  const dataUrl = exportCanvas.toDataURL('image/jpeg', 0.9)
  return dataUrl.split(',')[1] ?? null
}

function confirm() {
  const base64 = exportCircularAvatar()
  if (base64) emit('confirm', base64)
}

watch(() => props.file, (file) => loadImage(file ?? null))
watch([scale, offsetX, offsetY], draw)

onMounted(() => {
  loadImage(props.file)
  window.addEventListener('pointerup', onPointerUp)
})

onUnmounted(() => {
  window.removeEventListener('pointerup', onPointerUp)
})
</script>

<template>
  <Teleport to="body">
    <div
      v-if="visible"
      class="fixed inset-0 z-[100] flex items-center justify-center bg-black/75 px-4"
      @click.self="emit('cancel')"
    >
      <div class="w-full max-w-sm rounded-3xl border border-white/10 bg-game-input p-4 shadow-2xl">
        <h3 class="mb-3 text-center text-lg font-bold text-white">Crop Avatar</h3>
        <div
          class="relative mx-auto touch-none select-none overflow-hidden rounded-full border-4 border-white/20"
          :style="{ width: `${viewportSize}px`, height: `${viewportSize}px` }"
          @pointerdown="onPointerDown"
          @pointermove="onPointerMove"
          @wheel="onWheel"
        >
          <canvas ref="canvasRef" class="h-full w-full" />
          <div
            v-if="!imageLoaded"
            class="absolute inset-0 flex items-center justify-center text-sm text-white/60"
          >
            Loading...
          </div>
        </div>
        <p class="mt-2 text-center text-xs text-white/65">Drag to move, scroll to zoom</p>
        <div class="mt-4 flex gap-3">
          <button
            type="button"
            class="flex-1 rounded-pill border border-white/20 py-3 text-sm font-semibold text-white"
            @click="emit('cancel')"
          >
            Cancel
          </button>
          <button
            type="button"
            class="flex-1 rounded-pill bg-gradient-to-b from-game-cta-start to-game-cta-end py-3 text-sm font-semibold text-on-game"
            :disabled="!imageLoaded"
            @click="confirm"
          >
            Use Photo
          </button>
        </div>
      </div>
    </div>
  </Teleport>
</template>
