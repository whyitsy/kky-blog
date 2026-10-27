<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { useThemeStore } from '@/ui/runtime/store'

const container = ref<HTMLElement>()
const theme = useThemeStore()

/** giscus 评论（GitHub Discussions 驱动），按当前主题配色适配亮/暗 */
function loadGiscus() {
  if (!container.value) return
  container.value.innerHTML = ''

  const script = document.createElement('script')
  script.src = 'https://giscus.app/client.js'
  script.async = true
  script.crossOrigin = 'anonymous'
  const attrs: Record<string, string> = {
    'data-repo': 'whyitsy/blog-comment',
    'data-repo-id': 'R_kgDOUODt5w',
    'data-category': 'Announcements',
    'data-category-id': 'DIC_kwDOUODt584DE2uV',
    'data-mapping': 'pathname',
    'data-strict': '0',
    'data-reactions-enabled': '1',
    'data-emit-metadata': '0',
    'data-input-position': 'top',
    'data-theme': theme.isLight ? 'light' : 'dark',
    'data-lang': 'zh-CN',
    'data-loading': 'lazy',
  }
  for (const [k, v] of Object.entries(attrs)) script.setAttribute(k, v)
  container.value.appendChild(script)
}

// 主题切换会改 <html data-color-mode>，此时重载 iframe 让评论配色跟随
const observer = new MutationObserver(() => loadGiscus())

onMounted(() => {
  loadGiscus()
  observer.observe(document.documentElement, {
    attributes: true,
    attributeFilter: ['data-color-mode'],
  })
})

onBeforeUnmount(() => observer.disconnect())
</script>

<template>
  <div ref="container" class="giscus-container" />
</template>

<style scoped>
.giscus-container {
  margin-top: var(--space-8);
  min-height: 120px;
}
</style>
