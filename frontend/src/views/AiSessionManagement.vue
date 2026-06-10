<template>
  <div class="session-mgmt-page">
    <el-card>
      <template #header>
        <div class="page-header">
          <span class="page-title">AI 会话管理</span>
          <div class="header-actions">
            <span class="total-hint">共 {{ total }} 个会话</span>
            <el-button
              type="danger"
              :disabled="selectedIds.length === 0"
              @click="handleBatchDelete"
            >
              批量删除 ({{ selectedIds.length }})
            </el-button>
          </div>
        </div>
      </template>

      <!-- 筛选栏 -->
      <div class="filter-bar">
        <div class="filter-row">
          <el-date-picker
            v-model="startDate"
            type="date"
            placeholder="开始日期"
            value-format="YYYY-MM-DD"
            style="width: 150px"
            size="small"
            @change="fetchData"
          />
          <span class="filter-sep">—</span>
          <el-date-picker
            v-model="endDate"
            type="date"
            placeholder="结束日期"
            value-format="YYYY-MM-DD"
            style="width: 150px"
            size="small"
            @change="fetchData"
          />
          <el-select
            v-model="filterUserId"
            placeholder="筛选用户"
            clearable
            filterable
            style="width: 180px"
            size="small"
            @change="fetchData"
          >
            <el-option
              v-for="u in users"
              :key="u.id"
              :label="`${u.username} (ID: ${u.id})`"
              :value="u.id"
            />
          </el-select>
          <el-button size="small" @click="resetFilters">重置</el-button>
        </div>
      </div>

      <!-- 表格 -->
      <el-table
        :data="sessions"
        stripe
        v-loading="loading"
        @selection-change="handleSelectionChange"
      >
        <el-table-column type="selection" width="44" />
        <el-table-column prop="id" label="ID" width="60" align="center" />
        <el-table-column prop="title" label="会话标题" min-width="180" show-overflow-tooltip />
        <el-table-column prop="username" label="用户" width="100" />
        <el-table-column prop="modelName" label="模型" width="140">
          <template #default="{ row }">
            <code v-if="row.modelName" style="font-size: 12px">{{ row.modelName }}</code>
            <span v-else style="color: #909399">—</span>
          </template>
        </el-table-column>
        <el-table-column prop="messageCount" label="消息数" width="80" align="center" />
        <el-table-column prop="createdAt" label="创建时间" width="160" />
        <el-table-column label="操作" width="160" fixed="right">
          <template #default="{ row }">
            <el-button link type="primary" size="small" @click="openMessages(row)">查看</el-button>
            <el-popconfirm
              title="确定删除此会话及其所有消息？"
              @confirm="handleDelete(row.id)"
            >
              <template #reference>
                <el-button link type="danger" size="small">删除</el-button>
              </template>
            </el-popconfirm>
          </template>
        </el-table-column>
      </el-table>

      <el-empty v-if="!loading && sessions.length === 0" description="暂无有效会话" />

      <div v-if="total > pageSize" style="margin-top: 16px; text-align: right">
        <el-pagination
          v-model:current-page="page"
          :page-size="pageSize"
          :total="total"
          layout="total, prev, pager, next"
          @current-change="fetchData"
        />
      </div>
    </el-card>

    <!-- 查看对话内容弹窗 -->
    <el-dialog
      v-model="msgVisible"
      :title="`会话 #${currentSession?.id} — ${currentSession?.title}`"
      width="720px"
      :close-on-click-modal="false"
      @opened="scrollToBottom"
    >
      <div v-if="msgLoading" style="text-align: center; padding: 40px">
        <el-icon :size="28" class="is-loading"><Loading /></el-icon>
        <p style="margin-top: 8px; color: #909399">加载消息中...</p>
      </div>
      <div v-else class="messages-container" ref="msgContainerRef">
        <div
          v-for="msg in messages"
          :key="msg.id"
          class="msg-item"
          :class="msg.role"
        >
          <div class="msg-role-tag">
            <el-tag :type="msg.role === 'user' ? '' : 'success'" size="small">
              {{ msg.role === 'user' ? '用户' : 'AI' }}
            </el-tag>
            <span class="msg-time">{{ msg.createdAt }}</span>
          </div>
          <div class="msg-body">
            <template v-if="msg.role === 'assistant' && msg.content.length > 500 && !msg._expanded">
              <div class="msg-content" v-html="renderMsgMarkdown(msg.content.slice(0, 500))"></div>
              <el-button
                link type="primary" size="small"
                style="margin-top: 4px"
                @click="msg._expanded = true"
              >
                展开全部 ({{ msg.content.length }} 字)...
              </el-button>
            </template>
            <div v-else class="msg-content" v-html="renderMsgMarkdown(msg.content)"></div>
          </div>
        </div>
      </div>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, onMounted, nextTick } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Loading } from '@element-plus/icons-vue'
import { getAdminSessions, adminDeleteSession, adminBatchDeleteSessions, getChatMessages } from '../api/ai'
import { getUsers } from '../api/auth'

