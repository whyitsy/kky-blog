<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { getCollectionBySlug } from '@/api/collections'
import { isNotFoundError } from '@/api/http'
import type { CollectionDetailDto } from '@/types'
import { useThemePage, type CollectionDetailPageModel } from '@/ui'

/**
 * 专栏详情数据控制器：按 slug 拉数据，区分「不存在」与「加载失败」。
 */
const route = useRoute()
const page = useThemePage('collection-detail')

const detail = ref<CollectionDetailDto | null>(null)
const loading = ref(true)
const notFound = ref(false)
const loadFailed = ref(false)

const slug = computed(() => String(route.params.slug ?? ''))

async function load(s: string) {
  loading.value = true
  notFound.value = false
  loadFailed.value = false
  detail.value = null
  try {
    detail.value = await getCollectionBySlug(s)
  } catch (e) {
    // 后端对「不存在」与「未发布且非管理员」都返回 404，避免探测。
    // 其余错误（网络异常、502/503、500…）是服务端故障，不能当成「专栏不存在」，
    // 也不把内部错误信息展示到页面上——技术细节只写控制台，页面给中性提示。
    if (isNotFoundError(e)) {
      notFound.value = true
    } else {
      loadFailed.value = true
      console.error('[collection-detail] 加载专栏失败：', e)
    }
  } finally {
    loading.value = false
  }
}

function retry() {
  void load(slug.value)
}

watch(slug, (s) => s && load(s), { immediate: true })

const model = computed<CollectionDetailPageModel>(() => ({
  detail: detail.value,
  loading: loading.value,
  notFound: notFound.value,
  loadFailed: loadFailed.value,
  retry,
}))
</script>

<template>
  <component :is="page" v-if="page" :model="model" />
</template>
