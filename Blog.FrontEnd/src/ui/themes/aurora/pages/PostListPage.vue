<script setup lang="ts">
import type { PostListPageModel } from '@/ui/contracts/pages'
import PostCardList from '../components/PostCardList.vue'

/**
 * 文章列表页：居中页头（标题/篇数）+ 文章卡片网格。
 * 标题由数据控制器归一化（搜索/标签/分类各自的文案），页面只负责展示。
 */
defineProps<{ model: PostListPageModel }>()
</script>

<template>
  <div class="post-list-page">
    <header class="page-header">
      <p class="eyebrow">POSTS</p>
      <h1 class="page-title gradient-text">{{ model.title }}</h1>
      <p v-if="model.subtitle" class="page-sub">{{ model.subtitle }}</p>
    </header>

    <div class="container list-container">
      <PostCardList
        :result="model.result"
        :loading="model.loading"
        @page-change="model.goPage"
      />
    </div>
  </div>
</template>

<style scoped>
.post-list-page {
  min-height: 70vh;
}

.list-container {
  padding-bottom: var(--space-16);
}
</style>
