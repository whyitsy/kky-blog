<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { getPostDetail } from '@/api/posts'
import { isNotFoundError } from '@/api/http'
import { renderMarkdown } from '@/utils/markdown'
import { DEFAULT_SITE_NAME } from '@/utils/site'
import { useSiteStore } from '@/stores/site'
import type { PostDetailDto } from '@/types'
import { useThemePage, type PostDetailPageModel, type TocItem } from '@/ui'

/**
 * 文章详情数据控制器：拉数据、渲染并净化正文、生成目录、跟踪滚动高亮/阅读进度，
 * 把展示所需的一切组装成 `PostDetailPageModel`。
 */
const route = useRoute()
const site = useSiteStore()

/** 当前主题提供的文章详情页组件 */
const page = useThemePage('post-detail')

const post = ref<PostDetailDto | null>(null)
const loading = ref(true)
const notFound = ref(false)
const loadFailed = ref(false)

const toc = ref<TocItem[]>([])
const activeHeading = ref('')
const readProgress = ref(0)

/**
 * 给渲染后的 `<h2>/<h3>` 依次补 `id="heading-N"`。
 *
 * 在**渲染结果字符串**上做（而不是挂载后查询 `.article-body h2`）：
 * 锚点由数据层注入，任何主题直接 `v-html` 都天然可跳转，不再依赖某个主题的 DOM 结构。
 * 顺序与 `buildToc()` 解析 markdown 的顺序一致。
 */
function injectHeadingIds(html: string): string {
  if (!html) return ''
  let index = 0
  return html.replace(/<h([23])(\s[^>]*)?>/gi, (_match, level: string, attrs?: string) => {
    // 清掉作者可能自己带的 id，避免与目录 id 冲突
    const rest = (attrs ?? '').replace(/\sid=("[^"]*"|'[^']*'|[^\s>]+)/i, '')
    return `<h${level}${rest} id="heading-${index++}">`
  })
}

const renderedContent = computed(() => {
  if (!post.value) return ''
  return injectHeadingIds(renderMarkdown(post.value.content))
})

async function load(id: string) {
  loading.value = true
  notFound.value = false
  loadFailed.value = false
  post.value = null
  try {
    post.value = await getPostDetail(id)
    document.title = `${post.value.title} - ${site.config?.siteName || DEFAULT_SITE_NAME}`
    buildToc()
  } catch (e) {
    // 只有真正的「不存在」（业务码 4040 / 裸 404）才是 404 页；
    // 服务端故障与网络异常要说成加载失败，不能误导用户以为文章没了。
    if (isNotFoundError(e)) {
      notFound.value = true
    } else {
      loadFailed.value = true
      console.error('[post-detail] 加载文章失败：', e)
    }
  } finally {
    loading.value = false
  }
}

/**
 * 从 markdown 提取 h2/h3 生成目录（与 injectHeadingIds 的编号顺序一致）。
 *
 * 必须跳过围栏代码块：正文里出现的 ``` ``` 段落中的 `## 注释` 是代码而不是标题，
 * 旧实现用裸正则会在目录里生成「幽灵条目」，点进去还找不到对应 heading。
 */
function buildToc() {
  if (!post.value) return
  const items: TocItem[] = []
  let inFence = false
  let fenceMarker = ''
  let idx = 0

  for (const line of post.value.content.split(/\r?\n/)) {
    const fence = line.match(/^\s*(`{3,}|~{3,})/)
    if (fence) {
      const marker = fence[1]!
      if (!inFence) {
        inFence = true
        fenceMarker = marker[0]!
      } else if (marker[0] === fenceMarker) {
        inFence = false
      }
      continue
    }
    if (inFence) continue

    const heading = line.match(/^(#{2,3})\s+(.+)$/)
    if (!heading) continue
    items.push({
      id: `heading-${idx++}`,
      text: heading[2]!.replace(/[*`]/g, ''),
      level: heading[1]!.length,
    })
  }
  toc.value = items
}

function onScroll() {
  // 阅读进度
  const el = document.documentElement
  const total = el.scrollHeight - el.clientHeight
  readProgress.value = total > 0 ? Math.min(100, Math.round((window.scrollY / total) * 100)) : 0

  // 目录高亮：按 toc 的 id 逐个取元素（不再依赖某主题正文的 class 结构）
  let current = ''
  for (const item of toc.value) {
    const heading = document.getElementById(item.id)
    if (heading && heading.getBoundingClientRect().top < 120) current = item.id
  }
  activeHeading.value = current
}

function scrollToHeading(id: string) {
  document.getElementById(id)?.scrollIntoView({ behavior: 'smooth' })
}

function retry() {
  void load(String(route.params.id ?? ''))
}

watch(() => route.params.id, (id) => id && load(String(id)))

onMounted(() => {
  load(String(route.params.id))
  window.addEventListener('scroll', onScroll, { passive: true })
})
onBeforeUnmount(() => window.removeEventListener('scroll', onScroll))

function formatDate(iso: string | null) {
  if (!iso) return ''
  return new Date(iso).toLocaleDateString('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  })
}

const publishedLabel = computed(() => formatDate(post.value?.publishedAt ?? null))
const readMinutes = computed(() =>
  post.value ? Math.max(1, Math.round(post.value.wordCount / 400)) : 0,
)

const model = computed<PostDetailPageModel>(() => ({
  post: post.value,
  loading: loading.value,
  notFound: notFound.value,
  loadFailed: loadFailed.value,
  renderedContent: renderedContent.value,
  toc: toc.value,
  activeHeading: activeHeading.value,
  readProgress: readProgress.value,
  readMinutes: readMinutes.value,
  publishedLabel: publishedLabel.value,
  retry,
  scrollToHeading,
}))
</script>

<template>
  <component :is="page" v-if="page" :model="model" />
</template>
