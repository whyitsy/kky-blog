<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { useSearchStore } from '@/stores/app'
import { useSiteStore } from '@/stores/site'
import { DEFAULT_SITE_NAME } from '@/utils/site'
import SiteLogo from '@/ui/components/SiteLogo.vue'
import {
  PUBLIC_NAV_ITEMS,
  isNavItemActive,
  type PublicNavItem,
} from '@/ui/contracts/nav'

/**
 * Aurora 顶部导航：固定 + 玻璃拟态。
 *
 * - 导航入口来自 `@/ui/contracts/nav` 的 `PUBLIC_NAV_ITEMS`（主题只决定「怎么排」）；
 *   「管理」入口对所有人展示，未登录点击后再由路由守卫带到登录页；
 * - 滚动超过 40px 才加 `.glass`，首页 Hero 上保持透明；
 * - 主题/配色切换由 DefaultLayout 挂载的全局 ThemeSwitcher 负责，这里只留搜索按钮。
 */
const route = useRoute()
const search = useSearchStore()
const site = useSiteStore()

const navItems = PUBLIC_NAV_ITEMS

const siteName = computed(() => site.config?.siteName || DEFAULT_SITE_NAME)

// 滚动超过阈值后导航栏加毛玻璃背景（首页 Hero 上保持透明）
const scrolled = ref(false)
function onScroll() {
  scrolled.value = window.scrollY > 40
}
onMounted(() => window.addEventListener('scroll', onScroll, { passive: true }))
onBeforeUnmount(() => window.removeEventListener('scroll', onScroll))

function isActive(item: PublicNavItem): boolean {
  return isNavItemActive(item, route.path, typeof route.name === 'string' ? route.name : undefined)
}
</script>

<template>
  <header class="navbar" :class="{ glass: scrolled }">
    <div class="navbar-inner container">
      <RouterLink to="/" class="logo">
        <SiteLogo :size="32" />
        <span class="logo-name">{{ siteName }}</span>
      </RouterLink>

      <nav class="nav-links">
        <RouterLink
          v-for="item in navItems"
          :key="item.key"
          :to="item.to"
          class="nav-link"
          :class="{ active: isActive(item) }"
        >
          {{ item.label }}
        </RouterLink>
      </nav>

      <div class="nav-actions">
        <button class="action-btn search-btn" aria-label="搜索" @click="search.show()">
          <svg
            viewBox="0 0 24 24"
            width="18"
            height="18"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
          >
            <circle cx="11" cy="11" r="7" />
            <path d="m20 20-3.5-3.5" />
          </svg>
          <span class="search-text">搜索文章…</span>
        </button>
      </div>
    </div>
  </header>
</template>

<style scoped>
.navbar {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  z-index: var(--z-nav);
  transition:
    background var(--transition-normal),
    box-shadow var(--transition-normal);
}

/* 未滚动时导航压在 Hero 照片上：铺一层与主题底色同源的渐隐底色。
   否则亮色模式（或浅色封面图）下，深色文字会淹没在照片里；
   滚动后交给全局 .glass 的毛玻璃实底接管。
   透明度刻意压得比较低：只在文字周围托底，不要变成第二层整屏遮罩。 */
.navbar:not(.glass) {
  background: linear-gradient(
    180deg,
    color-mix(in srgb, var(--bg-canvas) 72%, transparent) 0%,
    color-mix(in srgb, var(--bg-canvas) 40%, transparent) 60%,
    transparent 100%
  );
}

.navbar-inner {
  display: flex;
  align-items: center;
  gap: var(--space-8);
  height: var(--nav-height);
}

.logo {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  font-weight: 700;
  font-size: 17px;
  color: var(--text-strong);
}

.nav-links {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  flex: 1;
}

.nav-link {
  position: relative;
  padding: var(--space-2) var(--space-3);
  font-size: 14px;
  font-weight: 500;
  color: var(--text-muted);
  border-radius: var(--radius-sm);
  transition: color var(--transition-fast);
}

.nav-link:hover {
  color: var(--text-strong);
}

.nav-link.active {
  color: var(--brand-500);
}

.nav-link.active::after {
  content: '';
  position: absolute;
  left: var(--space-3);
  right: var(--space-3);
  bottom: 2px;
  height: 2px;
  border-radius: 1px;
  background: linear-gradient(
    90deg,
    var(--gradient-start),
    var(--gradient-mid),
    var(--gradient-end)
  );
}

.nav-actions {
  display: flex;
  align-items: center;
  gap: var(--space-2);
}

.action-btn {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  height: 36px;
  padding: 0 var(--space-3);
  border-radius: var(--radius-2xl);
  border: 1px solid var(--border-default);
  color: var(--text-muted);
  font-size: 13px;
  transition:
    color var(--transition-fast),
    border-color var(--transition-fast),
    background var(--transition-fast);
}

.action-btn:hover {
  color: var(--text-strong);
  border-color: var(--border-strong);
  background: var(--bg-raised);
}

.search-text {
  color: var(--text-subtle);
}

@media (max-width: 640px) {
  .search-text {
    display: none;
  }
  .navbar-inner {
    gap: var(--space-3);
  }
  /* 窄屏：站名让位给导航；导航横向滚动而不是挤成竖排文字 */
  .logo-name {
    display: none;
  }
  .nav-links {
    overflow-x: auto;
    scrollbar-width: none;
    -ms-overflow-style: none;
  }
  .nav-links::-webkit-scrollbar {
    display: none;
  }
  .nav-link {
    flex-shrink: 0;
  }
}
</style>
