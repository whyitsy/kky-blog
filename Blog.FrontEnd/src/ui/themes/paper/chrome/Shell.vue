<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import SiteLogo from '@/ui/components/SiteLogo.vue'
import SiteFiling from '@/ui/components/SiteFiling.vue'
import {
  PUBLIC_NAV_ITEMS,
  isNavItemActive,
  type PublicNavItem,
} from '@/ui/contracts/nav'
import { useSearchStore } from '@/stores/app'
import { useSiteStore } from '@/stores/site'
import { useAuthStore } from '@/stores/auth'
import { pad2 } from '../format'

/**
 * Paper · 墨纸主题外壳。
 *
 * 与 aurora 的「顶部固定导航 + 全屏 Hero」相对：
 * 这里是**左侧固定侧栏 + 右侧单栏内容区**——导航成为一本书的目录，
 * 站点信息、社交链接与统计全部收进侧栏页脚，正文区只留给文章。
 * 窄屏（<900px）侧栏折叠成顶部横条：logo + 横向滚动导航 + 搜索。
 */
const route = useRoute()
const site = useSiteStore()
const search = useSearchStore()
const auth = useAuthStore()

const year = new Date().getFullYear()
// 与 aurora 一致：adminOnly 入口只对管理员展示（匿名不显示「管理」）
const navItems = computed(() =>
  PUBLIC_NAV_ITEMS.filter((item) => !item.adminOnly || auth.isAdmin),
)

/** 栏目对应的西文小标，纯装饰（编辑部的栏目索引感） */
const NAV_KICKERS: Record<string, string> = {
  home: 'Front Page',
  tags: 'Tags',
  categories: 'Categories',
  collections: 'Columns',
  archive: 'Archive',
  admin: 'Desk',
}

const siteName = computed(() => site.config?.siteName || '墨纸')
const visibleLinks = computed(() => site.socialLinks.filter((link) => link.isVisible))

const statsItems = computed(() => {
  const stats = site.stats
  if (!stats) return []
  return [
    { label: '存稿', value: `${stats.totalPosts} 篇` },
    { label: '标签', value: `${stats.tagCount} 个` },
    { label: '运行', value: `${stats.siteDays} 天` },
  ]
})

function isActive(item: PublicNavItem): boolean {
  return isNavItemActive(item, route.path, typeof route.name === 'string' ? route.name : undefined)
}

function kicker(key: string): string {
  return NAV_KICKERS[key] ?? key.toUpperCase()
}
</script>

<template>
  <div class="pp-shell">
    <aside class="pp-sidebar">
      <div class="pp-sidebar-inner">
        <RouterLink to="/" class="pp-brand">
          <SiteLogo :size="36" :glow="false" />
          <span class="pp-brand-text">
            <span class="pp-brand-name">{{ siteName }}</span>
            <span class="pp-brand-kicker">A Paper Journal</span>
          </span>
        </RouterLink>

        <div class="pp-nav-wrap">
          <p class="pp-sidebar-label">栏目 / Sections</p>
          <nav class="pp-nav" aria-label="主导航">
            <RouterLink
              v-for="(item, index) in navItems"
              :key="item.key"
              :to="item.to"
              class="pp-nav-link"
              :class="{ 'is-active': isActive(item) }"
            >
              <span class="pp-nav-index">{{ pad2(index + 1) }}</span>
              <span class="pp-nav-label">{{ item.label }}</span>
              <span class="pp-nav-kicker">{{ kicker(item.key) }}</span>
            </RouterLink>
          </nav>
        </div>

        <div class="pp-search-wrap">
          <button type="button" class="pp-search" aria-label="搜索文章" @click="search.show()">
            <svg
              viewBox="0 0 24 24"
              width="15"
              height="15"
              fill="none"
              stroke="currentColor"
              stroke-width="1.8"
              stroke-linecap="round"
              aria-hidden="true"
            >
              <circle cx="11" cy="11" r="7" />
              <path d="m20 20-3.5-3.5" />
            </svg>
            <span class="pp-search-text">搜索文章</span>
            <kbd class="pp-kbd">Ctrl K</kbd>
          </button>
        </div>

        <div class="pp-sidebar-foot">
          <nav v-if="visibleLinks.length" class="pp-socials" aria-label="社交链接">
            <a
              v-for="link in visibleLinks"
              :key="link.id"
              :href="link.url"
              target="_blank"
              rel="noopener noreferrer"
              class="pp-social"
            >
              <span>{{ link.name }}</span>
              <span aria-hidden="true">↗</span>
            </a>
          </nav>

          <dl v-if="statsItems.length" class="pp-stats">
            <div v-for="item in statsItems" :key="item.label" class="pp-stat">
              <dt>{{ item.label }}</dt>
              <dd>{{ item.value }}</dd>
            </div>
          </dl>

          <p class="pp-copyright">© {{ year }} {{ siteName }}</p>
          <p class="pp-colophon">墨纸 · Paper Edition</p>
          <SiteFiling class="pp-filing" />
        </div>
      </div>
    </aside>

    <div class="pp-content">
      <main class="pp-main">
        <slot />
      </main>
      <!-- 窄屏侧栏页脚会隐藏，备案信息单独放在内容区底部 -->
      <div class="pp-filing-mobile">
        <SiteFiling />
      </div>
    </div>
  </div>
