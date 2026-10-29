<script setup lang="ts">
import { RouterLink } from 'vue-router'
import type { PostDetailPageModel } from '@/ui/contracts/pages'
import PostDetailSkeleton from '@/ui/components/skeleton/PostDetailSkeleton.vue'
import GiscusComments from '@/ui/components/GiscusComments.vue'
import StateNotice from '../components/StateNotice.vue'

/**
 * 文章详情页：阅读进度条 + 头图 + 徽章区 + 双栏（正文 + 目录/阅读进度）+ 评论。
 *
 * 正文 HTML 由数据控制器渲染并**已注入 heading id**（与 toc 对齐），
 * 页面只负责 `v-html` 与排版；滚动高亮/进度值都在 controller 里算好。
 */
defineProps<{ model: PostDetailPageModel }>()
</script>

<template>
  <div class="post-detail-page">
    <!-- 阅读进度条 -->
    <div class="progress-bar" :style="{ width: `${model.readProgress}%` }" />

    <PostDetailSkeleton v-if="model.loading" />

    <StateNotice
      v-else-if="model.notFound"
      icon="🔍"
      title="文章不存在"
      description="该文章可能已被删除，或尚未发布。"
    >
      <RouterLink to="/" class="btn btn-primary">返回首页</RouterLink>
    </StateNotice>

    <StateNotice
      v-else-if="model.loadFailed"
      icon="⚠️"
      title="加载失败"
      description="服务暂时不可用，请稍后重试。"
    >
      <button type="button" class="btn btn-primary" @click="model.retry()">重新加载</button>
      <RouterLink to="/" class="btn btn-ghost">返回首页</RouterLink>
    </StateNotice>

    <template v-else-if="model.post">
      <!-- 封面头图 -->
      <header class="post-hero">
        <div
          class="post-hero-bg"
          :style="
            model.post.coverImage ? { backgroundImage: `url(${model.post.coverImage})` } : undefined
          "
          :class="{ 'cover-fallback': !model.post.coverImage }"
        />
        <div class="post-hero-mask" />
        <div class="container post-hero-content">
          <div class="post-badges">
            <RouterLink
              v-if="model.post.categoryId"
              :to="{ path: '/posts', query: { categoryId: model.post.categoryId } }"
              class="category-badge"
            >
              {{ model.post.categoryName }}
            </RouterLink>
            <RouterLink
              v-for="tag in model.post.tags"
              :key="tag.id"
              :to="{ path: '/posts', query: { tagId: tag.id } }"
              class="tag-badge"
            >
              {{ tag.name }}
            </RouterLink>
            <!-- 所属专栏：指向专栏详情页（一篇文章可属于多个专栏） -->
            <RouterLink
              v-for="col in model.post.collections ?? []"
              :key="col.id"
              :to="`/collections/${col.slug}`"
              class="collection-badge"
            >
              📚 {{ col.title }}
            </RouterLink>
          </div>
          <h1 class="post-title">{{ model.post.title }}</h1>
          <div class="post-meta">
            <img
              :src="model.post.authorAvatar"
              :alt="model.post.authorName"
              class="avatar meta-avatar"
            />
            <span class="meta-author">{{ model.post.authorName }}</span>
            <span class="meta-sep">·</span>
            <time>{{ model.publishedLabel }}</time>
            <span class="meta-sep">·</span>
            <span>{{ model.readMinutes }} 分钟阅读</span>
            <span class="meta-sep">·</span>
            <span>{{ model.post.viewCount }} 次浏览</span>
          </div>
        </div>
      </header>

      <!-- 正文 + 侧栏 -->
      <div class="container detail-layout">
        <article class="article-body" v-html="model.renderedContent" />

        <aside v-if="model.toc.length" class="detail-sidebar">
          <div class="sidebar-card card">
            <h4 class="sidebar-title">目录</h4>
            <nav class="toc">
              <a
                v-for="item in model.toc"
                :key="item.id"
                :href="`#${item.id}`"
                class="toc-link"
                :class="{ active: model.activeHeading === item.id, 'toc-h3': item.level === 3 }"
                @click.prevent="model.scrollToHeading(item.id)"
              >
                {{ item.text }}
              </a>
            </nav>
          </div>

          <div class="sidebar-card card">
            <h4 class="sidebar-title">阅读进度</h4>
            <p class="progress-num">{{ model.readProgress }}%</p>
            <div class="progress-track">
              <div class="progress-fill" :style="{ width: `${model.readProgress}%` }" />
            </div>
          </div>
        </aside>
      </div>

      <!-- 评论 -->
      <div class="container comments-section">
        <h3 class="comments-title">评论</h3>
        <GiscusComments />
      </div>
    </template>
  </div>
