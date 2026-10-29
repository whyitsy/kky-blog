<script setup lang="ts">
/**
 * Aurora 页面背景装饰层（方案 A：极光星屑 + 软云）。
 *
 * 只出现在公开站，铺在内容后面：首页有全屏 Hero 时基本被遮住，其余页面
 * 用很淡的星屑、软云和点阵让纯色背景不至于空荡。所有元素：
 *   · `aria-hidden` + `pointer-events: none`，不参与交互与读屏；
 *   · 透明度压得很低，不抢正文注意力；
 *   · 颜色全部来自主题 Token，亮/暗色自动适配；
 *   · 尊重 `prefers-reduced-motion`（视觉上只保留静态层）。
 *
 * 位置和数量刻意克制：星星固定在页面四周，云只放左右下角，中间区域留给内容。
 */
const stars = ['star-1', 'star-2', 'star-3', 'star-4', 'star-5']
</script>

<template>
  <div class="page-decor" aria-hidden="true">
    <svg
      v-for="star in stars"
      :key="star"
      class="decor-star"
      :class="star"
      viewBox="0 0 24 24"
    >
      <path
        d="M12 1c.9 5.7 4.4 9.2 11 10.1-6.6.9-10.1 4.4-11 10.1-.9-5.7-4.4-9.2-11-10.1C7.6 10.2 11.1 6.7 12 1Z"
      />
    </svg>

    <svg class="decor-cloud cloud-1" viewBox="0 0 200 80">
      <g fill="currentColor">
        <circle cx="45" cy="52" r="24" />
        <circle cx="92" cy="34" r="32" />
        <circle cx="140" cy="46" r="27" />
        <circle cx="168" cy="58" r="18" />
        <rect x="18" y="48" width="174" height="30" rx="15" />
      </g>
    </svg>

    <svg class="decor-cloud cloud-2" viewBox="0 0 200 80">
      <g fill="currentColor">
        <circle cx="45" cy="52" r="24" />
        <circle cx="92" cy="34" r="32" />
        <circle cx="140" cy="46" r="27" />
        <circle cx="168" cy="58" r="18" />
        <rect x="18" y="48" width="174" height="30" rx="15" />
      </g>
    </svg>
  </div>
</template>

<style scoped>
.page-decor {
  position: fixed;
  inset: 0;
  z-index: 0;
  overflow: hidden;
  pointer-events: none;
  /* 两团大范围的环境光，让纯色背景多一点层次；页面本身的极光光晕仍由 body 提供 */
  background:
    radial-gradient(
      52vw 42vh at 92% -8%,
      color-mix(in srgb, var(--accent-500) 10%, transparent),
      transparent 68%
    ),
    radial-gradient(
      48vw 38vh at 4% 104%,
      color-mix(in srgb, var(--brand-400) 8%, transparent),
      transparent 68%
    );
}

/* 极细点阵：只提供一点纸面质感，不画完整网格，避免和内容争夺注意力 */
.page-decor::before {
  content: '';
  position: absolute;
  inset: 0;
  background-image: radial-gradient(
    color-mix(in srgb, var(--text-strong) 9%, transparent) 1px,
    transparent 1.4px
  );
  background-size: 30px 30px;
  opacity: 0.16;
}

/* ---------------------------------------------------------------- 星屑 */
.decor-star {
  position: absolute;
  width: 20px;
  height: 20px;
  fill: currentColor;
  opacity: 0.4;
  color: color-mix(in srgb, var(--brand-400) 70%, transparent);
  filter: drop-shadow(0 0 10px color-mix(in srgb, var(--brand-400) 50%, transparent));
  animation: decor-twinkle 8s ease-in-out infinite;
  will-change: opacity, transform;
}

.star-1 {
  top: 13%;
  left: 4%;
  width: 22px;
  height: 22px;
}

.star-2 {
  top: 27%;
  right: 6%;
  width: 15px;
  height: 15px;
  color: color-mix(in srgb, var(--sub-500) 70%, transparent);
  filter: drop-shadow(0 0 10px color-mix(in srgb, var(--sub-500) 50%, transparent));
  animation-delay: -2.4s;
}

.star-3 {
  top: 56%;
  left: 5%;
  width: 11px;
  height: 11px;
  color: color-mix(in srgb, var(--accent-500) 70%, transparent);
  filter: drop-shadow(0 0 8px color-mix(in srgb, var(--accent-500) 50%, transparent));
  opacity: 0.3;
  animation-delay: -4.2s;
}

.star-4 {
  bottom: 22%;
  right: 4%;
  width: 17px;
  height: 17px;
  color: color-mix(in srgb, var(--sub-500) 70%, transparent);
  filter: drop-shadow(0 0 10px color-mix(in srgb, var(--sub-500) 50%, transparent));
  animation-delay: -1.2s;
}

.star-5 {
  top: 40%;
  right: 3%;
  width: 9px;
  height: 9px;
  color: color-mix(in srgb, var(--accent-500) 70%, transparent);
  filter: drop-shadow(0 0 8px color-mix(in srgb, var(--accent-500) 50%, transparent));
  opacity: 0.28;
  animation-delay: -5.5s;
}

/* ---------------------------------------------------------------- 软云 */
.decor-cloud {
  position: absolute;
  height: auto;
  aspect-ratio: 200 / 80;
  color: color-mix(in srgb, var(--brand-400) 8%, transparent);
  opacity: 0.85;
  /* 轻微模糊：几个圆叠出来的云才不会被看成「一排泡泡」 */
  filter: blur(1.5px);
  animation: decor-drift 28s ease-in-out infinite alternate;
  will-change: transform;
}

.cloud-1 {
  width: min(380px, 30vw);
  left: -7%;
  bottom: 2%;
}

.cloud-2 {
  width: min(240px, 20vw);
  right: -4%;
  bottom: 20%;
  color: color-mix(in srgb, var(--sub-500) 7%, transparent);
  animation-duration: 38s;
  animation-delay: -8s;
}

/* ---------------------------------------------------------------- 动效 */
@keyframes decor-twinkle {
  0%,
  100% {
    opacity: 0.18;
    transform: scale(0.9) rotate(0deg);
  }

  50% {
    opacity: 0.62;
    transform: scale(1.05) rotate(8deg);
  }
}

@keyframes decor-drift {
  from {
    transform: translate3d(-10px, 0, 0);
  }

  to {
    transform: translate3d(10px, -8px, 0);
  }
}

/* ---------------------------------------------------------------- 窄屏
   内容占满宽度，减少边缘装饰的数量和存在感 */
@media (max-width: 767px) {
  .star-3,
  .star-5,
  .cloud-2 {
    display: none;
  }

  .star-1 {
    top: 10%;
    left: 5%;
    width: 16px;
    height: 16px;
  }

  .star-2 {
    top: 22%;
    right: 7%;
    width: 12px;
    height: 12px;
  }

  .star-4 {
    bottom: 18%;
    right: 6%;
    width: 13px;
    height: 13px;
  }

  .cloud-1 {
    width: 250px;
    left: -24%;
    bottom: 1%;
    opacity: 0.6;
  }

  .page-decor::before {
    background-size: 26px 26px;
    opacity: 0.12;
  }
}

/* 系统开启减少动效时，保留静态装饰即可 */
@media (prefers-reduced-motion: reduce) {
  .decor-star,
  .decor-cloud {
    animation: none;
  }

  .decor-star {
    opacity: 0.3;
  }
}
</style>
