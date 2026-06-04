<template>
  <div class="sso-management">
    <!-- 创建 SSO 链接 / 授权码 -->
    <el-card class="create-card">
      <template #header>
        <span><el-icon><Link /></el-icon> 生成 SSO 一键登录</span>
      </template>
      <el-tabs v-model="activeTab">
        <!-- Tab 1: SSO 链接 -->
        <el-tab-pane label="SSO链接" name="link">
          <el-form :model="createForm" :rules="createRules" ref="createFormRef" inline>
            <el-form-item label="目标用户" prop="userId">
              <el-select
                v-model="createForm.userId"
                placeholder="选择用户"
                filterable
                style="width: 220px"
              >
                <el-option
                  v-for="u in userList"
                  :key="u.id"
                  :label="`${u.username} (${u.email})`"
                  :value="u.id"
                >
                  <span>{{ u.username }}</span>
                  <span style="color: #999; float: right; font-size: 12px">{{ u.email }}</span>
                </el-option>
              </el-select>
            </el-form-item>
            <el-form-item label="过期时间" prop="expireHours">
              <el-input-number v-model="createForm.expireHours" :min="1" :max="720" style="width: 120px" />
              <span style="margin-left: 6px; color: #999">小时（1~720）</span>
            </el-form-item>
            <el-form-item>
              <el-button type="primary" :loading="creating" @click="handleCreate">
                <el-icon><Plus /></el-icon> 生成链接
              </el-button>
            </el-form-item>
          </el-form>
        </el-tab-pane>

        <!-- Tab 2: 授权码 -->
        <el-tab-pane label="授权码" name="code">
          <el-form :model="codeForm" :rules="codeRules" ref="codeFormRef" inline>
            <el-form-item label="目标用户" prop="userId">
              <el-select
                v-model="codeForm.userId"
                placeholder="选择用户"
                filterable
                style="width: 220px"
              >
                <el-option
                  v-for="u in userList"
                  :key="u.id"
                  :label="`${u.username} (${u.email})`"
                  :value="u.id"
                >
                  <span>{{ u.username }}</span>
                  <span style="color: #999; float: right; font-size: 12px">{{ u.email }}</span>
                </el-option>
              </el-select>
            </el-form-item>
            <el-form-item label="过期时间" prop="expireMinutes">
              <el-input-number v-model="codeForm.expireMinutes" :min="1" :max="1440" style="width: 120px" />
              <span style="margin-left: 6px; color: #999">分钟（1~1440）</span>
            </el-form-item>
            <el-form-item>
              <el-button type="primary" :loading="codeCreating" @click="handleCreateCode">
                <el-icon><Plus /></el-icon> 生成授权码
              </el-button>
            </el-form-item>
          </el-form>
        </el-tab-pane>
      </el-tabs>
    </el-card>

    <!-- 生成结果弹窗 -->
    <el-dialog v-model="resultVisible" :title="resultData.type === 'code' ? '授权码已生成' : 'SSO 链接已生成'" width="580px" :close-on-click-modal="false">
      <el-alert type="warning" :closable="false" show-icon style="margin-bottom: 16px">
        {{ resultData.type === 'code' ? '以下授权码仅显示一次，请立即复制并发送给用户。关闭后无法再次查看。' : '以下链接仅显示一次，请立即复制并发送给用户。关闭后无法再次查看完整 Token。' }}
      </el-alert>
      <el-descriptions :column="1" border size="small">
        <el-descriptions-item label="用户">{{ resultData.username }}</el-descriptions-item>
        <el-descriptions-item label="邮箱">{{ resultData.email }}</el-descriptions-item>
        <el-descriptions-item label="过期时间">{{ formatDate(resultData.expiresAt) }}</el-descriptions-item>

        <!-- 链接模式 -->
        <el-descriptions-item v-if="resultData.type !== 'code'" label="SSO 链接">
          <div class="link-container">
            <code class="link-text">{{ resultData.loginLink }}</code>
            <el-button size="small" type="primary" @click="copyLink">
              <el-icon><CopyDocument /></el-icon> 复制链接
            </el-button>
          </div>
        </el-descriptions-item>

        <!-- 授权码模式 -->
        <el-descriptions-item v-if="resultData.type === 'code'" label="授权码">
          <div class="code-display">
            <span class="auth-code-text">{{ resultData.authCode }}</span>
            <el-button size="small" type="primary" @click="copyCode">
              <el-icon><CopyDocument /></el-icon> 复制授权码
            </el-button>
          </div>
        </el-descriptions-item>
      </el-descriptions>
      <template #footer>
        <el-button @click="resultVisible = false">关闭</el-button>
      </template>
    </el-dialog>

    <!-- Token 列表 -->
    <el-card style="margin-top: 20px">
      <template #header>
        <span><el-icon><List /></el-icon> SSO Token 列表</span>
        <el-button style="float: right" size="small" @click="fetchTokens">
          <el-icon><RefreshRight /></el-icon> 刷新
        </el-button>
      </template>
      <el-table :data="tokens" stripe border v-loading="loading">
        <el-table-column prop="id" label="ID" width="60" />
        <el-table-column label="类型" width="80">
          <template #default="{ row }">
            <el-tag v-if="row.type === 'code'" size="small" type="warning">授权码</el-tag>
            <el-tag v-else size="small" type="primary">链接</el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="username" label="目标用户" width="120" />
        <el-table-column prop="email" label="邮箱" width="180" show-overflow-tooltip />
        <el-table-column prop="createdByAdmin" label="创建者" width="100" />
        <el-table-column label="Token/授权码" width="150" show-overflow-tooltip>
          <template #default="{ row }">
            {{ row.type === 'code' ? (row.authCode || '****') : (row.token || '****') }}
          </template>
        </el-table-column>
        <el-table-column label="状态" width="100">
          <template #default="{ row }">
            <el-tag v-if="row.isUsed" type="info" size="small">已使用</el-tag>
            <el-tag v-else-if="new Date(row.expiresAt) < new Date()" type="danger" size="small">已过期</el-tag>
            <el-tag v-else type="success" size="small">有效</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="过期时间" width="170">
          <template #default="{ row }">{{ formatDate(row.expiresAt) }}</template>
        </el-table-column>
        <el-table-column label="使用时间" width="170">
          <template #default="{ row }">{{ row.usedAt ? formatDate(row.usedAt) : '-' }}</template>
        </el-table-column>
        <el-table-column label="创建时间" width="170">
          <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
        </el-table-column>
        <el-table-column label="操作" width="100" fixed="right">
          <template #default="{ row }">
            <el-popconfirm
              v-if="!row.isUsed && new Date(row.expiresAt) >= new Date()"
              title="确定要撤销该 Token 吗？撤销后链接立即失效。"
              confirm-button-text="确定"
              cancel-button-text="取消"
              @confirm="handleRevoke(row.id)"
            >
              <template #reference>
                <el-button size="small" type="danger">撤销</el-button>
              </template>
            </el-popconfirm>
          </template>
        </el-table-column>
      </el-table>

      <div class="pagination-wrapper">
        <el-pagination
          v-model:current-page="page"
          v-model:page-size="pageSize"
          :page-sizes="[10, 20, 50]"
          :total="total"
          layout="sizes, prev, pager, next"
          background
          @size-change="fetchTokens"
          @current-change="fetchTokens"
        />
      </div>
    </el-card>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { Link, Plus, RefreshRight, List, CopyDocument } from '@element-plus/icons-vue'
