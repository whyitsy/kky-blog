<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { getCollections } from '@/api/collections'
import type { CollectionDto } from '@/types'
import { useThemePage, type CollectionListPageModel } from '@/ui'

/**
 * 专栏列表数据控制器：拉全部已发布专栏，交给主题的 collection-list 页面渲染。
 */
const page = useThemePage('collection-list')

const collections = ref<CollectionDto[]>([])
const loading = ref(true)

async function load() {
  loading.value = true
  try {
    collections.value = await getCollections()
  } catch (e) {
    // 加载失败按「空列表」呈现，与分类墙/标签墙等公开页保持一致：
    // 技术细节只进控制台，页面不暴露 HTTP 状态码等内部信息，也不弹错误横幅。
    collections.value = []
    console.error('[collections] 加载专栏列表失败：', e)
  } finally {
    loading.value = false
  }
}

onMounted(load)

const model = computed<CollectionListPageModel>(() => ({
  collections: collections.value,
  loading: loading.value,
}))
</script>

<template>
  <component :is="page" v-if="page" :model="model" />
</template>
