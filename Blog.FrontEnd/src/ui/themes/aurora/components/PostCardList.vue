<script setup lang="ts">
import { nextTick, onBeforeUnmount, ref, watch } from 'vue'
import type { PagedResult, PostListItemDto } from '@/types'
import PostCard from './PostCard.vue'
import PostCardSkeleton from '@/ui/components/skeleton/PostCardSkeleton.vue'
import PaginationBar from './PaginationBar.vue'

/**
 * 文章卡片列表：骨架屏 / 空态 / 三列栅格（1180px 以上三列、768+ 两列、手机单列）+ 分页。
 * 只负责展示，翻页通过 `page-change` 事件交回页面（由视图的数据控制器处理路由）。
 *
 * <h3>滚动入场动画</h3>
 * 首页首屏是 100vh 的 Hero，卡片本来就在首屏之外；列表数据先到、DOM 也先渲染，
 * 直接出现在第二屏会显得「页面已经加载完了」。这里用 IntersectionObserver 让每张卡片
 * 滚动进入视口时才播放「自下而上 + 从小到大」的过渡，并按顺序错开一点延迟。
 *
 * 降级：`prefers-reduced-motion` 或浏览器不支持 IntersectionObserver 时直接显示，
 * 不依赖动画也能读到内容。类名是通过 DOM 挂的，所以即使 JS 失效，卡片默认可见。
 */
const props = defineProps<{
  result: PagedResult<PostListItemDto> | null
  loading: boolean
  skeletonCount?: number
}>()

const emit = defineEmits<{ (e: 'page-change', page: number): void }>()

const gridRef = ref<HTMLElement | null>(null)
let observer: IntersectionObserver | null = null

function teardownReveal() {
  observer?.disconnect()
  observer = null
}

function setupReveal() {
  teardownReveal()

  const grid = gridRef.value
  if (!grid) return

  const items = Array.from(grid.children) as HTMLElement[]
  const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches
  if (reducedMotion || typeof IntersectionObserver === 'undefined') {
    for (const item of items) item.classList.remove('reveal-item', 'is-revealed')
    return
  }

  observer = new IntersectionObserver(
    (entries) => {
      for (const entry of entries) {
        if (!entry.isIntersecting) continue
        entry.target.classList.add('is-revealed')
        observer?.unobserve(entry.target)
      }
    },
    // 卡片进入视口 12% 时触发；底部预留一点空间，避免「刚露头就开始播」
    { threshold: 0.12, rootMargin: '0px 0px -6% 0px' },
  )

  items.forEach((item, index) => {
    // 翻页/筛选后 DOM 可能被复用：先复位，再重新观察
    item.classList.remove('is-revealed')
    item.classList.add('reveal-item')
    item.style.setProperty('--reveal-delay', `${Math.min(index, 6) * 65}ms`)
    observer!.observe(item)
  })
}

watch(
  () => [props.loading, props.result?.page, props.result?.items.length],
  async () => {
    await nextTick()
    setupReveal()
  },
  { immediate: true },
)

onBeforeUnmount(teardownReveal)
</script>

<template>
  <div class="post-list">
    <!-- 骨架屏（组件化） -->
    <PostCardSkeleton v-if="loading" :count="skeletonCount ?? 12" />

    <!-- 空态 -->
    <div v-else-if="!result || result.items.length === 0" class="empty">
      <p class="empty-icon">📝</p>
      <p>暂无文章</p>
    </div>

    <!-- 卡片栅格：≥1200px 三列 / 768-1199 两列 / <768 单列 -->
    <template v-else>
      <div ref="gridRef" class="grid">
        <div v-for="post in result.items" :key="post.id" class="grid-item">
          <PostCard :post="post" />
        </div>
      </div>
      <PaginationBar
        :page="result.page"
        :total-pages="result.totalPages"
        :total-count="result.total"
        @change="emit('page-change', $event)"
      />
    </template>
  </div>
</template>

<style scoped>
.grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: var(--space-6);
}

/* 包一层：入场动画的 transform 只作用在包装层，不影响卡片自身的 hover 上浮 */
.grid-item {
  display: flex;
  min-width: 0;
}

.grid-item > :deep(*) {
  flex: 1;
  min-width: 0;
}

/* 滚动入场：自下而上 + 从小到大，逐张错峰 */
.reveal-item {
  opacity: 0;
  transform: translateY(36px) scale(0.94);
  transition:
    opacity 560ms ease,
    transform 560ms cubic-bezier(0.22, 1, 0.36, 1);
  transition-delay: var(--reveal-delay, 0ms);
  will-change: opacity, transform;
}

.reveal-item.is-revealed {
  opacity: 1;
  transform: none;
}

@media (max-width: 1199px) {
  .grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 767px) {
  .grid {
    grid-template-columns: 1fr;
  }
}

/* 空态：视觉来自 foundation `.empty`，这里只补纵向留白 */
.empty {
  padding: var(--space-24) 0;
}

.empty-icon {
  font-size: 42px;
  margin-bottom: var(--space-4);
}

@media (prefers-reduced-motion: reduce) {
  .reveal-item {
    opacity: 1;
    transform: none;
    transition: none;
  }
}
</style>
