<script setup lang="ts">
/**
 * 主题内的通用状态提示（文章不存在 / 加载失败 / 专栏不存在…）。
 *
 * 只是「图标 + 标题 + 说明 + 操作按钮」的布局组合，视觉值全部来自 Token 与
 * foundation 的 `.btn` 系列；操作按钮由调用方通过默认插槽传入。
 */
defineProps<{
  icon: string
  title: string
  description: string
}>()
</script>

<template>
  <section class="container state-notice">
    <p class="state-icon">{{ icon }}</p>
    <h1 class="state-title">{{ title }}</h1>
    <p class="state-desc">{{ description }}</p>
    <div class="state-actions">
      <slot />
    </div>
  </section>
</template>

<style scoped>
.state-notice {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--space-3);
  padding: var(--space-24) var(--space-6);
  text-align: center;
}

.state-icon {
  font-size: 48px;
  margin-bottom: var(--space-2);
}

.state-title {
  font: var(--text-h2);
  color: var(--text-strong);
}

.state-desc {
  font: var(--text-body-sm);
  color: var(--text-muted);
  margin-bottom: var(--space-3);
}

.state-actions {
  display: flex;
  gap: var(--space-4);
  align-items: center;
  flex-wrap: wrap;
}
</style>
