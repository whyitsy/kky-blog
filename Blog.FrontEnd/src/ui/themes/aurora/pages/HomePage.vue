<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink } from 'vue-router'
import type { HomePageModel } from '@/ui/contracts/pages'
import SiteLogo from '@/ui/components/SiteLogo.vue'
import SocialIcon from '@/ui/components/SocialIcon.vue'
import { useSiteStore } from '@/stores/site'
import { DEFAULT_SITE_NAME } from '@/utils/site'
import HeroSection from '../components/HeroSection.vue'
import PostCardList from '../components/PostCardList.vue'

/**
 * 首页：通栏 Hero + 「最新文章（主区）+ 站点侧栏」双栏布局。
 *
 * 布局动机：用「更宽的容器 + 正常尺寸的卡片」填充两侧空白，而不是把卡片放大。
 * 宽容器 1600px + 3 列卡片 + 300px 侧栏：1920 屏两侧留白从 320px 收到 160px，
 * 单卡约 382px 宽、430px 高，比原来的 293px 卡片略大，也不至于占满整屏。
 *
 * Hero 的大图背景保持完整，不做渐隐/遮罩；内容区顶部保留一层很淡的极光柔光，
 * 只作为页面氛围，不改变首屏画面的边界。
 *
 * 数据全部来自 `HomePageModel`；翻页后的滚动定位属于页面布局行为，留在这里。
 */
const props = defineProps<{ model: HomePageModel }>()

const site = useSiteStore()

const siteName = computed(() => site.config?.siteName || DEFAULT_SITE_NAME)

/** 社交入口（与 Hero 之前用的是同一份数据源，现在收进「关于本站」卡片） */
const visibleLinks = computed(() => site.socialLinks.filter((link) => link.isVisible))

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
        <!-- 关于本站（原来的 Hero 社交图标收进这里，改成「图标 + 名称」的胶囊） -->
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

          <div v-if="visibleLinks.length" class="about-socials">
            <p class="about-socials-label">FOLLOW · 关注</p>
            <div class="social-pills">
              <a
                v-for="link in visibleLinks"
                :key="link.id"
                :href="link.url"
                target="_blank"
                rel="noopener noreferrer"
                class="social-pill"
                :title="`打开 ${link.name}`"
              >
                <!-- 图标只是装饰：链接里已有可见文字，避免读屏把名字念两遍 -->
                <span class="social-pill-icon" aria-hidden="true">
                  <SocialIcon :icon="link.icon" :name="link.name" :size="16" />
                </span>
                <span>{{ link.name }}</span>
              </a>
            </div>
          </div>
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
  position: relative;
  display: grid;
  grid-template-columns: minmax(0, 1fr) var(--size-sidebar);
  gap: var(--space-10);
  align-items: start;
  padding-top: var(--space-16);
  padding-bottom: var(--space-16);
}

/* 内容区顶部的静态极光柔光：补一点背景层次（不是加在 Hero 大图上的过渡层）。 */
.home-section::before {
  content: '';
  position: absolute;
  top: 0;
  left: 50%;
  width: min(1000px, 100%);
  height: 260px;
  transform: translateX(-50%);
  pointer-events: none;
  background: radial-gradient(
    55% 100% at 50% 0%,
    color-mix(in srgb, var(--accent-500) 13%, transparent),
    transparent 72%
  );
}

.home-main,
.home-side {
  position: relative;
  z-index: 1;
}

.home-main {
  min-width: 0;
}

/* 页头从居中改为左对齐 + 细分隔线，与右侧栏的「编辑感」一致 */
.section-header {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  margin-bottom: var(--space-8);
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
  gap: var(--space-5);
}

.side-card {
  padding: var(--space-5);
}

.side-head {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: var(--space-3);
  margin-bottom: var(--space-5);
}

.side-title {
  font: var(--text-h3);
  font-size: 18px;
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
  gap: var(--space-4);
  margin-bottom: var(--space-5);
}

.about-text {
  min-width: 0;
}

.about-name {
  display: block;
  font-size: 18px;
  font-weight: 700;
  color: var(--text-strong);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.about-meta {
  margin-top: 3px;
  font: var(--text-caption);
  color: var(--text-subtle);
}

.stat-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: var(--space-4);
  padding-top: var(--space-5);
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
  font-size: 20px;
  font-weight: 700;
  color: var(--text-strong);
}

/* 社交入口：从 Hero 底部的圆形浮标改成卡片内的「图标 + 名称」胶囊，
   信息量更大，也不再和首屏的「向下翻阅」抢注意力。 */
.about-socials {
  margin-top: var(--space-5);
  padding-top: var(--space-5);
  border-top: 1px solid var(--border-subtle);
}

.about-socials-label {
  margin-bottom: var(--space-3);
  font: var(--text-caption);
  letter-spacing: 2px;
  color: var(--text-disabled);
}

.social-pills {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
}

.social-pill {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 6px var(--space-3);
  border: 1px solid var(--border-default);
  border-radius: 999px;
  font-size: 13px;
  font-weight: 500;
  color: var(--text-muted);
  background: var(--bg-canvas);
  transition:
    color var(--transition-fast),
    border-color var(--transition-fast),
    background var(--transition-fast),
    transform var(--transition-fast),
    box-shadow var(--transition-fast);
}

.social-pill-icon {
  display: inline-flex;
  flex-shrink: 0;
}

.social-pill:hover {
  color: var(--brand-500);
  border-color: color-mix(in srgb, var(--brand-500) 55%, transparent);
  background: color-mix(in srgb, var(--brand-500) 10%, transparent);
  transform: translateY(-2px);
  box-shadow: var(--glow-cyan);
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
  font-size: 12px;
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
/* 1200 以下侧栏折到主区下方：主区恢复整宽后放 3 列卡片，
   否则 1024 左右的侧栏会把卡片挤回 300px 出头（又变回「小卡片」）。 */
@media (max-width: 1199px) {
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