</template>

<style scoped>
.progress-bar {
  position: fixed;
  top: var(--nav-height);
  left: 0;
  height: 2px;
  z-index: calc(var(--z-nav) - 1);
  background: linear-gradient(
    90deg,
    var(--gradient-start),
    var(--gradient-mid),
    var(--gradient-end)
  );
  transition: width 100ms linear;
}

/* ---------- 头图 ---------- */
.post-hero {
  position: relative;
  min-height: 340px;
  display: flex;
  align-items: flex-end;
  overflow: hidden;
}

.post-hero-bg {
  position: absolute;
  inset: 0;
  background-size: cover;
  background-position: center;
}

/* 无封面时的头图视觉由 theme.css 的 .cover-fallback 提供（与文章卡片共用） */

/* 遮罩使用主题私有 Token（亮/暗两套值都在 tokens.css 里，不再写 [data-theme] 分支） */
.post-hero-mask {
  position: absolute;
  inset: 0;
  background: var(--post-hero-mask);
}

.post-hero-content {
  position: relative;
  z-index: 2;
  padding-top: var(--space-16);
  padding-bottom: var(--space-8);
}

.post-badges {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
  margin-bottom: var(--space-4);
}

/* DESIGN.md 6.4：aurora 文章详情内的专栏徽章用虚线描边，与分类/标签三者可区分 */
.collection-badge {
  border-style: dashed;
  border-color: var(--border-strong);
}

.post-title {
  font: var(--text-h1);
  font-size: 36px;
  color: var(--text-strong);
  max-width: 800px;
  margin-bottom: var(--space-4);
}

.post-meta {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  font: var(--text-body-sm);
  color: var(--text-muted);
  flex-wrap: wrap;
}

.meta-avatar {
  --avatar-size: 28px;
}

.meta-author {
  font-weight: 600;
  color: var(--text-default);
}

.meta-sep {
  color: var(--text-disabled);
}

/* ---------- 双栏 ---------- */
.detail-layout {
  display: grid;
  grid-template-columns: minmax(0, var(--size-prose)) var(--size-sidebar);
  gap: var(--space-10);
  justify-content: center;
  padding-top: var(--space-8);
}

.detail-sidebar {
  position: sticky;
  top: calc(var(--nav-height) + var(--space-6));
  align-self: start;
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
}

@media (max-width: 1023px) {
  .detail-layout {
    grid-template-columns: minmax(0, 1fr);
  }
  .detail-sidebar {
    display: none;
  }
}

.sidebar-card {
  padding: var(--space-5);
}

.sidebar-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--text-strong);
  margin-bottom: var(--space-3);
}

.toc {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  max-height: 320px;
  overflow-y: auto;
}

.toc-link {
  font: var(--text-body-sm);
  color: var(--text-muted);
  border-left: 2px solid var(--border-subtle);
  padding-left: var(--space-3);
  transition:
    color var(--transition-fast),
    border-color var(--transition-fast);
}

.toc-link.toc-h3 {
  padding-left: var(--space-6);
  font-size: 13px;
}

.toc-link:hover,
.toc-link.active {
  color: var(--brand-500);
  border-left-color: var(--brand-500);
}

.progress-num {
  font-size: 26px;
  font-weight: 700;
  color: var(--text-strong);
  margin-bottom: var(--space-2);
}

