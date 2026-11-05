<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  getExternalAuthBindings,
  getExternalAuthProviders,
  startExternalAuthBind,
  unbindExternalAuth,
} from '@/api/externalAuth'
import type {
  ExternalAuthBinding,
  ExternalAuthProvider,
  ExternalAuthProviderInfo,
} from '@/types'

const route = useRoute()
const router = useRouter()

const providers = ref<ExternalAuthProviderInfo[]>([])
const bindings = ref<ExternalAuthBinding[]>([])
const loading = ref(true)
const busy = ref<ExternalAuthProvider | null>(null)
const errorMsg = ref('')
const okMsg = ref('')

const bindingMap = computed(() => {
  const map = new Map<ExternalAuthProvider, ExternalAuthBinding>()
  for (const b of bindings.value) map.set(b.provider, b)
  return map
})

const bindingNotice = ref('')

onMounted(async () => {
  const status = typeof route.query.binding === 'string' ? route.query.binding : ''
  if (status === 'success') {
    bindingNotice.value = '第三方账号已绑定。'
  } else if (status === 'error') {
    const reason = typeof route.query.reason === 'string' ? route.query.reason : ''
    bindingNotice.value =
      reason === 'bind_conflict'
        ? '该第三方账号已经绑定到其他本站账号，请先解绑原账号。'
        : reason === 'tenant_mismatch'
          ? '该飞书账号不属于允许的企业。'
          : reason === 'invalid_state' || reason === 'state_expired'
            ? '授权已过期，请重新发起绑定。'
            : '绑定失败，请重试。'
  }
  if (status) {
    // 提示放在内存里，URL 清理掉，刷新时不重复弹出
    void router.replace({ name: 'admin-security' })
  }

  await load()
})

async function load() {
  loading.value = true
  errorMsg.value = ''
  try {
    const [providerList, bindingList] = await Promise.all([
      getExternalAuthProviders(),
      getExternalAuthBindings(),
    ])
    providers.value = providerList
    bindings.value = bindingList
  } catch (e) {
    errorMsg.value = e instanceof Error ? e.message : '加载第三方绑定信息失败'
  } finally {
    loading.value = false
  }
}

async function onBind(provider: ExternalAuthProvider) {
  busy.value = provider
  errorMsg.value = ''
  okMsg.value = ''
  try {
    const { url } = await startExternalAuthBind(provider, '/admin/security')
    // 整页跳转；回调完成后回到本页
    window.location.assign(url)
  } catch (e) {
    errorMsg.value = e instanceof Error ? e.message : '发起绑定失败'
    busy.value = null
  }
}

async function onUnbind(provider: ExternalAuthProvider) {
  const name = providers.value.find((p) => p.provider === provider)?.displayName ?? provider
  if (!window.confirm(`确定解绑 ${name} 吗？解绑后仍可使用密码登录。`)) return

  busy.value = provider
  errorMsg.value = ''
  okMsg.value = ''
  try {
    await unbindExternalAuth(provider)
    okMsg.value = `已解绑 ${name}。`
    await load()
  } catch (e) {
    errorMsg.value = e instanceof Error ? e.message : '解绑失败'
  } finally {
    busy.value = null
  }
}

function formatTime(value: string | null): string {
  if (!value) return '—'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleString('zh-CN')
}
</script>

<template>
  <section class="security-page">
    <p class="hint security-intro">
      把当前管理员账号与 GitHub / 飞书绑定。绑定后可以用第三方账号登录；
      本站只保存平台用户标识，不保存昵称、头像、邮箱或第三方 token。
    </p>

    <p v-if="errorMsg" class="banner banner-error">{{ errorMsg }}</p>
    <p v-else-if="okMsg" class="banner banner-success">{{ okMsg }}</p>
    <p v-else-if="bindingNotice" class="banner banner-info">{{ bindingNotice }}</p>

    <div v-if="loading" class="empty">加载中...</div>

    <div v-else-if="!providers.length" class="panel panel-stack empty">
      <h3 class="panel-title">暂未启用第三方登录</h3>
      <p class="hint">请先在部署配置中启用 GitHub 或飞书，并填写对应凭据。</p>
    </div>

    <div v-else class="binding-grid">
      <article v-for="p in providers" :key="p.provider" class="panel binding-card">
        <header class="binding-head">
          <div class="provider-title">
            <span class="provider-mark" aria-hidden="true">
              {{ p.provider === 'GitHub' ? 'GH' : '飞' }}
            </span>
            <div>
              <h2 class="binding-name">{{ p.displayName }}</h2>
              <p class="hint">
                {{ p.supportsQr ? '支持网页跳转登录与扫码登录' : '支持网页跳转登录' }}
              </p>
            </div>
          </div>
          <span class="badge" :class="bindingMap.has(p.provider) ? 'badge-success' : 'badge-info'">
            {{ bindingMap.has(p.provider) ? '已绑定' : '未绑定' }}
          </span>
        </header>

        <dl v-if="bindingMap.get(p.provider)" class="binding-meta">
          <div>
            <dt class="label">绑定时间</dt>
            <dd>{{ formatTime(bindingMap.get(p.provider)?.boundAt ?? null) }}</dd>
          </div>
          <div>
            <dt class="label">最近登录</dt>
            <dd>{{ formatTime(bindingMap.get(p.provider)?.lastLoginAt ?? null) }}</dd>
          </div>
        </dl>

        <div class="binding-actions">
          <button
            v-if="!bindingMap.has(p.provider)"
            type="button"
            class="btn btn-primary"
            :disabled="busy !== null"
            @click="onBind(p.provider)"
          >
            {{ busy === p.provider ? '跳转中...' : `绑定 ${p.displayName}` }}
          </button>
          <button
            v-else
            type="button"
            class="btn btn-ghost"
            :disabled="busy !== null"
            @click="onUnbind(p.provider)"
          >
            {{ busy === p.provider ? '处理中...' : '解绑' }}
          </button>
        </div>
      </article>
    </div>
  </section>
</template>

<style scoped>
.security-page {
  display: flex;
  flex-direction: column;
  gap: var(--space-5);
}

.security-intro {
  max-width: 760px;
}

.binding-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
  gap: var(--space-4);
}

.binding-card {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  min-height: 210px;
}

.binding-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: var(--space-3);
}

.provider-title {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  min-width: 0;
}

.provider-mark {
  display: inline-grid;
  place-items: center;
  width: 40px;
  height: 40px;
  flex: 0 0 40px;
  border-radius: var(--radius-pill);
  background: var(--bg-sunken);
  color: var(--text-strong);
  font: var(--text-body-sm);
  font-weight: 700;
}

.binding-name {
  margin: 0 0 var(--space-1);
  font: var(--text-h3);
  color: var(--text-strong);
}

.binding-meta {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: var(--space-3);
  margin: 0;
  padding: var(--space-3);
  border-radius: var(--radius-sm);
  background: var(--bg-sunken);
}

.binding-meta dt {
  margin-bottom: var(--space-1);
}

.binding-meta dd {
  margin: 0;
  font: var(--text-body-sm);
  color: var(--text-strong);
}

.binding-actions {
  margin-top: auto;
  display: flex;
  justify-content: flex-end;
}

@media (max-width: 640px) {
  .binding-meta {
    grid-template-columns: 1fr;
  }
}
</style>
