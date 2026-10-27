<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { getArchives } from '@/api/posts'
import type { ArchiveGroupDto } from '@/types'
import {
  useThemePage,
  type ArchiveMonthModel,
  type ArchivePageModel,
  type ArchiveYearModel,
} from '@/ui'

/**
 * 归档数据控制器：拉年月分组，再做「年 → 月 → 文章」的归一化。
 * 日期展示格式（MM-DD）也在这里生成，主题页面只排版。
 */
const page = useThemePage('archive')

const groups = ref<ArchiveGroupDto[]>([])
const loading = ref(true)

onMounted(async () => {
  try {
    groups.value = await getArchives()
  } catch (e) {
    // 加载失败按「空归档」呈现 —— 理由与写法同 TagsView（那里有完整说明）：
    // 只写 try/finally 会让 rejection 无人接收，冒出 Uncaught (in promise)。
    groups.value = []
    console.error('[archives] 加载归档失败：', e)
  } finally {
    loading.value = false
  }
})

const total = computed(() => groups.value.reduce((sum, g) => sum + g.items.length, 0))

function dayOf(iso: string) {
  const d = new Date(iso)
  return `${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

/** 按年份再分组（年月 → 年），年份倒序 */
const years = computed<ArchiveYearModel[]>(() => {
  const map = new Map<number, ArchiveMonthModel[]>()
  for (const group of groups.value) {
    const months = map.get(group.year) ?? []
    months.push({
      month: group.month,
      items: group.items.map((item) => ({
        id: item.id,
        title: item.title,
        dateLabel: dayOf(item.publishedAt),
        publishedAt: item.publishedAt,
      })),
    })
    map.set(group.year, months)
  }
  return [...map.entries()]
    .sort((a, b) => b[0] - a[0])
    .map(([year, months]) => ({ year, months }))
})

const model = computed<ArchivePageModel>(() => ({
  total: total.value,
  loading: loading.value,
  years: years.value,
}))
</script>

<template>
  <component :is="page" v-if="page" :model="model" />
</template>
