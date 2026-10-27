<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink } from 'vue-router'
import type { CollectionDetailPageModel } from '@/ui/contracts/pages'
import PaperPageHead from '../components/PaperPageHead.vue'
import { pad2, shortDate } from '../format'

/**
 * 专栏详情：编辑部式的编号阅读清单。
 * 标题区之后按专栏顺序列出文章，用大号衬线编号压住每一行。
 */
const props = defineProps<{
  model: CollectionDetailPageModel
}>()

const detail = computed(() => props.model.detail)
const posts = computed(() => detail.value?.posts ?? [])

function retry(): void {
  props.model.retry()
}
</script>

<template>
  <div class="pp-collection-detail">
    <template v-if="model.loading">
      <div class="pp-loading" aria-hidden="true">
        <span class="pp-sk-kicker" />
        <span class="pp-sk-title" />
        <span v-for="n in 4" :key="n" class="pp-sk-row" />
      </div>
      <p class="sr-only">正在加载专栏</p>
    </template>

    <section v-else-if="model.notFound" class="pp-state">
      <p class="pp-state-kicker">Not Found · 未收录</p>
      <h1 class="pp-state-title">专栏不存在</h1>
      <p class="pp-state-desc">该专栏可能已被删除，或尚未发布。</p>
      <RouterLink to="/collections" class="btn btn-ghost">查看全部专栏</RouterLink>
    </section>

    <section v-else-if="model.loadFailed" class="pp-state">
      <p class="pp-state-kicker">Loading Error · 暂不可读</p>
      <h1 class="pp-state-title">加载失败</h1>
      <p class="pp-state-desc">服务暂时不可用，请稍后重试。</p>
      <div class="pp-state-actions">
        <button type="button" class="btn btn-primary" @click="retry">重新加载</button>
        <RouterLink to="/collections" class="btn btn-ghost">查看全部专栏</RouterLink>
      </div>
    </section>

    <template v-else-if="detail">
      <PaperPageHead
        eyebrow="Collection · 阅读清单"
        :title="detail.title"
        :subtitle="detail.description || '按顺序阅读这个系列。'"
      >
        <template #meta>
          <span>{{ posts.length }} 篇 · 依序编入</span>
        </template>
      </PaperPageHead>

      <p v-if="!posts.length" class="pp-empty">该专栏还没有文章。</p>

      <ol v-else class="pp-reading-list">
        <li v-for="(post, index) in posts" :key="post.id" class="pp-reading-item">
          <span class="pp-reading-num" aria-hidden="true">{{ pad2(index + 1) }}</span>

          <div class="pp-reading-main">
            <RouterLink :to="`/post/${post.id}`" class="pp-reading-title">
              {{ post.title }}
            </RouterLink>
            <p v-if="post.summary" class="pp-reading-summary">{{ post.summary }}</p>
            <p class="pp-reading-meta">
              <time :datetime="post.publishedAt ?? undefined">{{ shortDate(post.publishedAt) }}</time>
              <span aria-hidden="true">·</span>
              <span>{{ post.viewCount }} 次浏览</span>
            </p>
          </div>

          <RouterLink :to="`/post/${post.id}`" class="pp-reading-read">阅读 →</RouterLink>
        </li>
      </ol>

      <RouterLink to="/collections" class="pp-back">← 返回全部专栏</RouterLink>
    </template>
  </div>
</template>

<style scoped>
.pp-reading-list {
  display: flex;
  flex-direction: column;
}

.pp-reading-item {
  display: grid;
  grid-template-columns: 84px minmax(0, 1fr) auto;
  gap: var(--space-5);
  padding: var(--space-6) 0;
  border-bottom: 1px solid var(--border-subtle);
}

.pp-reading-item:first-child {
  border-top: 1px solid var(--border-default);
  margin-top: var(--space-6);
}

.pp-reading-num {
  font: 400 46px / 0.95 var(--font-serif);
  letter-spacing: 0.01em;
  color: var(--border-strong);
}

.pp-reading-item:hover .pp-reading-num {
  color: var(--brand-500);
}

.pp-reading-main {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  min-width: 0;
}

.pp-reading-title {
  font: 700 23px / 1.4 var(--font-serif);
  color: var(--text-strong);
  transition: color var(--transition-fast);
}

.pp-reading-title:hover {
  color: var(--brand-500);
}

.pp-reading-summary {
  display: -webkit-box;
  max-width: 66ch;
  overflow: hidden;
  font: 400 15px / 1.75 var(--font-serif);
  color: var(--text-muted);
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 2;
  line-clamp: 2;
}

.pp-reading-meta {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
  font: var(--text-caption);
  letter-spacing: 0.12em;
  color: var(--text-subtle);
}

.pp-reading-read {
  align-self: center;
  font: var(--text-caption);
  letter-spacing: 0.14em;
  color: var(--text-muted);
  white-space: nowrap;
  border-bottom: 1px solid transparent;
  transition:
    color var(--transition-fast),
    border-color var(--transition-fast);
}

.pp-reading-read:hover {
  color: var(--brand-500);
  border-bottom-color: var(--brand-500);
}

.pp-back {
  display: inline-block;
  margin-top: var(--space-8);
  font: var(--text-caption);
  letter-spacing: 0.14em;
  color: var(--text-muted);
  border-bottom: 1px solid transparent;
  transition:
    color var(--transition-fast),
    border-color var(--transition-fast);
}

.pp-back:hover {
  color: var(--brand-500);
  border-bottom-color: var(--brand-500);
}

/* ---------------------------------------------------------------- 加载 / 空态 / 状态页 */
.pp-loading {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  padding-top: var(--space-8);
}

.pp-sk-kicker {
  width: 160px;
  height: 12px;
  background: var(--bg-sunken);
}

.pp-sk-title {
  width: 52%;
  height: 40px;
  background: var(--bg-sunken);
}

.pp-sk-row {
  width: 100%;
  height: 54px;
  background: var(--bg-sunken);
}

.pp-empty {
  padding: var(--space-16) 0;
  font: 400 17px / 1.8 var(--font-serif);
  color: var(--text-subtle);
  text-align: center;
}

.pp-state {
  max-width: 560px;
  margin: var(--space-12) auto;
  padding: var(--space-10) var(--space-6);
  text-align: center;
  border-top: var(--pp-rule-double);
  border-bottom: 1px solid var(--border-default);
}

.pp-state-kicker {
  margin-bottom: var(--space-3);
  font: var(--text-caption);
  letter-spacing: 0.26em;
  text-transform: uppercase;
  color: var(--brand-500);
}

.pp-state-title {
  font: 700 34px / 1.3 var(--font-serif);
  color: var(--text-strong);
}

.pp-state-desc {
  margin: var(--space-3) 0 var(--space-6);
  font: 400 16px / 1.8 var(--font-serif);
  color: var(--text-muted);
}

.pp-state-actions {
  display: flex;
  justify-content: center;
  flex-wrap: wrap;
  gap: var(--space-3);
}

/* ---------------------------------------------------------------- 窄屏 */
@media (max-width: 639px) {
  .pp-reading-item {
    grid-template-columns: 52px minmax(0, 1fr);
    gap: var(--space-3);
  }

  .pp-reading-num {
    font-size: 32px;
  }

  .pp-reading-read {
    display: none;
  }

  .pp-reading-title {
    font-size: 19px;
  }
}
</style>
