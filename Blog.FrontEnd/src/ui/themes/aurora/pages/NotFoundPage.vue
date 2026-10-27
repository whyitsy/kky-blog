<script setup lang="ts">
import type { NotFoundPageModel } from '@/ui/contracts/pages'

/**
 * 404 页：极光背景 + 渐变 404 大字。
 * 地址路径展示出来便于排查（旧行为：不再静默重定向首页）。
 */
defineProps<{ model: NotFoundPageModel }>()
</script>

<template>
  <section class="not-found">
    <div class="aurora-blobs" />
    <div class="container not-found-inner">
      <p class="code-404 gradient-text">404</p>
      <h1 class="title">页面不存在</h1>
      <p class="desc">
        找不到 <code class="code">{{ model.attemptedPath }}</code>
        <br />
        它可能已被删除，或者链接拼写有误。
      </p>
      <div class="actions">
        <button type="button" class="btn btn-primary" @click="model.goHome()">返回首页</button>
        <button type="button" class="btn btn-ghost" @click="model.goBack()">返回上一页</button>
      </div>
    </div>
  </section>
</template>

<style scoped>
/* 全宽铺满视口：极光背景不属于某个居中容器，否则会出现一个「发光矩形」 */
.not-found {
  position: relative;
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: calc(100vh - var(--nav-height));
  overflow: hidden;
}

.not-found-inner {
  position: relative;
  z-index: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--space-3);
  padding-top: var(--space-16);
  padding-bottom: var(--space-16);
  text-align: center;
}

.code-404 {
  font-size: 96px;
  font-weight: 700;
  line-height: 1;
  margin: 0;
}

.title {
  font: var(--text-h2);
  color: var(--text-strong);
  margin: 0;
}

.desc {
  font: var(--text-body-sm);
  color: var(--text-muted);
  line-height: 1.9;
}

.actions {
  display: flex;
  gap: var(--space-3);
  margin-top: var(--space-4);
}
</style>
