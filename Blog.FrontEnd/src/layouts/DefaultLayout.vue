<script setup lang="ts">
import { onMounted } from 'vue'
import { RouterView } from 'vue-router'
import { useThemeStore, SearchModal, ThemeSwitcher } from '@/ui'
import { useSiteStore } from '@/stores/site'

/**
 * 公开站外壳。
 *
 * 具体导航/页脚/整体布局由**当前主题的 shell** 决定（换主题 = 换布局）；
 * 这里只做三件与主题无关的事：
 *   1. 把路由页面放进主题 shell 的默认插槽；
 *   2. 加载站点配置（导航/页脚/Hero 共用）；
 *   3. 挂全局浮层：搜索弹窗 + 主题切换器。
 */
const site = useSiteStore()
const theme = useThemeStore()

onMounted(() => site.ensureLoaded())
</script>

<template>
  <component :is="theme.shell">
    <RouterView v-slot="{ Component }">
      <Transition name="fade" mode="out-in">
        <component :is="Component" />
      </Transition>
    </RouterView>
  </component>
  <SearchModal />
  <ThemeSwitcher />
</template>
