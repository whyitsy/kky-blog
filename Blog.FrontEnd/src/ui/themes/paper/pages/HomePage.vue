<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink } from 'vue-router'
import type { HomePageModel } from '@/ui/contracts/pages'
import { useThemeStore } from '@/ui/runtime/store'
import { useSiteStore } from '@/stores/site'
import PaperPager from '../components/PaperPager.vue'
import { foundedYear, issueNumber, longDate, shortDate, weekday } from '../format'

/**
 * 头版：报刊 masthead + 编号文章列表。
 * 没有全屏 Hero —— 站点名就是头版的刊名，日期/期号/版次像报纸一样压在刊头上。
 */
const props = defineProps<{
  model: HomePageModel
}>()

const site = useSiteStore()
const theme = useThemeStore()

const result = computed(() => props.model.result)
const items = computed(() => result.value?.items ?? [])
const totalPages = computed(() => result.value?.totalPages ?? 0)
const startIndex = computed(() => ((result.value?.page ?? 1) - 1) * (result.value?.pageSize ?? 12))

const siteName = computed(() => site.config?.siteName || '墨纸')
const subtitle = computed(
  () =>
    site.config?.heroSubtitles?.[0] ??
    '记录代码与生活 —— 写下来的东西，才算真正想清楚。',
)
const dateLine = computed(() => `${longDate()} · ${weekday()}`)
const issue = computed(() => issueNumber(site.config?.foundingDate))
const startYear = computed(() => foundedYear(site.config?.foundingDate))
const editionLabel = computed(() => `${theme.mode.label}版`)

function indexLabel(index: number): string {
  return String(startIndex.value + index + 1).padStart(2, '0')
}

function goPage(page: number): void {
  props.model.goPage(page)
}
</script>

<template>
  <div class="pp-home">
    <!-- 刊头：日期行 / 刊名 / 版次与存稿 -->
    <header class="pp-masthead">
      <div class="pp-masthead-bar">
        <span>总第 {{ issue }} 期</span>
        <span class="pp-masthead-date">{{ dateLine }}</span>
        <span>{{ editionLabel }}</span>
      </div>

      <div class="pp-masthead-main">
        <h1 class="pp-masthead-title">{{ siteName }}</h1>
        <p class="pp-masthead-sub">{{ subtitle }}</p>
      </div>

      <div class="pp-masthead-foot">
        <span v-if="startYear">EST. {{ startYear }}</span>
        <span class="pp-masthead-motto">墨纸 · 编辑部手记</span>
        <span>存稿 {{ site.stats?.totalPosts ?? 0 }} 篇</span>
      </div>
    </header>

    <section class="pp-latest" aria-labelledby="pp-latest-title">
      <div class="pp-section-head">
        <h2 id="pp-latest-title" class="pp-section-title">最新文章</h2>
        <span class="pp-section-en">Latest Entries</span>
        <RouterLink to="/archive" class="pp-section-more">全部归档 →</RouterLink>
      </div>

      <template v-if="model.loading">
        <div class="pp-loading" aria-hidden="true">
          <div v-for="n in 4" :key="n" class="pp-loading-row">
            <span class="pp-sk-num" />
            <span class="pp-sk-line" />
          </div>
        </div>
        <p class="sr-only">正在加载文章列表</p>
      </template>

      <p v-else-if="!items.length" class="pp-empty">暂无文章，编辑部正在赶稿。</p>

      <ol v-else class="pp-post-list">
        <li v-for="(item, index) in items" :key="item.id" class="pp-post-item">
          <span class="pp-post-num" aria-hidden="true">{{ indexLabel(index) }}</span>
          <div class="pp-post-body">
            <p class="pp-post-meta">
              <time :datetime="item.publishedAt ?? undefined">{{ shortDate(item.publishedAt) }}</time>
              <span v-if="item.categoryName" class="pp-post-cat">{{ item.categoryName }}</span>
              <span>{{ item.viewCount }} 阅读</span>
            </p>
            <RouterLink :to="`/post/${item.id}`" class="pp-post-title">{{ item.title }}</RouterLink>
            <p v-if="item.summary" class="pp-post-summary">{{ item.summary }}</p>
          </div>
        </li>
      </ol>

      <PaperPager :page="result?.page ?? 1" :total-pages="totalPages" @change="goPage" />
    </section>
  </div>
</template>

<style scoped>
/* ---------------------------------------------------------------- 刊头 */
.pp-masthead {
  padding-bottom: var(--space-5);
  border-bottom: var(--pp-rule-double);
}

.pp-masthead-bar {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: var(--space-4);
  padding-bottom: var(--space-4);
  border-bottom: 1px solid var(--border-subtle);
  font: var(--text-caption);
  letter-spacing: 0.18em;
  text-transform: uppercase;
  color: var(--text-subtle);
}

.pp-masthead-date {
  color: var(--text-muted);
}

.pp-masthead-main {
  padding: var(--space-8) 0 var(--space-6);
  text-align: center;
}

