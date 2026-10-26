/**
 * 从 `.md` 文件内容中解析出 front matter 与正文。
 *
 * 刻意**不引入 YAML 库**：项目的前端依赖极简，而这里只需要 front matter 的常见子集
 * （标量、引号字符串、行内数组、块状列表、布尔）。不支持的写法（嵌套对象、多行块标量）
 * 会被忽略，而不是报错 —— 导入是「尽量帮用户填好」，不是「校验 YAML 合规」。
 */

export type FrontMatterValue = string | string[] | boolean

export interface ParsedMarkdownFile {
  /** front matter 的键值；键保持文件里的写法（大小写不敏感匹配由调用方处理） */
  data: Record<string, FrontMatterValue>
  /** 去掉 front matter 之后的正文 */
  content: string
}

const FENCE = '---'

export function parseMarkdownFile(raw: string): ParsedMarkdownFile {
  // 去 BOM，统一换行，避免同一份文件在不同编辑器保存后产生两套解析路径
  const text = raw.replace(/^\uFEFF/, '').replace(/\r\n?/g, '\n')

  const lines = text.split('\n')
  if (lines[0]?.trim() !== FENCE) {
    return { data: {}, content: text }
  }

  // 找闭合的 `---`；找不到就认为整份文件是正文（宁可当正文，也不吞掉用户内容）
  let endLine = -1
  for (let i = 1; i < lines.length; i++) {
    if (lines[i]!.trim() === FENCE) {
      endLine = i
      break
    }
  }
  if (endLine < 0) {
    return { data: {}, content: text }
  }

  const data = parseFrontMatterBlock(lines.slice(1, endLine))
  const content = lines
    .slice(endLine + 1)
    .join('\n')
    .replace(/^\n/, '') // 分隔线后的那一个空行属于格式，不属于正文

  return { data, content }
}

function parseFrontMatterBlock(lines: string[]): Record<string, FrontMatterValue> {
  const data: Record<string, FrontMatterValue> = {}

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i]!
    const trimmed = line.trim()
    if (!trimmed || trimmed.startsWith('#')) continue

    const pair = /^([A-Za-z0-9_.-]+)\s*:\s*(.*)$/.exec(line)
    if (!pair) continue

    const key = pair[1]!
    const value = pair[2]!.trim()

    if (value === '') {
      // `tags:` 后跟若干 `- item` 的块状列表
      const items: string[] = []
      let j = i + 1
      for (; j < lines.length; j++) {
        const item = /^\s*-\s+(.*)$/.exec(lines[j]!)
        if (!item) break
        items.push(parseScalar(item[1]!))
      }
      if (items.length > 0) {
        data[key] = items
        i = j - 1
      }
      continue
    }

    data[key] = parseValue(value)
  }

  return data
}

function parseValue(value: string): FrontMatterValue {
  if (value.startsWith('[') && value.endsWith(']')) {
    return value
      .slice(1, -1)
      .split(',')
      .map((item) => parseScalar(item))
      .filter((item) => item !== '')
  }

  if (value === 'true') return true
  if (value === 'false') return false

  return parseScalar(value)
}

