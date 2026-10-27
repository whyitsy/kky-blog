import { registerTheme } from '../runtime/registry'
import aurora from './aurora'
import paper from './paper'

/**
 * 内置主题注册。
 *
 * 内置主题在构建期静态 import：切换时无需下载 JS/CSS，零闪烁。
 * 外部主题插件（运行时安装）走同一个 `registerTheme()`，可以选择异步加载后注册。
 */
export function registerBuiltinThemes(): void {
  registerTheme(aurora)
  registerTheme(paper)
}

export { aurora, paper }
