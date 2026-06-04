import axios from 'axios'

const api = axios.create({
  baseURL: '/api',
  timeout: 10000,
})

// 请求拦截器：自动附加 JWT Token
api.interceptors.request.use(config => {
  const token = localStorage.getItem('token')
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

// 响应拦截器：Token 过期跳转登录
api.interceptors.response.use(
  response => response,
  error => {
    if (error.response?.status === 401) {
      localStorage.removeItem('token')
      localStorage.removeItem('user')
      window.location.href = '/login'
    }
    return Promise.reject(error)
  }
)

export function login(username, password) {
  return api.post('/auth/login', { username, password })
}

export function register(username, password, email) {
  return api.post('/auth/register', { username, password, email })
}

export function getUserProfile() {
  return api.get('/user/profile')
}

export function updateUserProfile(data) {
  return api.put('/user/profile', data)
}

// ========== 人员管理 API ==========

export function getUsers(params = {}) {
  return api.get('/usermanagement', { params })
}

export function createUser(data) {
  return api.post('/usermanagement', data)
}

export function updateUser(id, data) {
  return api.put(`/usermanagement/${id}`, data)
}

export function resetPassword(userIds) {
  return api.post('/usermanagement/reset-password', userIds)
}

export function batchFreeze(userIds, freeze) {
  return api.post('/usermanagement/batch-freeze', { userIds, freeze })
}

export function kickUsers(userIds) {
  return api.post('/usermanagement/kick', userIds)
}

export function deleteUser(id) {
  return api.delete(`/usermanagement/${id}`)
}

export function batchDeleteUsers(userIds) {
  return api.post('/usermanagement/batch-delete', userIds)
}

// ========== 忘记密码 ==========

export function sendResetCode(email) {
  return api.post('/auth/send-reset-code', { email })
}

export function forgotPassword(email, code, newPassword) {
  return api.post('/auth/reset-password', { email, code, newPassword })
}

// ========== 角色管理 API ==========

export function getRoles(params = {}) {
  return api.get('/role', { params })
}

export function getRoleById(id) {
  return api.get(`/role/${id}`)
}

export function createRole(data) {
  return api.post('/role', data)
}

export function updateRole(id, data) {
  return api.put(`/role/${id}`, data)
}

export function deleteRole(id) {
  return api.delete(`/role/${id}`)
}

// ========== 权限列表 API ==========

export function getPermissions() {
  return api.get('/permission')
}

export function getPermissionTree() {
  return api.get('/permission/tree')
}

// ========== 菜单 API ==========

export function getUserMenus() {
  return api.get('/menu')
}

export function getAllMenus() {
  return api.get('/menu/all')
}

// ========== 用户角色 API ==========

export function getUserRoles(userId) {
  return api.get(`/usermanagement/${userId}/roles`)
}

export function assignUserRoles(userId, roleIds) {
  return api.put(`/usermanagement/${userId}/roles`, { roleIds })
}

// ========== 材料字典 API ==========

export function getMaterials(params = {}) {
  return api.get('/material', { params })
}

export function getMaterialById(id) {
  return api.get(`/material/${id}`)
}

export function createMaterial(data) {
  return api.post('/material', data)
}

export function updateMaterial(id, data) {
  return api.put(`/material/${id}`, data)
}

export function deleteMaterial(id) {
  return api.delete(`/material/${id}`)
}

export function batchDeleteMaterials(ids) {
  return api.post('/material/batch-delete', ids)
}

// ========== 入库单 API ==========

export function getInboundOrders(params = {}) {
  return api.get('/inboundorder', { params })
}

export function getInboundOrderById(id) {
  return api.get(`/inboundorder/${id}`)
}

export function createInboundOrder(data) {
  return api.post('/inboundorder', data)
}

export function updateInboundOrder(id, data) {
  return api.put(`/inboundorder/${id}`, data)
}

export function syncInboundDetails(orderId, details) {
  return api.put(`/inboundorder/${orderId}/details/sync`, details)
}

export function deleteInboundOrder(id) {
  return api.delete(`/inboundorder/${id}`)
}

// 明细
export function getInboundDetails(orderId, params = {}) {
  return api.get(`/inboundorder/${orderId}/details`, { params })
}

export function addInboundDetail(orderId, data) {
  return api.post(`/inboundorder/${orderId}/details`, data)
}

export function updateInboundDetail(detailId, data) {
  return api.put(`/inboundorder/details/${detailId}`, data)
}

export function deleteInboundDetail(detailId) {
  return api.delete(`/inboundorder/details/${detailId}`)
}

export function batchDeleteInboundDetails(detailIds) {
  return api.post('/inboundorder/details/batch-delete', detailIds)
}

// ========== 附件 API ==========

export function getAttachments(orderId, params = {}) {
  return api.get(`/inboundorder/${orderId}/attachments`, { params })
}

export function uploadAttachments(orderId, formData) {
  return api.post(`/inboundorder/${orderId}/attachments`, formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
  })
}

export function downloadAttachmentUrl(attachmentId) {
  return `/api/inboundorder/attachments/${attachmentId}/download`
}

export function deleteAttachment(attachmentId) {
  return api.delete(`/inboundorder/attachments/${attachmentId}`)
}

// 导出
export function exportInboundOrders(params = {}) {
  return api.get('/inboundorder/export', { params, responseType: 'blob' })
}

// ========== SSO 单点登录 API ==========

export function ssoLogin(ssoToken) {
  return api.post('/sso/login', { token: ssoToken })
}

export function createSsoToken(data) {
  return api.post('/sso/create', data)
}

export function getSsoTokens(params) {
  return api.get('/sso/tokens', { params })
}

export function revokeSsoToken(id) {
  return api.post(`/sso/revoke/${id}`)
}

// ========== SSO 授权码登录 API ==========

export function loginByAuthCode(code) {
  return api.post('/sso/login-by-code', { code })
}

export function createAuthCode(data) {
  return api.post('/sso/create-auth-code', data)
}

// ========== OAuth 2.0 / OIDC API ==========

export function exchangeCodeForToken(data) {
  return api.post('/oauth/token', {
    grantType: data.grantType,
    code: data.code,
    codeVerifier: data.codeVerifier,
    clientId: data.clientId,
    redirectUri: data.redirectUri,
    ...(data.grantType === 'refresh_token' ? { refreshToken: data.refreshToken } : {}),
  })
}

export function refreshAccessToken(refreshToken) {
  return api.post('/oauth/token', {
    grantType: 'refresh_token',
    refreshToken: refreshToken,
    clientId: 'vue-spa',
  })
}

export function getUserInfo() {
  return api.get('/oauth/userinfo')
}

export function revokeOAuthToken(token, tokenTypeHint) {
  return api.post('/oauth/revoke', { token, tokenTypeHint })
}

// ========== OAuth 客户端管理 API ==========

export function getOAuthClients(params) {
  return api.get('/oauth/clients', { params })
}

export function getOAuthClientById(id) {
  return api.get(`/oauth/clients/${id}`)
}

export function createOAuthClient(data) {
  return api.post('/oauth/clients', data)
}

export function updateOAuthClient(id, data) {
  return api.put(`/oauth/clients/${id}`, data)
}

export function deleteOAuthClient(id) {
  return api.delete(`/oauth/clients/${id}`)
}

// ========== 集成平台 API ==========

export function getIntegrationConnections() { return api.get('/integration/connections') }
export function createIntegrationConnection(data) { return api.post('/integration/connections', data) }
export function updateIntegrationConnection(id, data) { return api.put(`/integration/connections/${id}`, data) }
export function deleteIntegrationConnection(id) { return api.delete(`/integration/connections/${id}`) }
export function testIntegrationConnection(data) { return api.post('/integration/connections/test', data) }

export function getIntegrationTasks() { return api.get('/integration/tasks') }
export function createIntegrationTask(data) { return api.post('/integration/tasks', data) }
export function updateIntegrationTask(id, data) { return api.put(`/integration/tasks/${id}`, data) }
export function deleteIntegrationTask(id) { return api.delete(`/integration/tasks/${id}`) }
export function executeIntegrationTask(id) { return api.post(`/integration/tasks/${id}/execute`) }
export function getIntegrationLogs(params) { return api.get('/integration/logs', { params }) }

// ========== 计划任务 API ==========
export function getScheduledTasks() { return api.get('/schedule') }
export function createScheduledTask(data) { return api.post('/schedule', data) }
export function updateScheduledTask(id, data) { return api.put(`/schedule/${id}`, data) }
export function deleteScheduledTask(id) { return api.delete(`/schedule/${id}`) }
export function triggerScheduledTask(id) { return api.post(`/schedule/${id}/trigger`) }
export function discoverScheduledTaskHandlers() { return api.get('/schedule/discover-handlers') }

// ========== 数据库管理 API ==========

export function getDbConnections() {
  return api.get('/database/connections')
}

export function createDbConnection(data) {
  return api.post('/database/connections', data)
}

export function updateDbConnection(id, data) {
  return api.put(`/database/connections/${id}`, data)
}

export function deleteDbConnection(id) {
  return api.delete(`/database/connections/${id}`)
}

export function testDbConnection(data) {
  return api.post('/database/test-connection', data)
}

export function getDbTables(connectionId) {
  return api.get(`/database/${connectionId}/tables`)
}

export function getDbTableSchema(connectionId, tableName) {
  return api.get(`/database/${connectionId}/tables/${encodeURIComponent(tableName)}/schema`)
}

export function executeDbSql(connectionId, sql) {
  return api.post(`/database/${connectionId}/execute`, { sql })
}

// ========== 主页配置 API ==========

export function getHomeConfig() {
  return api.get('/user/home-config')
}

export function saveHomeConfig(config) {
  return api.put('/user/home-config', { config })
}

// ========== 统一附件管理 API ==========

export function getUnifiedAttachments(params = {}) {
  return api.get('/attachment', { params })
}

export function getAttachmentModules() {
  return api.get('/attachment/modules')
}

export function uploadUnifiedAttachment(formData) {
  return api.post('/attachment/upload', formData)
}

export function downloadUnifiedAttachmentUrl(attachmentId) {
  return `/api/attachment/${attachmentId}/download`
}

export function deleteUnifiedAttachment(attachmentId) {
  return api.delete(`/attachment/${attachmentId}`)
}

// 导出 axios 实例供其他模块使用
export { api }
