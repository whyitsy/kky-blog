<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getPosts, searchPosts } from '@/api/posts'
import { getCategories } from '@/api/categories'
import { getTags } from '@/api/tags'
import type { PagedResult, PostListItemDto } from '@/types'
import { useThemePage, type PostListPageModel } from '@/ui'

/**
 * 文章列表数据控制器：按 keyword / tagId / categoryId / page 拉数据，
 * 并把「标题/篇数」归一化成页面模型（搜索、标签筛选、分类筛选共用同一套文案规则）。
 */
const route = useRoute()
const router = useRouter()

/** 当前主题提供的文章列表页组件 */
const page = useThemePage('post-list')

const result = ref<PagedResult<PostListItemDto> | null>(null)
const loading = ref(true)
const filterName = ref('')

const keyword = computed(() => String(route.query.keyword ?? ''))
const tagId = computed(() => String(route.query.tagId ?? ''))
const categoryId = computed(() => String(route.query.categoryId ?? ''))
const pageNumber = computed(() => Math.max(1, Number(route.query.page) || 1))

const headerTitle = computed(() => {
  if (keyword.value) return `搜索「${keyword.value}」`
  if (filterName.value) return filterName.value
  return '文章列表'
})

async function load() {
  loading.value = true
  try {
    if (keyword.value) {
      result.value = await searchPosts(keyword.value, pageNumber.value, 12)
    } else {
      result.value = await getPosts({
        page: pageNumber.value,
        pageSize: 12,
        tagId: tagId.value || undefined,
        categoryId: categoryId.value || undefined,
      })
    }
  } catch {
    result.value = null
  } finally {
    loading.value = false
  }
}

async function resolveFilterName() {
  filterName.value = ''
  try {
    if (tagId.value) {
      const tag = (await getTags()).find((t) => t.id === tagId.value)
      if (tag) filterName.value = `标签：${tag.name}`
    } else if (categoryId.value) {
      const cat = (await getCategories()).find((c) => c.id === categoryId.value)
      if (cat) filterName.value = `分类：${cat.name}`
    }
  } catch {
    /* 名称解析失败不阻塞列表 */
  }
}

function goPage(nextPage: number) {
  router.push({ query: { ...route.query, page: nextPage > 1 ? nextPage : undefined } })
}

const model = computed<PostListPageModel>(() => ({
  result: result.value,
  loading: loading.value,
  title: headerTitle.value,
  subtitle: result.value ? `共 ${result.value.total} 篇文章` : '',
  goPage,
}))

watch([keyword, tagId, categoryId], () => {
  resolveFilterName()
  load()
})
watch(pageNumber, load)

onMounted(() => {
  resolveFilterName()
  load()
})
</script>

<template>
  <component :is="page" v-if="page" :model="model" />
</template>
