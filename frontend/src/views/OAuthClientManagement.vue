<template>
  <div class="oauth-client-management">
    <el-card>
      <template #header>
        <span><el-icon><Key /></el-icon> OAuth 2.0 客户端管理</span>
        <el-button style="float: right" type="primary" size="small" @click="openCreateDialog">
          <el-icon><Plus /></el-icon> 新增客户端
        </el-button>
      </template>

      <el-table :data="clients" stripe border v-loading="loading">
        <el-table-column prop="id" label="ID" width="60" />
        <el-table-column prop="clientId" label="Client ID" width="140" show-overflow-tooltip />
        <el-table-column prop="clientName" label="名称" width="150" />
        <el-table-column label="首方" width="70">
          <template #default="{ row }">
            <el-tag v-if="row.isFirstParty" type="success" size="small">是</el-tag>
            <el-tag v-else type="info" size="small">否</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="状态" width="80">
          <template #default="{ row }">
            <el-tag v-if="row.isActive" type="success" size="small">启用</el-tag>
            <el-tag v-else type="danger" size="small">禁用</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="Grant Types" width="200" show-overflow-tooltip>
          <template #default="{ row }">{{ row.allowedGrantTypes?.join(', ') }}</template>
        </el-table-column>
        <el-table-column label="Scopes" width="180" show-overflow-tooltip>
          <template #default="{ row }">{{ row.allowedScopes?.join(', ') }}</template>
        </el-table-column>
        <el-table-column label="回调地址" min-width="200" show-overflow-tooltip>
          <template #default="{ row }">{{ row.redirectUris?.join(', ') }}</template>
        </el-table-column>
        <el-table-column label="创建时间" width="170">
          <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
        </el-table-column>
        <el-table-column label="操作" width="200" fixed="right">
          <template #default="{ row }">
            <el-button size="small" @click="openEditDialog(row)">编辑</el-button>
            <el-popconfirm
              v-if="row.isActive"
              title="确定要禁用该客户端吗？"
              @confirm="handleToggle(row)"
            >
              <template #reference>
                <el-button size="small" type="danger">禁用</el-button>
              </template>
            </el-popconfirm>
            <el-popconfirm
              v-else
              title="确定要启用该客户端吗？"
              @confirm="handleToggle(row)"
            >
              <template #reference>
                <el-button size="small" type="success">启用</el-button>
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
          @size-change="fetchClients"
          @current-change="fetchClients"
        />
      </div>
    </el-card>

    <!-- 创建/编辑弹窗 -->
    <el-dialog
      v-model="dialogVisible"
      :title="editingClient ? '编辑客户端' : '新增客户端'"
      width="600px"
      :close-on-click-modal="false"
    >
      <el-form :model="form" :rules="rules" ref="formRef" label-width="120px">
        <el-form-item label="Client ID" prop="clientId">
          <el-input v-model="form.clientId" :disabled="!!editingClient" placeholder="唯一标识，如 vue-spa" />
        </el-form-item>
        <el-form-item label="Client Secret" prop="clientSecret">
          <el-input v-model="form.clientSecret" placeholder="留空则不设置（公开客户端）" />
        </el-form-item>
        <el-form-item label="名称" prop="clientName">
          <el-input v-model="form.clientName" placeholder="客户端显示名称" />
        </el-form-item>
        <el-form-item label="回调地址" prop="redirectUris">
          <el-input
            v-model="redirectUrisText"
            placeholder="每行一个，如 http://localhost:5173/callback"
            type="textarea"
            :rows="3"
          />
        </el-form-item>
        <el-form-item label="允许的 Scopes">
          <el-checkbox-group v-model="form.allowedScopes">
            <el-checkbox label="openid" disabled>openid</el-checkbox>
            <el-checkbox label="profile">profile</el-checkbox>
            <el-checkbox label="email">email</el-checkbox>
          </el-checkbox-group>
        </el-form-item>
        <el-form-item label="允许的 Grant Types">
          <el-checkbox-group v-model="form.allowedGrantTypes">
            <el-checkbox label="authorization_code">authorization_code</el-checkbox>
            <el-checkbox label="refresh_token">refresh_token</el-checkbox>
          </el-checkbox-group>
        </el-form-item>
        <el-form-item label="首方客户端">
          <el-switch v-model="form.isFirstParty" />
          <span style="margin-left: 8px; color: #999; font-size: 12px">首方客户端自动授权，无需用户确认</span>
        </el-form-item>
        <el-form-item label="强制 PKCE">
          <el-switch v-model="form.requirePkce" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="handleSave">
          {{ editingClient ? '保存' : '创建' }}
        </el-button>
      </template>
    </el-dialog>

    <!-- 创建结果（显示 Secret） -->
    <el-dialog v-model="secretVisible" title="客户端已创建" width="500px">
      <el-alert type="warning" :closable="false" show-icon style="margin-bottom: 16px">
        Client Secret 仅显示一次，请立即保存。关闭后无法再次查看。
      </el-alert>
      <el-descriptions :column="1" border size="small">
        <el-descriptions-item label="Client ID">{{ createdClient.clientId }}</el-descriptions-item>
        <el-descriptions-item label="Client Secret">
          <code>{{ createdClient.clientSecretPlain || '无' }}</code>
        </el-descriptions-item>
      </el-descriptions>
      <template #footer>
        <el-button @click="secretVisible = false">关闭</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { Key, Plus } from '@element-plus/icons-vue'
