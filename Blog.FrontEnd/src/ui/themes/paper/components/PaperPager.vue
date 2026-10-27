<script setup lang="ts">
import { computed } from 'vue'

/**
 * 墨纸分页：没有药丸、没有阴影，只有上下细线与衬线数字。
 * 供 HomePage / PostListPage 复用（调用方把 `change` 接到 model.goPage）。
 */
const props = defineProps<{
  page: number
  totalPages: number
}>()

const emit = defineEmits<{
  change: [page: number]
}>()

type PageCell = number | 'gap'

/** 页码窗口：首尾页永远显示，当前页左右各留一页，其余折叠成省略号 */
const cells = computed<PageCell[]>(() => {
  const total = Math.max(1, props.totalPages)
  if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1)

  const current = Math.min(Math.max(1, props.page), total)
  const wanted = new Set<number>([1, total, current - 1, current, current + 1])
  if (current <= 3) [2, 3, 4].forEach((n) => wanted.add(n))
  if (current >= total - 2) [total - 3, total - 2, total - 1].forEach((n) => wanted.add(n))

  const sorted = [...wanted].filter((n) => n >= 1 && n <= total).sort((a, b) => a - b)
  const result: PageCell[] = []
  for (let i = 0; i < sorted.length; i += 1) {
    const value = sorted[i]!
    const previous = sorted[i - 1]
    if (previous !== undefined && value - previous > 1) result.push('gap')
    result.push(value)
  }
  return result
})

function go(target: number): void {
  if (target < 1 || target > props.totalPages || target === props.page) return
  emit('change', target)
}

/** 模板里把 'gap' 与数字分开处理，避免在模板中做类型收窄 */
function goCell(cell: PageCell): void {
  if (cell === 'gap') return
  go(cell)
}
</script>

<template>
  <nav v-if="totalPages > 1" class="pp-pager" aria-label="分页">
    <button type="button" class="pp-pager-word" :disabled="page <= 1" @click="go(page - 1)">
      ← 上一页
    </button>

    <div class="pp-pager-nums">
      <template v-for="(cell, index) in cells" :key="`${cell}-${index}`">
        <span v-if="cell === 'gap'" class="pp-pager-gap">…</span>
        <button
          v-else
          type="button"
          class="pp-pager-num"
          :class="{ 'is-current': cell === page }"
          :aria-current="cell === page ? 'page' : undefined"
          @click="goCell(cell)"
        >
          {{ cell }}
        </button>
      </template>
    </div>

    <span class="pp-pager-count">{{ page }} / {{ totalPages }}</span>

    <button type="button" class="pp-pager-word" :disabled="page >= totalPages" @click="go(page + 1)">
      下一页 →
    </button>
  </nav>
</template>

<style scoped>
.pp-pager {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-4);
  margin-top: var(--space-10);
  padding-top: var(--space-4);
  border-top: 1px solid var(--border-default);
}

.pp-pager-word {
  padding: 4px 0;
  font: var(--text-caption);
  letter-spacing: 0.16em;
  color: var(--text-muted);
  background: none;
  border: 0;
  transition: color var(--transition-fast);
}

.pp-pager-word:hover:not(:disabled) {
  color: var(--brand-500);
}

.pp-pager-word:disabled {
  color: var(--text-disabled);
  cursor: not-allowed;
}

.pp-pager-nums {
  display: flex;
  align-items: baseline;
  gap: var(--space-1);
}

.pp-pager-num {
  min-width: 30px;
  padding: 3px 0;
  font: 400 15px / 1.2 var(--font-serif);
  color: var(--text-subtle);
  text-align: center;
  background: none;
  border: 0;
  border-bottom: 2px solid transparent;
  transition:
    color var(--transition-fast),
    border-color var(--transition-fast);
}

.pp-pager-num:hover {
  color: var(--text-strong);
}

.pp-pager-num.is-current {
  color: var(--brand-500);
  border-bottom-color: var(--brand-500);
}

.pp-pager-gap {
  padding: 0 2px;
  font: 400 15px / 1.2 var(--font-serif);
  color: var(--text-disabled);
}

/* 窄屏只保留「上一页 / 下一页」与页码计数，数字窗口隐藏 */
.pp-pager-count {
  display: none;
  font: var(--text-caption);
  letter-spacing: 0.14em;
  color: var(--text-subtle);
}

@media (max-width: 639px) {
  .pp-pager-nums {
    display: none;
  }

  .pp-pager-count {
    display: inline;
  }
}
</style>
