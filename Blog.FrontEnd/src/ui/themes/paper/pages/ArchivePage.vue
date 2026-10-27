<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink } from 'vue-router'
import type { ArchivePageModel, ArchiveYearModel } from '@/ui/contracts/pages'
import PaperPageHead from '../components/PaperPageHead.vue'
import { pad2 } from '../format'

/**
 * 年表：年份左栏 + 文章索引右栏。
 * 不是 aurora 那种居中时间轴 —— 这里像刊物的年度目录：左边年份压阵，右边按月份排条目。
 */
const props = defineProps<{
  model: ArchivePageModel
}>()

const years = computed(() => props.model.years)

function yearCount(year: ArchiveYearModel): number {
  return year.months.reduce((sum, month) => sum + month.items.length, 0)
}
</script>

<template>
  <div class="pp-archive-page">
    <PaperPageHead
      eyebrow="Archive · 年表"
      title="文章年表"
      :subtitle="`共 ${model.total} 篇文章，按刊期倒序整理。`"
    >
      <template #meta>
        <span>{{ years.length }} 个年份</span>
      </template>
    </PaperPageHead>

    <template v-if="model.loading">
      <div class="pp-loading" aria-hidden="true">
        <div v-for="n in 3" :key="n" class="pp-loading-year">
          <span class="pp-sk-year" />
          <span class="pp-sk-line" />
        </div>
      </div>
      <p class="sr-only">正在加载文章年表</p>
    </template>

    <p v-else-if="!model.total" class="pp-empty">暂无文章存档。</p>

    <div v-else class="pp-years">
      <section v-for="year in years" :key="year.year" class="pp-year">
        <header class="pp-year-head">
          <h2 class="pp-year-title">{{ year.year }}</h2>
          <span class="pp-year-count">{{ yearCount(year) }} 篇</span>
        </header>

        <div class="pp-year-body">
          <div
            v-for="month in year.months"
            :key="`${year.year}-${month.month}`"
            class="pp-month"
          >
            <h3 class="pp-month-title">
              <span>{{ pad2(month.month) }} 月</span>
              <span class="pp-month-count">{{ month.items.length }} 篇</span>
            </h3>

            <ul class="pp-archive-list">
              <li v-for="item in month.items" :key="item.id" class="pp-archive-row">
                <time class="pp-archive-date" :datetime="item.publishedAt">{{ item.dateLabel }}</time>
                <RouterLink :to="`/post/${item.id}`" class="pp-archive-title">
                  {{ item.title }}
                </RouterLink>
              </li>
            </ul>
          </div>
        </div>
      </section>
    </div>
  </div>
</template>

<style scoped>
/* ---------------------------------------------------------------- 年份分栏 */
.pp-year {
  display: grid;
  grid-template-columns: 168px minmax(0, 1fr);
  gap: var(--space-10);
  padding: var(--space-10) 0;
  border-top: var(--pp-rule-strong);
}

.pp-year:first-child {
  margin-top: var(--space-6);
}

.pp-year-head {
  position: sticky;
  top: var(--space-6);
  align-self: start;
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
}

.pp-year-title {
  font: 700 56px / 1 var(--font-serif);
  letter-spacing: 0.02em;
  color: var(--text-strong);
}

.pp-year-count {
  font: var(--text-caption);
  letter-spacing: 0.18em;
  color: var(--brand-500);
}

/* ---------------------------------------------------------------- 月份条目 */
.pp-month + .pp-month {
  margin-top: var(--space-6);
}

.pp-month-title {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: var(--space-4);
  padding-bottom: var(--space-2);
  font: 600 13px / 1.5 var(--font-sans);
  letter-spacing: 0.2em;
  text-transform: uppercase;
  color: var(--text-muted);
  border-bottom: 1px solid var(--border-default);
}

.pp-month-count {
  font: var(--text-caption);
  letter-spacing: 0.12em;
  color: var(--text-subtle);
}

.pp-archive-list {
  display: flex;
  flex-direction: column;
}

.pp-archive-row {
  display: grid;
  grid-template-columns: 64px minmax(0, 1fr);
  gap: var(--space-4);
  align-items: baseline;
  padding: 10px 0;
  border-bottom: 1px dotted var(--border-subtle);
}

.pp-archive-date {
  font: var(--text-caption);
  letter-spacing: 0.1em;
  color: var(--text-subtle);
}

.pp-archive-title {
  font: 400 17px / 1.55 var(--font-serif);
  color: var(--text-default);
  transition: color var(--transition-fast);
}

.pp-archive-title:hover {
  color: var(--brand-500);
  text-decoration: underline;
  text-decoration-thickness: 1px;
  text-underline-offset: 4px;
}

/* ---------------------------------------------------------------- 加载 / 空态 */
.pp-loading {
  display: flex;
  flex-direction: column;
  gap: var(--space-6);
  padding-top: var(--space-8);
}

.pp-loading-year {
  display: grid;
  grid-template-columns: 168px minmax(0, 1fr);
  gap: var(--space-10);
  align-items: center;
  padding-bottom: var(--space-6);
  border-bottom: 1px solid var(--border-subtle);
}

.pp-sk-year {
  width: 120px;
  height: 40px;
  background: var(--bg-sunken);
}

.pp-sk-line {
  width: 70%;
  height: 18px;
  background: var(--bg-sunken);
}

.pp-empty {
  padding: var(--space-16) 0;
  font: 400 17px / 1.8 var(--font-serif);
  color: var(--text-subtle);
  text-align: center;
}

/* ---------------------------------------------------------------- 窄屏 */
@media (max-width: 767px) {
  .pp-year {
    grid-template-columns: minmax(0, 1fr);
    gap: var(--space-4);
    padding: var(--space-6) 0;
  }

  .pp-year-head {
    position: static;
    flex-direction: row;
    align-items: baseline;
    gap: var(--space-3);
  }

  .pp-year-title {
    font-size: 38px;
  }

  .pp-loading-year {
    grid-template-columns: minmax(0, 1fr);
    gap: var(--space-3);
  }
}
</style>
