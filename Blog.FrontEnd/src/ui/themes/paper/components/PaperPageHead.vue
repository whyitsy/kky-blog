<script setup lang="ts">
/**
 * 墨纸页头：左对齐 + 上双线下细线。
 * 与 aurora 的居中渐变页头相对，这里更像一本刊物的章节扉页。
 */
withDefaults(
  defineProps<{
    eyebrow: string
    title: string
    subtitle?: string
  }>(),
  { subtitle: '' },
)
</script>

<template>
  <header class="pp-page-head">
    <div class="pp-page-head-top">
      <p class="pp-page-eyebrow">{{ eyebrow }}</p>
      <div v-if="$slots.meta" class="pp-page-head-meta">
        <slot name="meta" />
      </div>
    </div>
    <h1 class="pp-page-title">{{ title }}</h1>
    <p v-if="subtitle" class="pp-page-sub">{{ subtitle }}</p>
  </header>
</template>

<style scoped>
.pp-page-head {
  padding: var(--space-6) 0 var(--space-5);
  border-top: var(--pp-rule-double);
  border-bottom: 1px solid var(--border-default);
}

.pp-page-head-top {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: var(--space-4);
  flex-wrap: wrap;
  margin-bottom: var(--space-4);
}

.pp-page-eyebrow {
  font: var(--text-caption);
  letter-spacing: 0.28em;
  text-transform: uppercase;
  color: var(--brand-500);
}

.pp-page-head-meta {
  font: var(--text-caption);
  letter-spacing: 0.14em;
  color: var(--text-subtle);
}

.pp-page-title {
  font: 700 clamp(30px, 4.2vw, 46px) / 1.2 var(--font-serif);
  letter-spacing: 0.02em;
  color: var(--text-strong);
}

.pp-page-sub {
  max-width: 48em;
  margin-top: var(--space-3);
  font: 400 16px / 1.8 var(--font-serif);
  color: var(--text-muted);
}
</style>
