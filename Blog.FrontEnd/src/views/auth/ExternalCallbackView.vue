<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { completeExternalLogin } from '@/api/externalAuth'
import { useAuthStore } from '@/stores/auth'
import { SiteLogo } from '@/ui'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const errorMsg = ref('')

onMounted(async () => {
  try {
    const session = await completeExternalLogin()
    auth.setSession(session)

    const raw = route.query.returnUrl
    if (typeof raw === 'string' && raw.startsWith('/') && !raw.startsWith('//')) {
      await router.replace(raw)
    } else {
      await router.replace({ name: session.role === 'Admin' ? 'admin-posts' : 'my-posts' })
    }
  } catch {
    errorMsg.value = '第三方登录已失效，请重新登录。'
    await router.replace({ name: 'login', query: { error: 'external_failed' } })
  }
})
</script>

<template>
  <div class="auth-callback">
    <div class="auth-callback-card card">
      <SiteLogo :size="40" />
      <h1 class="panel-title">正在完成登录</h1>
      <p v-if="errorMsg" class="banner banner-error">{{ errorMsg }}</p>
      <p v-else class="hint">正在确认第三方授权，请稍候...</p>
    </div>
  </div>
</template>

<style scoped>
.auth-callback {
  min-height: 100vh;
  display: grid;
  place-items: center;
  padding: var(--space-6);
}

.auth-callback-card {
  width: 100%;
  max-width: 360px;
  padding: var(--space-8);
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--space-3);
  text-align: center;
}
</style>
