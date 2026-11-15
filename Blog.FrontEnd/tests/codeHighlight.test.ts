import { test } from 'node:test'
import assert from 'node:assert/strict'
import {
  highlightCode,
  normalizeLanguage,
  renderCodeBlock,
} from '../src/utils/codeHighlight.ts'

// ---------------------------------------------------------------- 语言识别

test('C# 的常见写法都归一为 csharp', () => {
  assert.equal(normalizeLanguage('csharp'), 'csharp')
  assert.equal(normalizeLanguage('cs'), 'csharp')
  assert.equal(normalizeLanguage('c#'), 'csharp')
  assert.equal(normalizeLanguage('C#'), 'csharp')
})

test('围栏的 info string 只取第一个词', () => {
  assert.equal(normalizeLanguage('csharp title="demo"'), 'csharp')
  assert.equal(normalizeLanguage('  js  '), 'javascript')
})

test('不认识的语言、空语言与恶意语言名都返回 null', () => {
  assert.equal(normalizeLanguage('not-a-real-language'), null)
  assert.equal(normalizeLanguage(''), null)
  assert.equal(normalizeLanguage(undefined), null)
  assert.equal(normalizeLanguage('"><img src=x onerror=alert(1)>'), null)
  // 对象原型上的属性不能被 `in` 误判成已注册语言
  assert.equal(normalizeLanguage('constructor'), null)
  assert.equal(normalizeLanguage('toString'), null)
})

// ---------------------------------------------------------------- 高亮结果

test('C# 代码输出带 hljs class 的 span', () => {
  const result = highlightCode('public int Count { get; }', 'csharp')

  assert.equal(result.language, 'csharp')
  assert.match(result.html, /<span class="hljs-keyword">public<\/span>/)
  assert.match(result.html, /<span class="hljs-built_in">int<\/span>/)
})

test('未知语言回退为 plaintext，并转义 HTML', () => {
  const result = highlightCode('<script>alert(1)</script>', 'not-a-real-language')

  assert.equal(result.language, null)
  assert.ok(result.html.includes('&lt;script&gt;'))
  assert.ok(!result.html.includes('<script>'))
})

test('没有语言时也按 plaintext 转义，不自动猜测', () => {
  const result = highlightCode('a < b && c > d', undefined)

  assert.equal(result.language, null)
  assert.ok(result.html.includes('&lt;'))
  assert.ok(result.html.includes('&amp;&amp;'))
})

// ---------------------------------------------------------------- 代码块 HTML

test('renderCodeBlock 输出 hljs 与规范化后的语言 class', () => {
  const html = renderCodeBlock('var x = 1;', 'js')

  assert.match(html, /^<pre><code class="hljs language-javascript">/)
  assert.match(html, /hljs-keyword/)
  assert.match(html, /<\/code><\/pre>\n$/)
})

test('没有语言时只输出 hljs class', () => {
  const html = renderCodeBlock('plain < text', undefined)

  assert.match(html, /^<pre><code class="hljs">/)
  assert.ok(html.includes('&lt;'))
})

test('恶意语言名不会进入 class 属性', () => {
  const html = renderCodeBlock('x', '"><img src=x onerror=alert(1)>')

  assert.equal(html, '<pre><code class="hljs">x</code></pre>\n')
})
