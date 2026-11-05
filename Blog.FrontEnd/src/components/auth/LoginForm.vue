<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { login } from '@/api/auth'
import { getExternalAuthProviders, startFeishuQr } from '@/api/externalAuth'
import { ApiError, ErrorCode } from '@/api/http'
import { SiteLogo } from '@/ui'
import type { ExternalAuthProvider, ExternalAuthProviderInfo } from '@/types'

const router = useRouter()
const auth = useAuthStore()

const email = ref('')
const password = ref('')
const showPassword = ref(false)
const submitting = ref(false)
const errorMsg = ref('')

// 第三方入口
const providers = ref<ExternalAuthProviderInfo[]>([])
const externalBusy = ref(false)
const qrOpen = ref(false)
const qrLoading = ref(false)
const qrError = ref('')

let qrHandle: ReturnType<NonNullable<Window['QRLogin']>> | null = null
let qrListener: ((event: MessageEvent) => void) | null = null
let qrScriptPromise: Promise<void> | null = null

const FEISHU_QR_SDK =
  'https://lf-package-cn.feishucdn.com/obj/feishu-static/lark/passport/qrcode/LarkSSOSDKWebQRCode-1.0.3.js'

const feishuProvider = computed(() =>
  providers.value.find((p) => p.provider === 'Feishu'),
)

// 被守卫/到期跳转过来时带 reason=expired：给一句明确解释，
// 否则用户只会看到「莫名其妙回到登录页」。
const expiredNotice = computed(() => router.currentRoute.value.query.reason === 'expired')
const notice = computed(() =>
  expiredNotice.value ? '登录状态已过期，请重新登录后继续。' : '',
)

/** 回调失败时后端只回错误码，不把第三方细节暴露到 URL。 */
const externalError = computed(() => {
  const raw = router.currentRoute.value.query.error
  const code = typeof raw === 'string' ? raw : ''
  switch (code) {
    case 'external_unbound':
      return '该第三方账号尚未绑定本站账号。请先用邮箱密码登录，在「账号安全」中绑定后再使用。'
    case 'external_denied':
      return '你取消了第三方授权，可以重新尝试或使用密码登录。'
    case 'external_unavailable':
      return '第三方登录暂时不可用，请稍后再试。'
    case 'tenant_mismatch':
      return '该飞书账号不属于允许的企业，无法登录。'
    case 'account_unavailable':
      return '该账号当前不可用，请联系管理员。'
    default:
      return code ? '第三方登录失败，请重试或使用密码登录。' : ''
  }
})

const title = '登录'
// 登录前无法知道账号角色，因此副标题不区分身份
const subtitle = '登录后进入你的创作工作台或管理后台'

onMounted(async () => {
  try {
    providers.value = await getExternalAuthProviders()
  } catch {
    // 后端未启动/接口不可用时只隐藏第三方入口，不影响密码登录
  }
})

onUnmounted(cleanupQr)

/** 当前登录页的 returnUrl；只接受站内路径，和后端保持同一约束。 */
function currentReturnUrl(): string {
  const raw = router.currentRoute.value.query.returnUrl
  return typeof raw === 'string' && raw.startsWith('/') && !raw.startsWith('//') ? raw : '/'
}

function startProvider(provider: ExternalAuthProvider) {
  externalBusy.value = true
  errorMsg.value = ''
  window.location.assign(
    `/api/auth/external/${provider}/start?returnUrl=${encodeURIComponent(currentReturnUrl())}`,
  )
}

