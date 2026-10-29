/**
 * Markdown 里的 LaTeX 公式提取与渲染。
 *
 * <h3>为什么不直接在 marked 的渲染结果上替换</h3>
 * 语雀导出的公式是 `$...$` / `$$...$$` 形式的**纯文本**。如果先交给 marked，
 * `_`、`\\`、`*` 这些数学符号会先被 Markdown 的强调/转义规则吃掉；
 * 再经过 DOMPurify，KaTeX 需要的结构也可能被清洗掉。所以顺序必须是：
 *
 *   1. 在 Markdown 原文里把公式替换成**普通文本占位符**（marked/DOMPurify 都不会动它）；
 *   2. 正常渲染并净化 Markdown；
 *   3. 在**净化之后**把占位符换回 KaTeX 生成的 HTML。
 *
 * KaTeX 的输出由可信库生成、且已经净化的正文里只包含占位符，所以第 3 步
 * 不会把用户内容重新引入 XSS 面（`\href` 等靠 `trust: false` 关闭）。
 *
 * <h3>支持的分隔符</h3>
 * - `$$...$$`、`\[...\]` → 独立公式（display）
 * - `$...$`、`\(...\)` → 行内公式；但**独占一行**的 `$...$` 按独立公式渲染
 *   （语雀把独立公式也导成单 `$`，靠这个启发式兜住）
 *
 * 代码块 / 行内代码里的 `$` 一律不处理（见 markdownFences.ts）。
 */

import katex from 'katex'
import { mapMarkdownTextChunks } from './markdownFences.ts'

export interface MathFormula {
  /** 在 Markdown 里代表公式的占位符 */
  token: string
  /** 公式源码（不含分隔符） */
  tex: string
  /** 是否按独立公式（display mode）渲染 */
  display: boolean
}

export interface ProtectedMarkdown {
  /** 公式已替换成占位符的 Markdown */
  text: string
  formulas: MathFormula[]
}

/** 占位符只含大写字母和数字，Markdown / HTML 净化都不会改写它 */
function mathToken(index: number): string {
  return `KATEXMATHFORMULA${index}END`
}

/**
 * 单次扫描用的正则。顺序很重要：`$$` 必须排在单个 `$` 之前。
 * 捕获组：[1] $$...$$、[2] \[...\]、[3] \(...\)、[4] $...$
 */
const MATH_PATTERN =
  /\$\$([\s\S]+?)\$\$|\\\[([\s\S]+?)\\\]|\\\(([\s\S]+?)\\\)|(\$(?!\$)[^$\n]+?\$)/g

