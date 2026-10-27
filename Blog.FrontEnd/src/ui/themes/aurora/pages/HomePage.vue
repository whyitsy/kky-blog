<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink } from 'vue-router'
import type { HomePageModel } from '@/ui/contracts/pages'
import SiteLogo from '@/ui/components/SiteLogo.vue'
import { useSiteStore } from '@/stores/site'
import { DEFAULT_SITE_NAME } from '@/utils/site'
import HeroSection from '../components/HeroSection.vue'
import PostCardList from '../components/PostCardList.vue'

/**
 * 首页：通栏 Hero + 「最新文章（主区）+ 站点侧栏」双栏布局。
 *
 * 布局动机：首页只有 12 篇文章时，纯 3 列卡片在宽屏上两侧会显得空。
 * 这里用宽容器（1280px）把文章区收窄成 3 列，右侧 300px 放站点信息 / 热门标签 /
 * 专栏推荐 / 探索入口——同一屏内信息密度更高，且侧栏内容全部来自真实接口。
 *
 * 数据全部来自 `HomePageModel`；翻页后的滚动定位属于页面布局行为，留在这里。
 */
const props = defineProps<{ model: HomePageModel }>()

const site = useSiteStore()

const siteName = computed(() => site.config?.siteName || DEFAULT_SITE_NAME)

const foundingYear = computed(() => {
  const raw = site.config?.foundingDate
  return raw ? raw.slice(0, 4) : ''
})

const siteDays = computed(() => site.stats?.siteDays ?? null)

function formatCount(n: number): string {
  if (n >= 10000) return `${(n / 10000).toFixed(1)}w`
  if (n >= 1000) return `${(n / 1000).toFixed(1)}k`
  return String(n)
}

function onPageChange(page: number) {
  props.model.goPage(page)
  document.getElementById('post-list')?.scrollIntoView({ behavior: 'smooth' })
}
</script>

<template>
  <div class="home-page">
    <HeroSection />

    <section id="post-list" class="home-section container container-wide">
      <div class="home-main">
        <header class="section-header">
          <p class="eyebrow">LATEST POSTS</p>
          <div class="section-heading">
            <h2 class="section-title">最新文章</h2>
            <p class="section-sub">写下来的东西，才算真正想清楚。</p>
          </div>
        </header>

        <PostCardList
          :result="model.result"
          :loading="model.loading"
          @page-change="onPageChange"
        />
      </div>

      <aside class="home-side">
        <!-- 关于本站 -->
        <section class="side-card card">
          <header class="side-head">
            <h3 class="side-title">关于本站</h3>
            <span class="side-kicker">ABOUT</span>
          </header>
          <div class="about-brand">
            <SiteLogo :size="42" :glow="false" />
            <div class="about-text">
              <strong class="about-name">{{ siteName }}</strong>
              <p class="about-meta">
                <template v-if="foundingYear">自 {{ foundingYear }} 年起记录</template>
                <template v-else>记录代码与生活</template>
              </p>
            </div>
          </div>
          <dl v-if="site.stats" class="stat-grid">
            <div class="stat">
              <dt>文章</dt>
              <dd>{{ site.stats.totalPosts }}</dd>
            </div>
            <div class="stat">
              <dt>分类 / 标签</dt>
              <dd>{{ site.stats.categoryCount }} / {{ site.stats.tagCount }}</dd>
            </div>
            <div class="stat">
              <dt>总浏览</dt>
              <dd>{{ formatCount(site.stats.totalViews) }}</dd>
            </div>
            <div class="stat">
              <dt>建站天数</dt>
              <dd>{{ siteDays ?? '—' }}</dd>
            </div>
          </dl>
        </section>

        <!-- 热门标签 -->
        <section v-if="model.tags.length" class="side-card card">
          <header class="side-head">
            <h3 class="side-title">热门标签</h3>
            <RouterLink to="/tags" class="side-more">全部 →</RouterLink>
          </header>
          <div class="tag-cloud">
            <RouterLink
              v-for="tag in model.tags"
              :key="tag.id"
              :to="{ path: '/posts', query: { tagId: tag.id } }"
              class="tag-badge tag-chip"
            >
              {{ tag.name }}
              <span class="tag-count">{{ tag.postCount }}</span>
            </RouterLink>
          </div>
        </section>

        <!-- 专栏推荐 -->
        <section v-if="model.collections.length" class="side-card card">
          <header class="side-head">
            <h3 class="side-title">专栏推荐</h3>
            <RouterLink to="/collections" class="side-more">全部 →</RouterLink>
          </header>
          <div class="collection-list">
            <RouterLink
              v-for="collection in model.collections"
              :key="collection.id"
              :to="`/collections/${collection.slug}`"
              class="collection-row"
            >
              <span class="collection-top">
                <span class="collection-name">{{ collection.title }}</span>
                <span class="collection-count">{{ collection.postCount }} 篇</span>
              </span>
              <span class="collection-desc">{{ collection.description || '暂无简介' }}</span>
            </RouterLink>
          </div>
        </section>

        <!-- 探索入口 -->
        <section class="side-card card">
          <header class="side-head">
            <h3 class="side-title">探索</h3>
            <span class="side-kicker">EXPLORE</span>
          </header>
          <nav class="explore-nav">
            <RouterLink to="/archive" class="explore-link">
              <span>归档时间轴</span><span class="explore-arrow">→</span>
            </RouterLink>
            <RouterLink to="/categories" class="explore-link">
              <span>分类墙</span><span class="explore-arrow">→</span>
            </RouterLink>
            <RouterLink to="/tags" class="explore-link">
              <span>标签墙</span><span class="explore-arrow">→</span>
            </RouterLink>
            <RouterLink to="/collections" class="explore-link">
              <span>全部专栏</span><span class="explore-arrow">→</span>
            </RouterLink>
          </nav>
        </section>
      </aside>
    </section>
  </div>
