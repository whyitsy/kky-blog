import { defineStore } from 'pinia'
import { getMe } from '@/api/auth'
import type { AuthUser, LoginResponse, UserRole } from '@/types'

/**
 * token 与用户信息的持久化键。
 *
 * 存 localStorage 的权衡（见 learn/01-后端知识地图.md §8.4）：
 *   - 优点：不会自动随请求发送，因此**不引入 CSRF 问题**
 *   - 缺点：JS 可读，一旦 XSS 即被窃取 → 用「短有效期(30min)」+ CSP 补偿
 * token 过期后 `restore()` 会清掉本地状态。
 */
const TOKEN_KEY = 'blog-auth-token'
const USER_KEY = 'blog-auth-user'
const EXPIRES_KEY = 'blog-auth-expires'

/**
 * 到期检查的轮询间隔。
 *
 * 为什么轮询而不是直接 `setTimeout(到期时刻 - now)`：
 * 单次 setTimeout 的延迟上限是 2^31-1 毫秒（约 24.8 天）。有效期短（30 分钟）时没事，
 * 但一旦把 `Jwt:AccessTokenMinutes` 调大就会**立刻溢出**，定时器变成「马上触发」，
 * 于是刚登录就被踢出去 —— 一个只有改配置才会暴露的隐蔽缺陷。
 * 固定间隔轮询既没有这个上限问题，实现也更简单。
 */
const EXPIRY_POLL_MS = 30_000

/**
 * 到期处理器（由 `main.ts` 注入：清理登录态 + 跳登录页）。
 *
 * 用注入而不是在 store 里 `import router`：那会造成 store ↔ router 循环引用
 * （router 的守卫要用 store）—— 与 `api/http.ts` 的 `configureHttp` 同一套做法。
 */
let expiryHandler: (() => void) | null = null

export function configureAuthExpiry(handler: () => void) {
  expiryHandler = handler
}

function readUser(): AuthUser | null {
  const raw = localStorage.getItem(USER_KEY)
  if (!raw) return null
  try {
    return JSON.parse(raw) as AuthUser
  } catch {
    return null
  }
}

export const useAuthStore = defineStore('auth', {
  state: () => ({
    token: localStorage.getItem(TOKEN_KEY) ?? '',
    user: readUser(),
    expiresAt: localStorage.getItem(EXPIRES_KEY) ?? '',
    /** 是否已向后端确认过 token 有效性（避免每次导航都请求 /me） */
    verified: false,
    /**
     * 到期轮询的定时器 id（null = 未在轮询）。
     * 用 `number` 而不是 `NodeJS.Timeout`：这是浏览器环境，
     * `setInterval` 返回数字；类型断言在 `scheduleExpiryWatch` 里完成。
     */
    expiryTimer: null as number | null,
  }),

  getters: {
    /**
     * token 是否**本地已过期**。
     *
     * 只看本地时间戳，不请求后端 —— 它要能被路由守卫在每次导航时同步调用。
     * 注意这是「已过期」而不是「仍有效」：时钟偏差、后端提前作废（改密/停用/注销）
     * 都不在这里判断，那些由后端 401 + onUnauthorized 兜底。
     */
    isExpired: (s): boolean =>
      Boolean(s.expiresAt) && new Date(s.expiresAt).getTime() <= Date.now(),

    /**
     * 是否处于可用的登录态。
     *
     * ⚠️ **必须叠加 `!isExpired`**：只看 `token && user` 的话，token 过期后本地
     * 依然算「已登录」，守卫会放行到后台页面，然后由某个 API 的 401 才把人踢走 ——
     * 表现为「后台页面进去了但一片空白/无权限」，而不是干净地重定向到登录页。
     */
    isAuthenticated(): boolean {
      return Boolean(this.token && this.user) && !this.isExpired
    },

    role: (s): UserRole | null => s.user?.role ?? null,

    isAdmin(): boolean {
      return this.role === 'Admin'
    },

    /** 能写内容的人：管理员或作者（对应后端 ContentWriter 策略） */
    canWriteContent(): boolean {
      return this.role === 'Admin' || this.role === 'Author'
    },

    displayName: (s) => s.user?.authorName || s.user?.email || '',
  },

  actions: {
    /**
     * 保存登录结果。
     *
     * 顺带启动到期轮询 —— 放在这里而不是让调用方记得调 `scheduleExpiryWatch`：
     * `setSession` 是「会话开始」的唯一入口，把两件事绑在一起才不会漏。
     */
    setSession(data: LoginResponse) {
      this.token = data.token
      this.user = data.user
      this.expiresAt = data.expiresAt
      this.verified = true

      localStorage.setItem(TOKEN_KEY, data.token)
      localStorage.setItem(USER_KEY, JSON.stringify(data.user))
      localStorage.setItem(EXPIRES_KEY, data.expiresAt)

      this.scheduleExpiryWatch()
    },

    /** 清空本地登录状态（不请求后端） */
    clear() {
      this.stopExpiryWatch()

      this.token = ''
      this.user = null
      this.expiresAt = ''
      this.verified = false

      localStorage.removeItem(TOKEN_KEY)
      localStorage.removeItem(USER_KEY)
      localStorage.removeItem(EXPIRES_KEY)
    },

    /**
     * 安排「到期时自动登出」的轮询（幂等：重复调用只会保留一个定时器）。
     *
     * 覆盖的场景：用户**停在某个后台页面不动**。没有它的话，页面会一直显示着旧数据，
     * 直到用户点了什么触发请求、拿到 401 才跳走。
     */
    scheduleExpiryWatch() {
      this.stopExpiryWatch()

      if (!this.token || !this.expiresAt) return

      this.expiryTimer = setInterval(() => {
        if (!this.isExpired) return

        this.clear() // 内部会停掉轮询
        expiryHandler?.()
      }, EXPIRY_POLL_MS) as unknown as number
    },

    /** 停止到期轮询 */
    stopExpiryWatch() {
      if (this.expiryTimer === null) return
      clearInterval(this.expiryTimer)
      this.expiryTimer = null
    },

    /**
     * 启动/刷新时恢复会话：本地有 token 就向后端确认一次。
     * 这样能正确处理「token 未过期但账号已被停用/改密」的情况
     * （后端 TokenVersion 校验会拒绝，/me 返回 401）。
     */
    async restore() {
      if (!this.token) {
        this.clear()
        return
      }

      // 本地已知过期 -> 直接清理，省一次请求
      if (this.isExpired) {
        this.clear()
        return
      }

      try {
        const me = await getMe()
        this.user = me
        localStorage.setItem(USER_KEY, JSON.stringify(me))
        this.verified = true
      } catch {
        // 401（未认证/已过期/被踢下线）或其他失败：清空本地状态
        this.clear()
      }
    },
  },
})
