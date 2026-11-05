/**
 * 飞书官方二维码 SDK（LarkSSOSDKWebQRCode-1.0.3.js）的最小类型声明。
 * 只在登录页按需加载，不在全站引入。
 */
export {}

declare global {
  interface Window {
    QRLogin?: (options: {
      id: string
      goto: string
      width?: string
      height?: string
      style?: string
    }) => {
      matchOrigin: (origin: string) => boolean
      matchData: (data: unknown) => boolean
    }
  }
}
