<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink } from 'vue-router'
import type { PostDetailPageModel } from '@/ui/contracts/pages'
import GiscusComments from '@/ui/components/GiscusComments.vue'

/**
 * 文章详情：单栏 720px 正文，没有右侧目录栏。
 * 顶部 2px 阅读进度线；目录折叠 <details> 放在正文之前；
 * 标题区是「栏目 / 日期 / 阅读时长」的眉题式排版。
 */
const props = defineProps<{
  model: PostDetailPageModel
}>()

const post = computed(() => props.model.post)
const toc = computed(() => props.model.toc)

const progressStyle = computed(() => ({
  width: `${Math.min(100, Math.max(0, props.model.readProgress))}%`,
}))

function jumpTo(id: string): void {
  props.model.scrollToHeading(id)
}

function retry(): void {
  props.model.retry()
}
</script>

<template>
  <div class="pp-post-detail">
    <!-- 阅读进度：贴在内容区顶端的 2px 墨线 -->
    <div
      class="pp-progress"
      role="progressbar"
      aria-label="阅读进度"
      aria-valuemin="0"
      aria-valuemax="100"
      :aria-valuenow="Math.round(model.readProgress)"
    >
      <span class="pp-progress-fill" :style="progressStyle" />
    </div>

    <!-- 加载中：排版骨架 -->
    <template v-if="model.loading">
      <div class="pp-skeleton" aria-hidden="true">
        <span class="pp-sk-kicker" />
        <span class="pp-sk-title" />
        <span class="pp-sk-title pp-sk-title-short" />
        <span class="pp-sk-meta" />
        <span v-for="n in 5" :key="n" class="pp-sk-line" />
      </div>
      <p class="sr-only">正在加载文章</p>
    </template>

    <!-- 文章不存在 -->
    <section v-else-if="model.notFound" class="pp-state">
      <p class="pp-state-kicker">Not Found · 未收录</p>
      <h1 class="pp-state-title">文章不存在</h1>
      <p class="pp-state-desc">该文章可能已被删除，或尚未发布。编辑部没有找到这一页。</p>
      <RouterLink to="/" class="btn btn-ghost">返回头版</RouterLink>
    </section>

    <!-- 服务端故障 / 网络异常 -->
    <section v-else-if="model.loadFailed" class="pp-state">
      <p class="pp-state-kicker">Loading Error · 暂不可读</p>
      <h1 class="pp-state-title">加载失败</h1>
      <p class="pp-state-desc">服务暂时不可用，请稍后重试。</p>
      <div class="pp-state-actions">
        <button type="button" class="btn btn-primary" @click="retry">重新加载</button>
        <RouterLink to="/" class="btn btn-ghost">返回头版</RouterLink>
      </div>
    </section>

    <article v-else-if="post" class="pp-article">
      <header class="pp-article-head">
        <p class="pp-article-kicker">
          <span>{{ post.categoryName || '随笔' }}</span>
          <span class="pp-kicker-sep">/</span>
          <time>{{ model.publishedLabel }}</time>
          <span class="pp-kicker-sep">/</span>
          <span>{{ model.readMinutes }} 分钟阅读</span>
        </p>

        <h1 class="pp-article-title">{{ post.title }}</h1>

        <p v-if="post.summary" class="pp-article-dek">{{ post.summary }}</p>

        <div class="pp-article-byline">
          <span class="pp-byline-author">{{ post.authorName }}</span>
          <span class="pp-byline-meta">{{ model.publishedLabel }} 刊发</span>
          <span class="pp-byline-meta">{{ post.viewCount }} 次浏览</span>
        </div>

        <div v-if="post.tags.length || post.collections.length" class="pp-article-taxonomy">
          <RouterLink
            v-for="tag in post.tags"
            :key="tag.id"
            :to="{ path: '/posts', query: { tagId: tag.id } }"
            class="pp-tag"
          >
            {{ tag.name }}
          </RouterLink>
          <RouterLink
            v-for="collection in post.collections"
            :key="collection.id"
            :to="`/collections/${collection.slug}`"
            class="pp-tag pp-tag-collection"
          >
            《{{ collection.title }}》
          </RouterLink>
        </div>
      </header>

      <!-- 头图：小图，不铺满首屏 -->
      <figure v-if="post.coverImage" class="pp-article-figure">
        <img :src="post.coverImage" :alt="post.title" />
        <figcaption>{{ post.categoryName || '题图' }} · 墨纸制版</figcaption>
      </figure>

      <!-- 目录：折叠在正文之前，不占右侧栏 -->
      <details v-if="toc.length" class="pp-toc">
        <summary class="pp-toc-summary">
          <span class="pp-toc-label">目录</span>
          <span class="pp-toc-en">Contents · {{ toc.length }} 节</span>
          <span class="pp-toc-mark" aria-hidden="true">＋</span>
        </summary>
        <ol class="pp-toc-list">
          <li
            v-for="item in toc"
            :key="item.id"
            class="pp-toc-item"
            :class="[`is-level-${item.level}`, { 'is-active': item.id === model.activeHeading }]"
          >
            <button type="button" @click="jumpTo(item.id)">{{ item.text }}</button>
          </li>
        </ol>
      </details>

      <!-- 正文（已渲染并净化） -->
      <div class="pp-article-body" v-html="model.renderedContent" />

      <footer class="pp-article-foot">
        <p class="paper-endmark" aria-hidden="true">■ ■ ■</p>
        <p class="pp-article-foot-note">本文排版于「墨纸」主题 · 终</p>
        <div class="pp-article-foot-links">
          <RouterLink to="/archive" class="pp-foot-link">翻阅历期文章 →</RouterLink>
          <RouterLink to="/" class="pp-foot-link">返回头版 →</RouterLink>
        </div>
      </footer>

      <GiscusComments />
    </article>
  </div>
