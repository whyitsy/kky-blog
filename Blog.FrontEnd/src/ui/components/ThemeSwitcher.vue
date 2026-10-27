<script setup lang="ts">
import { onBeforeUnmount, ref, watch } from 'vue'
import { useThemeStore } from '../runtime/store'

/**
 * 主题切换器（浮动按钮 + 面板）。
 *
 * 挂在公开站 Layout、后台 Layout、作者区 Layout 各一份；同一时刻只有一个布局在渲染，
 * 所以不会出现多个按钮。切换只改 `<html data-ui-theme/data-color-mode>`，
 * 颜色由 Token 全局生效，布局由主题的 shell/pages 接管（后台布局不参与换布局）。
 */
const theme = useThemeStore()

const open = ref(false)
const rootRef = ref<HTMLElement>()

function togglePanel() {
  open.value = !open.value
}

function close() {
  open.value = false
}

function pickTheme(id: string) {
  theme.setTheme(id)
}

function pickMode(id: string) {
  theme.setMode(id)
}

function onDocumentPointerDown(e: PointerEvent) {
  if (!open.value) return
  const target = e.target as Node
  if (rootRef.value && !rootRef.value.contains(target)) close()
}

function onDocumentKeydown(e: KeyboardEvent) {
  if (e.key === 'Escape' && open.value) close()
}

watch(open, (value) => {
  if (value) {
    document.addEventListener('pointerdown', onDocumentPointerDown, true)
    document.addEventListener('keydown', onDocumentKeydown)
  } else {
    document.removeEventListener('pointerdown', onDocumentPointerDown, true)
    document.removeEventListener('keydown', onDocumentKeydown)
  }
})

onBeforeUnmount(() => {
  document.removeEventListener('pointerdown', onDocumentPointerDown, true)
  document.removeEventListener('keydown', onDocumentKeydown)
})
</script>

<template>
  <div ref="rootRef" class="theme-switcher">
    <Transition name="pop">
      <section v-if="open" class="panel" role="dialog" aria-label="切换主题">
        <header class="panel-header">
          <div>
            <h2 class="panel-title">主题</h2>
            <p class="panel-sub">颜色与布局一起切换，本地保存</p>
          </div>
          <button class="close" aria-label="关闭主题面板" @click="close">
            <svg
              viewBox="0 0 24 24"
              width="16"
              height="16"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
            >
              <path d="M18 6 6 18M6 6l12 12" />
            </svg>
          </button>
        </header>

        <div class="theme-list">
          <button
            v-for="item in theme.themes"
            :key="item.id"
            class="theme-card"
            :class="{ active: item.id === theme.theme.id }"
            @click="pickTheme(item.id)"
          >
            <span
              class="swatch"
              :style="{
                background: item.preview.background,
                borderColor: item.preview.surface,
              }"
            >
              <span class="swatch-surface" :style="{ background: item.preview.surface }" />
              <span class="swatch-dots">
                <i :style="{ background: item.preview.primary }" />
                <i :style="{ background: item.preview.accent }" />
                <i :style="{ background: item.preview.text }" />
              </span>
            </span>
            <span class="theme-text">
              <span class="theme-name">{{ item.name }}</span>
              <span class="theme-desc">{{ item.description }}</span>
            </span>
            <span v-if="item.id === theme.theme.id" class="check" aria-hidden="true">
              <svg
                viewBox="0 0 24 24"
                width="14"
                height="14"
                fill="none"
                stroke="currentColor"
                stroke-width="3"
                stroke-linecap="round"
                stroke-linejoin="round"
              >
                <path d="m5 12 4.5 4.5L19 7" />
              </svg>
            </span>
          </button>
        </div>

        <div class="mode-block">
          <span class="mode-label">配色</span>
          <div class="mode-row" role="group" aria-label="配色模式">
            <button
              v-for="item in theme.modes"
              :key="item.id"
              class="mode-btn"
              :class="{ active: item.id === theme.mode.id }"
              @click="pickMode(item.id)"
            >
              <svg
                v-if="item.scheme === 'light'"
                viewBox="0 0 24 24"
                width="14"
                height="14"
                fill="none"
                stroke="currentColor"
                stroke-width="2"
                stroke-linecap="round"
              >
                <circle cx="12" cy="12" r="4" />
                <path d="M12 2v2m0 16v2M4.9 4.9l1.4 1.4m11.4 11.4 1.4 1.4M2 12h2m16 0h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4" />
              </svg>
              <svg
                v-else
                viewBox="0 0 24 24"
                width="14"
                height="14"
                fill="none"
                stroke="currentColor"
                stroke-width="2"
                stroke-linecap="round"
              >
                <path d="M21 12.8A9 9 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8Z" />
              </svg>
              {{ item.label }}
            </button>
          </div>
        </div>
      </section>
    </Transition>

    <button
      class="trigger"
      :class="{ open }"
      :aria-expanded="open"
      aria-label="切换主题"
      @click="togglePanel"
    >
      <svg
        viewBox="0 0 24 24"
        width="19"
        height="19"
        fill="none"
        stroke="currentColor"
        stroke-width="2"
        stroke-linecap="round"
        stroke-linejoin="round"
      >
        <circle cx="13.5" cy="6.5" r=".5" fill="currentColor" />
        <circle cx="17.5" cy="10.5" r=".5" fill="currentColor" />
        <circle cx="8.5" cy="7.5" r=".5" fill="currentColor" />
        <circle cx="6.5" cy="12.5" r=".5" fill="currentColor" />
        <path d="M12 2a10 10 0 1 0 0 20 2.5 2.5 0 0 0 2-4 2.5 2.5 0 0 1 2-4h2a4 4 0 0 0 4-4 10 10 0 0 0-10-8Z" />
      </svg>
    </button>
  </div>
