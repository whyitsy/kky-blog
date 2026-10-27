import { computed, ref, watch, type Component, type ComputedRef } from 'vue'
import { defineStore } from 'pinia'
import type { PageId } from '../contracts/pages'
import {
  getDefaultTheme,
  getTheme,
  listThemes,
  resolvePageComponent,
  resolveShell,
} from './registry'
import type { ThemeMode } from './types'

/**
 * 主题状态。
 *
 * 持久化键必须与 `index.html` 的内联首屏脚本保持一致（两处都写死字符串，
 * 因为内联脚本在模块加载前执行，不能 import）：
 *   - `blog-ui-theme`：主题 id
 *   - `blog-ui-mode`：配色模式 id
 *
 * DOM 契约：`<html data-ui-theme="aurora" data-color-mode="dark">`
 */
export const THEME_STORAGE_KEY = 'blog-ui-theme'
export const MODE_STORAGE_KEY = 'blog-ui-mode'

function readStorage(key: string): string {
  try {
    return localStorage.getItem(key) ?? ''
  } catch {
    return ''
  }
}

function writeStorage(key: string, value: string): void {
  try {
    localStorage.setItem(key, value)
  } catch {
    /* 隐私模式下 localStorage 可能不可写；主题仍在本次会话内生效 */
  }
}

/** 默认主题/模式：构建期 env 指定，非法值退化为第一个注册主题的默认模式 */
function initialThemeId(): string {
  const fromEnv = import.meta.env.VITE_DEFAULT_THEME
  const fromStorage = readStorage(THEME_STORAGE_KEY)
  const candidate = getTheme(fromStorage) ?? getTheme(fromEnv) ?? getDefaultTheme()
  return candidate.id
}

function initialModeId(themeId: string): string {
  const theme = getTheme(themeId) ?? getDefaultTheme()
  const fromEnv = import.meta.env.VITE_DEFAULT_COLOR_MODE
  const fromStorage = readStorage(MODE_STORAGE_KEY)
  const candidate = fromStorage || fromEnv
  return theme.modes.some((m) => m.id === candidate) ? candidate : theme.defaultMode
}

export const useThemeStore = defineStore('ui-theme', () => {
  const themeId = ref(initialThemeId())
  const modeId = ref(initialModeId(themeId.value))

  /** 当前主题插件（localStorage 里的 id 失效时回退默认主题） */
  const theme = computed(() => getTheme(themeId.value) ?? getDefaultTheme())
  const modes = computed<ThemeMode[]>(() => theme.value.modes)
  const mode = computed<ThemeMode>(
    () => modes.value.find((m) => m.id === modeId.value) ?? modes.value[0]!,
  )
  /** 当前配色是否亮色（Giscus 等第三方组件需要） */
  const isLight = computed(() => mode.value.scheme === 'light')
  const themes = computed(() => listThemes())
  const shell = computed<Component>(() => resolveShell(theme.value))

  /** 外部主题插件的运行时 CSS：注册时给了 css 字符串才注入 */
  let injectedStyle: HTMLStyleElement | null = null
  function syncPluginCss(): void {
    if (!theme.value.css) {
      injectedStyle?.remove()
      injectedStyle = null
      return
    }
    if (!injectedStyle) {
      injectedStyle = document.createElement('style')
      injectedStyle.dataset.uiThemeCss = theme.value.id
      document.head.appendChild(injectedStyle)
    }
    injectedStyle.dataset.uiThemeCss = theme.value.id
    injectedStyle.textContent = theme.value.css
  }

  /** 把当前主题/模式写到 <html>；index.html 首屏脚本会先执行一次同样的写入 */
  function apply(): void {
    const root = document.documentElement
    root.dataset.uiTheme = theme.value.id
    root.dataset.colorMode = mode.value.id
    // 让原生控件（滚动条、表单、日期选择器）跟随亮暗
    root.style.colorScheme = mode.value.scheme
    syncPluginCss()
  }

  /** 切换主题；当前配色在新主题里不存在时落到新主题默认模式 */
  function setTheme(id: string): void {
    const next = getTheme(id)
    if (!next) return
    themeId.value = next.id
    if (!next.modes.some((m) => m.id === modeId.value)) {
      modeId.value = next.defaultMode
    }
    writeStorage(THEME_STORAGE_KEY, themeId.value)
    writeStorage(MODE_STORAGE_KEY, modeId.value)
    apply()
  }

  function setMode(id: string): void {
    if (!modes.value.some((m) => m.id === id)) return
    modeId.value = id
    writeStorage(MODE_STORAGE_KEY, modeId.value)
    apply()
  }

  /** 在同一个主题的亮/暗模式间轮换（给导航栏的快捷切换用） */
  function toggleScheme(): void {
    const light = modes.value.find((m) => m.scheme === 'light')
    const dark = modes.value.find((m) => m.scheme === 'dark')
    const next = isLight.value ? dark : light
    if (next) setMode(next.id)
  }

  // 其它地方直接改 ref（如 devtools）时也保证 DOM 同步
  watch([theme, mode], apply)

  return {
    themeId,
    modeId,
    theme,
    themes,
    modes,
    mode,
    isLight,
    shell,
    apply,
    setTheme,
    setMode,
    toggleScheme,
  }
})

/** 当前主题的某个页面组件；未实现（且继承链也没有）时返回 null */
export function useThemePage(page: PageId): ComputedRef<Component | null> {
  const store = useThemeStore()
  return computed(() => resolvePageComponent(store.theme, page))
}
