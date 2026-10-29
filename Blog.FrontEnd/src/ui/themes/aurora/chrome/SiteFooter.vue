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
    <div class="container footer-inner">
      <div class="footer-brand">
        <div class="footer-logo">
          <!-- 页脚没有发光效果，用 glow=false 保持原样 -->
          <SiteLogo :size="28" :glow="false" />
          <span class="site-name">{{ site.config?.siteName || DEFAULT_SITE_NAME }}</span>
        </div>
        <p class="copyright">© {{ year }} kky · 记录代码与生活</p>
      </div>

      <div v-if="statsItems.length" class="footer-stats">
        <div v-for="item in statsItems" :key="item.label" class="stat-item">
          <span class="stat-value gradient-text">{{ item.value }}</span>
          <span class="stat-label">{{ item.label }}</span>
        </div>
      </div>
    </div>

    <!-- 备案信息：ICP 备案号 + 公安联网备案（审核通过前不渲染） -->
    <div class="container footer-filing">
      <SiteFiling />
    </div>
  </footer>
</template>

<style scoped>
.site-footer {
  margin-top: auto;
  background: var(--bg-surface);
  border-top: 1px solid var(--border-subtle);
}

.footer-inner {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-6);
  padding-top: var(--space-8);
  padding-bottom: var(--space-8);
  flex-wrap: wrap;
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
  font-size: 20px;
  font-weight: 700;
}

.stat-label {
  font: var(--text-caption);
  color: var(--text-subtle);
}

/* 备案信息居中放在页脚最底部，字号/颜色统一由 SiteFiling 组件控制 */
.footer-filing {
  display: flex;
  justify-content: center;
  padding-bottom: var(--space-6);
}
</style>
