<script setup lang="ts">
import { RouterView } from 'vue-router'
import RouteProgress from '@/ui/components/RouteProgress.vue'

/**
 * 应用根组件。
 *
 * 只放两件跨布局的东西：
 *   · 顶部路由加载条（公开站 / 后台 / 作者区 / 登录页共用同一套切换反馈）；
 *   · 路由出口（具体导航/页脚/布局由各 Layout 决定）。
 */
</script>

<template>
  <RouteProgress />
  <RouterView />
</template>
