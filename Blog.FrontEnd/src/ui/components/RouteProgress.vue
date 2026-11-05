<script setup lang="ts">
import { computed } from 'vue'
import { progressFailed, progressValue, progressVisible } from '@/utils/progress'

/**
 * 顶部路由加载条。
 *
 * 挂在 `App.vue`（而不是某个布局里），公开站 / 后台 / 作者区 / 登录页共用；
 * 视觉上是一条贴着视口顶边的 3px 渐变细条，颜色全部来自主题 Token
 * （aurora 是极光渐变，paper 退化为墨红单色），不写死任何颜色。
 *
 * 无障碍：`role="progressbar"` + `aria-valuenow`，隐藏时 `aria-hidden`；
 * 纯装饰的头部高光单独 `aria-hidden`。`prefers-reduced-motion` 由
 * foundation/reset.css 的全局降级规则统一处理（只保留状态变化，不播动画）。
 */
const barStyle = computed(() => ({ width: `${progressValue.value}%` }))
const ariaValue = computed(() => Math.round(progressValue.value))
</script>

<template>
  <div
    class="route-progress"
    :class="{ 'is-visible': progressVisible, 'is-failed': progressFailed }"
    role="progressbar"
    aria-label="页面加载中"
    aria-valuemin="0"
    aria-valuemax="100"
    :aria-valuenow="ariaValue"
    :aria-hidden="progressVisible ? undefined : 'true'"
  >
    <div class="route-progress-bar" :style="barStyle">
      <span class="route-progress-peg" aria-hidden="true" />
    </div>
  </div>
</template>

<style scoped>
.route-progress {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  height: 3px;
  z-index: var(--z-progress);
  pointer-events: none;
  opacity: 0;
  transition: opacity 200ms ease;
}

.route-progress.is-visible {
  opacity: 1;
}

.route-progress-bar {
  position: relative;
  height: 100%;
  border-radius: 0 999px 999px 0;
  background: linear-gradient(
    90deg,
    var(--gradient-start),
    var(--gradient-mid),
    var(--gradient-end)
  );
  box-shadow:
    0 0 10px color-mix(in srgb, var(--brand-500) 55%, transparent),
    0 0 3px color-mix(in srgb, var(--brand-500) 70%, transparent);
  transition: width 240ms var(--ease-out-expo);
}

/* 头部高光：让进度条有一个「当前进度」的亮点，不是一根死板的色带 */
.route-progress-peg {
  position: absolute;
  top: 0;
  right: 0;
  width: 96px;
  height: 100%;
  border-radius: 999px;
  background: linear-gradient(
    90deg,
    transparent,
    color-mix(in srgb, var(--text-on-brand) 65%, transparent)
  );
  opacity: 0.6;
  filter: blur(0.4px);
}

/* 导航失败：不假装成功，收尾变成警示色 */
.route-progress.is-failed .route-progress-bar {
  background: var(--status-danger);
  box-shadow: 0 0 10px color-mix(in srgb, var(--status-danger) 55%, transparent);
}

.route-progress.is-failed .route-progress-peg {
  background: linear-gradient(
    90deg,
    transparent,
    color-mix(in srgb, var(--text-on-brand) 55%, transparent)
  );
}
</style>
