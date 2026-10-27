<script setup lang="ts">
import { computed, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { login } from '@/api/auth'
import { ApiError, ErrorCode } from '@/api/http'
import { SiteLogo } from '@/ui'

const router = useRouter()
const auth = useAuthStore()

const email = ref('')
const password = ref('')
const showPassword = ref(false)
const submitting = ref(false)
const errorMsg = ref('')

// 被守卫/到期跳转过来时带 reason=expired：给一句明确解释，
// 否则用户只会看到「莫名其妙回到登录页」。
const expiredNotice = computed(() => router.currentRoute.value.query.reason === 'expired')
const notice = computed(() =>
  expiredNotice.value ? '登录状态已过期，请重新登录后继续。' : '',
)

const title = '登录'
// 登录前无法知道账号角色，因此副标题不区分身份
const subtitle = '登录后进入你的创作工作台或管理后台'

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

      <p v-if="errorMsg" class="banner banner-error">{{ errorMsg }}</p>
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
  max-width: 400px;
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
</style>
