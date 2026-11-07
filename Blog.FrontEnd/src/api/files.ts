import { upload } from './http'

/** 上传文件（图片等），返回后端可访问的相对 URL */
export function uploadFile(file: File): Promise<string> {
  return upload<{ url: string }>('/api/files/upload', file).then((res) => res.url)
}

/**
 * 与 `FileStorage:MaxFileSize` 对齐。后端超限会返回 413，这里先拦一次是为了
 * 不把整张图读进内存再被拒（大图在浏览器里同样占内存）。
 */
const MAX_IMAGE_BYTES = 10 * 1024 * 1024

/** 后端 `LocalFileStorageService.AllowedExtensions` 的图片子集；刻意不含 svg */
const MIME_EXTENSION: Record<string, string> = {
  'image/png': 'png',
  'image/jpeg': 'jpg',
  'image/jpg': 'jpg',
  'image/gif': 'gif',
  'image/webp': 'webp',
  'image/x-icon': 'ico',
  'image/vnd.microsoft.icon': 'ico',
}

const URL_EXTENSION = /\.(png|jpe?g|gif|webp|ico)$/i

/**
 * 把正文里的在线图片抓下来，转存到本站 `/api/files/`，返回站内相对地址。
 * 语雀 CDN 与站外图片走同一条链路，差别只在对方的 CORS/防盗链策略。
 *
 * <h3>为什么是 `referrerPolicy: 'no-referrer'`</h3>
 * 语雀 CDN 有 Referer 防盗链：带本站域名（或任何非语雀域名）的 Referer 会直接 403。
 * 而 nginx 给全站下发了 `Referrer-Policy: strict-origin-when-cross-origin`，
 * 浏览器跨域抓图默认会带上本站 Referer —— 必须在这个请求上显式关掉。
 * 实测：不带 Referer 返回 200，且响应头有 `access-control-allow-origin: *`，
 * 所以浏览器可以读响应体。（服务器端抓取虽然更稳，但会引入出站 SSRF 面；
 * 对"作者在编辑器里手动导入"这个场景，浏览器直连是更小的代价。）
 * 站外站点如果不允许跨域读取，会在上层编排里变成"单张图片失败"，原链接保留。
 *
 * <h3>为什么限制格式</h3>
 * 本站上传白名单不接受 SVG（可内嵌脚本，同源下发会形成存储型 XSS），
 * 这里提前给出可读的提示，而不是让后端抛一个笼统的"不支持的文件类型"。
 */
export async function importRemoteImage(url: string): Promise<string> {
  // HTTPS 页面抓 http:// 图片会被浏览器按混合内容直接拦截；提前给可操作的提示
  if (/^http:\/\//i.test(url) && typeof window !== 'undefined' && window.location.protocol === 'https:') {
    throw new Error('HTTPS 页面无法抓取 HTTP 图片，请把图片地址改成 HTTPS')
  }

  // 串行导入时一个挂起的请求会卡住整篇文章，所以给单张图片一个硬超时
  const controller = new AbortController()
  const timer = setTimeout(() => controller.abort(), 20_000)

  try {
    let response: Response
    try {
      response = await fetch(url, {
        mode: 'cors',
        credentials: 'omit',
        referrerPolicy: 'no-referrer',
        signal: controller.signal,
      })
    } catch {
      // fetch 的 TypeError 只有 "Failed to fetch"，对作者没有意义
      throw new Error(
        controller.signal.aborted
          ? '下载超时（20 秒），请检查图片地址是否可访问'
          : '浏览器无法读取该图片（可能受 CORS 或防盗链限制）',
      )
    }

    if (!response.ok) {
      if (response.status === 403) {
        throw new Error('图片源拒绝访问（HTTP 403），可能开启了防盗链')
      }
      throw new Error(`下载失败（HTTP ${response.status}）`)
    }

    const declared = Number(response.headers.get('content-length') ?? '')
    if (Number.isFinite(declared) && declared > MAX_IMAGE_BYTES) {
      throw new Error('图片超过 10MB，请先压缩后再导入')
    }

    let blob: Blob
    try {
      blob = await response.blob()
    } catch {
      throw new Error(
        controller.signal.aborted ? '下载超时（20 秒），请检查图片地址是否可访问' : '图片读取中断，请重试',
      )
    }
    if (blob.size === 0) throw new Error('图片内容为空')
    if (blob.size > MAX_IMAGE_BYTES) throw new Error('图片超过 10MB，请先压缩后再导入')

    const contentType = (blob.type || '').split(';')[0]!.trim().toLowerCase()

    // 优先信 Content-Type；少数 CDN 会返回 application/octet-stream，此时按 URL 后缀兜底。
    // 明确是 text/html / image/svg+xml 这类不能收的类型时不会走到兜底。
    const allowUrlFallback = contentType === '' || contentType === 'application/octet-stream'
    const extension = MIME_EXTENSION[contentType] ?? (allowUrlFallback ? extensionFromUrl(url) : '')
    if (!extension) {
      throw new Error(`不支持的图片格式（${contentType || '未知类型'}）`)
    }

    // 文件名只用于后端判断扩展名，真正的落盘名由后端生成（GUID + 转换后的扩展名）
    const file = new File([blob], `imported.${extension}`, { type: contentType })
    return await uploadFile(file)
  } finally {
    clearTimeout(timer)
  }
}

function extensionFromUrl(url: string): string {
  try {
    const match = URL_EXTENSION.exec(new URL(url, 'https://placeholder.invalid').pathname)
    if (!match) return ''
    const extension = match[1]!.toLowerCase()
    return extension === 'jpeg' ? 'jpg' : extension
  } catch {
    return ''
  }
}
