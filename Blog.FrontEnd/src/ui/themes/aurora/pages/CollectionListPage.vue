<script setup lang="ts">
import { RouterLink } from 'vue-router'
import type { CollectionListPageModel } from '@/ui/contracts/pages'
import TaxonomySkeleton from '@/ui/components/skeleton/TaxonomySkeleton.vue'

/**
 * 专栏列表页：居中页头 + 封面/简介/篇数的卡片栅格。
 * 加载失败在数据控制器里按「空列表」处理（公开页不弹错误横幅）。
 */
defineProps<{ model: CollectionListPageModel }>()
</script>

<template>
  <div class="collection-list-page">
    <header class="page-header">
      <p class="eyebrow">COLLECTIONS · 共 {{ model.collections.length }} 个专栏</p>
      <h1 class="page-title gradient-text">专栏</h1>
      <p class="page-sub">把多篇文章组织成一个系列，按顺序阅读。</p>
    </header>

    <div class="container list-container">
      <TaxonomySkeleton v-if="model.loading" :count="6" />

      <!-- 与分类墙/标签墙一致：无数据显示「暂无」，加载失败也走这里（错误只进控制台） -->
      <p v-else-if="model.collections.length === 0" class="empty">暂无专栏</p>

      <div v-else class="grid">
        <RouterLink
          v-for="c in model.collections"
          :key="c.id"
          :to="`/collections/${c.slug}`"
          class="collection-card card"
        >
          <div class="cover">
            <img v-if="c.coverImage" :src="c.coverImage" :alt="c.title" loading="lazy" />
            <div v-else class="cover-placeholder cover-fallback" />
          </div>
          <div class="body">
            <h2 class="title">{{ c.title }}</h2>
            <p class="desc">{{ c.description || '暂无简介' }}</p>
            <span class="count">{{ c.postCount }} 篇文章</span>
          </div>
        </RouterLink>
      </div>
    </div>
  </div>
</template>

<style scoped>
.collection-list-page {
  min-height: 70vh;
}

.list-container {
  padding-bottom: var(--space-16);
}

.grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: var(--space-6);
}

.collection-card {
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.collection-card:hover {
  transform: translateY(-4px);
  border-color: var(--border-default);
  box-shadow: var(--shadow-md), var(--glow-purple);
}

.cover {
  aspect-ratio: 16 / 9;
  overflow: hidden;
}

.cover img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

/* 默认封面：视觉由 theme.css 的 .cover-fallback 提供，这里只撑满封面区域 */
.cover-placeholder {
  width: 100%;
  height: 100%;
}

.body {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  padding: var(--space-4);
}

.title {
  font: var(--text-h3);
  color: var(--text-strong);
  margin: 0;
}

.desc {
  font: var(--text-body-sm);
  color: var(--text-muted);
  display: -webkit-box;
  -webkit-line-clamp: 2;
  line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.count {
  font: var(--text-caption);
  color: var(--brand-500);
}

/* 空态留白（视觉来自 foundation `.empty`） */
.empty {
  padding: var(--space-16) 0;
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
</style>
