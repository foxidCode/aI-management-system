import { createRouter, createWebHistory } from 'vue-router'
import Login from '../views/Login.vue'
import Register from '../views/Register.vue'
import Dashboard from '../views/Dashboard.vue'
import HomePage from '../views/HomePage.vue'
import UserProfile from '../views/UserProfile.vue'
import UserManagement from '../views/UserManagement.vue'
import RoleManagement from '../views/RoleManagement.vue'
import MaterialDictionary from '../views/MaterialDictionary.vue'
import InboundOrder from '../views/InboundOrder.vue'
import InboundOrderDetail from '../views/InboundOrderDetail.vue'
import OAuthCallback from '../views/OAuthCallback.vue'

const routes = [
  { path: '/', redirect: '/login' },
  { path: '/login', component: Login },
  { path: '/register', component: Register },
  { path: '/callback', component: OAuthCallback },
  {
    path: '/dashboard',
    component: Dashboard,
    meta: { requiresAuth: true },
    children: [
      { path: '', redirect: '/dashboard/home' },
      { path: 'home', component: HomePage },
      { path: 'profile', component: UserProfile },
      { path: 'users', component: UserManagement },
      { path: 'roles', component: RoleManagement },
      { path: 'materials', component: MaterialDictionary },
      { path: 'inbound', component: InboundOrder },
      { path: 'inbound/detail/:id?', component: InboundOrderDetail },
      { path: 'sso', component: () => import('../views/SsoManagement.vue') },
      { path: 'oauth-clients', component: () => import('../views/OAuthClientManagement.vue') },
      { path: 'home-config', component: () => import('../views/HomeConfig.vue') },
      { path: 'attachments', component: () => import('../views/AttachmentManagement.vue') },
      { path: 'database', component: () => import('../views/DatabaseManagement.vue') },
      { path: 'integration', component: () => import('../views/IntegrationManagement.vue') },
      { path: 'schedule', component: () => import('../views/ScheduleManagement.vue') },
    ]
  },
]

const router = createRouter({
  history: createWebHistory(),
  routes,
})

// 路由守卫：未登录跳转登录页，处理 SSO Token / OAuth 回调
router.beforeEach(async (to, from, next) => {
  const token = localStorage.getItem('token')
  const ssoToken = to.query.sso_token

  // /callback 路径始终放行（OAuth 回调）
  if (to.path === '/callback') {
    next()
    return
  }

  // 已登录：忽略 sso_token，清理 URL 后直接放行
  if (token) {
    if (ssoToken) {
      const query = { ...to.query }
      delete query.sso_token
      next({ path: to.path, query, replace: true })
      return
    }
    if (to.meta.requiresAuth) {
      next()
    } else {
      next()
    }
    return
  }

  // 未登录 + 有 sso_token：尝试 SSO 一键登录
  if (ssoToken) {
    try {
      const { ssoLogin } = await import('../api/auth')
      const res = await ssoLogin(ssoToken)
      if (res.data.success) {
        localStorage.setItem('token', res.data.data.token)
        localStorage.setItem('user', JSON.stringify(res.data.data))
        localStorage.setItem('permissions', JSON.stringify(res.data.data.permissions || []))
        // 清理 URL 中的 sso_token
        const query = { ...to.query }
        delete query.sso_token
        next({ path: '/dashboard', query, replace: true })
        return
      }
    } catch (err) {
      const msg = err.response?.data?.message || 'SSO链接无效或已过期'
      next({ path: '/login', query: { sso_error: msg } })
      return
    }
  }

  // 未登录 + 无 sso_token：常规鉴权
  if (to.meta.requiresAuth) {
    next('/login')
  } else {
    next()
  }
})

export default router