async function toggleQr() {
  if (qrOpen.value) {
    qrOpen.value = false
    cleanupQr()
    return
  }

  qrOpen.value = true
  await nextTick()
  qrLoading.value = true
  qrError.value = ''

  try {
    const { url } = await startFeishuQr(currentReturnUrl())
    await loadFeishuSdk()

    const container = document.getElementById('feishu-qr')
    if (!container) throw new Error('二维码容器未准备好')
    container.innerHTML = ''

    const handle = window.QRLogin?.({
      id: 'feishu-qr',
      goto: url,
      width: '260',
      height: '260',
      style: 'width:260px;height:260px;border:0;background:transparent',
    })
    if (!handle) throw new Error('飞书二维码 SDK 未加载')
    qrHandle = handle

    qrListener = (event: MessageEvent) => {
      try {
        if (!qrHandle?.matchOrigin(event.origin) || !qrHandle.matchData(event.data)) return

        const data = event.data as { tmp_code?: string } | string | null
        const tmpCode = typeof data === 'string' ? data : data?.tmp_code
        if (!tmpCode) return

        // 官方 SDK 的约定：把 tmp_code 拼回授权 URL，飞书会 302 到本站 callback
        window.location.assign(`${url}&tmp_code=${encodeURIComponent(tmpCode)}`)
      } catch {
        // 第三方 postMessage 数据不可信，校验失败直接忽略
      }
    }
    window.addEventListener('message', qrListener)
  } catch (e) {
    qrError.value = e instanceof Error ? e.message : '二维码加载失败，请改用网页跳转登录'
  } finally {
    qrLoading.value = false
  }
}

function loadFeishuSdk(): Promise<void> {
  if (window.QRLogin) return Promise.resolve()
  if (qrScriptPromise) return qrScriptPromise

  qrScriptPromise = new Promise<void>((resolve, reject) => {
    const script = document.createElement('script')
    script.src = FEISHU_QR_SDK
    script.async = true
    script.onload = () => resolve()
    script.onerror = () => {
      qrScriptPromise = null
      reject(new Error('飞书二维码 SDK 加载失败，请改用网页跳转登录'))
    }
    document.head.appendChild(script)
  })

  return qrScriptPromise
}

function cleanupQr() {
  if (qrListener) {
    window.removeEventListener('message', qrListener)
    qrListener = null
  }
  qrHandle = null
}

