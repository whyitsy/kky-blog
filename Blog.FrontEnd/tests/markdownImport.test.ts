import { test } from 'node:test'
import assert from 'node:assert/strict'
import { buildImportPlan, parseMarkdownFile } from '../src/utils/markdownImport.ts'

// ---------------------------------------------------------------- 解析器

test('没有 front matter 时整份文件都是正文', () => {
  const result = parseMarkdownFile('# 标题\n\n正文内容')

  assert.deepEqual(result.data, {})
  assert.equal(result.content, '# 标题\n\n正文内容')
})

test('解析基础 front matter 并从正文中剥离', () => {
  const result = parseMarkdownFile(
    ['---', 'title: Hello', 'summary: 这是摘要', 'tags: [vue, ts]', '---', '', '正文第一段'].join('\n'),
  )

  assert.equal(result.data.title, 'Hello')
  assert.equal(result.data.summary, '这是摘要')
  assert.deepEqual(result.data.tags, ['vue', 'ts'])
  assert.equal(result.content, '正文第一段')
})

test('容忍 BOM、CRLF、引号、块状列表与注释', () => {
  const raw = [
    '\uFEFF---',
    '# 这是注释，应被忽略',
    'title: "带: 冒号的标题"',
    'description: \'单引号值\'',
    'tags:',
    '  - 中文',
    '  - "带, 逗号"',
    'draft: true',
    '---',
    '正文',
  ].join('\r\n')

  const result = parseMarkdownFile(raw)

  assert.equal(result.data.title, '带: 冒号的标题')
  assert.equal(result.data.description, '单引号值')
  assert.deepEqual(result.data.tags, ['中文', '带, 逗号'])
  assert.equal(result.data.draft, true)
  assert.equal(result.content, '正文')
})

test('front matter 没有闭合时整份文件当正文，不抛异常', () => {
  const raw = '---\ntitle: 未闭合\n\n正文'

  const result = parseMarkdownFile(raw)

  assert.deepEqual(result.data, {})
  assert.equal(result.content, raw)
})

// ---------------------------------------------------------------- 映射到表单

const categories = [
  { id: 'c1', name: '后端' },
  { id: 'c2', name: '前端' },
]

const tags = [
  { id: 't1', name: 'Vue' },
  { id: 't2', name: 'C#' },
]

test('标题：front matter 优先，否则用文件名（去掉 .md）', () => {
  const withTitle = buildImportPlan({
    fileName: 'file.md',
    text: '---\ntitle: 文章标题\n---\n正文',
    categories: [],
    tags: [],
  })
  assert.equal(withTitle.title, '文章标题')

  const fallback = buildImportPlan({ fileName: '我的文章.md', text: '正文', categories: [], tags: [] })
  assert.equal(fallback.title, '我的文章')
  assert.equal(fallback.content, '正文')
})

test('front matter 的键大小写不敏感', () => {
  const plan = buildImportPlan({ fileName: 'a.md', text: '---\nTitle: 大写键\n---\nbody', categories: [], tags: [] })

  assert.equal(plan.title, '大写键')
})

test('标题与摘要按字段上限截断', () => {
  const long = 'x'.repeat(250)
  const plan = buildImportPlan({
    fileName: 'a.md',
    text: `---\ntitle: ${long}\nsummary: ${long}\n---\nbody`,
    categories: [],
    tags: [],
  })

  assert.equal(plan.title?.length, 200)
  assert.equal(plan.summary?.length, 200)
})

test('摘要按 summary > description > excerpt 取值', () => {
  const plan = buildImportPlan({
    fileName: 'a.md',
    text: '---\ndescription: 描述\nexcerpt: 摘录\n---\nbody',
    categories: [],
    tags: [],
  })

  assert.equal(plan.summary, '描述')
})

test('标签按名字大小写不敏感匹配，重复去重，未匹配的返回给调用方', () => {
  const plan = buildImportPlan({
    fileName: 'a.md',
    text: '---\ntags: [vue, 不存在, c#]\n---\nbody',
    categories: [],
    tags,
  })

  assert.deepEqual(plan.tagIds, ['t1', 't2'])
  assert.deepEqual(plan.missingTags, ['不存在'])
})

test('标签也支持逗号字符串写法', () => {
  const plan = buildImportPlan({
    fileName: 'a.md',
    text: '---\ntags: Vue, C#\n---\nbody',
    categories: [],
    tags,
  })

  assert.deepEqual(plan.tagIds, ['t1', 't2'])
})

test('分类按名字匹配，支持 categories 列表并返回未匹配项', () => {
  const plan = buildImportPlan({
    fileName: 'a.md',
    text: '---\ncategories: [不存在, 后端]\n---\nbody',
    categories,
    tags: [],
  })

  assert.equal(plan.categoryId, 'c1')
  assert.deepEqual(plan.missingCategories, ['不存在'])
})

test('封面只接受站内 /api/files/ 地址，其余忽略并回传', () => {
  const ok = buildImportPlan({
    fileName: 'a.md',
    text: '---\ncover: /api/files/2026/10/a.webp\n---\nbody',
    categories: [],
    tags: [],
  })
  assert.equal(ok.coverImage, '/api/files/2026/10/a.webp')

  for (const bad of [
    'https://evil.com/x.png',
    '//evil.com/x.png',
    'javascript:alert(1)',
    '/api/files/../x.png',
    'data:image/png;base64,xx',
    '/api/files/',
  ]) {
    const plan = buildImportPlan({
      fileName: 'a.md',
      text: `---\ncoverImage: ${bad}\n---\nbody`,
      categories: [],
      tags: [],
    })
    assert.equal(plan.coverImage, undefined, `应拒绝：${bad}`)
  }
})

test('draft / published 映射为“不立即发布”，未写则不改动', () => {
  const draft = buildImportPlan({ fileName: 'a.md', text: '---\ndraft: true\n---\nbody', categories: [], tags: [] })
  assert.equal(draft.publish, false)

  const unpublished = buildImportPlan({
    fileName: 'a.md',
    text: '---\npublished: false\n---\nbody',
    categories: [],
    tags: [],
  })
  assert.equal(unpublished.publish, false)

  const none = buildImportPlan({ fileName: 'a.md', text: 'body', categories: [], tags: [] })
  assert.equal(none.publish, undefined)
})

test('没有 tags 键时不改动标签，空数组表示清空', () => {
  const none = buildImportPlan({ fileName: 'a.md', text: 'body', categories: [], tags })
  assert.equal(none.tagIds, undefined)

  const empty = buildImportPlan({ fileName: 'a.md', text: '---\ntags: []\n---\nbody', categories: [], tags })
  assert.deepEqual(empty.tagIds, [])
})

test('只有 front matter 时正文为空', () => {
  const plan = buildImportPlan({ fileName: 'a.md', text: '---\ntitle: X\n---\n', categories: [], tags: [] })

  assert.equal(plan.content, '')
})