</template>

<style scoped>
.pp-shell {
  min-height: 100vh;
}

/* ---------------------------------------------------------------- 侧栏 */
.pp-sidebar {
  position: fixed;
  top: 0;
  right: auto;
  bottom: 0;
  left: 0;
  z-index: 10;
  width: var(--pp-sidebar-width);
  display: flex;
  flex-direction: column;
  overflow-y: auto;
  background: var(--bg-surface);
  border-right: 1px solid var(--border-default);
}

.pp-sidebar-inner {
  display: flex;
  flex-direction: column;
  gap: var(--space-8);
  min-height: 100%;
  padding: var(--space-10) var(--space-6) var(--space-8);
}

/* 品牌区：双线收口，像刊头 */
.pp-brand {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  padding-bottom: var(--space-6);
  border-bottom: var(--pp-rule-double);
}

.pp-brand-text {
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
}

.pp-brand-name {
  overflow: hidden;
  font: 700 20px / 1.3 var(--font-serif);
  letter-spacing: 0.04em;
  color: var(--text-strong);
  text-overflow: ellipsis;
  white-space: nowrap;
}

.pp-brand-kicker {
  font: 500 9px / 1.4 var(--font-sans);
  letter-spacing: 0.22em;
  text-transform: uppercase;
  color: var(--text-subtle);
}

.pp-sidebar-label {
  margin-bottom: var(--space-3);
  font: var(--text-caption);
  letter-spacing: 0.22em;
  text-transform: uppercase;
  color: var(--text-subtle);
}

