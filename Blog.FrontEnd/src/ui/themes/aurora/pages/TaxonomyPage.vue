<script setup lang="ts">
import type { TaxonomyPageModel } from '@/ui/contracts/pages'
import TaxonomySkeleton from '@/ui/components/skeleton/TaxonomySkeleton.vue'

/**
 * 标签墙 / 分类墙（同一个页面组件，按 `model.kind` 决定文案）。
 * 点击项通过 `model.onSelect` 交回数据控制器做路由跳转。
 */
defineProps<{ model: TaxonomyPageModel }>()
</script>

<template>
  <div class="taxonomy-page">
    <header class="page-header">
      <p class="eyebrow">{{ model.eyebrow }}</p>
      <h1 class="page-title gradient-text">{{ model.title }}</h1>
      <p class="page-sub">{{ model.subtitle }}</p>
    </header>

    <div class="container wall-container">
      <TaxonomySkeleton v-if="model.loading" :count="10" />

      <p v-else-if="model.items.length === 0" class="empty">
        {{ model.kind === 'tag' ? '暂无标签' : '暂无分类' }}
      </p>

      <div v-else class="wall">
        <button
          v-for="item in model.items"
          :key="item.id"
          class="btn wall-btn"
          @click="model.onSelect(item.id)"
        >
          <span class="wall-name">{{ item.name }}</span>
          <span class="wall-count">{{ item.count }}</span>
        </button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.taxonomy-page {
  min-height: 70vh;
}

.wall-container {
  padding-bottom: var(--space-16);
}

/* 标签/分类墙：页面专属布局（flex 换行 + 圆角胶囊），视觉值全部来自 Token */
.wall {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: var(--space-4);
  max-width: 860px;
  margin: 0 auto;
}

.wall-btn {
  gap: var(--space-2);
  padding: var(--space-3) var(--space-5);
  font-size: 15px;
  font-weight: 500;
  color: var(--text-default);
  background: var(--bg-surface);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-2xl);
  transition: all var(--transition-fast);
}

.wall-btn:hover {
  color: var(--text-on-brand);
  border-color: transparent;
  background: linear-gradient(
    135deg,
    var(--gradient-start),
    var(--gradient-mid),
    var(--gradient-end)
  );
  transform: translateY(-2px);
  box-shadow: var(--glow-purple);
}

.wall-count {
  min-width: 22px;
  padding: 1px var(--space-2);
  font: var(--text-caption);
  text-align: center;
  color: var(--brand-500);
  background: color-mix(in srgb, var(--brand-500) 12%, transparent);
  border-radius: var(--radius-2xl);
}

.wall-btn:hover .wall-count {
  color: var(--text-on-brand);
  background: color-mix(in srgb, var(--text-on-brand) 22%, transparent);
}

/* 空态留白（视觉来自 foundation `.empty`） */
.empty {
  padding: var(--space-16) 0;
}
</style>