import { createSsoToken, createAuthCode, getSsoTokens, revokeSsoToken } from '../api/auth'
import { getUsers } from '../api/auth'

// ========== 通用状态 ==========

const userList = ref([])
const tokens = ref([])
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)
const loading = ref(false)

// ========== 标签页 ==========

const activeTab = ref('link')

// ========== 结果弹窗 ==========

const resultVisible = ref(false)
const resultData = reactive({ username: '', email: '', expiresAt: '', loginLink: '', authCode: '', type: '' })

// ========== 链接模式 ==========

const createFormRef = ref(null)
const creating = ref(false)
const createForm = reactive({ userId: null, expireHours: 24 })
const createRules = {
  userId: [{ required: true, message: '请选择用户', trigger: 'change' }],
  expireHours: [{ required: true, message: '请设置过期时间', trigger: 'blur' }],
}

// ========== 授权码模式 ==========

const codeFormRef = ref(null)
const codeCreating = ref(false)
const codeForm = reactive({ userId: null, expireMinutes: 30 })
const codeRules = {
  userId: [{ required: true, message: '请选择用户', trigger: 'change' }],
  expireMinutes: [{ required: true, message: '请设置过期时间', trigger: 'blur' }],
}

// ========== 工具函数 ==========

function formatDate(dateStr) {
  if (!dateStr) return ''
  const d = new Date(dateStr)
  const pad = n => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`
}

// ========== 数据获取 ==========

async function fetchUsers() {
  try {
    const res = await getUsers({ pageSize: 9999 })
    userList.value = (res.data.data?.list || []).filter(u => !u.isFrozen)
  } catch { /* ignore */ }
}

async function fetchTokens() {
  loading.value = true
  try {
    const res = await getSsoTokens({ page: page.value, pageSize: pageSize.value })
    tokens.value = res.data.data || []
    total.value = res.data.total || tokens.value.length
  } catch { tokens.value = [] }
  loading.value = false
}

// ========== 链接模式操作 ==========

async function handleCreate() {
  const valid = await createFormRef.value?.validate().catch(() => false)
  if (!valid) return

  creating.value = true
  try {
    const res = await createSsoToken({ userId: createForm.userId, expireHours: createForm.expireHours })
    if (res.data.success) {
      const d = res.data.data
      Object.assign(resultData, {
        username: d.username,
        email: d.email,
        expiresAt: d.expiresAt,
        loginLink: d.loginLink,
        authCode: '',
        type: 'link',
      })
      resultVisible.value = true
      fetchTokens()
    }
  } catch (err) {
    ElMessage.error(err.response?.data?.message || '创建失败')
  }
  creating.value = false
}

function copyLink() {
  navigator.clipboard.writeText(resultData.loginLink).then(
    () => ElMessage.success('链接已复制到剪贴板'),
    () => ElMessage.warning('复制失败，请手动选择复制'),
  )
}

// ========== 授权码模式操作 ==========

async function handleCreateCode() {
  const valid = await codeFormRef.value?.validate().catch(() => false)
  if (!valid) return

  codeCreating.value = true
  try {
    const res = await createAuthCode({ userId: codeForm.userId, expireMinutes: codeForm.expireMinutes })
    if (res.data.success) {
      const d = res.data.data
      Object.assign(resultData, {
        username: d.username,
        email: d.email,
        expiresAt: d.expiresAt,
        loginLink: '',
        authCode: d.authCode,
        type: 'code',
      })
      resultVisible.value = true
      fetchTokens()
    }
  } catch (err) {
    ElMessage.error(err.response?.data?.message || '创建失败')
  }
  codeCreating.value = false
}

function copyCode() {
  navigator.clipboard.writeText(resultData.authCode).then(
    () => ElMessage.success('授权码已复制到剪贴板'),
    () => ElMessage.warning('复制失败，请手动选择复制'),
  )
}

// ========== 通用操作 ==========

async function handleRevoke(id) {
  try {
    await revokeSsoToken(id)
    ElMessage.success('Token 已撤销')
    fetchTokens()
  } catch (err) {
    ElMessage.error(err.response?.data?.message || '撤销失败')
  }
}

onMounted(() => {
  fetchUsers()
  fetchTokens()
})
</script>

<style scoped>
.sso-management { padding: 4px; }
.create-card { margin-bottom: 20px; }
.link-container {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
}
.link-text {
  background: #f5f7fa;
  padding: 6px 12px;
  border-radius: 4px;
  font-size: 12px;
  word-break: break-all;
  flex: 1;
  min-width: 200px;
}
.code-display {
  display: flex;
  align-items: center;
  gap: 12px;
}
.auth-code-text {
  font-size: 28px;
  font-weight: bold;
  font-family: 'Courier New', Courier, monospace;
  letter-spacing: 4px;
  color: #409eff;
  background: #f0f5ff;
  padding: 8px 20px;
  border-radius: 6px;
  border: 2px dashed #409eff;
}
.pagination-wrapper {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
}
</style>
