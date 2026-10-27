<script setup lang="ts">
import { computed } from 'vue'
import { useSiteStore } from '@/stores/site'

/**
 * 站点 Logo —— 全站唯一渲染 logo 的地方（公开站导航/页脚、后台侧栏、作者区、登录页共用）。
 *
 * 两种形态：
 *   · 配置了 `SiteLogo` 图片 → 展示图片
 *   · 未配置 → 渐变圆点 + `LogoName` 文字（缺省 "k"）
 *
 * `size` 吸收尺寸差异，`glow` 吸收是否带发光（页脚不带）。
 */
withDefaults(defineProps<{ size?: number; glow?: boolean }>(), {
  size: 32,
  glow: true,
})

const site = useSiteStore()

const logoName = computed(() => site.config?.logoName || 'k')
const logoImage = computed(() => site.config?.siteLogo || '')
</script>

<template>
  <img
    v-if="logoImage"
    class="site-logo"
    :src="logoImage"
    alt=""
    :style="{ '--logo-size': `${size}px` }"
  />
  <span v-else class="logo-dot" :class="{ glow }" :style="{ '--logo-size': `${size}px` }">
    {{ logoName }}
  </span>
</template>

<style scoped>
.logo-dot,
.site-logo {
  width: var(--logo-size);
  height: var(--logo-size);
  flex-shrink: 0;
  border-radius: 50%;
}

.logo-dot {
  display: grid;
  place-items: center;
  font-size: calc(var(--logo-size) * 0.5);
  font-weight: 700;
  color: var(--text-on-brand);
  background: linear-gradient(
    135deg,
    var(--gradient-start),
    var(--gradient-mid),
    var(--gradient-end)
  );
}

.logo-dot.glow {
  box-shadow: var(--glow-purple);
}

.site-logo {
  display: block;
  object-fit: cover;
}
</style>