.pp-masthead-title {
  font: 700 clamp(40px, 6vw, 76px) / 1.06 var(--font-serif);
  letter-spacing: 0.04em;
  color: var(--pp-masthead-title);
}

.pp-masthead-sub {
  max-width: 620px;
  margin: var(--space-4) auto 0;
  font: 400 16px / 1.85 var(--font-serif);
  color: var(--text-muted);
}

.pp-masthead-foot {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: var(--space-4);
  padding-top: var(--space-4);
  border-top: 1px solid var(--border-subtle);
  font: var(--text-caption);
  letter-spacing: 0.16em;
  text-transform: uppercase;
  color: var(--text-subtle);
}

.pp-masthead-motto {
  color: var(--brand-500);
}

/* ---------------------------------------------------------------- 最新文章 */
.pp-latest {
  margin-top: var(--space-10);
}

.pp-section-head {
  display: flex;
  align-items: baseline;
  gap: var(--space-4);
  padding-bottom: var(--space-3);
  border-bottom: var(--pp-rule-strong);
}

.pp-section-title {
  font: 700 24px / 1.25 var(--font-serif);
  letter-spacing: 0.08em;
  color: var(--text-strong);
}

.pp-section-en {
  font: var(--text-caption);
  letter-spacing: 0.28em;
  text-transform: uppercase;
  color: var(--brand-500);
}

.pp-section-more {
  margin-left: auto;
  font: var(--text-caption);
  letter-spacing: 0.14em;
  color: var(--text-muted);
  border-bottom: 1px solid transparent;
  transition:
    color var(--transition-fast),
    border-color var(--transition-fast);
}

.pp-section-more:hover {
  color: var(--brand-500);
  border-bottom-color: var(--brand-500);
}

/* 编号列表：悬停时左侧浮出一条细线，而不是整块浮起 */
.pp-post-list {
  display: flex;
  flex-direction: column;
}

.pp-post-item {
  display: grid;
  grid-template-columns: 76px minmax(0, 1fr);
  gap: var(--space-5);
  margin-left: calc(-1 * var(--space-5));
  padding: var(--space-6) 0 var(--space-6) var(--space-5);
  border-left: 2px solid transparent;
  border-bottom: 1px solid var(--border-subtle);
  transition: border-color var(--transition-fast);
}

.pp-post-item:hover {
  border-left-color: var(--brand-500);
}

.pp-post-num {
  font: 400 30px / 1 var(--font-serif);
  letter-spacing: 0.02em;
  color: var(--border-strong);
}

.pp-post-item:hover .pp-post-num {
  color: var(--brand-500);
}

.pp-post-body {
  display: flex;
  flex-direction: column;
  gap: 10px;
  min-width: 0;
}

.pp-post-meta {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: var(--space-3);
  font: var(--text-caption);
  letter-spacing: 0.12em;
  color: var(--text-subtle);
}

.pp-post-cat::before {
  content: '·';
  margin-right: var(--space-3);
  color: var(--border-strong);
}

.pp-post-title {
  font: 700 25px / 1.35 var(--font-serif);
  letter-spacing: 0.01em;
  color: var(--text-strong);
  transition: color var(--transition-fast);
}

.pp-post-item:hover .pp-post-title {
  color: var(--brand-500);
}

.pp-post-summary {
  display: -webkit-box;
  max-width: 64ch;
  overflow: hidden;
  font: 400 15px / 1.8 var(--font-serif);
  color: var(--text-muted);
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 2;
  line-clamp: 2;
}

/* ---------------------------------------------------------------- 加载 / 空态 */
.pp-loading {
  display: flex;
  flex-direction: column;
}

.pp-loading-row {
  display: grid;
  grid-template-columns: 76px minmax(0, 1fr);
  gap: var(--space-5);
  padding: var(--space-6) 0;
  border-bottom: 1px solid var(--border-subtle);
}

.pp-sk-num {
  width: 44px;
  height: 26px;
  background: var(--bg-sunken);
}

.pp-sk-line {
  width: 62%;
  height: 20px;
  background: var(--bg-sunken);
}

.pp-empty {
  padding: var(--space-16) 0;
  font: 400 17px / 1.8 var(--font-serif);
  color: var(--text-subtle);
  text-align: center;
}

/* ---------------------------------------------------------------- 窄屏 */
@media (max-width: 639px) {
  .pp-masthead-bar {
    flex-wrap: wrap;
    gap: var(--space-2);
  }

  .pp-masthead-foot {
    flex-wrap: wrap;
  }

  .pp-section-en {
    display: none;
  }

  .pp-post-item {
    grid-template-columns: 44px minmax(0, 1fr);
    gap: var(--space-3);
    margin-left: 0;
    padding-left: var(--space-3);
  }

  .pp-post-num {
    font-size: 22px;
  }

  .pp-post-title {
    font-size: 21px;
  }

  .pp-loading-row {
    grid-template-columns: 44px minmax(0, 1fr);
  }
}
</style>
