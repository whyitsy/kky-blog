/**
 * 墨纸主题的排版辅助函数：日期、期号、序号。
 *
 * 纯展示逻辑，不依赖任何业务模块 —— 主题页面只消费 Page Model。
 */

const WEEKDAYS = ['星期日', '星期一', '星期二', '星期三', '星期四', '星期五', '星期六']

/** 两位补零：3 -> "03" */
export function pad2(n: number): string {
  return String(n).padStart(2, '0')
}

/** 列表里的紧凑日期：2026.10.28；无日期时给「未刊」 */
export function shortDate(iso: string | null): string {
  if (!iso) return '未刊'
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return '未刊'
  return `${d.getFullYear()}.${pad2(d.getMonth() + 1)}.${pad2(d.getDate())}`
}

/** 头版日期：2026 年 10 月 28 日 */
export function longDate(date: Date = new Date()): string {
  return `${date.getFullYear()} 年 ${date.getMonth() + 1} 月 ${date.getDate()} 日`
}

/** 星期几（头版日期行用） */
export function weekday(date: Date = new Date()): string {
  return WEEKDAYS[date.getDay()] ?? ''
}

/** 当前是今年第几周（没有建站日期时的期号兜底） */
export function weekOfYear(date: Date = new Date()): number {
  const start = new Date(date.getFullYear(), 0, 1)
  const days = Math.floor((date.getTime() - start.getTime()) / 86_400_000)
  return Math.floor(days / 7) + 1
}

/**
 * 期号：建站日期存在时是「建站以来的第几周」，否则用今年第几周。
 * 目的只是给头版一个报刊式的期号感，不追求真实刊期。
 */
export function issueNumber(foundingDate?: string | null): number {
  const now = new Date()
  if (!foundingDate) return weekOfYear(now)
  const start = new Date(foundingDate)
  if (Number.isNaN(start.getTime())) return weekOfYear(now)
  const weeks = Math.floor((now.getTime() - start.getTime()) / (7 * 86_400_000)) + 1
  return weeks > 0 ? weeks : 1
}

/** 从建站日期里取年份（头版 EST. 用） */
export function foundedYear(foundingDate?: string | null): number | null {
  if (!foundingDate) return null
  const d = new Date(foundingDate)
  return Number.isNaN(d.getTime()) ? null : d.getFullYear()
}