</template>

<style scoped>
/* ---------------------------------------------------------------- 进度线 */
.pp-progress {
  position: fixed;
  top: 0;
  right: 0;
  left: 0;
  z-index: var(--z-nav);
  height: 2px;
  background: transparent;
  pointer-events: none;
}

.pp-progress-fill {
  display: block;
  height: 100%;
  background: var(--brand-500);
  transition: width 80ms linear;
}

/* ---------------------------------------------------------------- 文章骨架 */
.pp-skeleton {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  max-width: var(--size-prose);
  margin: 0 auto;
  padding-top: var(--space-8);
}

.pp-sk-kicker {
  width: 180px;
  height: 12px;
  background: var(--bg-sunken);
}

.pp-sk-title {
  width: 88%;
  height: 40px;
  background: var(--bg-sunken);
}

.pp-sk-title-short {
  width: 56%;
}

.pp-sk-meta {
  width: 240px;
  height: 14px;
  margin-bottom: var(--space-6);
  background: var(--bg-sunken);
}

.pp-sk-line {
  width: 100%;
  height: 16px;
  background: var(--bg-sunken);
}

/* ---------------------------------------------------------------- 文章容器 */
.pp-article {
  max-width: var(--size-prose);
  margin: 0 auto;
}

.pp-article-head {
  padding-bottom: var(--space-6);
}

.pp-article-kicker {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: var(--space-2);
  margin-bottom: var(--space-4);
  font: var(--text-caption);
  letter-spacing: 0.16em;
  text-transform: uppercase;
  color: var(--brand-500);
}

.pp-kicker-sep {
  color: var(--border-strong);
}

.pp-article-title {
  font: 700 clamp(30px, 4.4vw, 46px) / 1.24 var(--font-serif);
  letter-spacing: 0.01em;
  color: var(--text-strong);
}

.pp-article-dek {
  margin-top: var(--space-4);
  font: 400 17px / 1.85 var(--font-serif);
  color: var(--text-muted);
}

.pp-article-byline {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: var(--space-4);
  margin-top: var(--space-6);
  padding: var(--space-3) 0;
  border-top: 1px solid var(--border-default);
  border-bottom: 1px solid var(--border-subtle);
}