.progress-track {
  height: 6px;
  border-radius: 3px;
  background: var(--bg-sunken);
  overflow: hidden;
}

.progress-fill {
  height: 100%;
  border-radius: 3px;
  background: linear-gradient(90deg, var(--gradient-start), var(--gradient-end));
  transition: width 100ms linear;
}

/* ---------- 正文排版 ---------- */
.article-body {
  font-size: 16px;
  line-height: 1.75;
  color: var(--text-default);
  overflow-wrap: break-word;
  /* 正文面板：与画布底色/极光光晕拉开层次，长文阅读时不至于「字和背景糊在一起」 */
  padding: var(--space-8) var(--space-10);
  background: var(--bg-surface);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-sm);
}

@media (max-width: 767px) {
  .article-body {
    padding: var(--space-6) var(--space-5);
  }
}

/* 面板内第一段标题不再叠加排版上边距（否则面板顶部会显得很空） */
.article-body :deep(h2:first-child),
.article-body :deep(h3:first-child) {
  margin-top: 0;
}

.article-body :deep(h2) {
  font: var(--text-h2);
  font-size: 24px;
  color: var(--text-strong);
  margin: var(--space-10) 0 var(--space-4);
  scroll-margin-top: calc(var(--nav-height) + var(--space-6));
}

.article-body :deep(h3) {
  font: var(--text-h3);
  font-size: 19px;
  color: var(--text-strong);
  margin: var(--space-8) 0 var(--space-3);
  scroll-margin-top: calc(var(--nav-height) + var(--space-6));
}

.article-body :deep(p) {
  margin-bottom: var(--space-4);
}

.article-body :deep(a) {
  color: var(--brand-500);
  border-bottom: 1px dashed color-mix(in srgb, var(--brand-500) 50%, transparent);
}

.article-body :deep(strong) {
  color: var(--text-strong);
  font-weight: 600;
}

.article-body :deep(ul),
.article-body :deep(ol) {
  margin: 0 0 var(--space-4) var(--space-6);
}

.article-body :deep(ul) {
  list-style: disc;
}

.article-body :deep(ol) {
  list-style: decimal;
}

.article-body :deep(li) {
  margin-bottom: var(--space-2);
}

.article-body :deep(blockquote) {
  margin: 0 0 var(--space-4);
  padding: var(--space-3) var(--space-4);
  border-left: 3px solid var(--brand-500);
  background: color-mix(in srgb, var(--brand-500) 6%, transparent);
  border-radius: 0 var(--radius-sm) var(--radius-sm) 0;
  color: var(--text-muted);
}

.article-body :deep(code) {
  font-family: var(--font-mono);
  font-size: 0.88em;
  padding: 2px 6px;
  background: var(--bg-sunken);
  border-radius: var(--radius-xs);
}

.article-body :deep(pre) {
  margin-bottom: var(--space-4);
  padding: var(--space-4);
  background: var(--bg-sunken);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-md);
  overflow-x: auto;
}

.article-body :deep(pre code) {
  padding: 0;
  background: none;
  font-size: 13px;
  line-height: 1.6;
}

.article-body :deep(img) {
  margin: var(--space-4) auto;
  border-radius: var(--radius-md);
}

.article-body :deep(hr) {
  margin: var(--space-8) 0;
  border: none;
  height: 1px;
  background: var(--border-subtle);
}

.article-body :deep(table) {
  width: 100%;
  margin-bottom: var(--space-4);
  border-collapse: collapse;
  font-size: 14px;
}

.article-body :deep(th),
.article-body :deep(td) {
  padding: var(--space-2) var(--space-3);
  border: 1px solid var(--border-subtle);
}

.article-body :deep(th) {
  background: var(--bg-raised);
  color: var(--text-strong);
}

/* ---------- 评论 ---------- */
.comments-section {
  max-width: var(--size-prose);
  padding-top: var(--space-10);
  padding-bottom: var(--space-16);
}

.comments-title {
  font: var(--text-h3);
  color: var(--text-strong);
}
</style>
