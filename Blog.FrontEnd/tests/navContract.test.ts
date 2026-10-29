import { test } from 'node:test'
import assert from 'node:assert/strict'
import { PUBLIC_NAV_ITEMS, isNavItemActive } from '../src/ui/contracts/nav.ts'

/**
 * 回归背景：v0.3.0 的主题重构给「管理」入口加了 `adminOnly` 标记，
 * 两个主题在渲染时按 `auth.isAdmin` 过滤，结果未登录/会话过期的站长
 * 在导航栏里看不到后台入口（v0.2.0 是一直显示的）。
 *
 * 结论：管理入口必须对所有访客展示，点击后的跳转交给路由守卫
 * （未登录 → /login?returnUrl=/admin，作者 → /me，管理员 → /admin）。
 */
test('公开导航始终包含管理入口，且不带角色过滤标记', () => {
  const admin = PUBLIC_NAV_ITEMS.find((item) => item.key === 'admin')

  assert.ok(admin, '管理入口不能从 PUBLIC_NAV_ITEMS 中移除')
  assert.equal(admin.to, '/admin')
  assert.equal(admin.prefix, true, '/admin/* 子路由需要前缀匹配高亮')
  assert.equal('adminOnly' in admin, false, '管理入口不应再带 adminOnly 标记')
})

test('导航 key 唯一，管理入口在 /admin 子路由下保持高亮', () => {
  const keys = PUBLIC_NAV_ITEMS.map((item) => item.key)
  assert.equal(new Set(keys).size, keys.length, '导航 key 不能重复')

  const admin = PUBLIC_NAV_ITEMS.find((item) => item.key === 'admin')!
  assert.equal(isNavItemActive(admin, '/admin'), true)
  assert.equal(isNavItemActive(admin, '/admin/posts'), true)
  assert.equal(isNavItemActive(admin, '/admin/site'), true)
  assert.equal(isNavItemActive(admin, '/'), false)
})
