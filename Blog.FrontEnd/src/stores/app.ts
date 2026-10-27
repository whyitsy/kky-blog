import { defineStore } from 'pinia'

/**
 * 全局搜索弹窗开关。
 *
 * 主题（颜色/布局）状态见 `src/ui/runtime/store.ts` 的 `useThemeStore` ——
 * 主题属于 UI 层，不再放在这里。
 */
export const useSearchStore = defineStore('search', {
  state: () => ({ open: false }),
  actions: {
    show() {
      this.open = true
    },
    hide() {
      this.open = false
    },
  },
})
