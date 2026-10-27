<script setup lang="ts">
import { RouterLink } from 'vue-router'
import type { CollectionDetailPageModel } from '@/ui/contracts/pages'
import PostDetailSkeleton from '@/ui/components/skeleton/PostDetailSkeleton.vue'
import StateNotice from '../components/StateNotice.vue'

/**
 * 专栏详情页：页头 + 编号文章列表 + 返回列表。
 * 不存在 / 加载失败由数据控制器区分（后端对未发布也返回 404，避免探测）。
 */
defineProps<{ model: CollectionDetailPageModel }>()

/** 只展示日期部分（完整 ISO 时间对读者没有意义） */
function fmtDate(iso: string | null) {
  return iso ? iso.slice(0, 10) : ''
}
</script>

<template>
  <div class="collection-detail-page">
    <PostDetailSkeleton v-if="model.loading" />

    <StateNotice
      v-else-if="model.notFound"
      icon="📚"
      title="专栏不存在"
      description="该专栏可能已被删除，或尚未发布。"
    >
      <RouterLink to="/collections" class="btn btn-primary">查看全部专栏</RouterLink>
    </StateNotice>

    <StateNotice
      v-else-if="model.loadFailed"
      icon="⚠️"
      title="加载失败"
      description="服务暂时不可用，请稍后重试。"
    >
      <button type="button" class="btn btn-primary" @click="model.retry()">重新加载</button>
      <RouterLink to="/collections" class="btn btn-ghost">查看全部专栏</RouterLink>
    </StateNotice>

    <template v-else-if="model.detail">
      <header class="page-header">
          <p class="eyebrow">COLLECTION</p>
        <h1 class="page-title gradient-text">{{ model.detail.title }}</h1>
        <p v-if="model.detail.description" class="page-sub">{{ model.detail.description }}</p>
        <p class="hint meta">共 {{ model.detail.posts.length }} 篇文章 · 按专栏顺序阅读</p>
      </header>

      <div class="container body">
        <ol v-if="model.detail.posts.length" class="post-list">
          <li v-for="(p, index) in model.detail.posts" :key="p.id" class="post-item card">
            <span class="order">{{ index + 1 }}</span>
            <div class="post-main">
              <RouterLink :to="`/post/${p.id}`" class="post-title">{{ p.title }}</RouterLink>
              <p class="post-summary">{{ p.summary }}</p>
              <div class="post-meta">
                <span>{{ fmtDate(p.publishedAt) }}</span>
                <span>·</span>
                <span>{{ p.viewCount }} 次浏览</span>
              </div>
            </div>
            <RouterLink :to="`/post/${p.id}`" class="link read-link">阅读 →</RouterLink>
          </li>
        </ol>

        <p v-else class="empty">该专栏还没有文章</p>

        <RouterLink to="/collections" class="link link-muted back-link">← 返回专栏列表</RouterLink>
      </div>
    </template>
  </div>
</template>

<style scoped>
.collection-detail-page {
  min-height: 70vh;
}

.page-sub {
  max-width: 640px;
  margin: 0 auto var(--space-3);
}

.meta {
  position: relative;
}

.body {
  padding-bottom: var(--space-16);
  display: flex;
  flex-direction: column;
  gap: var(--space-6);
}

.post-list {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  list-style: none;
  padding: 0;
  margin: 0;
}

.post-item {
  display: grid;
  grid-template-columns: 44px minmax(0, 1fr) auto;
  gap: var(--space-4);
  align-items: center;
  padding: var(--space-5);
}

.order {
  display: grid;
  place-items: center;
  width: 36px;
  height: 36px;
  border-radius: 50%;
  font-weight: 700;
  color: var(--brand-500);
  background: color-mix(in srgb, var(--brand-500) 12%, transparent);
}

.post-main {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  min-width: 0;
}

.post-title {
  font-size: 17px;
  font-weight: 600;
  color: var(--text-strong);
}

.post-title:hover {
  color: var(--brand-500);
}

.post-summary {
  font: var(--text-body-sm);
  color: var(--text-muted);
  display: -webkit-box;
  -webkit-line-clamp: 2;
  line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.post-meta {
  display: flex;
  gap: var(--space-2);
  font: var(--text-caption);
  color: var(--text-subtle);
}

.read-link {
  font-size: 14px;
}

/* 空态留白（视觉来自 foundation `.empty`） */
.empty {
  padding: var(--space-16) 0;
  text-align: center;
}

.back-link {
  font: var(--text-body-sm);
  align-self: flex-start;
}

@media (max-width: 767px) {
  .post-item {
    grid-template-columns: 32px minmax(0, 1fr);
  }
  .read-link {
    display: none;
  }
}
</style>
