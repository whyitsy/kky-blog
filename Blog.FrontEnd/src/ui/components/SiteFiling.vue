<script setup lang="ts">
import { computed } from 'vue'
import policeFilingIcon from '@/assets/备案图标.png'
import {
  ICP_LICENSE,
  ICP_LICENSE_URL,
  POLICE_FILING_LICENSE,
  POLICE_FILING_URL,
} from '@/utils/site'

/**
 * 网站底部备案信息（主题无关）。
 *
 * 工信部要求 ICP 备案号展示在网站底部并链接到备案查询系统；公安联网备案
 * 同样需要展示，且编号左侧要带公安部统一图标。号码配置在 `src/utils/site.ts`：
 *   · ICP 号码已配置；
 *   · 公安联网备案号已配置，留空时 `v-if` 不渲染，不留空链接。
 *
 * 两个链接都加 `rel="noopener noreferrer"`：`target="_blank"` 的新窗口不能
 * 通过 `window.opener` 反向操作本站。
 */
const icp = computed(() => ICP_LICENSE.trim())
const police = computed(() => POLICE_FILING_LICENSE.trim())
</script>

<template>
  <p v-if="icp || police" class="site-filing">
    <a
      v-if="icp"
      :href="ICP_LICENSE_URL"
      target="_blank"
      rel="noopener noreferrer"
      class="filing-link"
    >
      {{ icp }}
    </a>
    <span v-if="icp && police" class="filing-sep" aria-hidden="true">·</span>
    <a
      v-if="police"
      :href="POLICE_FILING_URL"
      target="_blank"
      rel="noopener noreferrer"
      class="filing-link filing-link--police"
    >
      <img
        :src="policeFilingIcon"
        alt=""
        class="filing-icon"
        width="18"
        height="20"
      />
      <span>{{ police }}</span>
    </a>
  </p>
</template>

<style scoped>
.site-filing {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: center;
  gap: var(--space-2);
  font: var(--text-caption);
  color: var(--text-subtle);
  text-align: center;
}

.filing-link {
  color: inherit;
  transition: color var(--transition-fast);
}

.filing-link:hover {
  color: var(--brand-500);
}

/* 公安备案：图标在左、编号在右，图标与文字作为整体换行，不拆开 */
.filing-link--police {
  display: inline-flex;
  align-items: center;
  gap: 5px;
}

.filing-icon {
  display: block;
  flex: none;
  width: 18px;
  height: 20px;
  object-fit: contain;
}

.filing-sep {
  color: var(--text-disabled);
}
</style>
