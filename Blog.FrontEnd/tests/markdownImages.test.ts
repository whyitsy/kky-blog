import { test } from 'node:test'
import assert from 'node:assert/strict'
import {
  applyMarkdownImageRewrites,
  classifyImageUrl,
  collectMarkdownImages,
  normalizeImageUrl,
} from '../src/utils/markdownImages.ts'

// ---------------------------------------------------------------- URL 归一化

test('归一化：去掉语雀的锚点参数、转义空格、修复 [object Object]', () => {
  assert.equal(
    normalizeImageUrl('https://cdn.nlark.com/yuque/a.png#clientId=abc&from=ui'),
    'https://cdn.nlark.com/yuque/a.png',
  )
  assert.equal(
    normalizeImageUrl('https://cdn.nlark.com/yuque/a b.png'),
    'https://cdn.nlark.com/yuque/a%20b.png',
  )
  assert.equal(
    normalizeImageUrl('https://cdn.nlark.com/yuque/a[object Object].png'),
    'https://cdn.nlark.com/yuque/a[object%20Object].png',
  )
  // 尖括号只是 Markdown 的写法，不属于 URL
  assert.equal(normalizeImageUrl('<https://cdn.nlark.com/yuque/a.png>'), 'https://cdn.nlark.com/yuque/a.png')
})

test('分类：在线 / 本地相对路径 / 忽略', () => {
  assert.equal(classifyImageUrl('https://cdn.nlark.com/yuque/a.png'), 'remote')
  assert.equal(classifyImageUrl('https://example.com/a.png'), 'remote')
  assert.equal(classifyImageUrl('http://example.com/a.png'), 'remote')
  assert.equal(classifyImageUrl('//cdn.example.com/a.png'), 'remote')
  assert.equal(classifyImageUrl('images/a.png'), 'local')
  assert.equal(classifyImageUrl('./images/a.png'), 'local')

  for (const ignored of [
    'data:image/png;base64,xx',
    'blob:https://www.kkynet.site/xxx',
    '#anchor',
    '/images/a.png',
    '',
  ]) {
    assert.equal(classifyImageUrl(ignored), 'ignored', `应忽略：${ignored}`)
  }
})

// ---------------------------------------------------------------- 收集

test('收集 Markdown 与 HTML 图片，按归一化后的 URL 去重且保持首次出现顺序', () => {
  const markdown = [
    '![图一](https://cdn.nlark.com/yuque/a.png#clientId=abc)',
    '<img src="https://cdn.nlark.com/yuque/b.png" width="10">',
    '![](./images/c.webp)',
    '![](https://cdn.nlark.com/yuque/a.png)',
  ].join('\n\n')

  const images = collectMarkdownImages(markdown)

  assert.deepEqual(
    images.map((image) => image.url),
    ['https://cdn.nlark.com/yuque/a.png', 'https://cdn.nlark.com/yuque/b.png', './images/c.webp'],
  )
  assert.deepEqual(
    images.map((image) => image.kind),
    ['remote', 'remote', 'local'],
  )
})

test('收集：围栏代码块与行内代码里的图片不参与导入', () => {
  const markdown = [
    '```md',
    '![](https://cdn.nlark.com/yuque/in-fence.png)',
    '```',
    '',
    '`![](https://cdn.nlark.com/yuque/inline-code.png)`',
    '',
    '![](https://cdn.nlark.com/yuque/real.png)',
  ].join('\n')

  const images = collectMarkdownImages(markdown)

  assert.equal(images.length, 1)
  assert.equal(images[0].url, 'https://cdn.nlark.com/yuque/real.png')
})

test('收集：支持尖括号包裹的 URL 与 title', () => {
  const markdown = '![图](<https://cdn.nlark.com/yuque/a b.png> "标题")'

  const images = collectMarkdownImages(markdown)

  assert.equal(images.length, 1)
  assert.equal(images[0].url, 'https://cdn.nlark.com/yuque/a%20b.png')
  assert.equal(images[0].kind, 'remote')
})

test('收集：站外图片与语雀图片同样归为在线图片，不做域名区分', () => {
  const images = collectMarkdownImages('![](https://example.com/a.png)')

  assert.equal(images.length, 1)
  assert.equal(images[0].kind, 'remote')
})

// ---------------------------------------------------------------- 改写

test('改写：同时处理 Markdown 与 HTML，保留 alt / title / 引号风格', () => {
  const markdown = [
    '![图一](https://cdn.nlark.com/yuque/a.png#clientId=abc "标题")',
    "<img alt='b' src='https://cdn.nlark.com/yuque/b.png' width='10'>",
  ].join('\n')

  const rewritten = applyMarkdownImageRewrites(
    markdown,
    new Map([
      ['https://cdn.nlark.com/yuque/a.png', '/api/files/2026/11/a.webp'],
      ['https://cdn.nlark.com/yuque/b.png', '/api/files/2026/11/b.webp'],
    ]),
  )

  assert.equal(
    rewritten,
    [
      '![图一](/api/files/2026/11/a.webp "标题")',
      "<img alt='b' src='/api/files/2026/11/b.webp' width='10'>",
    ].join('\n'),
  )
})

test('改写：代码块与行内代码原样保留', () => {
  const markdown = [
    '```md',
    '![](https://cdn.nlark.com/yuque/a.png)',
    '```',
    '',
    '`![](https://cdn.nlark.com/yuque/a.png)`',
    '',
    '![](https://cdn.nlark.com/yuque/a.png)',
  ].join('\n')

  const rewritten = applyMarkdownImageRewrites(
    markdown,
    new Map([['https://cdn.nlark.com/yuque/a.png', '/api/files/2026/11/a.webp']]),
  )

  assert.equal(
    rewritten,
    [
      '```md',
      '![](https://cdn.nlark.com/yuque/a.png)',
      '```',
      '',
      '`![](https://cdn.nlark.com/yuque/a.png)`',
      '',
      '![](/api/files/2026/11/a.webp)',
    ].join('\n'),
  )
})

test('改写：没有命中的链接不动，重复链接一次全部替换', () => {
  const markdown = [
    '![](https://cdn.nlark.com/yuque/a.png)',
    '![](https://cdn.nlark.com/yuque/unknown.png)',
    '![](https://cdn.nlark.com/yuque/a.png#fragment)',
  ].join('\n')

  const rewritten = applyMarkdownImageRewrites(
    markdown,
    new Map([['https://cdn.nlark.com/yuque/a.png', '/api/files/2026/11/a.webp']]),
  )

  assert.equal(
    rewritten,
    [
      '![](/api/files/2026/11/a.webp)',
      '![](https://cdn.nlark.com/yuque/unknown.png)',
      '![](/api/files/2026/11/a.webp)',
    ].join('\n'),
  )
})
