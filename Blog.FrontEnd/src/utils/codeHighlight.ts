import hljs from 'highlight.js/lib/core'
import bash from 'highlight.js/lib/languages/bash'
import csharp from 'highlight.js/lib/languages/csharp'
import css from 'highlight.js/lib/languages/css'
import dockerfile from 'highlight.js/lib/languages/dockerfile'
import javascript from 'highlight.js/lib/languages/javascript'
import json from 'highlight.js/lib/languages/json'
import markdown from 'highlight.js/lib/languages/markdown'
import plaintext from 'highlight.js/lib/languages/plaintext'
import powershell from 'highlight.js/lib/languages/powershell'
import python from 'highlight.js/lib/languages/python'
import sql from 'highlight.js/lib/languages/sql'
import typescript from 'highlight.js/lib/languages/typescript'
import xml from 'highlight.js/lib/languages/xml'
import yaml from 'highlight.js/lib/languages/yaml'

/**
 * 文章代码块的语法高亮。
 *
 * 只注册站点实际用得到的语言，避免把 highlight.js 的全量语言包打进详情页。
 * 新语言按需加一行 import + registry 即可；未注册的语言一律走 plaintext。
 */
const languageModules = {
  bash,
  csharp,
  css,
  dockerfile,
  javascript,
  json,
  markdown,
  powershell,
  python,
  sql,
  typescript,
  xml,
  yaml,
}

for (const [name, language] of Object.entries(languageModules)) {
  hljs.registerLanguage(name, language)
}
hljs.registerLanguage('plaintext', plaintext)

/**
 * 常见别名 → highlight.js 的规范名。
 *
 * highlight.js 自己也能解析 `cs` / `c#` 这类别名，但这里需要规范名来做
 * `language-*` class（也避免把原始 lang 直接拼进 HTML 属性）。
 */
const languageAliases: Record<string, string> = {
  'c#': 'csharp',
  'c-sharp': 'csharp',
  cs: 'csharp',
  docker: 'dockerfile',
  htm: 'xml',
  html: 'xml',
  js: 'javascript',
  md: 'markdown',
  ps: 'powershell',
  ps1: 'powershell',
  py: 'python',
  sh: 'bash',
  shell: 'bash',
  svg: 'xml',
  ts: 'typescript',
  vue: 'xml',
  yml: 'yaml',
  zsh: 'bash',
}

/** 只允许语言名里出现安全字符；真正的白名单是 languageModules */
const LANGUAGE_NAME_PATTERN = /^[a-z0-9#+._-]+$/

/** 把围栏里的语言标记归一为已注册的规范名；不认识时返回 null */
export function normalizeLanguage(rawLang?: string): string | null {
  const first = rawLang?.trim().split(/\s+/)[0]?.toLowerCase() ?? ''
  if (!first || !LANGUAGE_NAME_PATTERN.test(first)) return null

  const canonical = Object.prototype.hasOwnProperty.call(languageAliases, first)
    ? languageAliases[first]!
    : first
  return Object.prototype.hasOwnProperty.call(languageModules, canonical) ? canonical : null
}

function highlightPlaintext(code: string): string {
  return hljs.highlight(code, { language: 'plaintext' }).value
}

export interface HighlightedCode {
  html: string
  language: string | null
}

/**
 * 高亮一段代码。
 *
 * - 已知语言：按该语言高亮；`ignoreIllegals` 允许文章里出现不完整片段
 * - 未知语言 / 没写语言：只转义，不自动猜测（猜错比不猜更难看）
 * - 高亮器异常：回退 plaintext，不能因为一块代码让整篇文章渲染失败
 */
export function highlightCode(code: string, rawLang?: string): HighlightedCode {
  const language = normalizeLanguage(rawLang)
  if (!language) {
    return { html: highlightPlaintext(code), language: null }
  }

  try {
    return {
      html: hljs.highlight(code, { language, ignoreIllegals: true }).value,
      language,
    }
  } catch {
    return { html: highlightPlaintext(code), language: null }
  }
}

/** 生成 marked 需要的 `<pre><code>` HTML；class 只来自白名单语言 */
export function renderCodeBlock(code: string, rawLang?: string): string {
  const { html, language } = highlightCode(code, rawLang)
  const className = language ? `hljs language-${language}` : 'hljs'
  return `<pre><code class="${className}">${html}</code></pre>\n`
}
