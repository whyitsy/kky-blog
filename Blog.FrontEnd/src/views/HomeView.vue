<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getPosts } from '@/api/posts'
import { getTags } from '@/api/tags'
import { getCollections } from '@/api/collections'
import type {
  CollectionDto,
  PagedResult,
  PostListItemDto,
  TagDto,
} from '@/types'
import { useThemePage, type HomePageModel } from '@/ui'

/**
 * 首页数据控制器：拉最新文章、处理翻页，把数据组装成 `HomePageModel`。
 * 视觉/布局全部由主题的 home 页面组件渲染（这里没有模板与样式）。
 */
const route = useRoute()
const router = useRouter()

/** 当前主题提供的首页组件；主题不提供时为 null（渲染兜底空态） */
const page = useThemePage('home')

const result = ref<PagedResult<PostListItemDto> | null>(null)
const loading = ref(true)

/** 侧栏数据：热门标签 + 精选专栏（只拉一次，失败按空列表处理） */
const tags = ref<TagDto[]>([])
const collections = ref<CollectionDto[]>([])

async function loadSidebar() {
  const [allTags, allCollections] = await Promise.all([
    getTags().catch(() => [] as TagDto[]),
    getCollections().catch(() => [] as CollectionDto[]),
  ])
  tags.value = [...allTags].sort((a, b) => b.postCount - a.postCount).slice(0, 10)
  collections.value = [...allCollections].sort((a, b) => a.sortOrder - b.sortOrder).slice(0, 3)
}

async function load(pageNumber: number) {
  loading.value = true
  try {
    result.value = await getPosts({ page: pageNumber, pageSize: 12 })
  } catch {
    result.value = null
  } finally {
    loading.value = false
  }
}

function goPage(pageNumber: number) {
  router.push({ query: pageNumber > 1 ? { page: pageNumber } : {} })
}

const model = computed<HomePageModel>(() => ({
  result: result.value,
  loading: loading.value,
  goPage,
  tags: tags.value,
  collections: collections.value,
}))

watch(
  () => route.query.page,
  (p) => load(Math.max(1, Number(p) || 1)),
)

onMounted(() => {
  void load(Math.max(1, Number(route.query.page) || 1))
  void loadSidebar()
})
</script>

<template>
  <component :is="page" v-if="page" :model="model" />
</template>
