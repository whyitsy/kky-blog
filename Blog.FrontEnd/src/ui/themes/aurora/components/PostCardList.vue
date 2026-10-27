<script setup lang="ts">
import type { PagedResult, PostListItemDto } from '@/types'
import PostCard from './PostCard.vue'
import PostCardSkeleton from '@/ui/components/skeleton/PostCardSkeleton.vue'
import PaginationBar from './PaginationBar.vue'

/**
 * 文章卡片列表：骨架屏 / 空态 / 三列栅格（1180px 以上三列、768+ 两列、手机单列）+ 分页。
 * 只负责展示，翻页通过 `page-change` 事件交回页面（由视图的数据控制器处理路由）。
 */
defineProps<{
  result: PagedResult<PostListItemDto> | null
  loading: boolean
  skeletonCount?: number
}>()

const emit = defineEmits<{ (e: 'page-change', page: number): void }>()
</script>

<template>
  <div class="post-list">
    <!-- 骨架屏（组件化） -->
    <PostCardSkeleton v-if="loading" :count="skeletonCount ?? 12" />

    <!-- 空态 -->
    <div v-else-if="!result || result.items.length === 0" class="empty">
      <p class="empty-icon">📝</p>
      <p>暂无文章</p>
    </div>

    <!-- 卡片栅格：≥1200px 三列 / 768-1199 两列 / <768 单列 -->
    <template v-else>
      <div class="grid">
        <PostCard v-for="post in result.items" :key="post.id" :post="post" />
      </div>
      <PaginationBar
        :page="result.page"
        :total-pages="result.totalPages"
        :total-count="result.total"
        @change="emit('page-change', $event)"
      />
    </template>
  </div>
</template>

<style scoped>
.grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: var(--space-6);
}

@media (max-width: 1199px) {
  .grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 767px) {
  .grid {
    grid-template-columns: 1fr;
  }
}

/* 空态：视觉来自 foundation `.empty`，这里只补纵向留白 */
.empty {
  padding: var(--space-24) 0;
}

.empty-icon {
  font-size: 42px;
  margin-bottom: var(--space-4);
}
</style>
