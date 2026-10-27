import type { Component } from 'vue'
import type { PageId } from '../contracts/pages'

export interface PublicNavItem {
  key: string
  label: string
  to: string
  /** 有子路由的入口（/admin/*）用前缀匹配高亮 */
  prefix?: boolean
  /** 仅管理员可见 */
  adminOnly?: boolean
}

/**
 * 公开站导航项 —— 由 UI 层给出「有哪些入口」，主题决定「怎么排」。
 * 主题可以增删或改写顺序，但入口语义以这里为准，避免每个主题各抄一份。
 */
export const PUBLIC_NAV_ITEMS: PublicNavItem[] = [
  { key: 'home', label: '首页', to: '/' },
  { key: 'tags', label: '标签', to: '/tags' },
  { key: 'categories', label: '分类', to: '/categories' },
  { key: 'collections', label: '专栏', to: '/collections' },
  { key: 'archive', label: '归档', to: '/archive' },
  { key: 'admin', label: '管理', to: '/admin', prefix: true, adminOnly: true },
]

/** 是否为当前路由（主题 shell 用来算高亮） */
export function isNavItemActive(item: PublicNavItem, path: string, routeName?: string): boolean {
  if (item.prefix) return path === item.to || path.startsWith(`${item.to}/`)
  if (item.to === '/') return path === '/'
  return path === item.to || path.startsWith(`${item.to}/`) || routeName === item.key
}

/** 页面组件里常用的「路由页面列表」类型别名 */
export type PageComponent = Component
export type PageIdList = PageId
