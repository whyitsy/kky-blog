import { ref } from 'vue'

/**
 * 顶部路由加载条控制器（NProgress 风格的轻量实现，**不引第三方依赖**）。
 *
 * 为什么自己写：参考项目用的是 nprogress + 一份覆盖它默认样式的 SCSS，落到这个项目
 * 会多一个运行时依赖、一套需要对着主题 Token 重写的 CSS，而真正的需求只是
 * 「路由切换时给一条与主题同源的进度提示」。这里把「状态机」和「渲染」拆开：
 *   · 本文件只管进度值与计时器（纯逻辑，不碰 DOM，方便单测/复用）；
 *   · `@/ui/components/RouteProgress.vue` 只管把进度画成顶部渐变细条。
 *
 * 使用方式见 `router/index.ts`：导航开始 `progressStart()`，结束 `progressDone()`，
 * 导航出错 `progressFail()`（进度条收尾时变成警示红，而不是假装成功）。
 *
 * 体验上的三个细节：
 *   1. 最少显示 420ms —— 命中浏览器缓存时导航几乎是瞬间的，没有下限只会闪一下，
 *      反而像页面抖了一下；
 *   2. 到 100% 后先停 240ms 再淡出 —— 让「完成」这件事被看见；
 *   3. 自动补给（trickle）用递减增量逼近 90%，永远不会自己跑满；
 *      跑满只能由 `progressDone()` 触发，避免进度条与真实完成状态脱节。
 */

/** 当前进度（0-100） */
export const progressValue = ref(0)
/** 是否处于显示状态（隐藏后组件仍挂载，靠透明度淡出） */
export const progressVisible = ref(false)
/** 本次导航是否失败（失败时进度条转为警示色） */
export const progressFailed = ref(false)

/** 自动补给间隔 */
const TRICKLE_INTERVAL = 220
/** 进度条最短显示时长 */
const MIN_VISIBLE_MS = 420
/** 到 100% 后的停留时长 */
const HIDE_DELAY_MS = 240
/** 与组件里的淡出动画时长保持一致 */
const FADE_OUT_MS = 240

let trickleTimer: number | null = null
let hideTimer: number | null = null
let resetTimer: number | null = null
let startedAt = 0
/** 是否有一次导航正在进行（防止重复 start 叠加计时器） */
let running = false

function clearTimers() {
  if (trickleTimer !== null) {
    window.clearInterval(trickleTimer)
    trickleTimer = null
  }
  if (hideTimer !== null) {
    window.clearTimeout(hideTimer)
    hideTimer = null
  }
  if (resetTimer !== null) {
    window.clearTimeout(resetTimer)
    resetTimer = null
  }
}

/** 自动补给：越接近 90% 增量越小，视觉上「越走越慢」而非卡死 */
function trickle() {
  const remaining = 90 - progressValue.value
  progressValue.value = Math.min(
    90,
    progressValue.value + Math.max(1.2, remaining * 0.14),
  )
}

/** 导航开始 */
export function progressStart() {
  // 同一时刻只保留一条进度条：redirect 会连续触发 beforeEach，不重复叠加
  if (running) return
  running = true
  clearTimers()

  progressFailed.value = false
  startedAt = performance.now()

  // 上一次的收尾动画还没走完就开始了新导航：从新起点重来，不要从 100% 往回跳
  progressValue.value =
    progressVisible.value && progressValue.value < 90
      ? Math.max(progressValue.value, 10)
      : 10
  progressVisible.value = true

  trickleTimer = window.setInterval(trickle, TRICKLE_INTERVAL)
}

/** 导航结束（failed = true 时进度条收尾为警示色） */
export function progressFinish(failed = false) {
  if (!running) return
  running = false
  clearTimers()

  progressFailed.value = failed
  progressValue.value = 100

  const elapsed = performance.now() - startedAt
  const wait = Math.max(MIN_VISIBLE_MS - elapsed, 0) + HIDE_DELAY_MS

  hideTimer = window.setTimeout(() => {
    progressVisible.value = false
    // 等淡出动画结束再归零，避免下一轮开始时看到宽度从 100% 猛跳回 10%
    resetTimer = window.setTimeout(() => {
      progressValue.value = 0
      progressFailed.value = false
    }, FADE_OUT_MS)
  }, wait)
}

/** 导航正常完成 */
export function progressDone() {
  progressFinish(false)
}

/** 导航失败（路由解析/懒加载 chunk 加载失败等） */
export function progressFail() {
  progressFinish(true)
}
