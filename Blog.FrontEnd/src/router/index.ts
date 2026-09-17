import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import type { UserRole } from '@/types'

declare module 'vue-router' {
  interface RouteMeta {
    /** 需要登录 */
    requiresAuth?: boolean
    /** 允许的角色；不填则任何已登录用户都可访问 */
    roles?: UserRole[]
    /** 仅未登录可访问（登录/注册页），已登录会被送回首页 */
    guestOnly?: boolean
  }
}

const router = createRouter({
  history: createWebHistory(),
  routes: [
    // 公开站点：统一使用 DefaultLayout（NavBar + Footer + 搜索弹窗）
    {
      path: '/',
      component: () => import('@/layouts/DefaultLayout.vue'),
      children: [
        { path: '', name: 'home', component: () => import('@/views/HomeView.vue') },
        { path: 'post/:id', name: 'post-detail', component: () => import('@/views/PostDetailView.vue') },
        { path: 'tags', name: 'tags', component: () => import('@/views/TagsView.vue') },
        { path: 'categories', name: 'categories', component: () => import('@/views/CategoriesView.vue') },
        { path: 'archive', name: 'archive', component: () => import('@/views/ArchiveView.vue') },
        { path: 'posts', name: 'post-list', component: () => import('@/views/PostListView.vue') },
        { path: 'collections', name: 'collections', component: () => import('@/views/CollectionListView.vue') },
        { path: 'collections/:slug', name: 'collection-detail', component: () => import('@/views/CollectionDetailView.vue') },
      ],
    },

    // 认证页：极简独立布局（不套 NavBar/Footer/AdminLayout）
    // 管理员与作者共用同一个登录页，登录后按返回的 role 跳转
    {
      path: '/login',
      name: 'login',
      component: () => import('@/views/auth/LoginView.vue'),
      meta: { guestOnly: true },
    },

    // 作者工作区：需要登录，Admin 也可进入（便于帮作者处理）
    {
      path: '/me',
      component: () => import('@/layouts/AuthorLayout.vue'),
      meta: { requiresAuth: true, roles: ['Author', 'Admin'] },
      children: [
        { path: '', name: 'my-posts', component: () => import('@/views/me/MyPostListView.vue') },
        { path: 'posts/new', name: 'my-post-new', component: () => import('@/views/me/MyPostNewView.vue') },
        { path: 'posts/:id/edit', name: 'my-post-edit', component: () => import('@/views/me/MyPostEditView.vue') },
        { path: 'profile', name: 'my-profile', component: () => import('@/views/me/MyProfileView.vue') },
      ],
    },

    // 管理后台：独立 AdminLayout，仅管理员
    {
      path: '/admin',
      component: () => import('@/layouts/AdminLayout.vue'),
      meta: { requiresAuth: true, roles: ['Admin'] },
      children: [
        { path: '', name: 'admin-posts', component: () => import('@/views/AdminPostListView.vue') },
        { path: 'posts/new', name: 'admin-post-new', component: () => import('@/views/AdminPostNewView.vue') },
        { path: 'posts/:id/edit', name: 'admin-post-edit', component: () => import('@/views/AdminPostEditView.vue') },
        { path: 'categories', name: 'admin-categories', component: () => import('@/views/AdminCategoryListView.vue') },
        { path: 'tags', name: 'admin-tags', component: () => import('@/views/AdminTagListView.vue') },
        { path: 'users', name: 'admin-users', component: () => import('@/views/AdminUserListView.vue') },
        { path: 'authors', name: 'admin-authors', component: () => import('@/views/AdminAuthorListView.vue') },
        { path: 'collections', name: 'admin-collections', component: () => import('@/views/AdminCollectionListView.vue') },
        { path: 'site', name: 'admin-site', component: () => import('@/views/AdminSiteConfigView.vue') },
      ],
    },

    // 404：真实的不存在页面（此前是静默重定向首页，会产生软 404，见 docs/04-前端设计.md §3）
    { path: '/:pathMatch(.*)*', name: 'not-found', component: () => import('@/views/NotFoundView.vue') },
  ],
  scrollBehavior(_to, _from, savedPosition) {
    return savedPosition ?? { top: 0 }
  },
})

/**
 * 路由守卫。
 *
 * 重要：这只是**前端体验**控制，可被绕过（改 JS 即可）。
 * 真正的安全边界在后端 —— 所有受保护操作后端都会再校验一次角色与资源归属。
 *
 * 关于 `to.meta`：Vue Router 会把**匹配链上所有记录的 meta 合并**到 `to.meta`
 * （父路由的 `requiresAuth` 对子路由同样有效），因此这里不需要自己遍历 `to.matched`。
 * 实测：`/admin/posts/abc/edit` 解析出的 `meta.requiresAuth === true`。
 */
router.beforeEach((to) => {
  const auth = useAuthStore()

  // 本地已知过期：先记下「这次是因为过期」，再清干净。
  //
  // 顺序很重要：`clear()` 会把 expiresAt 抹掉，之后再问「是不是过期了」永远是 false。
  // 必须先取快照。
  //
  // 为什么要在这里清：`isAuthenticated` 已叠加 `!isExpired`（守卫不会再放行），
  // 这里顺手把 localStorage 里的残留 token 也抹掉，避免每次导航重复判断。
  const wasExpired = auth.isExpired
  if (wasExpired) {
    auth.clear()
  }

  // 仅未登录可访问（登录页）：已登录则送回各自首页
  if (to.meta.guestOnly && auth.isAuthenticated) {
    return auth.isAdmin ? { name: 'admin-posts' } : { name: 'my-posts' }
  }

  if (!to.meta.requiresAuth) return true

  if (!auth.isAuthenticated) {
    // 只有一个登录页：无论是 /admin 还是 /me 都回到它，登录后按角色落地
    return {
      name: 'login',
      query: {
        returnUrl: to.fullPath,
        // 过期与「从未登录」给不同提示
        ...(wasExpired ? { reason: 'expired' } : {}),
      },
    }
  }

  const allowed = to.meta.roles
  if (allowed && auth.role && !allowed.includes(auth.role)) {
    // 已登录但角色不足：回到自己的地盘，而不是报错页
    return auth.isAdmin ? { name: 'admin-posts' } : { name: 'my-posts' }
  }

  return true
})

export default router
