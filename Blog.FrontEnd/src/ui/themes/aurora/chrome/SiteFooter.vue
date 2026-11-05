<script setup lang="ts">
import { computed } from 'vue'
import { useSiteStore } from '@/stores/site'
import { DEFAULT_SITE_NAME } from '@/utils/site'
import SiteLogo from '@/ui/components/SiteLogo.vue'
import SiteFiling from '@/ui/components/SiteFiling.vue'

/**
 * Aurora 页脚：品牌信息 + 渐变分隔线 + 站点统计 + 备案信息。
 * 渐变分隔线复用主题全局装饰 `.gradient-line`（theme.css），不再重复定义。
 */
const props = withDefaults(
  defineProps<{
    /** 与导航一致：首页内容用宽容器时，页脚也放宽到同一宽度 */
    wide?: boolean
  }>(),
  { wide: false },
)

const site = useSiteStore()

const year = new Date().getFullYear()

const statsItems = computed(() => {
  const s = site.stats
  if (!s) return []
  return [
    { label: '建站天数', value: `${s.siteDays} 天` },
    { label: '文章', value: `${s.totalPosts} 篇` },
    { label: '总字数', value: formatWords(s.totalWords) },
    { label: '总浏览', value: formatViews(s.totalViews) },
  ]
})

function formatWords(n: number) {
  return n >= 10000 ? `${(n / 10000).toFixed(1)}w` : `${n}`
}
function formatViews(n: number) {
  return n >= 1000 ? `${(n / 1000).toFixed(1)}k` : `${n}`
}
</script>

<template>
  <footer class="site-footer">
    <div class="gradient-line" />
    <div class="container footer-inner" :class="{ 'container-wide': props.wide }">
      <div class="footer-brand">
        <div class="footer-logo">
          <!-- 页脚没有发光效果，用 glow=false 保持原样 -->
          <SiteLogo :size="28" :glow="false" />
          <span class="site-name">{{ site.config?.siteName || DEFAULT_SITE_NAME }}</span>
        </div>
        <p class="copyright">© {{ year }} kky · 记录代码与生活</p>
      </div>

      <!-- 备案信息：ICP 备案号 + 公安联网备案（审核通过前不渲染），放在品牌与统计之间的中间列 -->
      <div class="footer-filing">
        <SiteFiling />
      </div>

      <div v-if="statsItems.length" class="footer-stats">
        <div v-for="item in statsItems" :key="item.label" class="stat-item">
          <span class="stat-value gradient-text">{{ item.value }}</span>
          <span class="stat-label">{{ item.label }}</span>
        </div>
      </div>
    </div>
  </footer>
</template>

<style scoped>
.site-footer {
  margin-top: auto;
  background: var(--bg-surface);
  border-top: 1px solid var(--border-subtle);
}

/* 页脚：品牌靠左、统计靠右，备案放在中间列（三栏栅格保证备案在容器正中）。
   原先备案在 footer-inner 下面单独一行，中间空出一大段；现在收进同一行中间。 */
.footer-inner {
  display: grid;
  grid-template-columns: 1fr auto 1fr;
  align-items: center;
  gap: var(--space-6);
  padding-top: var(--space-8);
  padding-bottom: var(--space-8);
}

.footer-brand {
  justify-self: start;
}

.footer-logo {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  margin-bottom: var(--space-2);
}

.site-name {
  font-weight: 700;
  color: var(--text-strong);
}

.copyright {
  font: var(--text-caption);
  color: var(--text-subtle);
}

.footer-stats {
  justify-self: end;
  display: flex;
  gap: var(--space-8);
  flex-wrap: wrap;
}

.stat-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 2px;
}

.stat-value {
  font-size: 22px;
  font-weight: 700;
}

.stat-label {
  font: var(--text-caption);
  color: var(--text-subtle);
}

/* 备案信息在中间列居中，字号/颜色统一由 SiteFiling 组件控制 */
.footer-filing {
  justify-self: center;
  display: flex;
  justify-content: center;
}

/* 三栏放不下时（平板）仍保持品牌靠左、统计靠右，备案居中另起一行 */
@media (max-width: 959px) {
  .footer-inner {
    grid-template-columns: minmax(0, 1fr) auto;
    grid-template-areas:
      'brand stats'
      'filing filing';
    row-gap: var(--space-5);
  }

  .footer-brand {
    grid-area: brand;
    justify-self: start;
  }

  .footer-stats {
    grid-area: stats;
    justify-self: end;
  }

  .footer-filing {
    grid-area: filing;
    justify-self: center;
  }
}

/* 手机：整体居中堆叠，统计项排成 2×2，避免 4 项换行成 3+1 显得歪斜 */
@media (max-width: 639px) {
  .footer-inner {
    grid-template-columns: minmax(0, 1fr);
    grid-template-areas:
      'brand'
      'stats'
      'filing';
    justify-items: center;
    text-align: center;
  }

  .footer-brand {
    justify-self: center;
  }

  .footer-logo {
    justify-content: center;
  }

  .footer-stats {
    display: grid;
    grid-template-columns: repeat(2, auto);
    gap: var(--space-4) var(--space-8);
    justify-self: center;
  }
}
</style>