import { getOAuthClients, createOAuthClient, updateOAuthClient, deleteOAuthClient } from '../api/auth'

const loading = ref(false)
const saving = ref(false)
const dialogVisible = ref(false)
const secretVisible = ref(false)
const editingClient = ref(null)
const formRef = ref(null)

const clients = ref([])
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)

const redirectUrisText = ref('')

const form = reactive({
  clientId: '',
  clientSecret: '',
  clientName: '',
  allowedScopes: ['openid'],
  allowedGrantTypes: ['authorization_code'],
  isFirstParty: false,
  requirePkce: true,
})

const rules = {
  clientId: [{ required: true, message: '请输入 Client ID', trigger: 'blur' }],
  clientName: [{ required: true, message: '请输入名称', trigger: 'blur' }],
}

const createdClient = reactive({ clientId: '', clientSecretPlain: '' })

function formatDate(dateStr) {
  if (!dateStr) return ''
  const d = new Date(dateStr)
  const pad = n => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`
}

async function fetchClients() {
  loading.value = true
  try {
    const res = await getOAuthClients({ page: page.value, pageSize: pageSize.value })
    clients.value = res.data.data?.list || []
    total.value = res.data.data?.total || clients.value.length
  } catch { clients.value = [] }
  loading.value = false
}

function openCreateDialog() {
  editingClient.value = null
  form.clientId = ''
  form.clientSecret = ''
  form.clientName = ''
  form.allowedScopes = ['openid']
  form.allowedGrantTypes = ['authorization_code']
  form.isFirstParty = false
  form.requirePkce = true
  redirectUrisText.value = ''
  dialogVisible.value = true
}

function openEditDialog(client) {
  editingClient.value = client
  form.clientId = client.clientId
  form.clientSecret = ''
  form.clientName = client.clientName
  form.allowedScopes = [...(client.allowedScopes || ['openid'])]
  form.allowedGrantTypes = [...(client.allowedGrantTypes || ['authorization_code'])]
  form.isFirstParty = client.isFirstParty
  form.requirePkce = client.requirePkce
  redirectUrisText.value = (client.redirectUris || []).join('\n')
  dialogVisible.value = true
}

async function handleSave() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return

  saving.value = true
  try {
    const data = {
      clientId: form.clientId,
      clientSecret: form.clientSecret || null,
      clientName: form.clientName,
      redirectUris: redirectUrisText.value.split('\n').map(s => s.trim()).filter(Boolean),
      allowedScopes: form.allowedScopes,
      allowedGrantTypes: form.allowedGrantTypes,
      isFirstParty: form.isFirstParty,
      requirePkce: form.requirePkce,
    }

    if (editingClient.value) {
      await updateOAuthClient(editingClient.value.id, data)
      ElMessage.success('客户端已更新')
    } else {
      const res = await createOAuthClient(data)
      if (res.data.success && res.data.data.clientSecretPlain) {
        createdClient.clientId = res.data.data.clientId
        createdClient.clientSecretPlain = res.data.data.clientSecretPlain
        secretVisible.value = true
      }
      ElMessage.success('客户端已创建')
    }

    dialogVisible.value = false
    fetchClients()
  } catch (err) {
    ElMessage.error(err.response?.data?.message || '操作失败')
  }
  saving.value = false
}

async function handleToggle(client) {
  try {
    await updateOAuthClient(client.id, { isActive: !client.isActive })
    ElMessage.success(client.isActive ? '客户端已禁用' : '客户端已启用')
    fetchClients()
  } catch (err) {
    ElMessage.error(err.response?.data?.message || '操作失败')
  }
}

onMounted(() => {
  fetchClients()
})
</script>

<style scoped>
.oauth-client-management { padding: 4px; }
.pagination-wrapper {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
}
</style>
