/**
 * 工作流 API 模块
 * 复用现有项目的 axios 实例（已有 auth interceptor）
 */
import { api } from '@/api/auth.js'

// ===== 流程定义 =====

export function getDefinitions(params) {
  return api.get('/workflow/definitions', { params })
}

export function getDefinition(id) {
  return api.get(`/workflow/definitions/${id}`)
}

export function saveDefinition(data) {
  if (data.id) {
    return api.put(`/workflow/definitions/${data.id}`, data)
  }
  return api.post('/workflow/definitions', data)
}

export function deleteDefinition(id) {
  return api.delete(`/workflow/definitions/${id}`)
}

// ===== 流程实例 =====

export function getMyInstances(params) {
  return api.get('/workflow/instances', { params })
}

export function getInstanceDetail(id) {
  return api.get(`/workflow/instances/${id}`)
}

export function submitInstance(data) {
  return api.post('/workflow/instances', data)
}

// ===== 审批任务 =====

export function getMyTasks(params) {
  return api.get('/workflow/tasks', { params })
}

export function getTaskHistory(params) {
  return api.get('/workflow/tasks/history', { params })
}

export function approveTask(id, data) {
  return api.post(`/workflow/tasks/${id}/approve`, data)
}

export function rejectTask(id, data) {
  return api.post(`/workflow/tasks/${id}/reject`, data)
}

export function transferTask(id, data) {
  return api.post(`/workflow/tasks/${id}/transfer`, data)
}

// ===== 用户/角色（供选择对话框使用） =====

export function getUsers(params) {
  return api.get('/usermanagement', { params })
}

export function getRoles(params) {
  return api.get('/role', { params })
}

// ===== 兼容 AntFlow-Designer 原有调用方式 =====

/**
 * 获取流程设计数据（编辑时加载已有定义）
 */
export function getWorkFlowData(id) {
  return api.get(`/workflow/definitions/${id}`)
}

/**
 * 保存/发布流程设计数据
 */
export function setWorkFlowData(data) {
  return api.post('/workflow/definitions', data)
}