.pp-byline-author {
  font: 600 15px / 1.5 var(--font-serif);
  letter-spacing: 0.04em;
  color: var(--text-strong);
}

.pp-byline-meta {
  font: var(--text-caption);
  letter-spacing: 0.12em;
  color: var(--text-subtle);
}

.pp-article-taxonomy {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
  margin-top: var(--space-4);
}

.pp-tag {
  padding: 2px 8px;
  font: var(--text-caption);
  letter-spacing: 0.08em;
  color: var(--text-muted);
  border: 1px solid var(--border-default);
  transition:
    color var(--transition-fast),
    border-color var(--transition-fast);
}

.pp-tag:hover {
  color: var(--brand-500);
  border-color: var(--brand-500);
}

.pp-tag-collection {
  border-style: dashed;
}

/* ---------------------------------------------------------------- 头图 */
.pp-article-figure {
  margin: var(--space-8) 0 0;
}

.pp-article-figure img {
  width: 100%;
  max-height: 300px;
  object-fit: cover;
  border: 1px solid var(--border-default);
}

.pp-article-figure figcaption {
  margin-top: var(--space-2);
  font: var(--text-caption);
  letter-spacing: 0.12em;
  color: var(--text-subtle);
  text-align: right;
}

/* ---------------------------------------------------------------- 折叠目录 */
.pp-toc {
  margin-top: var(--space-8);
  border-top: var(--pp-rule-strong);
  border-bottom: 1px solid var(--border-default);
  padding: var(--space-3) 0;
}

.pp-toc-summary {
  display: flex;
  align-items: baseline;
  gap: var(--space-3);
  list-style: none;
  cursor: pointer;
}

.pp-toc-summary::-webkit-details-marker {
  display: none;
}

.pp-toc-label {
  font: 600 15px / 1.5 var(--font-serif);
  letter-spacing: 0.12em;
  color: var(--text-strong);
}

.pp-toc-en {
  font: var(--text-caption);
  letter-spacing: 0.18em;
  text-transform: uppercase;
  color: var(--text-subtle);
}

.pp-toc-mark {
  margin-left: auto;
  font: 400 15px / 1.4 var(--font-sans);
  color: var(--brand-500);
  transition: transform var(--transition-fast);
}

.pp-toc[open] .pp-toc-mark {
  transform: rotate(45deg);
}

.pp-toc-list {
  display: flex;
  flex-direction: column;
  gap: 2px;
  padding-top: var(--space-3);
}

.pp-toc-item button {
  width: 100%;
  padding: 3px 0;
  font: 400 14px / 1.6 var(--font-serif);
  color: var(--text-muted);
  text-align: left;
  transition: color var(--transition-fast);
}

.pp-toc-item button:hover {
  color: var(--text-strong);
}

.pp-toc-item.is-active button {
  color: var(--brand-500);
}

.pp-toc-item.is-level-3 {
  padding-left: var(--space-4);
}

/* ---------------------------------------------------------------- 正文排版 */
.pp-article-body {
  padding-top: var(--space-8);
  font: 400 17px / 1.95 var(--font-serif);
  color: var(--text-default);
}

.pp-article-body :deep(p) {
  margin: 0 0 1.5em;
}

/* 首段首字下沉，文学刊物的正文起手式（只作用于正文的直接子段落） */
.pp-article-body :deep(> p:first-of-type::first-letter) {
  float: left;
  padding: 4px 10px 0 0;
  font: 700 3.1em / 0.92 var(--font-serif);
  color: var(--brand-500);
}

.pp-article-body :deep(h2) {
  margin: 2.2em 0 0.8em;
  padding-top: 0.7em;
  font: 700 27px / 1.4 var(--font-serif);
  color: var(--text-strong);
  border-top: 1px solid var(--border-default);
  scroll-margin-top: calc(var(--nav-height) + var(--space-4));
}