/* 目录式导航：序号 + 栏目名 + 西文小标 */
.pp-nav {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.pp-nav-link {
  display: grid;
  grid-template-columns: 28px minmax(0, 1fr) auto;
  align-items: baseline;
  gap: var(--space-2);
  padding: 8px 0 8px 12px;
  border-left: 2px solid transparent;
  color: var(--text-muted);
  transition:
    color var(--transition-fast),
    border-color var(--transition-fast);
}

.pp-nav-link:hover {
  color: var(--text-strong);
  border-left-color: var(--border-strong);
}

.pp-nav-link.is-active {
  color: var(--text-strong);
  border-left-color: var(--brand-500);
}

.pp-nav-index {
  font: 500 10px / 1.4 var(--font-sans);
  letter-spacing: 0.12em;
  color: var(--border-strong);
}

.pp-nav-link.is-active .pp-nav-index {
  color: var(--brand-500);
}

.pp-nav-label {
  font: 400 17px / 1.5 var(--font-serif);
  letter-spacing: 0.06em;
}

.pp-nav-kicker {
  font: 500 9px / 1.4 var(--font-sans);
  letter-spacing: 0.14em;
  text-transform: uppercase;
  color: var(--text-subtle);
  opacity: 0.72;
}

/* 搜索入口：方框细线，不用药丸 */
.pp-search-wrap {
  margin-top: auto;
}

.pp-search {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  width: 100%;
  padding: 9px 12px;
  color: var(--text-muted);
  background: transparent;
  border: var(--pp-rule);
  transition:
    color var(--transition-fast),
    border-color var(--transition-fast);
}

.pp-search:hover {
  color: var(--text-strong);
  border-color: var(--border-strong);
}

.pp-search-text {
  font: var(--text-caption);
  letter-spacing: 0.12em;
}

.pp-kbd {
  margin-left: auto;
  padding: 0 4px;
  font: 500 10px / 1.5 var(--font-mono);
  color: var(--text-subtle);
  border: 1px solid var(--border-subtle);
}

/* 侧栏页脚：社交 / 统计 / 版权 */
.pp-sidebar-foot {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  padding-top: var(--space-6);
  border-top: var(--pp-rule);
}

.pp-socials {
  display: flex;
  flex-direction: column;
}

.pp-social {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: var(--space-2);
  padding: 4px 0;
  font: var(--text-caption);
  letter-spacing: 0.14em;
  color: var(--text-muted);
  border-bottom: 1px dotted var(--border-subtle);
}

.pp-social:hover {
  color: var(--brand-500);
}

.pp-stats {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.pp-stat {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: var(--space-2);
  padding: 3px 0;
}

.pp-stat dt {
  font: var(--text-caption);
  letter-spacing: 0.14em;
  color: var(--text-subtle);
}

.pp-stat dd {
  font: 600 13px / 1.4 var(--font-sans);
  color: var(--text-default);
}

.pp-copyright {
  font: var(--text-caption);
  letter-spacing: 0.08em;
  color: var(--text-subtle);
}

.pp-colophon {
  font: 500 9px / 1.4 var(--font-sans);
  letter-spacing: 0.24em;
  text-transform: uppercase;
  color: var(--border-strong);
}

/* 备案信息：侧栏较窄，左对齐并缩小一档字号 */
.pp-filing {
  justify-content: flex-start;
  font-size: 11px;
}

/* ---------------------------------------------------------------- 内容区 */
.pp-content {
  min-height: 100vh;
  margin-left: var(--pp-sidebar-width);
}

.pp-main {
  width: 100%;
  max-width: var(--pp-content-max);
  margin: 0 auto;
  padding: var(--space-12) var(--space-12) var(--space-24);
}

.pp-filing-mobile {
  display: none;
}

/* ---------------------------------------------------------------- 窄屏：顶部横条 */
@media (max-width: 899px) {
  .pp-sidebar {
    position: sticky;
    top: 0;
    width: 100%;
    height: auto;
    overflow: visible;
    border-right: 0;
    border-bottom: 1px solid var(--border-default);
  }

  .pp-sidebar-inner {
    flex-direction: row;
    align-items: center;
    gap: var(--space-4);
    min-height: 0;
    padding: var(--space-3) var(--space-4);
  }

  .pp-brand {
    flex: 0 0 auto;
    padding-bottom: 0;
    border-bottom: 0;
  }

  .pp-brand-kicker {
    display: none;
  }

  .pp-brand-name {
    font-size: 17px;
  }

  .pp-nav-wrap {
    flex: 1 1 auto;
    min-width: 0;
  }

  .pp-sidebar-label,
  .pp-nav-index,
  .pp-nav-kicker,
  .pp-sidebar-foot {
    display: none;
  }

  .pp-nav {
    flex-direction: row;
    gap: var(--space-5);
    padding: 2px 0;
    overflow-x: auto;
    scrollbar-width: none;
  }

  .pp-nav::-webkit-scrollbar {
    display: none;
  }

  .pp-nav-link {
    display: block;
    padding: 4px 0;
    border-bottom: 2px solid transparent;
  }

  .pp-nav-link,
  .pp-nav-link:hover {
    border-left-color: transparent;
  }

  .pp-nav-link.is-active {
    border-bottom-color: var(--brand-500);
  }

  .pp-nav-label {
    font-size: 15px;
    white-space: nowrap;
  }

  .pp-search-wrap {
    flex: 0 0 auto;
    margin-top: 0;
  }

  .pp-search {
    justify-content: center;
    width: 34px;
    height: 32px;
    padding: 0;
    border: 0;
  }

  .pp-search-text,
  .pp-kbd {
    display: none;
  }

  .pp-content {
    margin-left: 0;
  }

  .pp-main {
    padding: var(--space-8) var(--space-4) var(--space-16);
  }

  /* 侧栏页脚已隐藏，备案信息改在内容区底部展示 */
  .pp-filing-mobile {
    display: flex;
    justify-content: center;
    padding: 0 var(--space-4) var(--space-10);
  }
}

@media (max-width: 479px) {
  .pp-brand-text {
    display: none;
  }
}
</style>
