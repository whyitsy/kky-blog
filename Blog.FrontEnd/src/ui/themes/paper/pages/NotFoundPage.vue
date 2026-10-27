<script setup lang="ts">
import type { NotFoundPageModel } from '@/ui/contracts/pages'

/**
 * 404：一个排错版面的样子。
 * 巨大衬线 404 压阵，下面给出「找不到的路径」与返回入口。
 */
const props = defineProps<{
  model: NotFoundPageModel
}>()

function goHome(): void {
  props.model.goHome()
}

function goBack(): void {
  props.model.goBack()
}
</script>

<template>
  <section class="pp-not-found">
    <p class="pp-not-found-kicker">Error 404 · 版面之外</p>
    <p class="pp-not-found-code" aria-hidden="true">404</p>
    <h1 class="pp-not-found-title">这一页没有排进版面</h1>

    <p class="pp-not-found-desc">
      找不到 <code class="code">{{ model.attemptedPath }}</code>
      <br />
      它可能已被撤稿，或者链接在转版时拼错了。
    </p>

    <div class="pp-not-found-actions">
      <button type="button" class="btn btn-primary" @click="goHome">返回头版</button>
      <button type="button" class="btn btn-ghost" @click="goBack">退回上一页</button>
    </div>

    <p class="pp-not-found-colophon">墨纸 · 编辑部</p>
  </section>
</template>

<style scoped>
.pp-not-found {
  display: flex;
  flex-direction: column;
  align-items: center;
  max-width: 720px;
  margin: var(--space-12) auto;
  padding: var(--space-12) var(--space-6);
  text-align: center;
  border-top: var(--pp-rule-double);
  border-bottom: var(--pp-rule-double);
}

.pp-not-found-kicker {
  font: var(--text-caption);
  letter-spacing: 0.3em;
  text-transform: uppercase;
  color: var(--brand-500);
}

.pp-not-found-code {
  margin: var(--space-2) 0;
  font: 700 clamp(120px, 22vw, 220px) / 0.92 var(--font-serif);
  letter-spacing: 0.02em;
  color: var(--text-strong);
  user-select: none;
}

.pp-not-found-title {
  font: 700 28px / 1.4 var(--font-serif);
  letter-spacing: 0.04em;
  color: var(--text-strong);
}

.pp-not-found-desc {
  margin-top: var(--space-4);
  font: 400 16px / 1.9 var(--font-serif);
  color: var(--text-muted);
}

.pp-not-found-desc .code {
  word-break: break-all;
}

.pp-not-found-actions {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: var(--space-3);
  margin-top: var(--space-8);
}

.pp-not-found-colophon {
  margin-top: var(--space-8);
  font: 500 9px / 1.4 var(--font-sans);
  letter-spacing: 0.26em;
  text-transform: uppercase;
  color: var(--border-strong);
}

@media (max-width: 639px) {
  .pp-not-found {
    margin: var(--space-6) auto;
    padding: var(--space-8) var(--space-4);
  }

  .pp-not-found-title {
    font-size: 23px;
  }
}
</style>