.pp-article-body :deep(h3) {
  margin: 1.8em 0 0.6em;
  font: 600 21px / 1.5 var(--font-serif);
  color: var(--text-strong);
  scroll-margin-top: calc(var(--nav-height) + var(--space-4));
}

.pp-article-body :deep(h4) {
  margin: 1.6em 0 0.5em;
  font: 600 18px / 1.5 var(--font-serif);
  color: var(--text-strong);
}

.pp-article-body :deep(a) {
  color: var(--brand-500);
  text-decoration: underline;
  text-decoration-thickness: 1px;
  text-underline-offset: 3px;
}

.pp-article-body :deep(strong) {
  color: var(--text-strong);
  font-weight: 700;
}

.pp-article-body :deep(blockquote) {
  margin: 1.6em 0;
  padding: 2px 0 2px var(--space-5);
  font-style: italic;
  color: var(--text-muted);
  border-left: 2px solid var(--brand-500);
}

.pp-article-body :deep(blockquote p:last-child) {
  margin-bottom: 0;
}

.pp-article-body :deep(ul),
.pp-article-body :deep(ol) {
  margin: 0 0 1.5em;
  padding-left: 1.4em;
}

.pp-article-body :deep(ul) {
  list-style: disc;
}

.pp-article-body :deep(ol) {
  list-style: decimal;
}

.pp-article-body :deep(li) {
  margin: 0.3em 0;
}

.pp-article-body :deep(code) {
  padding: 1px 5px;
  font-family: var(--font-mono);
  font-size: 0.84em;
  color: var(--text-strong);
  background: var(--bg-sunken);
  border: 1px solid var(--border-subtle);
}

.pp-article-body :deep(pre) {
  margin: 1.6em 0;
  padding: var(--space-4);
  overflow-x: auto;
  font-size: 13px;
  line-height: 1.7;
  background: var(--bg-sunken);
  border: 1px solid var(--border-default);
}

.pp-article-body :deep(pre code) {
  padding: 0;
  background: none;
  border: 0;
}

.pp-article-body :deep(img) {
  margin: 1.8em auto;
  border: 1px solid var(--border-default);
}

.pp-article-body :deep(hr) {
  margin: 2.4em 0;
  border: 0;
  border-top: var(--pp-rule-double);
}

.pp-article-body :deep(table) {
  width: 100%;
  margin: 1.6em 0;
  font: 400 14px / 1.7 var(--font-sans);
  border-collapse: collapse;
}

.pp-article-body :deep(th),
.pp-article-body :deep(td) {
  padding: var(--space-2) var(--space-3);
  text-align: left;
  border: 1px solid var(--border-default);
}

.pp-article-body :deep(th) {
  color: var(--text-strong);
  background: var(--bg-sunken);
}

/* ---------------------------------------------------------------- 尾部 */
.pp-article-foot {
  margin-top: var(--space-12);
  padding-top: var(--space-8);
  border-top: 1px solid var(--border-default);
  text-align: center;
}

.pp-article-foot-note {
  margin-top: var(--space-4);
  font: var(--text-caption);
  letter-spacing: 0.18em;
  color: var(--text-subtle);
}

.pp-article-foot-links {
  display: flex;
  justify-content: center;
  flex-wrap: wrap;
  gap: var(--space-6);
  margin-top: var(--space-5);
}

.pp-foot-link {
  font: var(--text-caption);
  letter-spacing: 0.14em;
  color: var(--text-muted);
  border-bottom: 1px solid transparent;
  transition:
    color var(--transition-fast),
    border-color var(--transition-fast);
}

.pp-foot-link:hover {
  color: var(--brand-500);
  border-bottom-color: var(--brand-500);
}

/* ---------------------------------------------------------------- 状态页 */
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
@media (min-width: 900px) {
  .pp-progress {
    left: var(--pp-sidebar-width);
  }
}

@media (max-width: 639px) {
  .pp-article-body {
    font-size: 16px;
  }

  .pp-article-dek {
    font-size: 16px;
  }
}
</style>
