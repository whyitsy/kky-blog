<script setup lang="ts">
import { RouterLink } from 'vue-router'
import type { ArchivePageModel } from '@/ui/contracts/pages'
import ArchiveSkeleton from '@/ui/components/skeleton/ArchiveSkeleton.vue'

/**
 * 归档页：按年份/月份分组的时间轴。
 * 分组与日期格式化（dateLabel）已在数据控制器完成，页面只负责时间轴布局。
 */
defineProps<{ model: ArchivePageModel }>()
</script>

<template>
  <div class="archive-page">
    <header class="page-header">
      <p class="eyebrow">ARCHIVE · 共 {{ model.total }} 篇文章</p>
      <h1 class="page-title gradient-text">所有文章</h1>
      <p class="page-sub">按时间倒序排列，记录每一步的足迹。</p>
    </header>

    <div class="container timeline-container">
      <ArchiveSkeleton v-if="model.loading" :groups="3" />

      <p v-else-if="model.total === 0" class="empty">暂无文章</p>

      <div v-else class="timeline">
        <section v-for="year in model.years" :key="year.year" class="year-block">
          <div class="year-node">
            <span class="year-dot" />
            <h2 class="year-title">{{ year.year }}</h2>
          </div>

          <div v-for="month in year.months" :key="`${year.year}-${month.month}`" class="month-block">
            <!-- 年月标题统一左对齐 -->
            <h3 class="month-title">
              <span class="month-num">{{ String(month.month).padStart(2, '0') }}</span>
              <span class="month-count">共 {{ month.items.length }} 篇</span>
            </h3>

            <ul class="post-rows">
              <li v-for="item in month.items" :key="item.id" class="post-row">
                <time class="row-date">{{ item.dateLabel }}</time>
                <RouterLink :to="`/post/${item.id}`" class="row-title">{{ item.title }}</RouterLink>
                <span class="row-dot" />
              </li>
            </ul>
          </div>
        </section>
      </div>
    </div>
  </div>
</template>

<style scoped>
.archive-page {
  min-height: 70vh;
}

.timeline-container {
  max-width: 760px;
  padding-bottom: var(--space-16);
}

.timeline {
  position: relative;
  padding-left: var(--space-8);
}

/* 时间轴主线 */
.timeline::before {
  content: '';
  position: absolute;
  left: 7px;
  top: 12px;
  bottom: 12px;
  width: 2px;
  background: linear-gradient(
    180deg,
    var(--gradient-start),
    var(--gradient-mid),
    var(--gradient-end)
  );
  opacity: 0.5;
  border-radius: 1px;
}

.year-block {
  margin-bottom: var(--space-10);
}

.year-node {
  position: relative;
  display: flex;
  align-items: center;
  gap: var(--space-3);
  margin-bottom: var(--space-5);
  margin-left: calc(-1 * var(--space-8));
}

.year-dot {
  width: 16px;
  height: 16px;
  border-radius: 50%;
  background: linear-gradient(135deg, var(--gradient-start), var(--gradient-end));
  box-shadow: var(--glow-cyan);
  flex-shrink: 0;
}

.year-title {
  font-size: 30px;
  font-weight: 700;
  color: var(--text-strong);
}

.month-block {
  margin-bottom: var(--space-6);
}

.month-title {
  display: flex;
  align-items: baseline;
  gap: var(--space-3);
  margin-bottom: var(--space-3);
}

.month-num {
  font-size: 20px;
  font-weight: 700;
  color: var(--brand-500);
}

.month-count {
  font: var(--text-caption);
  color: var(--text-subtle);
}

.post-rows {
  display: flex;
  flex-direction: column;
}

.post-row {
  position: relative;
  display: flex;
  align-items: baseline;
  gap: var(--space-4);
  padding: var(--space-2) 0;
}

/* 行节点小圆点 */
.row-dot {
  position: absolute;
  left: calc(-1 * var(--space-8) + 3px);
  top: 50%;
  transform: translateY(-50%);
  width: 10px;
  height: 10px;
  border-radius: 50%;
  background: var(--bg-canvas);
  border: 2px solid var(--border-strong);
  transition:
    border-color var(--transition-fast),
    box-shadow var(--transition-fast);
}

.post-row:hover .row-dot {
  border-color: var(--brand-500);
  box-shadow: var(--glow-cyan);
}

.row-date {
  flex-shrink: 0;
  width: 48px;
  font: var(--text-caption);
  color: var(--text-subtle);
}

.row-title {
  font-size: 15px;
  font-weight: 500;
  color: var(--text-default);
  transition: color var(--transition-fast);
}

.post-row:hover .row-title {
  color: var(--brand-500);
}

/* 空态留白（视觉来自 foundation `.empty`） */
.empty {
  padding: var(--space-16) 0;
}
</style>
