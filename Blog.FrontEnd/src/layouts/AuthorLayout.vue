<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink, RouterView, useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { logout as logoutApi } from '@/api/auth'
import { SiteLogo, ThemeSwitcher } from '@/ui'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

// 管理员进作者工作区时，返回入口应是后台
const homeTarget = computed(() => (auth.isAdmin ? '/admin' : '/'))
const homeLabel = computed(() => (auth.isAdmin ? '管理后台' : '返回首页'))

async function onLogout() {
  try {
    // 后端会提升 TokenVersion，使该账号所有旧 token 立即失效
    await logoutApi()
  } catch {
    /* 即使请求失败也继续清理本地状态，避免卡在「登不出去」 */
  }
  auth.clear()
  await router.replace({ name: 'login' })
}
</script>

<template>
  <div class="author-shell">
    <header class="author-bar">
      <div class="bar-inner">
        <RouterLink to="/" class="brand">
          <SiteLogo :size="30" />
          <span class="brand-name">作者工作区</span>
        </RouterLink>

        <nav class="bar-nav">
          <RouterLink
            to="/me"
            class="console-nav-item"
            :class="{ active: route.name === 'my-posts' }"
          >
            我的文章
          </RouterLink>
          <RouterLink
            to="/me/profile"
            class="console-nav-item"
            :class="{ active: route.name === 'my-profile' }"
          >
            个人资料
          </RouterLink>
        </nav>

        <div class="bar-actions">
          <span class="who">
            <span class="who-name">{{ auth.displayName }}</span>
            <span class="hint">{{ auth.role }}</span>
          </span>
          <button class="btn btn-ghost btn-sm" @click="router.push(homeTarget)">
            {{ homeLabel }}
          </button>
          <button class="btn btn-ghost btn-sm" @click="onLogout">退出登录</button>
        </div>
      </div>
    </header>

    <main class="author-content">
      <RouterView v-slot="{ Component }">
        <Transition name="fade" mode="out-in">
          <component :is="Component" />
        </Transition>
      </RouterView>
    </main>

    <ThemeSwitcher />
  </div>
</template>

<style scoped>
.author-shell {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-height: 100vh;
}

.author-bar {
  border-bottom: 1px solid var(--border-subtle);
  background: var(--bg-surface);
}

.bar-inner {
  display: flex;
  align-items: center;
  gap: var(--space-6);
  max-width: var(--size-container);
  margin: 0 auto;
  padding: var(--space-3) var(--space-6);
  flex-wrap: wrap;
}

.brand {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  font-weight: 700;
  color: var(--text-strong);
}

.brand-name {
  font: var(--text-body-sm);
}

.bar-nav {
  display: flex;
  gap: var(--space-2);
  flex: 1;
}
.bar-nav .console-nav-item {
  padding: var(--space-2) var(--space-3);
}

.bar-actions {
  display: flex;
  align-items: center;
  gap: var(--space-3);
}

.who {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  line-height: 1.3;
}
.who-name {
  font: var(--text-body-sm);
  font-weight: 600;
  color: var(--text-strong);
}

.author-content {
  flex: 1;
  width: 100%;
  max-width: var(--size-container);
  margin: 0 auto;
  padding: var(--space-6) var(--space-6) var(--space-16);
}

@media (max-width: 640px) {
  .bar-inner {
    gap: var(--space-3);
    padding: var(--space-3) var(--space-4);
  }
  .who {
    display: none;
  }
}
</style>
