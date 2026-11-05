import { del, get, post } from './http'
import type {
  ExternalAuthBinding,
  ExternalAuthProvider,
  ExternalAuthProviderInfo,
  LoginResponse,
} from '@/types'

/**
 * 第三方登录接口。
 *
 * 注意：网页跳转登录由后端 `GET /api/auth/external/{provider}/start` 直接 302 到 provider，
 * 不走这里；这里只负责「读取能力 / 绑定 / 扫码 URL / 用一次性票据换 JWT」。
 */

export function getExternalAuthProviders(): Promise<ExternalAuthProviderInfo[]> {
  return get<ExternalAuthProviderInfo[]>('/api/auth/providers')
}

export function getExternalAuthBindings(): Promise<ExternalAuthBinding[]> {
  return get<ExternalAuthBinding[]>('/api/auth/external/bindings')
}

/** 管理员绑定：fetch 带 Bearer 拿授权 URL，再整页跳转 */
export function startExternalAuthBind(
  provider: ExternalAuthProvider,
  returnUrl: string,
): Promise<{ url: string }> {
  return post<{ url: string }>(`/api/auth/external/${provider}/bind/start`, { returnUrl })
}

/** 飞书扫码：拿旧版 passport 授权 URL，前端交给官方 QR SDK */
export function startFeishuQr(returnUrl: string): Promise<{ url: string }> {
  return post<{ url: string }>('/api/auth/external/feishu/qr/start', { returnUrl })
}

/** 用 callback 设置的 HttpOnly 一次性票据换取本站 JWT */
export function completeExternalLogin(): Promise<LoginResponse> {
  return post<LoginResponse>('/api/auth/external/complete')
}

/** 解绑当前账号的某个平台 */
export function unbindExternalAuth(provider: ExternalAuthProvider): Promise<null> {
  return del<null>(`/api/auth/external/bindings/${provider}`)
}
