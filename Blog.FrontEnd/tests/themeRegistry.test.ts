import { test } from 'node:test'
import assert from 'node:assert/strict'
import {
  getTheme,
  listThemes,
  registerTheme,
  resolvePageComponent,
  resolveShell,
  resolveThemeChain,
} from '../src/ui/runtime/registry.ts'
import type { ThemePlugin } from '../src/ui/runtime/types.ts'

const shell = { name: 'FakeShell' }
const homePage = { name: 'FakeHome' }
const listPage = { name: 'FakeList' }
const notFoundPage = { name: 'FakeNotFound' }

function makeTheme(overrides: Partial<ThemePlugin> = {}): ThemePlugin {
  return {
    id: 'test-base',
    name: '测试主题',
    description: '仅供单测',
    modes: [{ id: 'dark', label: '暗色', scheme: 'dark' }],
    defaultMode: 'dark',
    preview: {
      background: '#000',
      surface: '#111',
      text: '#fff',
      primary: '#0ff',
      accent: '#f0f',
    },
    shell,
    pages: {},
    ...overrides,
  }
}

test('注册主题后可查询，默认主题排在最前', () => {
  registerTheme(makeTheme({ id: 'test-base', pages: { home: homePage } }))
  assert.equal(getTheme('test-base')?.name, '测试主题')
  assert.equal(getTheme('不存在'), undefined)
  assert.equal(listThemes()[0]?.id, 'test-base')
})

test('缺少 id / shell / modes / defaultMode 时拒绝注册', () => {
  assert.throws(() => registerTheme(makeTheme({ id: '' })), /缺少 id/)
  assert.throws(() => registerTheme(makeTheme({ id: 'x', shell: undefined as never })), /缺少 shell/)
  assert.throws(() => registerTheme(makeTheme({ id: 'x', modes: [] })), /未声明任何配色模式/)
  assert.throws(
    () => registerTheme(makeTheme({ id: 'x', defaultMode: '不存在' })),
    /不在 modes 里/,
  )
})

test('extends 链按「自身 → 父 → 祖父」回退页面', () => {
  registerTheme(
    makeTheme({ id: 'test-base', pages: { home: homePage, 'post-list': listPage } }),
  )
  registerTheme(
    makeTheme({
      id: 'test-child',
      extends: 'test-base',
      pages: { 'not-found': notFoundPage },
    }),
  )
  registerTheme(
    makeTheme({
      id: 'test-grandchild',
      extends: 'test-child',
      shell: { name: 'GrandShell' },
      pages: {},
    }),
  )

  const child = getTheme('test-child')!
  const grandchild = getTheme('test-grandchild')!

  assert.equal(resolvePageComponent(child, 'home'), homePage)
  assert.equal(resolvePageComponent(child, 'not-found'), notFoundPage)
  assert.equal(resolvePageComponent(grandchild, 'post-list'), listPage)
  assert.equal(resolvePageComponent(grandchild, 'home'), homePage)
  assert.equal(resolvePageComponent(grandchild, 'collection-detail'), null)

  assert.deepEqual(
    resolveThemeChain(grandchild).map((t) => t.id),
    ['test-grandchild', 'test-child', 'test-base'],
  )
  // shell 不参与继承
  assert.equal(resolveShell(grandchild), grandchild.shell)
})

test('extends 成环时不会无限递归', () => {
  registerTheme(makeTheme({ id: 'test-cycle-a', extends: 'test-cycle-b', pages: {} }))
  registerTheme(makeTheme({ id: 'test-cycle-b', extends: 'test-cycle-a', pages: {} }))
  const chain = resolveThemeChain(getTheme('test-cycle-a')!)
  assert.equal(chain.length, 2)
})

test('主题缺失的页面由继承链兜底，最终为 null', () => {
  registerTheme(makeTheme({ id: 'test-solo', pages: {} }))
  const solo = getTheme('test-solo')!
  assert.equal(resolvePageComponent(solo, 'home'), null)
})
