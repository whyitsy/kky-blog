<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink } from 'vue-router'
import type { CollectionListPageModel } from '@/ui/contracts/pages'
import PaperPageHead from '../components/PaperPageHead.vue'
import { pad2 } from '../format'

/**
 * 专栏列表：编辑部式条目，不是三列卡片墙。
 * 每一条都是一个「栏目」：编号、题图（可选）、标题、简介、篇数。
 */
const props = defineProps<{
  model: CollectionListPageModel
}>()

const collections = computed(() => props.model.collections)
</script>

<template>
  <div class="pp-collections-page">
    <PaperPageHead
      eyebrow="Columns · 专栏"
      title="专栏"
      subtitle="把多篇文章编成一个系列，按顺序读下去。"
    >
      <template #meta>
        <span>{{ collections.length }} 个系列</span>
      </template>
    </PaperPageHead>

    <template v-if="model.loading">
      <div class="pp-loading" aria-hidden="true">
        <div v-for="n in 4" :key="n" class="pp-loading-row">
          <span class="pp-sk-num" />
          <span class="pp-sk-line" />
        </div>
      </div>
      <p class="sr-only">正在加载专栏列表</p>
    </template>

    <p v-else-if="!collections.length" class="pp-empty">暂无专栏。</p>

    <ol v-else class="pp-collection-list">
      <li v-for="(collection, index) in collections" :key="collection.id" class="pp-collection">
        <RouterLink :to="`/collections/${collection.slug}`" class="pp-collection-link">
          <span class="pp-collection-num" aria-hidden="true">{{ pad2(index + 1) }}</span>

          <span v-if="collection.coverImage" class="pp-collection-cover">
            <img :src="collection.coverImage" :alt="collection.title" loading="lazy" />
          </span>

          <span class="pp-collection-main">
            <span class="pp-collection-title">{{ collection.title }}</span>
            <span class="pp-collection-desc">{{ collection.description || '暂无简介。' }}</span>
            <span class="pp-collection-meta">{{ collection.postCount }} 篇文章 · 连续阅读</span>
          </span>

          <span class="pp-collection-arrow" aria-hidden="true">→</span>
        </RouterLink>
      </li>
    </ol>
  </div>
</template>

<style scoped>
.pp-collection-list {
  display: flex;
  flex-direction: column;
  padding-top: var(--space-6);
}

.pp-collection {
  border-bottom: 1px solid var(--border-default);
}

.pp-collection-link {
  display: grid;
  grid-template-columns: 56px auto minmax(0, 1fr) auto;
  align-items: center;
  gap: var(--space-6);
  padding: var(--space-6) 0;
  transition: background var(--transition-fast);
}

.pp-collection-link:hover {
  background: var(--bg-surface);
}

.pp-collection-num {
  font: 400 15px / 1.4 var(--font-mono);
  letter-spacing: 0.06em;
  color: var(--border-strong);
}

.pp-collection-link:hover .pp-collection-num {
  color: var(--brand-500);
}

.pp-collection-cover {
  display: block;
  width: 132px;
  height: 84px;
  overflow: hidden;
  border: 1px solid var(--border-default);
}

.pp-collection-cover img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.pp-collection-main {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  min-width: 0;
}

.pp-collection-title {
  font: 700 27px / 1.3 var(--font-serif);
  letter-spacing: 0.02em;
  color: var(--text-strong);
  transition: color var(--transition-fast);
}

.pp-collection-link:hover .pp-collection-title {
  color: var(--brand-500);
}

.pp-collection-desc {
  display: -webkit-box;
  max-width: 62ch;
  overflow: hidden;
  font: 400 15px / 1.75 var(--font-serif);
  color: var(--text-muted);
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 2;
  line-clamp: 2;
}

.pp-collection-meta {
  font: var(--text-caption);
  letter-spacing: 0.14em;
  color: var(--text-subtle);
}

.pp-collection-arrow {
  font: 400 20px / 1 var(--font-sans);
  color: var(--text-subtle);
  transition:
    color var(--transition-fast),
    transform var(--transition-fast);
}

.pp-collection-link:hover .pp-collection-arrow {
  color: var(--brand-500);
  transform: translateX(4px);
}

/* ---------------------------------------------------------------- 加载 / 空态 */
.pp-loading {
  display: flex;
  flex-direction: column;
  padding-top: var(--space-6);
}

.pp-loading-row {
  display: grid;
  grid-template-columns: 56px minmax(0, 1fr);
  gap: var(--space-6);
  align-items: center;
  padding: var(--space-6) 0;
  border-bottom: 1px solid var(--border-subtle);
}

.pp-sk-num {
  width: 28px;
  height: 14px;
  background: var(--bg-sunken);
}

.pp-sk-line {
  width: 58%;
  height: 24px;
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
  .pp-collection-link {
    grid-template-columns: 36px minmax(0, 1fr);
    gap: var(--space-3);
    align-items: start;
    padding: var(--space-5) 0;
  }

  .pp-collection-cover {
    display: none;
  }

  .pp-collection-arrow {
    display: none;
  }

  .pp-collection-title {
    font-size: 21px;
  }

  .pp-loading-row {
    grid-template-columns: 36px minmax(0, 1fr);
  }
}
</style>