</template>

<style scoped>
.theme-switcher {
  position: fixed;
  right: var(--space-5);
  bottom: var(--space-5);
  z-index: var(--z-toast);
}

.trigger {
  display: grid;
  place-items: center;
  width: 44px;
  height: 44px;
  border: 1px solid var(--border-default);
  border-radius: 50%;
  color: var(--text-default);
  background: var(--glass-bg);
  -webkit-backdrop-filter: blur(var(--glass-blur));
  backdrop-filter: blur(var(--glass-blur));
  box-shadow: var(--shadow-md);
  transition:
    color var(--transition-fast),
    border-color var(--transition-fast),
    transform var(--transition-fast);
}
.trigger:hover,
.trigger.open {
  color: var(--brand-500);
  border-color: var(--brand-500);
  transform: translateY(-1px);
}

.panel {
  position: absolute;
  right: 0;
  bottom: calc(100% + var(--space-3));
  width: 318px;
  padding: var(--space-5);
  background: var(--bg-surface);
  border: 1px solid var(--border-default);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-lg);
}

.panel-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: var(--space-3);
  margin-bottom: var(--space-4);
}

.panel-title {
  font: var(--text-h3);
}

.panel-sub {
  margin-top: 2px;
  font: var(--text-caption);
  color: var(--text-subtle);
}

.close {
  display: grid;
  place-items: center;
  width: 26px;
  height: 26px;
  border-radius: var(--radius-xs);
  color: var(--text-subtle);
}
.close:hover {
  color: var(--text-strong);
  background: var(--bg-raised);
}

.theme-list {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
}

.theme-card {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  width: 100%;
  padding: var(--space-2) var(--space-3);
  text-align: left;
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-md);
  background: var(--bg-canvas);
  transition:
    border-color var(--transition-fast),
    background var(--transition-fast);
}
.theme-card:hover {
  border-color: var(--border-strong);
}
.theme-card.active {
  border-color: var(--brand-500);
  background: color-mix(in srgb, var(--brand-500) 8%, var(--bg-canvas));
}

.swatch {
  position: relative;
  display: flex;
  align-items: flex-end;
  width: 46px;
  height: 38px;
  flex-shrink: 0;
  padding: 5px;
  border: 1px solid var(--border-default);
  border-radius: var(--radius-sm);
  overflow: hidden;
}

.swatch-surface {
  position: absolute;
  top: 6px;
  left: 6px;
  right: 6px;
  height: 9px;
  border-radius: 2px;
}

.swatch-dots {
  display: flex;
  gap: 3px;
}
.swatch-dots i {
  width: 6px;
  height: 6px;
  border-radius: 50%;
}

.theme-text {
  display: flex;
  flex-direction: column;
  gap: 1px;
  min-width: 0;
}

.theme-name {
  font-size: 14px;
  font-weight: 600;
  color: var(--text-strong);
}

.theme-desc {
  overflow: hidden;
  font: var(--text-caption);
  color: var(--text-subtle);
  text-overflow: ellipsis;
  white-space: nowrap;
}

.check {
  display: grid;
  place-items: center;
  margin-left: auto;
  width: 20px;
  height: 20px;
  flex-shrink: 0;
  border-radius: 50%;
  color: var(--text-on-brand);
  background: var(--brand-500);
}

.mode-block {
  margin-top: var(--space-4);
  padding-top: var(--space-4);
  border-top: 1px solid var(--border-subtle);
}

.mode-label {
  display: block;
  margin-bottom: var(--space-2);
  font: var(--text-caption);
  font-weight: 600;
  letter-spacing: 1px;
  text-transform: uppercase;
  color: var(--text-muted);
}

.mode-row {
  display: flex;
  gap: var(--space-2);
}

.mode-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: var(--space-1);
  flex: 1;
  min-height: 32px;
  padding: 0 var(--space-2);
  font-size: 13px;
  color: var(--text-muted);
  border: 1px solid var(--border-default);
  border-radius: var(--radius-sm);
  transition:
    color var(--transition-fast),
    border-color var(--transition-fast),
    background var(--transition-fast);
}
.mode-btn:hover {
  color: var(--text-strong);
  border-color: var(--border-strong);
}
.mode-btn.active {
  color: var(--brand-500);
  border-color: var(--brand-500);
  background: color-mix(in srgb, var(--brand-500) 10%, transparent);
}

.pop-enter-active,
.pop-leave-active {
  transition:
    opacity var(--transition-fast),
    transform var(--transition-fast);
  transform-origin: bottom right;
}
.pop-enter-from,
.pop-leave-to {
  opacity: 0;
  transform: scale(0.96) translateY(6px);
}
</style>
