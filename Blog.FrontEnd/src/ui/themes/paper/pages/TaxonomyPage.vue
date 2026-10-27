<script setup lang="ts">
import { computed } from 'vue'
import type { TaxonomyItemModel, TaxonomyPageModel } from '@/ui/contracts/pages'
import PaperPageHead from '../components/PaperPageHead.vue'
import { pad2 } from '../format'

/**
 * 标签墙 / 分类墙：按文章数（count）做权重。
 *   · 标签 → 字号随 count 变化的词云（越重越像标题）
 *   · 分类 → 带点线引导的分类索引表
 * 与 aurora 的等大药丸按钮相对。
 */
const props = defineProps<{
  model: TaxonomyPageModel
}>()

const maxCount = computed(() =>
  props.model.items.reduce((max, item) => Math.max(max, item.count), 0),
)

function sizeFor(item: TaxonomyItemModel): string {
  const ratio = maxCount.value > 0 ? item.count / maxCount.value : 0
  return `${(15 + 26 * Math.sqrt(ratio)).toFixed(0)}px`
}

function isHeavy(item: TaxonomyItemModel): boolean {
  return maxCount.value > 0 && item.count === maxCount.value
}

function select(id: string): void {
  props.model.onSelect(id)
}
</script>

<template>
  <div class="pp-taxonomy-page">
    <PaperPageHead :eyebrow="model.eyebrow" :title="model.title" :subtitle="model.subtitle">
      <template #meta>
        <span>{{ model.kind === 'tag' ? 'Tag Cloud' : 'Category Index' }}</span>
      </template>
    </PaperPageHead>

    <template v-if="model.loading">
      <div class="pp-loading" aria-hidden="true">
        <span
          v-for="n in 12"
          :key="n"
          class="pp-sk-item"
          :style="{ width: `${48 + (n % 4) * 22}px` }"
        />
      </div>
      <p class="sr-only">正在加载{{ model.kind === 'tag' ? '标签' : '分类' }}</p>
    </template>

    <p v-else-if="!model.items.length" class="pp-empty">
      暂无{{ model.kind === 'tag' ? '标签' : '分类' }}。
    </p>

    <!-- 标签：加权词云（字号 = 文章数的平方根映射） -->
    <div v-else-if="model.kind === 'tag'" class="pp-cloud">
      <button
        v-for="item in model.items"
        :key="item.id"
        type="button"
        class="pp-cloud-item"
        :class="{ 'is-heavy': isHeavy(item) }"
        :style="{ fontSize: sizeFor(item) }"
        @click="select(item.id)"
      >
        {{ item.name }}
        <sup class="pp-cloud-count">{{ item.count }}</sup>
      </button>
    </div>

    <!-- 分类：编号 + 点线引导 + 文章数的索引表 -->
    <ol v-else class="pp-category-list">
      <li v-for="(item, index) in model.items" :key="item.id" class="pp-category-row">
        <button type="button" class="pp-category-btn" @click="select(item.id)">
          <span class="pp-category-num">{{ pad2(index + 1) }}</span>
          <span class="pp-category-name">{{ item.name }}</span>
          <span class="pp-category-dots" aria-hidden="true" />
          <span class="pp-category-count">{{ item.count }} 篇</span>
        </button>
      </li>
    </ol>
  </div>
</template>

<style scoped>
/* ---------------------------------------------------------------- 标签词云 */
.pp-cloud {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: var(--space-3) var(--space-8);
  padding: var(--space-10) 0 var(--space-6);
}

.pp-cloud-item {
  position: relative;
  padding: 2px 0;
  font-family: var(--font-serif);
  font-weight: 400;
  line-height: 1.4;
  color: var(--text-default);
  background: none;
  border: 0;
  border-bottom: 1px solid transparent;
  transition:
    color var(--transition-fast),
    border-color var(--transition-fast);
}

.pp-cloud-item:hover {
  color: var(--brand-500);
  border-bottom-color: var(--brand-500);
}

.pp-cloud-item.is-heavy {
  font-weight: 700;
  color: var(--text-strong);
}

.pp-cloud-count {
  margin-left: 4px;
  font: 500 11px / 1 var(--font-sans);
  letter-spacing: 0.04em;
  color: var(--text-subtle);
  vertical-align: super;
}

.pp-cloud-item.is-heavy .pp-cloud-count {
  color: var(--brand-500);
}

/* ---------------------------------------------------------------- 分类索引 */
.pp-category-list {
  display: flex;
  flex-direction: column;
  padding-top: var(--space-6);
}

.pp-category-row {
  border-bottom: 1px solid var(--border-subtle);
}

.pp-category-btn {
  display: grid;
  grid-template-columns: 48px auto minmax(40px, 1fr) auto;
  align-items: baseline;
  gap: var(--space-4);
  width: 100%;
  padding: var(--space-4) 0;
  text-align: left;
  background: none;
  border: 0;
}

.pp-category-num {
  font: 400 13px / 1.4 var(--font-mono);
  letter-spacing: 0.06em;
  color: var(--border-strong);
}

.pp-category-name {
  font: 400 21px / 1.4 var(--font-serif);
  color: var(--text-strong);
  transition: color var(--transition-fast);
}

.pp-category-dots {
  align-self: center;
  border-bottom: 1px dotted var(--border-default);
}

.pp-category-count {
  font: var(--text-caption);
  letter-spacing: 0.12em;
  color: var(--text-subtle);
}

.pp-category-btn:hover .pp-category-name {
  color: var(--brand-500);
}

.pp-category-btn:hover .pp-category-num,
.pp-category-btn:hover .pp-category-count {
  color: var(--brand-500);
}

/* ---------------------------------------------------------------- 加载 / 空态 */
.pp-loading {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-4) var(--space-6);
  padding-top: var(--space-8);
}

.pp-sk-item {
  height: 26px;
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
  .pp-cloud {
    gap: var(--space-2) var(--space-5);
    padding-top: var(--space-6);
  }

  .pp-category-btn {
    grid-template-columns: 32px minmax(0, 1fr) auto;
    gap: var(--space-3);
  }

  .pp-category-dots {
    display: none;
  }

  .pp-category-name {
    font-size: 18px;
  }
}
</style>
