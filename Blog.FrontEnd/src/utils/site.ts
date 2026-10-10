/**
 * 站点级默认值。
 *
 * `VITE_SITE_NAME` 定义在 `.env`（随环境共享、**不是密钥** —— `VITE_` 前缀的变量会被
 * 打进客户端产物，任何人都能读到）。
 *
 * 这里做**唯一**的兜底：默认站点名以前散落在 5 个文件里各写一份字面量，
 * 改名字要全局搜索。现在改名只动 `.env`，改兜底只动这一行。
 */
export const DEFAULT_SITE_NAME = import.meta.env.VITE_SITE_NAME || "kky's blog"
