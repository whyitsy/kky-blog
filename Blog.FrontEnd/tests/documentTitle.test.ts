import { test } from 'node:test'
import assert from 'node:assert/strict'
import { formatDocumentTitle } from '../src/utils/documentTitle.ts'

test('页面标题与站点名用「 - 」连接', () => {
  assert.equal(formatDocumentTitle('文章管理', 'kky\'s blog'), '文章管理 - kky\'s blog')
})

test('没有页面标题时只显示站点名（首页）', () => {
  assert.equal(formatDocumentTitle(undefined, 'kky\'s blog'), 'kky\'s blog')
  assert.equal(formatDocumentTitle('  ', 'kky\'s blog'), 'kky\'s blog')
})

test('站点名为空时只显示页面标题', () => {
  assert.equal(formatDocumentTitle('登录', ''), '登录')
  assert.equal(formatDocumentTitle('登录', '   '), '登录')
})

test('两边都为空时返回空串，不留下多余的分隔符', () => {
  assert.equal(formatDocumentTitle(undefined, ''), '')
  assert.equal(formatDocumentTitle('', '  '), '')
})