</template>

<style scoped>
.home-section {
  display: grid;
  grid-template-columns: minmax(0, 1fr) var(--size-sidebar);
  gap: var(--space-10);
  align-items: start;
  padding-top: var(--space-16);
  padding-bottom: var(--space-16);
}

.home-main {
  min-width: 0;
}

/* 页头从居中改为左对齐 + 细分隔线，与右侧栏的「编辑感」一致 */
.section-header {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  margin-bottom: var(--space-6);
  padding-bottom: var(--space-4);
  border-bottom: 1px solid var(--border-subtle);
}

.section-heading {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: var(--space-4);
  flex-wrap: wrap;
}

.section-title {
  font: var(--text-h2);
  color: var(--text-strong);
}

.section-sub {
  font: var(--text-body-sm);
  color: var(--text-subtle);
}

/* ---------------------------------------------------------------- 侧栏 */
.home-side {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
}

.side-card {
  padding: var(--space-5);
}

.side-head {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: var(--space-3);
  margin-bottom: var(--space-4);
}

.side-title {
  font: var(--text-h3);
  font-size: 16px;
  color: var(--text-strong);
}

.side-kicker {
  font: var(--text-caption);
  letter-spacing: 2px;
  color: var(--text-disabled);
}

.side-more {
  font: var(--text-caption);
  color: var(--text-subtle);
  transition: color var(--transition-fast);
}
.side-more:hover {
  color: var(--brand-500);
}

/* 关于本站 */
.about-brand {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  margin-bottom: var(--space-4);
}

.about-text {
  min-width: 0;
}

.about-name {
  display: block;
  font-size: 16px;
  font-weight: 700;
  color: var(--text-strong);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.about-meta {
  margin-top: 2px;
  font: var(--text-caption);
  color: var(--text-subtle);
}

.stat-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: var(--space-3);
  padding-top: var(--space-4);
  border-top: 1px solid var(--border-subtle);
}

.stat {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.stat dt {
  font: var(--text-caption);
  color: var(--text-subtle);
}

.stat dd {
  font-size: 18px;
  font-weight: 700;
  color: var(--text-strong);
}

/* 热门标签 */
.tag-cloud {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
}

.tag-chip {
  color: var(--text-muted);
  border-color: var(--border-default);
  background: var(--bg-canvas);
  transition:
    color var(--transition-fast),
    border-color var(--transition-fast);
}
.tag-chip:hover {
  color: var(--brand-500);
  border-color: color-mix(in srgb, var(--brand-500) 45%, transparent);
}

.tag-count {
  margin-left: 2px;
  font-size: 11px;
  opacity: 0.68;
}

/* 专栏推荐 */
.collection-list {
  display: flex;
  flex-direction: column;
}

.collection-row {
  display: flex;
  flex-direction: column;
  gap: 3px;
  padding: var(--space-3) 0;
  border-bottom: 1px solid var(--border-subtle);
}
.collection-row:first-child {
  padding-top: 0;
}
.collection-row:last-child {
  padding-bottom: 0;
  border-bottom: none;
}

.collection-top {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: var(--space-2);
}

.collection-name {
  font: var(--text-body-sm);
  font-weight: 600;
  color: var(--text-strong);
  transition: color var(--transition-fast);
}
.collection-row:hover .collection-name {
  color: var(--brand-500);
}

.collection-count {
  flex-shrink: 0;
  font: var(--text-caption);
  color: var(--text-subtle);
}

.collection-desc {
  font: var(--text-caption);
  color: var(--text-subtle);
  display: -webkit-box;
  -webkit-line-clamp: 1;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

/* 探索入口 */
.explore-nav {
  display: flex;
  flex-direction: column;
}

.explore-link {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-3);
  padding: var(--space-3) 0;
  font: var(--text-body-sm);
  color: var(--text-muted);
  border-bottom: 1px solid var(--border-subtle);
  transition: color var(--transition-fast);
}
.explore-link:first-child {
  padding-top: 0;
}
.explore-link:last-child {
  padding-bottom: 0;
  border-bottom: none;
}
.explore-link:hover {
  color: var(--brand-500);
}

.explore-arrow {
  color: var(--text-disabled);
  transition:
    color var(--transition-fast),
    transform var(--transition-fast);
}
.explore-link:hover .explore-arrow {
  color: var(--brand-500);
  transform: translateX(3px);
}

/* ---------------------------------------------------------------- 响应式 */
@media (max-width: 1023px) {
  .home-section {
    grid-template-columns: minmax(0, 1fr);
    gap: var(--space-8);
  }

  /* 窄屏时侧栏横向排成两列，避免页面被拉得过长 */
  .home-side {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 639px) {
  .home-side {
    grid-template-columns: minmax(0, 1fr);
  }
}
</style>
