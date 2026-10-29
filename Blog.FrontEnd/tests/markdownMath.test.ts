import { test } from 'node:test'
import assert from 'node:assert/strict'
import { protectMath, restoreMath } from '../src/utils/markdownMath.ts'
import { buildImportPlan, cleanExportedMarkdown } from '../src/utils/markdownImport.ts'
import { splitMarkdownChunks } from '../src/utils/markdownFences.ts'

// ---------------------------------------------------------------- 公式提取

test('$$...$$ 识别为独立公式，并替换成占位符', () => {
  const { text, formulas } = protectMath('前文\n\n$$\nE = mc^2\n$$\n\n后文')

  assert.equal(formulas.length, 1)
  assert.equal(formulas[0]!.display, true)
  assert.equal(formulas[0]!.tex, '\nE = mc^2\n')
  assert.ok(text.includes(formulas[0]!.token))
  assert.ok(!text.includes('E = mc^2'))
})

test('独占一行的单 $ 公式按独立公式处理（语雀导出形态）', () => {
  const { formulas } = protectMath(
    '$ \\theta_{t+1}=\\theta_t-\\eta \\cdot \\frac{\\hat{m}_t}{\\sqrt{\\hat{v}_t}+\\epsilon} $',
  )

  assert.equal(formulas.length, 1)
  assert.equal(formulas[0]!.display, true)
  assert.ok(formulas[0]!.tex.includes('\\frac'))
})

test('同一行里夹在文字中的公式按行内公式处理', () => {
  const { text, formulas } = protectMath('一阶动量（$ m_t $，均值）')

  assert.equal(formulas.length, 1)
  assert.equal(formulas[0]!.display, false)
  assert.equal(formulas[0]!.tex, ' m_t ')
  assert.equal(text, `一阶动量（${formulas[0]!.token}，均值）`)
})

test('\\(...\\) 与 \\[...\\] 分隔符也能识别', () => {
  const inline = protectMath('行内 \\(a+b\\) 结束')
  assert.equal(inline.formulas.length, 1)
  assert.equal(inline.formulas[0]!.display, false)

  const display = protectMath('\\[a^2+b^2=c^2\\]')
  assert.equal(display.formulas.length, 1)
  assert.equal(display.formulas[0]!.display, true)
})

test('围栏代码块与行内代码里的 $ 不做公式处理', () => {
  const source = [
    '```bash',
    'echo $HOME 与 $PATH',
    '```',
    '',
    '行内 `$x$` 不应处理，但 $y^2$ 应该处理。',
  ].join('\n')
  const { formulas } = protectMath(source)

  assert.equal(formulas.length, 1)
  assert.ok(formulas[0]!.tex.includes('y^2'))
})

test('金额不会被误判成行内公式', () => {
  const { formulas } = protectMath('价格 $5 and $10 元，约 $12.5 。')
  assert.equal(formulas.length, 0)
})

test('转义的 \\$ 不处理', () => {
  const { formulas } = protectMath('这里只要美元符号：\\$x$')
  assert.equal(formulas.length, 0)
})

test('中文与中文标点混在公式里也能渲染出 KaTeX（不落回源码）', () => {
  const { text, formulas } = protectMath(
    '其中$ \\beta_1和\\beta_2是超参数，一般为0.9和0.999，\\epsilon为1e-8 $',
  )
  assert.equal(formulas.length, 1)

  const html = restoreMath(`<p>${text}</p>`, formulas)
  assert.match(html, /class="katex"/)
  assert.ok(!html.includes('math-fallback'))
})

test('restoreMath 把占位符换回 KaTeX HTML，且不影响其它文本', () => {
  const { text, formulas } = protectMath('公式 $a^2+b^2=c^2$ 结束')
  const html = restoreMath(`<p>${text}</p>`, formulas)

  assert.match(html, /^<p>公式 <span class="katex">/)
  assert.ok(html.includes('结束</p>'))
  assert.ok(!html.includes(formulas[0]!.token))
})

// ---------------------------------------------------------------- 围栏切分

test('围栏块切分后可用换行逐字还原原文', () => {
  const source = 'a\n```\ncode\n```\nb\n~~~\ntext\n~~~\nc'
  const chunks = splitMarkdownChunks(source)

  assert.equal(chunks.length, 5)
  assert.equal(chunks.map((chunk) => chunk.lines.join('\n')).join('\n'), source)
})

// ---------------------------------------------------------------- 语雀导出样式清理

test('语雀导出的 <font> 标签被 unwrap，文字保留', () => {
  const cleaned = cleanExportedMarkdown(
    '<font style="color:rgb(15, 17, 21);">是一个</font>**<font style="color:#DF2A3F;">状态对象</font>**',
  )

  assert.equal(cleaned, '是一个**状态对象**')
})

test('其它标签保留但去掉 style 属性', () => {
  const cleaned = cleanExportedMarkdown('<span style="color:red">红字</span>')
  assert.equal(cleaned, '<span>红字</span>')
})

test('围栏代码块里的 <font> 原样保留', () => {
  const source = '```html\n<font style="color:red">示例</font>\n```'
  assert.equal(cleanExportedMarkdown(source), source)
})

test('导入计划里的正文已经清理过导出样式', () => {
  const plan = buildImportPlan({
    fileName: 'a.md',
    text: '<font style="color:#DF2A3F;">requires_grad=true</font> 让梯度累积',
    categories: [],
    tags: [],
  })

  assert.equal(plan.content, 'requires_grad=true 让梯度累积')
})
