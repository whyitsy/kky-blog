import type { Component } from 'vue'
import type { PageId } from '../contracts/pages'
import type { ThemePlugin } from './types'

/**
 * 主题注册表。
 *
 * 内置主题与外部「主题插件」走同一个入口：`registerTheme(plugin)`。
 * 解析时支持 `extends` 继承链 —— 子主题只覆盖想改的页面/外壳，其余回退到父主题。
 */

const registry = new Map<string, ThemePlugin>()
let defaultThemeId = ''

/** 注册（或覆盖）一个主题；后注册的同 id 主题会替换前者 */
export function registerTheme(plugin: ThemePlugin): void {
  if (!plugin?.id) throw new Error('[ui] 主题缺少 id')
  if (!plugin.shell) throw new Error(`[ui] 主题 ${plugin.id} 缺少 shell 组件`)
  if (!plugin.modes?.length) throw new Error(`[ui] 主题 ${plugin.id} 未声明任何配色模式`)
  if (!plugin.modes.some((m) => m.id === plugin.defaultMode)) {
    throw new Error(`[ui] 主题 ${plugin.id} 的 defaultMode(${plugin.defaultMode}) 不在 modes 里`)
  }
  if (!plugin.pages) plugin.pages = {}
  registry.set(plugin.id, plugin)
  if (!defaultThemeId) defaultThemeId = plugin.id
}

/** 设置默认主题（首个注册的主题默认生效） */
export function setDefaultTheme(id: string): void {
  if (!registry.has(id)) throw new Error(`[ui] 未知主题：${id}`)
  defaultThemeId = id
}

export function getTheme(id: string | null | undefined): ThemePlugin | undefined {
  return id ? registry.get(id) : undefined
}

export function getDefaultTheme(): ThemePlugin {
  const theme = registry.get(defaultThemeId) ?? registry.values().next().value
  if (!theme) throw new Error('[ui] 尚未注册任何主题')
  return theme
}

/** 切换器展示顺序（默认注册顺序；默认主题排最前） */
export function listThemes(): ThemePlugin[] {
  return [...registry.values()].sort((a, b) => {
    if (a.id === defaultThemeId) return -1
    if (b.id === defaultThemeId) return 1
    return 0
  })
}

/**
 * 解析主题继承链（自身 → 父 → 祖父…），带环检测。
 * 用于 `resolvePageComponent` / `resolveShell` 的 fallback。
 */
export function resolveThemeChain(theme: ThemePlugin): ThemePlugin[] {
  const chain: ThemePlugin[] = []
  const seen = new Set<string>()
  let current: ThemePlugin | undefined = theme
  while (current && !seen.has(current.id)) {
    chain.push(current)
    seen.add(current.id)
    current = current.extends ? registry.get(current.extends) : undefined
  }
  return chain
}

/** 当前主题（含继承链）能否提供该页面 */
export function resolvePageComponent(theme: ThemePlugin, page: PageId): Component | null {
  for (const item of resolveThemeChain(theme)) {
    const component = item.pages[page]
    if (component) return component
  }
  return null
}

/** 主题外壳：shell 不参与继承（布局骨架必须由主题自己给出） */
export function resolveShell(theme: ThemePlugin): Component {
  return theme.shell
}
