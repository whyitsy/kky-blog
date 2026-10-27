<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useThemePage, type NotFoundPageModel } from '@/ui'

/**
 * 404 数据控制器：把尝试访问的路径与两个返回动作交给主题的 not-found 页面。
 */
const route = useRoute()
const router = useRouter()
const page = useThemePage('not-found')

// 不存在的路径在地址栏里也展示出来，便于排查（此前是静默重定向首页）
const model = computed<NotFoundPageModel>(() => ({
  attemptedPath: route.fullPath,
  goHome: () => {
    void router.push('/')
  },
  goBack: () => {
    router.back()
  },
}))
</script>

<template>
  <component :is="page" v-if="page" :model="model" />
</template>
