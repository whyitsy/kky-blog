/**
 * Markdown 正文里的图片引用：识别、分类与改写。
 *
 * <h3>为什么单独一个模块</h3>
 * 「从 .md 导入」原有的 `markdownImport.ts` 负责 front matter 与表单映射，已经够长；
 * 图片本地化是另一件事：它要**理解 Markdown 语法边界**（代码块里的示例不能改）、
 * 要把在线图片 / 本地相对路径 / 忽略分开，还要能按「归一化后的 URL」查表替换。
 * 这些是纯字符串逻辑，和网络、表单都无关，独立出来才能被 `node --test` 直接覆盖。
 *
 * <h3>只处理两种语雀导出形态</h3>
 *   · `![alt](https://cdn.nlark.com/...)` —— 语雀 Markdown 导出的标准写法；
 *   · `<img src="...">` —— 少数导出/粘贴场景会保留 HTML。
 * 引用式图片（`![alt][id]` + `[id]: url`）语雀不产出，这里刻意不做，避免误伤正文。
 *
 * <h3>为什么跳过行内代码</h3>
 * 语雀的文章常常就是讲 Markdown/代码的，正文里会出现
 * `` `![](url)` `` 这种**示例**。只跳过围栏块还不够：行内代码同样属于「内容」，
 * 改写它会把作者想展示的示例改掉。`markdownFences.ts` 只处理围栏块，
 * 这里在它之上再切一层行内代码。
 *
 * <h3>URL 归一化</h3>
 * 语雀导出的图片地址常带 `#clientId=...&from=ui` 这类只对语雀编辑器有意义的 fragment，
 * 同一张图在正文里出现两次时 fragment 可能不同。去 fragment 后再去重，
 * 才能保证「同一张图只下载/上传一次」。空格与 `[object Object]` 是语雀导出历史上
 * 出现过的脏数据（见 `yuque-localize.py` 的注释），一并修掉。
 */

import { splitMarkdownChunks } from './markdownFences.ts'

export type MarkdownImageKind =
  /** http(s) 在线图片：不管语雀 CDN 还是别的站点，都尝试抓取转存 */
  | 'remote'
  /** 本地相对路径：导入时先不动，用户选择图片文件夹后才上传 */
  | 'local'
  /** data:/blob:/#锚点/站内绝对路径：不是本次要处理的对象 */
  | 'ignored'

export interface MarkdownImageRef {
  /** 归一化后的地址（去 fragment、空格转义），也是改写时查表的键 */
  url: string
  kind: MarkdownImageKind
}

/**
 * 行内图片。刻意只匹配 `![](...)`，不匹配 `![alt][id]`。
 * URL 部分不允许空白与右括号：地址里含这两种字符时必须用 `<...>` 包起来（CommonMark 的要求）。
 */
