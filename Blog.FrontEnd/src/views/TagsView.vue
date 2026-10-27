<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { getTags } from '@/api/tags'
import type { TagDto } from '@/types'
import { useThemePage, type TaxonomyPageModel } from '@/ui'

/**
 * 标签墙数据控制器：拉标签，归一化成 taxonomy 页面模型（kind='tag'）。
 * 视觉由主题的 taxonomy 页面组件统一渲染（标签/分类共用同一个页面组件）。
 */
const router = useRouter()
const page = useThemePage('taxonomy')

const tags = ref<TagDto[]>([])
const loading = ref(true)

onMounted(async () => {
  try {
    tags.value = await getTags()
  } catch (e) {
    // 加载失败按「空列表」呈现，与专栏/分类/归档等公开页保持一致：
    // 技术细节只进控制台，页面不暴露 HTTP 状态码，也不弹错误横幅。
    //
    // ⚠️ 这个 catch 不是可有可无的：只写 try/finally 的话 UI 看起来完全正常
    //    （loading 置回 false、列表仍为空 → 显示「暂无标签」），
    //    但 Promise 的 rejection **没有人接**，控制台会冒出 Uncaught (in promise)。
    //    将来一旦接入全局的 unhandledrejection 上报，它就会变成一条假告警。
    tags.value = []
    console.error('[tags] 加载标签列表失败：', e)
  } finally {
    loading.value = false
  }
})

function goTag(id: string) {
  router.push({ path: '/posts', query: { tagId: id } })
}

const model = computed<TaxonomyPageModel>(() => ({
  kind: 'tag',
  eyebrow: `TAGS · 共 ${tags.value.length} 个标签`,
  title: '标签墙',
  subtitle: '点击标签，查看该标签下的全部文章。',
  items: tags.value.map((tag) => ({ id: tag.id, name: tag.name, count: tag.postCount })),
  loading: loading.value,
  onSelect: goTag,
}))
</script>

<template>
  <component :is="page" v-if="page" :model="model" />
</template>
