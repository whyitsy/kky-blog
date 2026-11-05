<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useSiteStore } from '@/stores/site'
import { DEFAULT_SITE_NAME } from '@/utils/site'

/**
 * Aurora 首屏 Hero：背景图/极光渐变 + 渐变标题 + 打字机副标题 + 向下翻阅入口。
 *
 * 背景图与内置渐变**二选一**（见模板）：
 *   · 抽到图 → 渲染 .hero-bg，不渲染 .aurora-blobs
 *   · 列表为空 → 不渲染图片层，只留 .aurora-blobs 的渐变
 * 两者条件必须互斥：两层都是 position:absolute 且同 z-index，同时存在会按 DOM 顺序
 * 把渐变叠在照片上，观感发花。
 *
 * 背景图不再叠整屏遮罩（此前的 .hero-mask 会把图片底部压暗到页面底色，
 * 与页面背景的极光光晕对不上）；文字可读性靠局部 text-shadow / 胶囊底色保证。
 *
 * 社交入口原本固定在首屏底部，现已收进首页侧栏的「关于本站」卡片（见 HomePage.vue）：
 * 首屏留给站点名与一句话介绍，视野更干净。
 */
const site = useSiteStore()

const subtitles = computed(() => site.config?.heroSubtitles ?? [])
const backgrounds = computed(() => site.config?.heroBackgrounds ?? [])

/**
 * 首屏背景：从后台配置的多张图里**随机挑一张**。
 *
 *   · 用 watch + immediate 而不是 onMounted —— 站点配置是异步拉的，
 *     HeroSection 挂载时 config 往往还是 null，写死在 mounted 里会永远选不到图。
 *   · 只在「当前这张不在新列表里」时才重新抽 —— 否则 site.refreshAll() 之类的
 *     刷新会把访客正在看的背景换掉。
 */
const pickedBackground = ref('')

watch(
  backgrounds,
  (list) => {
    if (!list.length) {
      pickedBackground.value = ''
      return
    }
    if (!list.includes(pickedBackground.value)) {
      pickedBackground.value = list[Math.floor(Math.random() * list.length)]
    }
  },
  { immediate: true },
)

function scrollToList() {
  document.getElementById('post-list')?.scrollIntoView({ behavior: 'smooth' })
}
</script>

<template>
  <section class="hero">
    <!-- 背景图与内置渐变二选一：配了图只留图；没配图才用 .aurora-blobs 的渐变本体 -->
    <div
      v-if="pickedBackground"
      class="hero-bg"
      :style="{ backgroundImage: `url(${pickedBackground})` }"
    />
    <div v-else class="aurora-blobs" />

    <div class="hero-content">
      <h1 class="hero-title gradient-text">{{ site.config?.siteName || DEFAULT_SITE_NAME }}</h1>

      <p class="hero-subtitle">
        <span v-typewriter="subtitles" class="tw-text" />
        <span class="tw-cursor">|</span>
      </p>

      <!-- 有可见文字就不再需要 aria-label：否则读屏会把同一句话念两遍 -->
      <button class="scroll-down" @click="scrollToList">
        <span>向下翻阅文章</span>
        <svg
          viewBox="0 0 24 24"
          width="18"
          height="18"
          fill="none"
          stroke="currentColor"
          stroke-width="2"
          stroke-linecap="round"
          stroke-linejoin="round"
        >
          <path d="M12 5v14m0 0 6-6m-6 6-6-6" />
        </svg>
      </button>
    </div>

  </section>
</template>

<style scoped>
.hero {
  position: relative;
  height: 100vh;
  min-height: 560px;
  display: flex;
  align-items: center;
  justify-content: center;
  overflow: hidden;
}

.hero-bg {
  position: absolute;
  inset: 0;
  background-size: cover;
  background-position: center;
  transform: scale(1.05);
}

.hero-content {
  position: relative;
  z-index: 2;
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
  padding: 0 var(--space-6);
}

.hero-title {
  font: var(--text-display);
  margin-bottom: var(--space-5);
  /* 主文本轻微上下浮动 */
  animation: float-title 3s ease-in-out infinite;
}

.hero-subtitle {
  display: flex;
  align-items: baseline;
  min-height: 34px;
  font-size: 20px;
  line-height: 1.5;
  color: var(--text-strong);
  /* 没有整屏遮罩后，用与主题底色同源的柔光保证照片上的可读性 */
  text-shadow:
    0 1px 2px color-mix(in srgb, var(--bg-canvas) 70%, transparent),
    0 0 22px color-mix(in srgb, var(--bg-canvas) 55%, transparent);
}

.tw-cursor {
  margin-left: 2px;
  color: var(--brand-500);
  animation: blink 1s step-end infinite;
}

/* 向下按钮：图标 + 文案胶囊，保留浮动呼吸动画 */
.scroll-down {
  margin-top: var(--space-16);
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  height: 44px;
  padding: 0 var(--space-5);
  border-radius: var(--radius-2xl);
  border: 3px solid var(--border-default);
  font-size: 15px;
  font-weight: 500;
  color: var(--text-strong);
  background: color-mix(in srgb, var(--bg-canvas) 55%, transparent);
  backdrop-filter: blur(6px);
  animation: float-btn 2.4s ease-in-out infinite;
  transition:
    border-color var(--transition-fast),
    color var(--transition-fast),
    background var(--transition-fast);
}

.scroll-down:hover {
  border-color: var(--brand-500);
  color: var(--brand-500);
}

@keyframes float-title {
  0%,
  100% {
    transform: translateY(0);
  }

  50% {
    transform: translateY(-6px);
  }
}

@keyframes float-btn {
  0%,
  100% {
    transform: translateY(0);
    opacity: 1;
  }

  50% {
    transform: translateY(14px);
    opacity: 0.35;
  }
}

@keyframes blink {
  0%,
  100% {
    opacity: 1;
  }

  50% {
    opacity: 0;
  }
}

/* ---------------------------------------------------------------- 窄屏
   标题字号来自 --text-display（桌面 64px），小屏上按屏幕宽度退两档，
   避免长站名被挤出视口。 */
@media (max-width: 900px) {
  .hero-title {
    font-size: 48px;
  }
}

@media (max-width: 640px) {
  .hero-title {
    font-size: 38px;
  }

  .hero-subtitle {
    font-size: 17px;
  }

  .scroll-down {
    margin-top: var(--space-10);
  }
}
</style>
