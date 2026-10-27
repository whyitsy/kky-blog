<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink } from 'vue-router'
import type { PostListPageModel } from '@/ui/contracts/pages'
import PaperPageHead from '../components/PaperPageHead.vue'
import PaperPager from '../components/PaperPager.vue'
import { shortDate } from '../format'

/**
 * 文章索引：左对齐页头（上双线下细线）+ 表格式的文章索引。
 * 与 aurora 的居中页头 + 卡片流相对，这里一篇文章就是一条索引行。
 */
const props = defineProps<{
  model: PostListPageModel
}>()

const result = computed(() => props.model.result)
const items = computed(() => result.value?.items ?? [])
const totalPages = computed(() => result.value?.totalPages ?? 0)
const startIndex = computed(() => ((result.value?.page ?? 1) - 1) * (result.value?.pageSize ?? 12))

function indexLabel(index: number): string {
  return String(startIndex.value + index + 1).padStart(2, '0')
}

function goPage(page: number): void {
  props.model.goPage(page)
}
</script>

<template>
  <div class="pp-post-list-page">
    <PaperPageHead eyebrow="Index · 文章索引" :title="model.title" :subtitle="model.subtitle">
      <template #meta>
        <span v-if="result">第 {{ result.page }} / {{ Math.max(1, totalPages) }} 页</span>
      </template>
    </PaperPageHead>

    <template v-if="model.loading">
      <div class="pp-index" aria-hidden="true">
        <div v-for="n in 6" :key="n" class="pp-index-skeleton">
          <span class="pp-sk-num" />
          <span class="pp-sk-line" />
          <span class="pp-sk-line pp-sk-short" />
        </div>
      </div>
      <p class="sr-only">正在加载文章列表</p>
    </template>

    <p v-else-if="!items.length" class="pp-empty">没有找到文章。</p>

    <template v-else>
      <div class="pp-index">
        <div class="pp-index-cols" aria-hidden="true">
          <span>序号</span>
          <span>标题</span>
          <span class="pp-col-cat">栏目</span>
          <span class="pp-col-date">日期</span>
        </div>

        <ol class="pp-index-list">
          <li v-for="(item, index) in items" :key="item.id" class="pp-index-row">
            <span class="pp-index-num">{{ indexLabel(index) }}</span>
            <span class="pp-index-main">
              <RouterLink :to="`/post/${item.id}`" class="pp-index-title">{{ item.title }}</RouterLink>
              <span class="pp-index-mobile-meta">
                {{ item.categoryName || '随笔' }} · {{ shortDate(item.publishedAt) }}
              </span>
            </span>
            <span class="pp-index-cat pp-col-cat">{{ item.categoryName || '随笔' }}</span>
            <time class="pp-index-date pp-col-date" :datetime="item.publishedAt ?? undefined">
                {{ shortDate(item.publishedAt) }}
              </time>
          </li>
        </ol>
      </div>

      <PaperPager :page="result?.page ?? 1" :total-pages="totalPages" @change="goPage" />
    </template>
  </div>
</template>

<style scoped>
/* ---------------------------------------------------------------- 索引表 */
.pp-index-cols,
.pp-index-row {
  display: grid;
  grid-template-columns: 64px minmax(0, 1fr) 128px 116px;
  align-items: baseline;
  gap: var(--space-4);
}

.pp-index-cols {
  padding-top: var(--space-6);
  padding-bottom: var(--space-2);
  border-bottom: 1px solid var(--border-default);
  font: var(--text-caption);
  letter-spacing: 0.2em;
  text-transform: uppercase;
  color: var(--text-subtle);
}

.pp-index-list {
  display: flex;
  flex-direction: column;
}

.pp-index-row {
  padding: var(--space-4) 0;
  border-bottom: 1px solid var(--border-subtle);
  transition: background var(--transition-fast);
}

.pp-index-row:hover {
  background: var(--bg-surface);
}

.pp-index-num {
  font: 400 15px / 1.4 var(--font-mono);
  letter-spacing: 0.06em;
  color: var(--border-strong);
}

.pp-index-row:hover .pp-index-num {
  color: var(--brand-500);
}

.pp-index-main {
  display: flex;
  flex-direction: column;
  gap: 4px;
  min-width: 0;
}

.pp-index-title {
  font: 400 19px / 1.5 var(--font-serif);
  color: var(--text-strong);
  transition: color var(--transition-fast);
}

.pp-index-title:hover {
  color: var(--brand-500);
}

.pp-index-cat {
  overflow: hidden;
  font: var(--text-caption);
  letter-spacing: 0.08em;
  color: var(--text-muted);
  text-overflow: ellipsis;
  white-space: nowrap;
}

.pp-index-date {
  font: var(--text-caption);
  letter-spacing: 0.08em;
  color: var(--text-subtle);
}

.pp-index-mobile-meta {
  display: none;
  font: var(--text-caption);
  letter-spacing: 0.08em;
  color: var(--text-subtle);
}

/* ---------------------------------------------------------------- 加载 / 空态 */
.pp-index-skeleton {
  display: grid;
  grid-template-columns: 64px minmax(0, 1fr) 128px 116px;
  gap: var(--space-4);
  align-items: center;
  padding: var(--space-4) 0;
  border-bottom: 1px solid var(--border-subtle);
}

.pp-sk-num {
  width: 28px;
  height: 14px;
  background: var(--bg-sunken);
}

.pp-sk-line {
  height: 18px;
  background: var(--bg-sunken);
}

.pp-sk-short {
  width: 60%;
}

.pp-empty {
  padding: var(--space-16) 0;
  font: 400 17px / 1.8 var(--font-serif);
  color: var(--text-subtle);
  text-align: center;
}

/* ---------------------------------------------------------------- 窄屏 */
@media (max-width: 767px) {
  .pp-index-cols,
  .pp-index-row {
    grid-template-columns: 40px minmax(0, 1fr);
  }

  .pp-col-cat,
  .pp-col-date {
    display: none;
  }

  .pp-index-mobile-meta {
    display: block;
  }

  .pp-index-title {
    font-size: 17px;
  }

  .pp-index-skeleton {
    grid-template-columns: 40px minmax(0, 1fr);
  }
}
</style>
