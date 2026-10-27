<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import NavBar from './NavBar.vue'
import SiteFooter from './SiteFooter.vue'

/**
 * Aurora 主题外壳：固定玻璃导航 + 内容插槽 + 页脚。
 *
 * 与主题无关的部分（搜索弹窗、主题切换器）由 `DefaultLayout` 挂在 shell 外面；
 * 这里只负责本主题的布局骨架。
 *
 * 布局约定：**给固定导航让位的偏移由 shell 负责**，页面组件不写 `padding-top`。
 * 首页 Hero 是通栏首屏，需要顶到导航下面（导航叠在 Hero 上），所以首页不加偏移。
 */
const route = useRoute()

/** 首页 Hero 通栏：main 不预留导航高度 */
const flushMain = computed(() => route.name === 'home')
</script>

<template>
  <div class="shell">
    <NavBar />

    <main class="shell-main" :class="{ 'shell-main--flush': flushMain }">
      <slot />
    </main>

    <SiteFooter />
  </div>
</template>

<style scoped>
.shell {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 100vh;
}

.shell-main {
  flex: 1;
  padding-top: var(--nav-height);
}

.shell-main--flush {
  padding-top: 0;
}
</style>
