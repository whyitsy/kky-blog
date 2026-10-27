import type {
  CollectionDetailDto,
  CollectionDto,
  PagedResult,
  PostDetailDto,
  PostListItemDto,
  TagDto,
} from '@/types'

/** 可被主题接管的公开站页面 */
export type PageId =
  | 'home'
  | 'post-list'
  | 'post-detail'
  | 'archive'
  | 'taxonomy'
  | 'collection-list'
  | 'collection-detail'
  | 'not-found'

/** 主题页面组件的统一 Props 形态（各页面模型见下方定义） */
export interface ThemePageProps<TModel> {
  model: TModel
}

// ---------------------------------------------------------------- home

export interface HomePageModel {
  result: PagedResult<PostListItemDto> | null
  loading: boolean
  goPage: (page: number) => void
  /** 首页侧栏：热门标签（已按文章数降序，最多 10 个） */
  tags: TagDto[]
  /** 首页侧栏：精选专栏（已按 sortOrder 排序，最多 3 个） */
  collections: CollectionDto[]
}

// ---------------------------------------------------------------- post list

export interface PostListPageModel {
  result: PagedResult<PostListItemDto> | null
  loading: boolean
  /** 页面大标题（如「搜索『vue』」「分类：后端」） */
  title: string
  /** 标题下方说明，如「共 N 篇文章」 */
  subtitle: string
  goPage: (page: number) => void
}

// ---------------------------------------------------------------- post detail

export interface TocItem {
  id: string
  text: string
  level: number
}

export interface PostDetailPageModel {
  post: PostDetailDto | null
  loading: boolean
  /** 文章不存在（业务 4040 / 裸 404） */
  notFound: boolean
  /** 服务端故障 / 网络异常 */
  loadFailed: boolean
  /** 已渲染并净化后的正文 HTML */
  renderedContent: string
  toc: TocItem[]
  activeHeading: string
  /** 0-100 */
  readProgress: number
  readMinutes: number
  /** 已格式化的发布日期，如 2026/10/28 */
  publishedLabel: string
  retry: () => void
  scrollToHeading: (id: string) => void
}

// ---------------------------------------------------------------- archive

export interface ArchiveItemModel {
  id: string
  title: string
  /** 月-日，如 10-28 */
  dateLabel: string
  publishedAt: string
}

export interface ArchiveMonthModel {
  month: number
  items: ArchiveItemModel[]
}

export interface ArchiveYearModel {
  year: number
  months: ArchiveMonthModel[]
}

export interface ArchivePageModel {
  total: number
  loading: boolean
  years: ArchiveYearModel[]
}

// ---------------------------------------------------------------- taxonomy（标签墙 / 分类墙）

export interface TaxonomyItemModel {
  id: string
  name: string
  count: number
}

export interface TaxonomyPageModel {
  kind: 'tag' | 'category'
  eyebrow: string
  title: string
  subtitle: string
  items: TaxonomyItemModel[]
  loading: boolean
  onSelect: (id: string) => void
}

// ---------------------------------------------------------------- collections

export interface CollectionListPageModel {
  collections: CollectionDto[]
  loading: boolean
}

export interface CollectionDetailPageModel {
  detail: CollectionDetailDto | null
  loading: boolean
  notFound: boolean
  loadFailed: boolean
  retry: () => void
}

// ---------------------------------------------------------------- 404

export interface NotFoundPageModel {
  attemptedPath: string
  goHome: () => void
  goBack: () => void
}

// ---------------------------------------------------------------- 类型映射

export interface PageModelMap {
  home: HomePageModel
  'post-list': PostListPageModel
  'post-detail': PostDetailPageModel
  archive: ArchivePageModel
  taxonomy: TaxonomyPageModel
  'collection-list': CollectionListPageModel
  'collection-detail': CollectionDetailPageModel
  'not-found': NotFoundPageModel
}