async function onSubmit() {
  errorMsg.value = ''

  if (!email.value.trim() || !password.value) {
    errorMsg.value = '请填写邮箱与密码'
    return
  }

  submitting.value = true
  try {
    const payload = { email: email.value.trim(), password: password.value }
    const res = await login(payload)
    auth.setSession(res)

    // 登录成功回到来源页；没有来源页时按角色进各自首页
    const returnUrl = router.currentRoute.value.query.returnUrl
    if (typeof returnUrl === 'string' && returnUrl.startsWith('/')) {
      await router.replace(returnUrl)
    } else {
      await router.replace({ name: res.role === 'Admin' ? 'admin-posts' : 'my-posts' })
    }
  } catch (e) {
    // 后端对「账号不存在/密码错误」统一返回 4010，不区分原因（防账号枚举）
    if (e instanceof ApiError && e.code === ErrorCode.Unauthorized) {
      errorMsg.value = '邮箱或密码错误'
    } else {
      errorMsg.value = e instanceof Error ? e.message : '登录失败'
    }
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="auth-page">
    <form class="auth-card card" @submit.prevent="onSubmit">
      <header class="auth-head">
        <SiteLogo :size="40" />
        <h1 class="panel-title">{{ title }}</h1>
        <p class="hint">{{ subtitle }}</p>
      </header>

      <p v-if="errorMsg || externalError" class="banner banner-error">
        {{ errorMsg || externalError }}
      </p>
      <p v-else-if="notice" class="banner banner-info">{{ notice }}</p>

      <label class="field">
        <span class="label">邮箱</span>
        <input
          v-model="email"
          class="input"
          type="email"
          autocomplete="username"
          placeholder="you@example.com"
          :disabled="submitting"
        />
      </label>

      <label class="field">
        <span class="label">密码</span>
        <span class="pw-wrap">
          <input
            v-model="password"
            class="input"
            :type="showPassword ? 'text' : 'password'"
            autocomplete="current-password"
            placeholder="请输入密码"
            :disabled="submitting"
          />
          <button
            type="button"
            class="link pw-toggle"
            :aria-label="showPassword ? '隐藏密码' : '显示密码'"
            @click="showPassword = !showPassword"
          >
            {{ showPassword ? '隐藏' : '显示' }}
          </button>
        </span>
      </label>

      <button type="submit" class="btn btn-primary btn-block" :disabled="submitting">
        {{ submitting ? '登录中...' : '登录' }}
      </button>

      <template v-if="providers.length">
        <div class="auth-divider"><span>或使用第三方账号</span></div>

        <div class="external-list">
          <button
            v-for="p in providers"
            :key="p.provider"
            type="button"
            class="btn btn-ghost btn-block external-btn"
            :disabled="externalBusy || submitting"
            @click="startProvider(p.provider)"
          >
            <span class="provider-mark" aria-hidden="true">
              {{ p.provider === 'GitHub' ? 'GH' : '飞' }}
            </span>
            <span>使用 {{ p.displayName }} 登录</span>
          </button>
        </div>

        <div v-if="feishuProvider?.supportsQr" class="qr-toggle-row">
          <button type="button" class="link qr-toggle" @click="toggleQr">
            {{ qrOpen ? '收起扫码登录' : '使用飞书扫码登录' }}
          </button>
        </div>

        <div v-if="qrOpen" class="qr-panel">
          <div id="feishu-qr" class="qr-box" :class="{ 'qr-box-busy': qrLoading }"></div>
          <p v-if="qrLoading" class="hint">二维码加载中...</p>
          <p v-if="qrError" class="banner banner-error">{{ qrError }}</p>
          <p class="hint qr-hint">打开飞书 App，扫描二维码并在手机上确认登录。</p>
        </div>
      </template>

      <footer class="auth-foot">
        <RouterLink to="/" class="link">返回首页</RouterLink>
      </footer>

      <!-- 没有注册页：账号由管理员在后台创建 -->
      <p class="hint auth-note">
        没有账号？账号由管理员在「账号管理」中创建后发放。管理员与作者使用同一个登录入口。
      </p>
    </form>
  </div>
</template>

<style scoped>
.auth-page {
  display: grid;
  place-items: center;
  min-height: 100vh;
  padding: var(--space-6);
}

.auth-card {
  width: 100%;
  max-width: 420px;
  padding: var(--space-8);
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
}

.auth-head {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--space-2);
  text-align: center;
  margin-bottom: var(--space-2);
}

.pw-wrap {
  position: relative;
  display: block;
}
.pw-wrap .input {
  padding-right: 64px;
}
.pw-toggle {
  position: absolute;
  top: 50%;
  right: var(--space-2);
  transform: translateY(-50%);
}

.auth-divider {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  color: var(--text-subtle);
  font: var(--text-caption);
}
.auth-divider::before,
.auth-divider::after {
  content: '';
  flex: 1;
  height: 1px;
  background: var(--border-subtle);
}

.external-list {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
}

.external-btn {
  justify-content: flex-start;
  gap: var(--space-3);
}

.provider-mark {
  display: inline-grid;
  place-items: center;
  width: 24px;
  height: 24px;
  flex: 0 0 24px;
  border-radius: var(--radius-pill);
  background: var(--bg-sunken);
  color: var(--text-strong);
  font: var(--text-caption);
  font-weight: 700;
}

.qr-toggle-row {
  display: flex;
  justify-content: center;
}
.qr-toggle {
  font: var(--text-body-sm);
}

.qr-panel {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--space-2);
  padding-top: var(--space-2);
  border-top: 1px solid var(--border-subtle);
}

.qr-box {
  width: 260px;
  max-width: 100%;
  min-height: 260px;
  display: grid;
  place-items: center;
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-md);
  background: var(--bg-surface);
  overflow: hidden;
}
.qr-box-busy {
  opacity: 0.6;
}

.qr-hint {
  text-align: center;
}

.auth-foot {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: var(--space-2);
  flex-wrap: wrap;
}

.auth-note {
  text-align: center;
}

@media (max-width: 480px) {
  .auth-card {
    padding: var(--space-6);
  }
  .qr-box {
    width: 220px;
    min-height: 220px;
  }
}
</style>
