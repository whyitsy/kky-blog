<script setup lang="ts">
import { computed } from 'vue'
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
 * 审核通过后同样需要展示。号码配置在 `src/utils/site.ts`：
 *   · ICP 号码已配置；
 *   · 公安联网备案号当前为空（审核中），`v-if` 不渲染，不留空链接。
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
      class="filing-link"
    >
      {{ police }}
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

.filing-sep {
  color: var(--text-disabled);
}
</style>
