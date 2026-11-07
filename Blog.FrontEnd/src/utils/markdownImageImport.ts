/**
 * `.md` 导入时的图片本地化编排。
 *
 * <h3>为什么需要这一步</h3>
 * 语雀导出的 Markdown 里图片是 `https://cdn.nlark.com/...` 的 CDN 链接。直接入库，
 * 前台就是在盗链：语雀随时可以改地址/加防盗链，访客的 IP 也会泄露给第三方。
 * 本站的媒体统一走 `/api/files/`（`MediaPath` 只认这个前缀），所以导入时要把图
 * **下载下来、上传成本站文件、再把链接改掉**。
 *
 * <h3>两种图片来源</h3>
 *   · 在线图片（http/https，语雀 CDN 或其它站点）：由 `importRemote` 抓取并入库
 *     （见 `api/files.ts` 的 `importRemoteImage`）；
 *   · 本地相对路径（`images/xxx.png`）：导入时**先不动**，等用户选择图片文件夹后，
 *     这里按路径/文件名匹配后交给 `uploadLocal`（即现有的 `/api/files/upload`）；
 *     用户不选就保持原样，等同于「忽略」。
 *
 * <h3>为什么所有在线地址都尝试</h3>
 * 对作者来说「在线图片」不应该再分语雀/站外：只要能被浏览器读到，就转存到本站，
 * 避免盗链和第三方失效。第三方站点的 CORS/防盗链不受我们控制，读不到时保留原链接，
 * 在结果里报失败原因；这比"按域名决定要不要处理"更可预期。
 *
 * <h3>失败策略</h3>
 * 单张图片失败**不能**让整次导入失败：正文照常导入，失败的链接保留原样，
 * 由调用方在界面上提示。图片是内容的附属品，不能因为它挡住作者保存文章。
 *
 * 本模块刻意不直接碰 `fetch` / `uploadFile`：依赖由调用方注入，
 * 这样编排逻辑（去重、进度、失败汇总）可以在 Node 里用假实现覆盖。
 */

import {
  applyMarkdownImageRewrites,
  collectMarkdownImages,
  type MarkdownImageRef,
} from './markdownImages.ts'

/** 只要求「有文件名」，`File` 天然满足；测试里可以传轻量对象 */
export interface NamedImageFile {
  name: string
  /** 目录选择（webkitdirectory）时才有，如 `export/images/a.png` */
  webkitRelativePath?: string
}

export interface LocalizeMarkdownImagesOptions<N extends NamedImageFile> {
  /** 用户选择图片文件夹后拿到的文件（用于匹配正文里的本地相对路径） */
  files?: readonly N[]
  /** 语雀 CDN → 站内地址 */
  importRemote: (url: string) => Promise<string>
  /** 本地图片 → 站内地址 */
  uploadLocal: (file: N) => Promise<string>
  /** 已处理 / 总数。总数为 0 时不会调用 */
  onProgress?: (done: number, total: number) => void
}

export interface LocalizeFailure {
  /** 归一化后的原链接，便于作者回到正文里定位 */
  url: string
  reason: string
}

export interface LocalizeMarkdownImagesResult {
  content: string
  /** 成功改写的去重图片数 */
  localized: number
  failures: LocalizeFailure[]
  /** 正文引用了、但用户没有选中的本地图片（调用方据此决定要不要显示选文件夹入口） */
  missingLocal: string[]
}

/**
 * 把正文里的本地相对路径匹配到用户选中的图片文件。
 *
 * 匹配优先级：完整相对路径 → 路径后缀（目录选择时带上了上级目录）→ 唯一 basename。
 * 刻意**不做模糊猜测**：同名文件出现多个时宁可不匹配，让作者自己确认，
 * 也好过把 A 文章的图传到 B 文章里。
 *
 * @returns key 为传入的路径原值（与 `MarkdownImageRef.url` 对齐），value 为文件
 */