function parseScalar(value: string): string {
  const trimmed = value.trim()
  if (trimmed.length >= 2) {
    const first = trimmed[0]
    const last = trimmed[trimmed.length - 1]
    if (first === '"' && last === '"') {
      return trimmed.slice(1, -1).replace(/\\"/g, '"')
    }
    if (first === "'" && last === "'") {
      return trimmed.slice(1, -1).replace(/''/g, "'")
    }
  }
  return trimmed
}

// ---------------------------------------------------------------- 映射到表单

/** 分类 / 标签在导入时只按名字匹配，因此只需要 id + name */
export interface NamedItem {
  id: string
  name: string
}

export interface BuildImportPlanInput {
  fileName: string
  text: string
  categories: NamedItem[]
  tags: NamedItem[]
}

export interface MarkdownImportPlan {
  title?: string
  summary?: string
  content: string
  /** undefined = 不改动分类；命中时为分类 id */
  categoryId?: string | null
  /** undefined = 不改动标签；空数组表示清空标签 */
  tagIds?: string[]
  coverImage?: string
  /** undefined = 不改动；false = 取消「创建后立即发布」 */
  publish?: boolean
  /** 文件里写了但数据库里不存在、需要调用方决定是否创建的标签名 */
  missingTags: string[]
  /** 同上，分类名 */
  missingCategories: string[]
}

/** 与后端 FieldLimits.PostTitle / Posts.Title 对齐 */
const TITLE_MAX = 200
/** 与后端 FieldLimits.PostSummary / Posts.Summary 对齐 */
const SUMMARY_MAX = 200
/** Posts.CoverImage 是 varchar(500) */
const COVER_MAX = 500
/** 与后端 MediaPath.OwnedPrefix 对齐 */
const OWNED_PREFIX = '/api/files/'

/**
 * 把一份 `.md` 文件映射成「要填到表单里的字段」。
 *
 * 纯函数、无副作用：不创建标签/分类，也不发请求。
 * 缺失的标签/分类通过 `missingTags` / `missingCategories` 返回，
 * 由调用方决定「管理员自动创建、作者忽略」。
 */
export function buildImportPlan(input: BuildImportPlanInput): MarkdownImportPlan {
  const parsed = parseMarkdownFile(input.text)
  const data = lowerCaseKeys(parsed.data)

  const title = truncate(firstString(data['title']) ?? stripMarkdownExtension(input.fileName), TITLE_MAX)
  const summary = truncate(
    firstString(data['summary']) ??
      firstString(data['description']) ??
      firstString(data['excerpt']),
    SUMMARY_MAX,
  )

  const { tagIds, missingTags } = matchTags(data['tags'], input.tags)
  const { categoryId, missingCategories } = matchCategory(
    data['category'] ?? data['categories'],
    input.categories,
  )

  const cover = firstString(data['cover']) ?? firstString(data['coverImage'])
  const coverImage = cover !== undefined && isOwnedMediaPath(cover) ? cover : undefined

  const publish = data['draft'] === true || data['published'] === false ? false : undefined

  return {
    title,
    summary,
    content: parsed.content,
    categoryId,
    tagIds,
    coverImage,
    publish,
    missingTags,
    missingCategories,
  }
}

function lowerCaseKeys(data: Record<string, FrontMatterValue>): Record<string, FrontMatterValue> {
  const result: Record<string, FrontMatterValue> = {}
  for (const [key, value] of Object.entries(data)) {
    result[key.toLowerCase()] = value
  }
  return result
}

function firstString(value: FrontMatterValue | undefined): string | undefined {
  if (typeof value !== 'string') return undefined
  const trimmed = value.trim()
  return trimmed.length > 0 ? trimmed : undefined
}

function toStringList(value: FrontMatterValue | undefined): string[] {
  if (value === undefined) return []
  if (Array.isArray(value)) return value.map((item) => item.trim()).filter((item) => item.length > 0)
  if (typeof value === 'boolean') return []
  return value
    .split(',')
    .map((item) => item.trim())
    .filter((item) => item.length > 0)
}

function stripMarkdownExtension(fileName: string): string {
  return fileName.replace(/\.(md|markdown)$/i, '').trim()
}

function truncate(value: string | undefined, max: number): string | undefined {
  if (value === undefined) return undefined
  return value.length > max ? value.slice(0, max) : value
}

function findByName(items: NamedItem[], name: string): NamedItem | undefined {
  const needle = name.trim().toLowerCase()
  return items.find((item) => item.name.trim().toLowerCase() === needle)
}

function matchTags(
  value: FrontMatterValue | undefined,
  tags: NamedItem[],
): { tagIds?: string[]; missingTags: string[] } {
  if (value === undefined) return { tagIds: undefined, missingTags: [] }

  const tagIds: string[] = []
  const missingTags: string[] = []

  for (const name of toStringList(value)) {
    const matched = findByName(tags, name)
    if (matched) {
      if (!tagIds.includes(matched.id)) tagIds.push(matched.id)
    } else if (!missingTags.includes(name)) {
      missingTags.push(name)
    }
  }

  return { tagIds, missingTags }
}

function matchCategory(
  value: FrontMatterValue | undefined,
  categories: NamedItem[],
): { categoryId?: string | null; missingCategories: string[] } {
  if (value === undefined) return { categoryId: undefined, missingCategories: [] }

  let categoryId: string | undefined
  const missingCategories: string[] = []

  for (const name of toStringList(value)) {
    const matched = findByName(categories, name)
    if (matched) {
      categoryId ??= matched.id
    } else if (!missingCategories.includes(name)) {
      missingCategories.push(name)
    }
  }

  return { categoryId, missingCategories }
}

/**
 * 与后端 `MediaPath.IsOwned` 同构的校验（这里只用于「要不要填封面」，
 * 真正的强校验仍在服务端）。不符合的直接忽略，绝不把外部链接塞进 `<img src>`。
 */
function isOwnedMediaPath(value: string): boolean {
  if (value.length > COVER_MAX) return false
  if (!value.startsWith(OWNED_PREFIX)) return false

  const relative = value.slice(OWNED_PREFIX.length)
  if (relative.length === 0) return false
  if (relative.startsWith('/') || relative.endsWith('/')) return false
  if (relative.includes('..') || relative.includes('//')) return false

  return [...relative].every((char) => /[A-Za-z0-9/._-]/.test(char))
}
