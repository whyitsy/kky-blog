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

// ---------------------------------------------------------------- 备案信息

/**
 * ICP 备案号（工信部要求展示在网站底部，并链接到 beian.miit.gov.cn）。
 *
 * 备案号本身是公开信息，直接写在源码里即可；换域名/主体后改这一行。
 * 展示逻辑见 `src/ui/components/SiteFiling.vue`。
 */
export const ICP_LICENSE = '蜀ICP备2026066227号'

/** 工信部备案查询系统 */
export const ICP_LICENSE_URL = 'https://beian.miit.gov.cn/'

/**
 * 公安联网备案号（公安部要求展示在网站底部，并链接到 beian.mps.gov.cn）。
 *
 * 完整号码含签发机关简称，例如：`川公网安备51010802033504号`。
 * 留空时页面完全不渲染这一项，不会留下空链接。
 */
export const POLICE_FILING_LICENSE = '川公网安备51010802033504号'

/** 公安联网备案查询系统（带 code 可直接定位到本站备案记录） */
export const POLICE_FILING_URL =
  'https://beian.mps.gov.cn/#/query/webSearch?code=51010802033504'

