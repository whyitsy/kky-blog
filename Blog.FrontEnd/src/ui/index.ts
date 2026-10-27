import './foundation/index.css'
import { registerBuiltinThemes } from './themes'

/**
 * 独立 UI 层入口。
 *
 * - `main.ts` 只需要 `import '@/ui'`：会注入 foundation 样式并注册内置主题；
 * - 业务/视图从这里取 `useThemeStore` / `useThemePage` / 类型；
 * - 外部主题插件：`registerTheme(plugin)`（内置与插件同一入口）。
 */
registerBuiltinThemes()

export * from './runtime'
export * from './contracts/pages'
export * from './contracts/nav'
export { default as ThemeSwitcher } from './components/ThemeSwitcher.vue'
export { default as SiteLogo } from './components/SiteLogo.vue'
export { default as SearchModal } from './components/SearchModal.vue'