export function resolveLocalImageFiles<N extends NamedImageFile>(
  paths: readonly string[],
  files: readonly N[],
): Map<string, N> {
  const entries = files
    .filter((file) => isSupportedImageName(file.name))
    .map((file) => ({ file, path: normalizeLocalPath(filePathOf(file)) }))
    .filter((entry) => entry.path.length > 0)

  const matched = new Map<string, N>()

  for (const rawPath of paths) {
    if (matched.has(rawPath)) continue
    const target = normalizeLocalPath(rawPath)
    if (!target) continue

    const exact = entries.find((entry) => entry.path === target)
    if (exact) {
      matched.set(rawPath, exact.file)
      continue
    }

    const bySuffix = pickUnique(
      entries.filter(
        (entry) => entry.path.endsWith(`/${target}`) || target.endsWith(`/${entry.path}`),
      ),
    )
    if (bySuffix) {
      matched.set(rawPath, bySuffix.file)
      continue
    }

    const targetName = basename(target)
    const byName = pickUnique(entries.filter((entry) => basename(entry.path) === targetName))
    if (byName) matched.set(rawPath, byName.file)
  }

  return matched
}

export async function localizeMarkdownImages<N extends NamedImageFile>(
  markdown: string,
  options: LocalizeMarkdownImagesOptions<N>,
): Promise<LocalizeMarkdownImagesResult> {
  const images = collectMarkdownImages(markdown)
  const remoteImages = images.filter((image) => image.kind === 'remote')
  const localImages = images.filter((image) => image.kind === 'local')

  const localMatches = resolveLocalImageFiles(
    localImages.map((image) => image.url),
    options.files ?? [],
  )

  // 同一个本地文件可能被「images/a.png」与「./images/a.png」两种写法引用：
  // 按文件分组，保证只上传一次，再把结果写给所有引用它的链接。
  const localGroups = new Map<N, MarkdownImageRef[]>()
  for (const image of localImages) {
    const file = localMatches.get(image.url)
    if (!file) continue
    const group = localGroups.get(file)
    if (group) group.push(image)
    else localGroups.set(file, [image])
  }

  const missingLocal = localImages
    .filter((image) => !localMatches.has(image.url))
    .map((image) => image.url)

  const total = remoteImages.length + localGroups.size
  const replacements = new Map<string, string>()
  const failures: LocalizeFailure[] = []
  let done = 0

  if (total > 0) options.onProgress?.(0, total)

  // 串行处理：上传接口有令牌桶限流（POST /api 30 容量、2/s 补充），
  // 并发抓取反而更容易撞 429；串行的总耗时在文章级导入里可以接受。
  for (const image of remoteImages) {
    try {
      replacements.set(image.url, await options.importRemote(image.url))
    } catch (error) {
      failures.push({ url: image.url, reason: messageOf(error) })
    }
    done += 1
    options.onProgress?.(done, total)
  }

  for (const [file, group] of localGroups) {
    try {
      const stored = await options.uploadLocal(file)
      for (const image of group) replacements.set(image.url, stored)
    } catch (error) {
      const reason = messageOf(error)
      for (const image of group) failures.push({ url: image.url, reason })
    }
    done += 1
    options.onProgress?.(done, total)
  }

  return {
    content: applyMarkdownImageRewrites(markdown, replacements),
    localized: replacements.size,
    failures,
    missingLocal,
  }
}

/** 与后端 `AllowedExtensions` 的图片子集对齐（不含 svg，理由见 media.ts） */
function isSupportedImageName(name: string): boolean {
  return /\.(png|jpe?g|gif|webp|ico)$/i.test(name)
}

function filePathOf(file: NamedImageFile): string {
  return file.webkitRelativePath || file.name
}

function normalizeLocalPath(value: string): string {
  let path = value.trim()
  if (!path) return ''

  // 中文/空格文件名在 Markdown 里常被百分号编码；解码失败（孤立的 %）就保留原样
  try {
    path = decodeURIComponent(path)
  } catch {
    /* 保留原值 */
  }

  path = path.replace(/\\/g, '/')

  // 本地图片的 ?v=2 之类查询串对文件系统没有意义，匹配前去掉
  const query = path.indexOf('?')
  if (query >= 0) path = path.slice(0, query)

  while (path.startsWith('./')) path = path.slice(2)
  return path.replace(/\/{2,}/g, '/')
}

function basename(path: string): string {
  const segments = path.split('/')
  return segments[segments.length - 1] ?? path
}

function pickUnique<T>(items: readonly T[]): T | undefined {
  return items.length === 1 ? items[0] : undefined
}

function messageOf(error: unknown): string {
  return error instanceof Error ? error.message : String(error)
}
