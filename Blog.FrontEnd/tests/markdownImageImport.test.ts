import { test } from 'node:test'
import assert from 'node:assert/strict'
import {
  localizeMarkdownImages,
  resolveLocalImageFiles,
  type NamedImageFile,
} from '../src/utils/markdownImageImport.ts'

// ---------------------------------------------------------------- 本地图片匹配

const file = (name: string, relativePath?: string): NamedImageFile & { id: string } => ({
  id: relativePath ?? name,
  name,
  ...(relativePath === undefined ? {} : { webkitRelativePath: relativePath }),
})

test('本地匹配：相对路径、./ 前缀、URL 编码与多级目录后缀', () => {
  const files = [
    file('b.png', 'export/images/b.png'),
    file('中文 图.png', 'export/images/中文 图.png'),
  ]

  assert.equal(resolveLocalImageFiles(['images/b.png'], files).get('images/b.png')?.name, 'b.png')
  assert.equal(resolveLocalImageFiles(['./images/b.png'], files).get('./images/b.png')?.name, 'b.png')
  assert.equal(
    resolveLocalImageFiles(['images/b.png?v=2'], files).get('images/b.png?v=2')?.name,
    'b.png',
  )
  assert.equal(
    resolveLocalImageFiles(['images/%E4%B8%AD%E6%96%87%20%E5%9B%BE.png'], files).get(
      'images/%E4%B8%AD%E6%96%87%20%E5%9B%BE.png',
    )?.name,
    '中文 图.png',
  )
})

test('本地匹配：只有 basename 相同且唯一时才认', () => {
  const ambiguous = [
    file('a.png', 'x/a.png'),
    file('a.png', 'y/a.png'),
  ]

  assert.equal(resolveLocalImageFiles(['a.png'], ambiguous).size, 0)
  assert.equal(resolveLocalImageFiles(['x/a.png'], ambiguous).get('x/a.png')?.name, 'a.png')

  const unique = [file('a.png', 'export/a.png')]
  assert.equal(resolveLocalImageFiles(['a.png'], unique).get('a.png')?.name, 'a.png')
})

test('本地匹配：忽略非图片文件与空路径', () => {
  const files = [file('readme.md', 'export/readme.md'), file('a.png', 'export/a.png')]

  assert.equal(resolveLocalImageFiles(['readme.md', ''], files).size, 0)
  assert.equal(resolveLocalImageFiles(['a.png'], files).get('a.png')?.name, 'a.png')
})

// ---------------------------------------------------------------- 编排

const REMOTE = 'https://cdn.nlark.com/yuque/remote.png'
const EXTERNAL = 'https://example.com/outside.png'
const LOCAL = 'images/local.png'

function markdown(): string {
  return [
    `![语雀](${REMOTE})`,
    '',
    `![站外](${EXTERNAL})`,
    '',
    `![本地](${LOCAL})`,
  ].join('\n')
}

test('编排：在线图片（语雀与站外）都处理，本地匹配后一起改写，进度按总数推进', async () => {
  const progress: string[] = []
  const calls: string[] = []

  const result = await localizeMarkdownImages(markdown(), {
    files: [file('local.png', 'export/images/local.png')],
    importRemote: async (url) => {
      calls.push(`remote:${url}`)
      return url === REMOTE
        ? '/api/files/2026/11/yuque.webp'
        : '/api/files/2026/11/outside.webp'
    },
    uploadLocal: async (picked) => {
      calls.push(`local:${picked.name}`)
      return '/api/files/2026/11/local.webp'
    },
    onProgress: (done, total) => progress.push(`${done}/${total}`),
  })

  assert.equal(
    result.content,
    [
      '![语雀](/api/files/2026/11/yuque.webp)',
      '',
      '![站外](/api/files/2026/11/outside.webp)',
      '',
      '![本地](/api/files/2026/11/local.webp)',
    ].join('\n'),
  )
  assert.equal(result.localized, 3)
  assert.deepEqual(result.failures, [])
  assert.deepEqual(result.missingLocal, [])
  assert.deepEqual(calls, [`remote:${REMOTE}`, `remote:${EXTERNAL}`, 'local:local.png'])
  assert.deepEqual(progress, ['0/3', '1/3', '2/3', '3/3'])
})

test('编排：单张在线图片失败不影响其它图片，失败项保留原链接并记录原因', async () => {
  const result = await localizeMarkdownImages(
    [`![语雀](${REMOTE})`, '', `![站外](${EXTERNAL})`].join('\n'),
    {
      importRemote: async (url) => {
        if (url === REMOTE) throw new Error('浏览器无法读取该图片（可能受 CORS 或防盗链限制）')
        return '/api/files/2026/11/outside.webp'
      },
      uploadLocal: async () => '/api/files/should-not-be-used.webp',
    },
  )

  assert.equal(
    result.content,
    ['![语雀](' + REMOTE + ')', '', '![站外](/api/files/2026/11/outside.webp)'].join('\n'),
  )
  assert.equal(result.localized, 1)
  assert.deepEqual(result.failures, [
    { url: REMOTE, reason: '浏览器无法读取该图片（可能受 CORS 或防盗链限制）' },
  ])
})

test('编排：没选本地图片时，相对路径记为缺失（由 UI 决定要不要选文件夹）', async () => {
  const result = await localizeMarkdownImages(`![本地](${LOCAL})`, {
    files: [],
    importRemote: async () => '/api/files/never.webp',
    uploadLocal: async () => '/api/files/never.webp',
  })

  assert.equal(result.content, `![本地](${LOCAL})`)
  assert.deepEqual(result.missingLocal, [LOCAL])
  assert.deepEqual(result.failures, [])
})

test('编排：没有图片时原样返回，且不触发进度回调', async () => {
  const progress: string[] = []

  const result = await localizeMarkdownImages('只有正文', {
    importRemote: async () => '/api/files/never.webp',
    uploadLocal: async () => '/api/files/never.webp',
    onProgress: (done, total) => progress.push(`${done}/${total}`),
  })

  assert.equal(result.content, '只有正文')
  assert.equal(result.localized, 0)
  assert.deepEqual(progress, [])
})

test('编排：同一个本地文件被多个链接引用时只上传一次', async () => {
  const uploads: string[] = []

  const result = await localizeMarkdownImages(
    ['![](images/a.png)', '', '![](./images/a.png)'].join('\n'),
    {
      files: [file('a.png', 'images/a.png')],
      importRemote: async () => '/api/files/never.webp',
      uploadLocal: async (picked) => {
        uploads.push(picked.name)
        return '/api/files/2026/11/a.webp'
      },
    },
  )

  assert.equal(uploads.length, 1)
  assert.equal(result.content, ['![](/api/files/2026/11/a.webp)', '', '![](/api/files/2026/11/a.webp)'].join('\n'))
})

test('编排：已经是站内 /api/files/ 的图片不会被再次抓取（分两步导入时幂等）', async () => {
  const imports: string[] = []

  const result = await localizeMarkdownImages(
    ['![](/api/files/2026/11/done.webp)', '', '![](images/a.png)'].join('\n'),
    {
      files: [file('a.png', 'images/a.png')],
      importRemote: async (url) => {
        imports.push(url)
        return '/api/files/never.webp'
      },
      uploadLocal: async () => '/api/files/2026/11/a.webp',
    },
  )

  assert.deepEqual(imports, [])
  assert.equal(
    result.content,
    ['![](/api/files/2026/11/done.webp)', '', '![](/api/files/2026/11/a.webp)'].join('\n'),
  )
  assert.equal(result.localized, 1)
})
