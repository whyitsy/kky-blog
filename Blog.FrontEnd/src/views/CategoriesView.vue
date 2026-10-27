<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { getCategories } from '@/api/categories'
import type { CategoryDto } from '@/types'
import { useThemePage, type TaxonomyPageModel } from '@/ui'

/**
 * 分类墙数据控制器：拉分类，归一化成 taxonomy 页面模型（kind='category'）。
 * 视觉由主题的 taxonomy 页面组件统一渲染（标签/分类共用同一个页面组件）。
 */
const router = useRouter()
const page = useThemePage('taxonomy')

const categories = ref<CategoryDto[]>([])
const loading = ref(true)

onMounted(async () => {
  try {
    categories.value = await getCategories()
  } catch (e) {
    // 加载失败按「空列表」呈现 —— 理由与写法同 TagsView（那里有完整说明）：
    // 只写 try/finally 会让 rejection 无人接收，冒出 Uncaught (in promise)。
    categories.value = []
    console.error('[categories] 加载分类列表失败：', e)
  } finally {
    loading.value = false
  }
})

function goCategory(id: string) {
  router.push({ path: '/posts', query: { categoryId: id } })
}

const model = computed<TaxonomyPageModel>(() => ({
  kind: 'category',
  eyebrow: `CATEGORIES · 共 ${categories.value.length} 个分类`,
  title: '分类墙',
  subtitle: '点击分类，查看该分类下的全部文章。',
  items: categories.value.map((category) => ({
    id: category.id,
    name: category.name,
    count: category.postCount,
  })),
  loading: loading.value,
  onSelect: goCategory,
}))
</script>

<template>
  <component :is="page" v-if="page" :model="model" />
</template>
