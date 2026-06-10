import { api } from './auth'

// ========== AI 配置 API ==========

export function getAiConfigs() {
  return api.get('/aiconfig/models')
}

export function getAiConfigById(id) {
  return api.get(`/aiconfig/models/${id}`)
}

export function createAiConfig(data) {
  return api.post('/aiconfig/models', data)
}

export function updateAiConfig(id, data) {
  return api.put(`/aiconfig/models/${id}`, data)
}

export function deleteAiConfig(id) {
  return api.delete(`/aiconfig/models/${id}`)
}

export function testAiConfig(data) {
  return api.post('/aiconfig/test', data)
}

// ========== AI 对话 API ==========

export function getChatSessions() {
  return api.get('/aichat/sessions')
}

export function createChatSession(data) {
  return api.post('/aichat/sessions', data)
}

export function deleteChatSession(id) {
  return api.delete(`/aichat/sessions/${id}`)
}

export function getChatMessages(sessionId) {
  return api.get(`/aichat/sessions/${sessionId}/messages`)
}

export function saveAssistantMessage(sessionId, content) {
  return api.post(`/aichat/sessions/${sessionId}/save-assistant`, { content })
}

/**
 * 发送消息（SSE 流式响应）
 * 返回一个 ReadableStream 读取器
 */
export function sendMessage(sessionId, message) {
  const token = localStorage.getItem('token')
  return fetch('/api/aichat/send', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${token}`,
    },
    body: JSON.stringify({ sessionId, message }),
  })
}

// ========== AI 会话管理 API（管理员） ==========

export function getAdminSessions(params = {}) {
  return api.get('/aichat/admin/sessions', { params })
}

export function adminDeleteSession(id) {
  return api.delete(`/aichat/admin/sessions/${id}`)
}

export function adminBatchDeleteSessions(ids) {
  return api.post('/aichat/admin/sessions/batch-delete', { ids })
}

// ========== AI 总结 API ==========

export function getDailySummaries(params = {}) {
  return api.get('/aisummary', { params })
}

export function getDailySummaryById(id) {
  return api.get(`/aisummary/${id}`)
}

export function generateDailySummary(date) {
  return api.post('/aisummary/generate', { date }, { timeout: 360000 })
}

export function reviewDailySummary(id, data) {
  return api.put(`/aisummary/${id}/review`, data)
}

export function deleteDailySummary(id) {
  return api.delete(`/aisummary/${id}`)
}

// ========== AI 知识库 API ==========

export function searchKnowledge(keyword, topK = 5) {
  return api.get('/aiknowledge/search', { params: { keyword, topK } })
}

export function getKnowledgeCategories() {
  return api.get('/aiknowledge/categories')
}

export function getKnowledgeEntries(params = {}) {
  return api.get('/aiknowledge', { params })
}

export function getKnowledgeEntryById(id) {
  return api.get(`/aiknowledge/${id}`)
}

export function createKnowledgeEntry(data) {
  return api.post('/aiknowledge', data)
}

export function updateKnowledgeEntry(id, data) {
  return api.put(`/aiknowledge/${id}`, data)
}

export function deleteKnowledgeEntry(id) {
  return api.delete(`/aiknowledge/${id}`)
}
