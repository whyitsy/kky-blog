/**
 * Markdown 围栏代码块切分。
 *
 * 公式识别、导出样式清理都需要「跳过代码块」——否则示例里的 `$...$`
 * 或 `<font>` 会被当成正文处理，把代码改坏。
 *
 * 这里只做**行级围栏**识别（``` / ~~~，最多 3 个前导空格，允许 4 个以上反引号），
 * 不处理缩进代码块：语雀导出的正文代码都是围栏块，而缩进块在列表/引用里
 * 与普通缩进无法可靠区分，强行识别反而容易误伤。
 */

export interface MarkdownChunk {
  /** 是否为围栏代码块（包含开、闭围栏行本身） */
  fenced: boolean
  lines: string[]
}

interface FenceMarker {
  char: string
  length: number
}

function fenceMarker(line: string): FenceMarker | null {
  const match = /^\s{0,3}(`{3,}|~{3,})/.exec(line)
  if (!match) return null
  return { char: match[1]![0]!, length: match[1]!.length }
}

/**
 * 按围栏代码块把 Markdown 切成若干块。
 *
 * 各块按顺序用 `\n` 拼回去可**逐字还原**原文（除对非围栏块做业务替换外），
 * 调用方不需要自己处理换行边界。
 */
export function splitMarkdownChunks(markdown: string): MarkdownChunk[] {
  const lines = markdown.split('\n')
  const chunks: MarkdownChunk[] = []
  let index = 0

  while (index < lines.length) {
    const opening = fenceMarker(lines[index]!)

    if (!opening) {
      let end = index + 1
      while (end < lines.length && fenceMarker(lines[end]!) === null) end++
      chunks.push({ fenced: false, lines: lines.slice(index, end) })
      index = end
      continue
    }

    // 闭合围栏必须同字符、长度不短于开启围栏；找不到闭合就吞到文末
    let end = index + 1
    while (end < lines.length) {
      const closing = fenceMarker(lines[end]!)
      end++
      if (closing && closing.char === opening.char && closing.length >= opening.length) break
    }
    chunks.push({ fenced: true, lines: lines.slice(index, end) })
    index = end
  }

  return chunks
}

/** 只对非围栏代码块做变换；代码块原样保留 */
export function mapMarkdownTextChunks(
  markdown: string,
  transform: (text: string) => string,
): string {
  return splitMarkdownChunks(markdown)
    .map((chunk) => {
      const text = chunk.lines.join('\n')
      return chunk.fenced ? text : transform(text)
    })
    .join('\n')
}
