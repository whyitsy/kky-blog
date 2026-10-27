import type { Component } from 'vue'
import type { PageId } from '../contracts/pages'

/** 主题提供的一种配色模式（dark / light / ink / dusk…） */
export interface ThemeMode {
  id: string
  label: string
  /** 该模式是亮色还是暗色；用于 Giscus 等第三方组件的亮暗映射 */
  scheme: 'light' | 'dark'
}

/** 主题切换器里的色板预览 */
export interface ThemePreview {
  background: string
  surface: string
  text: string
  primary: string
  accent: string
}

/**
 * 主题插件。
 *
 * 一个主题由三部分组成：
 *   1. Token（颜色/字体/圆角/尺寸…）—— 写在主题包的 CSS 里，通过
 *      `[data-ui-theme='<id>'][data-color-mode='<mode>']` 选择器生效；
 *   2. 页面外壳（shell）—— 导航/页脚/搜索弹窗与整体布局编排，默认插槽是路由页面；
 *   3. 页面组件（pages）—— 接管公开站的页面布局。
 *
 * `extends` 允许主题继承另一个主题未覆盖的页面；`css` 允许外部插件在运行时注入样式。
 */
export interface ThemePlugin {
  /** 唯一 id，同时写入 <html data-ui-theme> */
  id: string
  name: string
  description: string
  /** 继承另一个主题；未实现的页面回退到父主题。注意 shell 与 modes 不继承 */
  extends?: string
  modes: ThemeMode[]
  defaultMode: string
  preview: ThemePreview
  /** 运行时注入的主题 CSS 字符串；内置主题使用构建期 CSS import，不用它 */
  css?: string
  /** 页面外壳：默认插槽 = 当前路由页面 */
  shell: Component
  /** 可主题化页面；缺失的页面回退到 extends 链 */
  pages: Partial<Record<PageId, Component>>
}