const MD_IMAGE = /!\[([^\]\n]*)\]\(\s*(<[^>\n]*>|[^\s)\n]+)((?:\s+"[^"\n]*"|\s+'[^'\n]*')?)\s*\)/g

/** `<img src=...>`，引号形式与裸值都接受 */
const HTML_IMAGE = /(<img\b[^>]*?\bsrc\s*=\s*)("[^"]*"|'[^']*'|[^\s>]+)/gi

export function normalizeImageUrl(raw: string): string {
  let url = raw.trim()
  if (url.startsWith('<') && url.endsWith('>')) url = url.slice(1, -1).trim()

  url = url.replace(/\[object Object\]/gi, '[object%20Object]').replace(/ /g, '%20')

  // fragment 只对语雀编辑器有意义，外部平台无法解析（也避免同一张图被当成多张）
  const hash = url.indexOf('#')
  return hash >= 0 ? url.slice(0, hash) : url
}

export function classifyImageUrl(raw: string): MarkdownImageKind {
  const url = raw.trim()
  if (!url) return 'ignored'
  if (url.startsWith('#')) return 'ignored'

  // 协议相对 URL 先于「站内绝对路径」判断：//cdn.example.com 是站外在线图片
  if (url.startsWith('//')) return 'remote'
  if (url.startsWith('/')) return 'ignored'

  const scheme = /^([a-z][a-z0-9+.-]*):/i.exec(url)?.[1]?.toLowerCase() ?? ''
  if (scheme) {
    // 在线图片只有这两种传输协议；data:/blob:/javascript: 等一律不碰
    return scheme === 'http' || scheme === 'https' ? 'remote' : 'ignored'
  }

  return 'local'
}

/**
 * 收集正文中的图片（按归一化 URL 去重，保持首次出现顺序）。
 *
 * 只收集、不改写；调用方根据 `kind` 决定哪些去抓、哪些去上传、哪些只提示。
 */
export function collectMarkdownImages(markdown: string): MarkdownImageRef[] {
  const seen = new Set<string>()
  const images: MarkdownImageRef[] = []

  mapMarkdownProse(markdown, (text) => {
    visitImageUrls(text, (raw) => {
      const url = normalizeImageUrl(raw)
      if (seen.has(url)) return
      seen.add(url)
      images.push({ url, kind: classifyImageUrl(url) })
    })
    return text
  })

  return images
}

/**
 * 把命中的图片地址替换成站内地址（`/api/files/...`）。
 *
 * `replacements` 的键必须与 `collectMarkdownImages` 返回的 `url` 一致。
 * 没有命中的链接保持原样 —— 导入是"尽量帮忙"，不是"校验并拒绝"。
 */
export function applyMarkdownImageRewrites(
  markdown: string,
  replacements: ReadonlyMap<string, string>,
): string {
  if (replacements.size === 0) return markdown

  return mapMarkdownProse(markdown, (text) => rewriteImageUrls(text, replacements))
}

function visitImageUrls(text: string, visit: (url: string) => void): void {
  // 两种语法分开扫，再按出现位置排序：同一段里可能有 Markdown 图片与 HTML 图片混排，
  // 先后顺序要按原文，不能「所有 Markdown 图片 → 所有 HTML 图片」。
  const found: Array<{ index: number; url: string }> = []

  for (const match of text.matchAll(MD_IMAGE)) {
    found.push({ index: match.index ?? 0, url: stripAngleBrackets(match[2]!) })
  }
  for (const match of text.matchAll(HTML_IMAGE)) {
    found.push({ index: match.index ?? 0, url: stripQuotes(match[2]!) })
  }

  found.sort((left, right) => left.index - right.index)
  for (const image of found) visit(image.url)
}

function rewriteImageUrls(text: string, replacements: ReadonlyMap<string, string>): string {
  let output = text.replace(MD_IMAGE, (whole, alt: string, target: string, title: string) => {
    const replacement = replacements.get(normalizeImageUrl(stripAngleBrackets(target)))
    return replacement === undefined ? whole : `![${alt}](${replacement}${title})`
  })

  output = output.replace(HTML_IMAGE, (whole, prefix: string, quoted: string) => {
    const quote = quoted.startsWith('"') || quoted.startsWith("'") ? quoted[0]! : ''
    const raw = quote ? quoted.slice(1, -1) : quoted
    const replacement = replacements.get(normalizeImageUrl(raw))
    return replacement === undefined ? whole : `${prefix}${quote}${replacement}${quote}`
  })

  return output
}

function stripAngleBrackets(value: string): string {
  const trimmed = value.trim()
  return trimmed.startsWith('<') && trimmed.endsWith('>') ? trimmed.slice(1, -1) : trimmed
}

function stripQuotes(value: string): string {
  if (value.length >= 2 && (value.startsWith('"') || value.startsWith("'")) && value[0] === value.at(-1)) {
    return value.slice(1, -1)
  }
  return value
}

/**
 * 只把「正文」交给 transform：围栏代码块原样保留，行内代码也原样保留。
 *
 * 各段按原顺序拼回去可逐字还原（transform 只做替换时），不会丢换行。
 */
function mapMarkdownProse(markdown: string, transform: (text: string) => string): string {
  return splitMarkdownChunks(markdown)
    .map((chunk) => {
      const text = chunk.lines.join('\n')
      if (chunk.fenced) return text
      return splitInlineCode(text)
        .map((segment) => (segment.code ? segment.text : transform(segment.text)))
        .join('')
    })
    .join('\n')
}

interface ProseSegment {
  code: boolean
  text: string
}

/**
 * 按行内代码切分。只认「同长度的反引号」闭合，符合 CommonMark 的主要形态；
 * 找不到闭合时整段按正文处理（宁可多处理，也不要吞掉用户内容）。
 */
function splitInlineCode(text: string): ProseSegment[] {
  const segments: ProseSegment[] = []
  const pattern = /(`+)([\s\S]*?)\1/g
  let last = 0

  for (const match of text.matchAll(pattern)) {
    const index = match.index ?? 0
    if (index > last) segments.push({ code: false, text: text.slice(last, index) })
    segments.push({ code: true, text: match[0] })
    last = index + match[0].length
  }

  if (last < text.length) segments.push({ code: false, text: text.slice(last) })
  return segments
}