/** 行内代码的范围：这些区间内不识别公式 */
function inlineCodeRanges(text: string): Array<[number, number]> {
  const ranges: Array<[number, number]> = []
  const pattern = /(`+)([\s\S]*?)\1/g
  let match: RegExpExecArray | null
  while ((match = pattern.exec(text)) !== null) {
    ranges.push([match.index, match.index + match[0].length])
  }
  return ranges
}

function overlapsRange(
  start: number,
  end: number,
  ranges: Array<[number, number]>,
): boolean {
  return ranges.some(([rangeStart, rangeEnd]) => start < rangeEnd && end > rangeStart)
}

/** 反斜杠转义：`\$`、`\\(` 这类不应被当作分隔符 */
function isEscaped(text: string, index: number): boolean {
  let backslashes = 0
  for (let i = index - 1; i >= 0 && text[i] === '\\'; i--) backslashes++
  return backslashes % 2 === 1
}

/** 公式是否独占一行（前后只有空白） */
function isStandaloneLine(text: string, start: number, end: number): boolean {
  const lineStart = text.lastIndexOf('\n', start - 1) + 1
  const lineEndIndex = text.indexOf('\n', end)
  const lineEnd = lineEndIndex === -1 ? text.length : lineEndIndex
  return text.slice(lineStart, lineEnd).trim() === text.slice(start, end).trim()
}

/**
 * 单 `$` 的行内公式判定。
 *
 * 语雀的写法会在 `$` 内侧留空格（`$ m_t $`），和 `$5 and $10` 这类金额
 * 有歧义；因此：内侧带空白时要求内容里出现数学符号，紧凑写法则只排除纯金额。
 */
function isInlineDollarMath(tex: string): boolean {
  const trimmed = tex.trim()
  if (!trimmed) return false
  if (tex.includes('\n')) return false

  if (/^\s|\s$/.test(tex)) {
    return /[\\_^{}=+\-*/<>()[\]|]/.test(trimmed)
  }
  return !/^[\d\s.,]+$/.test(trimmed)
}

function protectText(text: string, formulas: MathFormula[]): string {
  const codeRanges = inlineCodeRanges(text)
  MATH_PATTERN.lastIndex = 0

  let result = ''
  let lastIndex = 0
  let match: RegExpExecArray | null

  while ((match = MATH_PATTERN.exec(text)) !== null) {
    const start = match.index
    const end = start + match[0].length
    if (overlapsRange(start, end, codeRanges) || isEscaped(text, start)) continue

    let tex: string
    let display: boolean

    if (match[1] !== undefined) {
      tex = match[1]
      display = true
    } else if (match[2] !== undefined) {
      tex = match[2]
      display = true
    } else if (match[3] !== undefined) {
      tex = match[3]
      display = false
    } else {
      // 第 4 组包含两侧的 `$`，需要剥掉
      tex = match[4]!.slice(1, -1)
      if (!isInlineDollarMath(tex)) continue
      display = isStandaloneLine(text, start, end)
    }

    if (!tex.trim()) continue

    const token = mathToken(formulas.length)
    formulas.push({ token, tex, display })
    result += text.slice(lastIndex, start) + token
    lastIndex = end
  }

  result += text.slice(lastIndex)
  return result
}

/** 把 Markdown 里的公式替换成占位符（跳过围栏代码块与行内代码） */
export function protectMath(markdown: string): ProtectedMarkdown {
  const formulas: MathFormula[] = []
  const text = mapMarkdownTextChunks(markdown, (chunk) => protectText(chunk, formulas))
  return { text, formulas }
}

function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
}

/**
 * 把公式里的中文片段包进 `\text{}`。
 *
 * 语雀里「中文写在公式里」很常见（`\beta_1和\beta_2是超参数`）。KaTeX 虽然能把
 * 裸中文当数学字符排出来，但会套数学字体，很多环境里没有中文字形（显示成方框）；
 * 包进 `\text{}` 后配合 `primitives.css` 的 `.katex .text { font-family: var(--font-sans) }`
 * 就能用正文字体正常显示。
 */
function wrapCjkRuns(tex: string): string {
  return tex.replace(/[\u2e80-\u9fff\u3000-\u303f\uff00-\uffef]+/g, (run) => {
    return `\\text{${run}}`
  })
}

/** 公式里是否含 CJK（中文、中文标点、全角符号） */
function hasCjk(tex: string): boolean {
  return /[\u2e80-\u9fff\u3000-\u303f\uff00-\uffef]/.test(tex)
}

function renderTex(tex: string, display: boolean): string {
  return katex.renderToString(tex, {
    displayMode: display,
    throwOnError: true,
    strict: 'ignore',
    trust: false,
  })
}

/** 渲染单个公式；失败时降级为可读的源码而不是抛错白屏 */
export function renderMathFormula(formula: MathFormula): string {
  // 含中文时优先走 \text 包装（字体更正确）；纯数学公式保持原样。
  // 两种写法都试一遍，谁先成功用谁，避免个别写法被包坏。
  const candidates = hasCjk(formula.tex)
    ? [wrapCjkRuns(formula.tex), formula.tex]
    : [formula.tex, wrapCjkRuns(formula.tex)]

  for (const candidate of candidates) {
    try {
      return renderTex(candidate, formula.display)
    } catch {
      // 换下一种写法
    }
  }

  const className = formula.display ? 'math-fallback math-fallback-display' : 'math-fallback'
  return `<code class="${className}" title="公式渲染失败">${escapeHtml(formula.tex.trim())}</code>`
}

/** 把净化后 HTML 里的占位符替换成 KaTeX 输出 */
export function restoreMath(html: string, formulas: MathFormula[]): string {
  if (formulas.length === 0) return html

  let result = html
  for (const formula of formulas) {
    if (!result.includes(formula.token)) continue
    result = result.split(formula.token).join(renderMathFormula(formula))
  }
  return result
}
