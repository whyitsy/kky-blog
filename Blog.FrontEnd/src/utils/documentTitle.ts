/**
 * 浏览器标签页标题的统一格式。
 *
 * 路由切走之后必须把 document.title 改回当前页面的标题，否则从文章详情离开时
 * tab 会一直停在上一篇文章的标题上（文章详情是唯一会在运行时覆盖标题的页面）。
 * 单独抽成纯函数是为了把「有页面标题 / 只有站点名 / 两边都空」这几种情况固定下来，
 * 避免以后在 router 的 afterEach 里手拼字符串又拼出 ` - ` 这种脏标题。
 */
export function formatDocumentTitle(pageTitle: string | undefined, siteName: string): string {
  const page = pageTitle?.trim() ?? ''
  const site = siteName.trim()
  if (page && site) return `${page} - ${site}`
  return page || site
}