const sessions = ref([])
const loading = ref(false)
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)

const startDate = ref('')
const endDate = ref('')
const filterUserId = ref(null)
const users = ref([])

const selectedIds = ref([])

// ========== Messages Dialog ==========
const msgVisible = ref(false)
const msgLoading = ref(false)
const messages = ref([])
const currentSession = ref(null)
const msgContainerRef = ref(null)

// ========== Data ==========
async function fetchData() {
  loading.value = true
  try {
    const params = { page: page.value, pageSize: pageSize.value }
    if (startDate.value) params.startDate = startDate.value
    if (endDate.value) params.endDate = endDate.value
    if (filterUserId.value) params.userId = filterUserId.value

    const res = await getAdminSessions(params)
    if (res.data.success) {
      sessions.value = res.data.data
      total.value = res.data.total
    }
  } catch { /* ignore */ }
  loading.value = false
}

async function fetchUsers() {
  try {
    const res = await getUsers({ page: 1, pageSize: 9999 })
    if (res.data.success) {
      users.value = res.data.data.list || []
    }
  } catch { /* ignore */ }
}

function resetFilters() {
  startDate.value = ''
  endDate.value = ''
  filterUserId.value = null
  page.value = 1
  fetchData()
}

// ========== Messages ==========
async function openMessages(row) {
  currentSession.value = row
  msgVisible.value = true
  msgLoading.value = true
  messages.value = []
  try {
    const res = await getChatMessages(row.id)
    if (res.data.success) {
      messages.value = (res.data.data.messages || []).map(m => ({
        ...m,
        _expanded: false
      }))
    }
  } catch {
    ElMessage.error('加载消息失败')
  }
  msgLoading.value = false
}

function scrollToBottom() {
  nextTick(() => {
    if (msgContainerRef.value) {
      msgContainerRef.value.scrollTop = msgContainerRef.value.scrollHeight
    }
  })
}

// ========== Delete ==========
async function handleDelete(id) {
  try {
    await adminDeleteSession(id)
    ElMessage.success('已删除')
    await fetchData()
  } catch {
    ElMessage.error('删除失败')
  }
}

function handleSelectionChange(rows) {
  selectedIds.value = rows.map(r => r.id)
}

async function handleBatchDelete() {
  try {
    await ElMessageBox.confirm(
      `确定要删除选中的 ${selectedIds.value.length} 个会话吗？这将同时删除所有关联的消息，不可恢复。`,
      '批量删除确认',
      { type: 'warning', confirmButtonText: '确认删除', cancelButtonText: '取消' }
    )
    await adminBatchDeleteSessions(selectedIds.value)
    ElMessage.success('批量删除成功')
    selectedIds.value = []
    await fetchData()
  } catch {
    // 取消操作
  }
}

// ========== Markdown ==========
function renderMsgMarkdown(text) {
  if (!text) return ''
  let html = text
  html = html.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
  html = html.replace(/```(\w*)\n?([\s\S]*?)```/g, (_, lang, code) =>
    `<pre class="code-block"><code>${code.trim()}</code></pre>`)
  html = html.replace(/`([^`]+)`/g, '<code>$1</code>')
  html = html.replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
  html = html.replace(/\n/g, '<br/>')
  return html
}

onMounted(() => {
  fetchUsers()
  fetchData()
})
</script>

<style scoped>
.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.page-title {
  font-size: 16px;
  font-weight: 600;
}
.header-actions {
  display: flex;
  align-items: center;
  gap: 12px;
}
.total-hint {
  font-size: 13px;
  color: #909399;
}

.filter-bar {
  margin-bottom: 16px;
  padding: 12px;
  background: #f5f7fa;
  border-radius: 8px;
}
.filter-row {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}
.filter-sep {
  color: #909399;
  font-size: 13px;
}

/* Messages dialog */
.messages-container {
  max-height: 60vh;
  overflow-y: auto;
  padding: 8px 0;
}
.msg-item {
  margin-bottom: 16px;
  padding: 0 4px;
}
.msg-role-tag {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 6px;
}
.msg-time {
  font-size: 12px;
  color: #c0c4cc;
}
.msg-body {
  background: #f5f7fa;
  border-radius: 8px;
  padding: 10px 14px;
  font-size: 14px;
  line-height: 1.7;
}
.msg-item.assistant .msg-body {
  background: #ecf5ff;
  border-left: 3px solid #409EFF;
}
.msg-content :deep(.code-block) {
  background: #1e1e1e;
  color: #d4d4d4;
  padding: 8px 12px;
  border-radius: 6px;
  margin: 6px 0;
  overflow-x: auto;
  font-size: 12px;
  line-height: 1.4;
  white-space: pre-wrap;
}
.msg-content :deep(code) {
  background: rgba(0,0,0,0.06);
  padding: 1px 5px;
  border-radius: 3px;
  font-size: 13px;
}
.msg-content :deep(strong) {
  font-weight: 600;
}
</style>
