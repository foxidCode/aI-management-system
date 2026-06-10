<template>
  <el-drawer
    v-model="visible"
    direction="rtl"
    size="780px"
    :close-on-click-modal="false"
    :show-close="false"
    custom-class="ai-chat-drawer"
  >
    <template #header>
      <div class="chat-header">
        <div class="chat-header-left">
          <el-icon :size="20" color="#409EFF"><ChatDotRound /></el-icon>
          <span class="chat-title">AI 助手</span>
          <el-tag v-if="activeModelName" size="small" type="success" style="margin-left: 12px">{{ activeModelName }}</el-tag>
          <el-tag v-else size="small" type="danger" style="margin-left: 12px">未启用模型</el-tag>
        </div>
        <el-button link @click="visible = false">
          <el-icon :size="20"><Close /></el-icon>
        </el-button>
      </div>
    </template>

    <div class="chat-container">
      <!-- 会话侧边栏 -->
      <div class="session-sidebar" :class="{ collapsed: sidebarCollapsed }">
        <div class="session-sidebar-header">
          <span v-if="!sidebarCollapsed" class="sidebar-title">会话</span>
          <div class="sidebar-actions">
            <el-button
              size="small"
              :icon="ChatLineSquare"
              circle
              @click="newChat"
              title="新建会话"
            />
          </div>
        </div>
        <div v-if="!sidebarCollapsed" class="session-list">
          <div
            v-for="session in sessions"
            :key="session.id"
            class="session-item"
            :class="{ active: session.id === currentSessionId }"
            @click="switchSession(session)"
          >
            <div class="session-item-content">
              <span class="session-title">{{ session.title }}</span>
              <span class="session-time">{{ formatTime(session.updatedAt) }}</span>
            </div>
            <el-popconfirm
              title="删除此会话？"
              @confirm="handleDeleteSession(session.id)"
              width="180"
            >
              <template #reference>
                <el-button link size="small" class="session-delete-btn" @click.stop>
                  <el-icon :size="14"><Delete /></el-icon>
                </el-button>
              </template>
            </el-popconfirm>
          </div>
          <el-empty v-if="sessions.length === 0" description="暂无对话" :image-size="48" />
        </div>
      </div>

      <!-- 对话区域 -->
      <div class="chat-main">
        <!-- 消息列表 -->
        <div class="message-list" ref="messageListRef">
          <div v-if="messages.length === 0 && !isStreaming" class="welcome-area">
            <div class="welcome-icon">
              <el-icon :size="48" color="#409EFF"><Cpu /></el-icon>
            </div>
            <h3>你好！我是 AI 助手</h3>
            <p>我可以帮你了解系统功能、解答使用问题。请开始提问吧！</p>
            <div class="quick-prompts">
              <div class="prompt-chip" @click="sendQuickPrompt('系统有哪些主要功能？')">
                系统有哪些主要功能？
              </div>
              <div class="prompt-chip" @click="sendQuickPrompt('如何管理用户和角色？')">
                如何管理用户和角色？
              </div>
              <div class="prompt-chip" @click="sendQuickPrompt('工作流系统怎么使用？')">
                工作流系统怎么使用？
              </div>
            </div>
          </div>

          <div
            v-for="(msg, idx) in messages"
            :key="idx"
            class="message-item"
            :class="msg.role"
          >
            <div class="message-avatar">
              <el-icon v-if="msg.role === 'user'" :size="18"><UserFilled /></el-icon>
              <el-icon v-else :size="18" color="#409EFF"><Cpu /></el-icon>
            </div>
            <div class="message-body">
              <div class="message-role">{{ msg.role === 'user' ? '你' : 'AI 助手' }}</div>
              <div class="message-content" v-html="renderMarkdown(msg.content)" />
            </div>
          </div>

          <!-- 流式输出中的临时消息 -->
          <div v-if="isStreaming && streamingContent" class="message-item assistant">
            <div class="message-avatar">
              <el-icon :size="18" color="#409EFF"><Cpu /></el-icon>
            </div>
            <div class="message-body">
              <div class="message-role">AI 助手 <span class="streaming-badge">正在输入...</span></div>
              <div class="message-content" v-html="renderMarkdown(streamingContent)" />
            </div>
          </div>

          <!-- 加载指示器（流未开始时） -->
          <div v-if="isStreaming && !streamingContent" class="message-item assistant">
            <div class="message-avatar">
              <el-icon :size="18" color="#409EFF"><Cpu /></el-icon>
            </div>
            <div class="message-body">
              <div class="typing-indicator">
                <span></span><span></span><span></span>
              </div>
            </div>
          </div>

          <div v-if="errorMsg" class="chat-error">
            <el-alert :title="errorMsg" type="error" :closable="true" @close="errorMsg = ''" />
          </div>
        </div>

        <!-- 输入区域 -->
        <div class="input-area">
          <el-input
            v-model="inputText"
            type="textarea"
            :rows="2"
            placeholder="输入你的问题，按 Enter 发送，Shift+Enter 换行..."
            :disabled="isStreaming"
            @keydown.enter.exact="sendMessage"
            resize="none"
          />
          <div class="input-actions">
            <el-button
              type="primary"
              :icon="Promotion"
              @click="sendMessage"
              :disabled="!inputText.trim() || isStreaming"
              :loading="isStreaming"
            >
              {{ isStreaming ? '回复中' : '发送' }}
            </el-button>
            <span class="input-hint" v-if="!isStreaming">Enter 发送 · Shift+Enter 换行</span>
          </div>
        </div>
      </div>
    </div>
  </el-drawer>
</template>

<script setup>
import { ref, watch, nextTick, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import {
  ChatDotRound, ChatLineSquare, Close, Delete, Promotion,
  UserFilled, Cpu
} from '@element-plus/icons-vue'
import {
  getAiConfigs, getChatSessions, createChatSession, deleteChatSession,
  getChatMessages, sendMessage as sendChatMessage, saveAssistantMessage
} from '../api/ai'

const visible = defineModel('visible', { default: false })

// ========== State ==========
const activeModelName = ref('')
const activeConfigId = ref(null)
const sessions = ref([])
const currentSessionId = ref(null)
const messages = ref([])
const inputText = ref('')
const isStreaming = ref(false)
const streamingContent = ref('')
const errorMsg = ref('')
const sidebarCollapsed = ref(false)
const messageListRef = ref(null)

// ========== Lifecycle ==========
onMounted(() => {
  fetchConfigs()
  fetchSessions()
})

watch(visible, (val) => {
  if (val) {
    fetchSessions()
    if (currentSessionId.value) {
      fetchMessages(currentSessionId.value)
    }
  }
})

// ========== Config ==========
async function fetchConfigs() {
  try {
    const res = await getAiConfigs()
    if (res.data.success) {
      const active = res.data.data.find(c => c.isActive)
      activeModelName.value = active ? active.name : ''
      activeConfigId.value = active ? active.id : null
    }
  } catch { /* ignore */ }
}

// ========== Sessions ==========
async function fetchSessions() {
  try {
    const res = await getChatSessions()
    if (res.data.success) sessions.value = res.data.data
  } catch { /* ignore */ }
}

function newChat() {
  // 不立即创建会话——等用户实际发送第一条消息时才建，避免产生空会话
  currentSessionId.value = null
  messages.value = []
}

async function switchSession(session) {
  currentSessionId.value = session.id
  await fetchMessages(session.id)
}

async function handleDeleteSession(id) {
  try {
    await deleteChatSession(id)
    if (currentSessionId.value === id) {
      currentSessionId.value = null
      messages.value = []
    }
    await fetchSessions()
    ElMessage.success('已删除')
  } catch {
    ElMessage.error('删除失败')
  }
}

async function fetchMessages(sessionId) {
  try {
    const res = await getChatMessages(sessionId)
    if (res.data.success) {
      messages.value = res.data.data.messages
    }
    await nextTick()
    scrollToBottom()
  } catch { /* ignore */ }
}

// ========== Send Message ==========
async function sendMessage() {
  const text = inputText.value.trim()
  if (!text || isStreaming.value) return

  // Auto-create session if none
  if (!currentSessionId.value) {
    try {
      const res = await createChatSession({ modelConfigId: activeConfigId.value })
      if (res.data.success) {
        currentSessionId.value = res.data.data.id
        await fetchSessions()
      } else {
        ElMessage.error('创建会话失败')
        return
      }
    } catch {
      ElMessage.error('创建会话失败')
      return
    }
  }

  // Add user message locally
  const userMsg = { role: 'user', content: text }
  messages.value.push(userMsg)
  inputText.value = ''
  errorMsg.value = ''

  await nextTick()
  scrollToBottom()

  // Start streaming
  isStreaming.value = true
  streamingContent.value = ''

  try {
    const response = await sendChatMessage(currentSessionId.value, text)

    if (!response.ok) {
      const errText = await response.text()
      errorMsg.value = `请求失败: ${response.status}`
      isStreaming.value = false
      return
    }

    const reader = response.body.getReader()
    const decoder = new TextDecoder()
    let buffer = ''
    let fullContent = ''

    while (true) {
      const { done, value } = await reader.read()
      if (done) break

      buffer += decoder.decode(value, { stream: true })
      const lines = buffer.split('\n')
      buffer = lines.pop() || ''

      for (const line of lines) {
        if (!line.startsWith('data: ')) continue
        const data = line.slice(6)

        try {
          const parsed = JSON.parse(data)

          if (parsed.type === 'start') {
            // Stream started
          } else if (parsed.type === 'chunk') {
            fullContent += parsed.content
            streamingContent.value = fullContent
            await nextTick()
            scrollToBottom()
          } else if (parsed.type === 'done') {
            fullContent = parsed.fullContent || fullContent
          } else if (parsed.type === 'error') {
            errorMsg.value = parsed.content
          }
        } catch {
          // Ignore parse errors
        }
      }
    }

    // 先清除流式状态 + 推入消息（同步，Vue 批量渲染，避免双回复框闪烁）
    isStreaming.value = false
    if (fullContent) {
      messages.value.push({ role: 'assistant', content: fullContent })
    }
    streamingContent.value = ''

    // 异步持久化（不阻塞 UI 更新）
    if (fullContent) {
      try {
        await saveAssistantMessage(currentSessionId.value, fullContent)
        await fetchSessions() // Refresh session list (title may have updated)
      } catch { /* ignore persist errors */ }
    }

  } catch (err) {
    errorMsg.value = `请求错误: ${err.message}`
    isStreaming.value = false
    streamingContent.value = ''
  } finally {
    await nextTick()
    scrollToBottom()
  }
}

function sendQuickPrompt(text) {
  inputText.value = text
  sendMessage()
}

// ========== Utils ==========
function scrollToBottom() {
  if (messageListRef.value) {
    const el = messageListRef.value
    el.scrollTop = el.scrollHeight
  }
}

function formatTime(ts) {
  if (!ts) return ''
  const d = new Date(ts)
  const now = new Date()
  const diff = now - d
  if (diff < 60000) return '刚刚'
  if (diff < 3600000) return `${Math.floor(diff / 60000)}m前`
  if (diff < 86400000) return `${Math.floor(diff / 3600000)}h前`
  return d.toLocaleDateString('zh-CN')
}

function renderMarkdown(text) {
  if (!text) return ''
  let html = text

  // Escape HTML
  html = html.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')

  // Code blocks (```...```)
  html = html.replace(/```(\w*)\n?([\s\S]*?)```/g, (_, lang, code) => {
    return `<pre class="code-block"><code>${code.trim()}</code></pre>`
  })

  // Inline code (`...`)
  html = html.replace(/`([^`]+)`/g, '<code class="inline-code">$1</code>')

  // Bold (**text**)
  html = html.replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')

  // Headers
  html = html.replace(/^### (.+)$/gm, '<h4 class="md-h4">$1</h4>')
  html = html.replace(/^## (.+)$/gm, '<h3 class="md-h3">$1</h3>')
  html = html.replace(/^# (.+)$/gm, '<h2 class="md-h2">$1</h2>')

  // Unordered lists
  html = html.replace(/^- (.+)$/gm, '<li class="md-li">$1</li>')
  html = html.replace(/^\* (.+)$/gm, '<li class="md-li">$1</li>')

  // Ordered lists (1. text)
  html = html.replace(/^\d+\. (.+)$/gm, '<li class="md-li">$1</li>')

  // Wrap adjacent <li> in <ul>
  html = html.replace(/(<li class="md-li">.*?<\/li>)\n(?=<li)/g, '$1')
  html = html.replace(/((?:<li class="md-li">.*?<\/li>\n?)+)/g, (match) => {
    return `<ul class="md-ul">${match.trim()}</ul>`
  })

  // Line breaks
  html = html.replace(/\n/g, '<br/>')

  return html
}
</script>

<style>
/* Drawer custom class — use global styles for el-drawer body */
.ai-chat-drawer .el-drawer__body {
  padding: 0;
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}
.ai-chat-drawer .el-drawer__header {
  margin-bottom: 0;
  padding: 12px 16px;
  border-bottom: 1px solid #e6e6e6;
}
</style>

<style scoped>
.chat-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  width: 100%;
}
.chat-header-left {
  display: flex;
  align-items: center;
  gap: 6px;
}
.chat-title {
  font-size: 16px;
  font-weight: 600;
  color: #303133;
}

.chat-container {
  display: flex;
  height: 100%;
  overflow: hidden;
}

/* ===== Session Sidebar ===== */
.session-sidebar {
  width: 200px;
  min-width: 200px;
  border-right: 1px solid #e6e6e6;
  display: flex;
  flex-direction: column;
  background: #fafafa;
  transition: width 0.2s, min-width 0.2s;
}
.session-sidebar.collapsed {
  width: 48px;
  min-width: 48px;
}
.session-sidebar-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 12px;
  border-bottom: 1px solid #e6e6e6;
}
.sidebar-title {
  font-size: 13px;
  font-weight: 600;
  color: #606266;
}
.session-list {
  flex: 1;
  overflow-y: auto;
  padding: 4px;
}
.session-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 10px;
  border-radius: 6px;
  cursor: pointer;
  margin-bottom: 2px;
  transition: background 0.15s;
}
.session-item:hover {
  background: #ecf5ff;
}
.session-item.active {
  background: #d9ecff;
}
.session-item-content {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.session-title {
  font-size: 13px;
  color: #303133;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.session-time {
  font-size: 11px;
  color: #909399;
}
.session-delete-btn {
  opacity: 0;
  transition: opacity 0.15s;
}
.session-item:hover .session-delete-btn {
  opacity: 1;
}

/* ===== Chat Main ===== */
.chat-main {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-width: 0;
}

/* Message List */
.message-list {
  flex: 1;
  overflow-y: auto;
  padding: 16px 20px;
  background: #f5f7fa;
}
.welcome-area {
  text-align: center;
  padding-top: 60px;
}
.welcome-icon {
  margin-bottom: 16px;
}
.welcome-area h3 {
  margin: 0 0 8px;
  font-size: 20px;
  color: #303133;
}
.welcome-area p {
  margin: 0 0 24px;
  color: #909399;
  font-size: 14px;
}
.quick-prompts {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: 10px;
}
.prompt-chip {
  padding: 8px 16px;
  background: #fff;
  border: 1px solid #dcdfe6;
  border-radius: 20px;
  font-size: 13px;
  color: #409EFF;
  cursor: pointer;
  transition: all 0.2s;
}
.prompt-chip:hover {
  background: #ecf5ff;
  border-color: #409EFF;
}

/* Message Item */
.message-item {
  display: flex;
  gap: 12px;
  margin-bottom: 16px;
}
.message-item.user {
  flex-direction: row-reverse;
}
.message-avatar {
  width: 32px;
  height: 32px;
  border-radius: 50%;
  background: #e8eaed;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}
.message-item.user .message-avatar {
  background: #409EFF;
  color: #fff;
}
.message-body {
  max-width: 75%;
}
.message-item.user .message-body {
  text-align: right;
}
.message-role {
  font-size: 12px;
  color: #909399;
  margin-bottom: 4px;
}
.streaming-badge {
  color: #409EFF;
  font-size: 11px;
  animation: blink 1.2s infinite;
}
@keyframes blink {
  50% { opacity: 0.4; }
}
.message-content {
  background: #fff;
  padding: 10px 14px;
  border-radius: 12px;
  font-size: 14px;
  line-height: 1.7;
  color: #303133;
  box-shadow: 0 1px 2px rgba(0,0,0,0.04);
  word-break: break-word;
}
.message-item.user .message-content {
  background: #409EFF;
  color: #fff;
}

/* Typing indicator */
.typing-indicator {
  display: flex;
  gap: 4px;
  padding: 10px 14px;
}
.typing-indicator span {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: #409EFF;
  animation: typing-bounce 1.4s ease-in-out infinite;
}
.typing-indicator span:nth-child(2) { animation-delay: 0.2s; }
.typing-indicator span:nth-child(3) { animation-delay: 0.4s; }
@keyframes typing-bounce {
  0%, 80%, 100% { transform: scale(0.6); opacity: 0.4; }
  40% { transform: scale(1); opacity: 1; }
}

/* Chat error */
.chat-error {
  margin-bottom: 12px;
}

/* ===== Input Area ===== */
.input-area {
  padding: 12px 16px;
  border-top: 1px solid #e6e6e6;
  background: #fff;
}
.input-area :deep(.el-textarea__inner) {
  border-radius: 8px;
  font-size: 14px;
  line-height: 1.6;
}
.input-actions {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: 8px;
}
.input-hint {
  font-size: 12px;
  color: #c0c4cc;
}

/* ===== Markdown Styles ===== */
.message-content :deep(.code-block) {
  background: #1e1e1e;
  color: #d4d4d4;
  padding: 12px 16px;
  border-radius: 8px;
  margin: 8px 0;
  overflow-x: auto;
  font-size: 13px;
  line-height: 1.5;
  white-space: pre-wrap;
}
.message-content :deep(.inline-code) {
  background: rgba(0,0,0,0.06);
  padding: 2px 6px;
  border-radius: 4px;
  font-size: 13px;
  font-family: 'Consolas', 'Courier New', monospace;
}
.message-item.user .message-content :deep(.inline-code) {
  background: rgba(255,255,255,0.2);
}
.message-content :deep(.md-h2) {
  font-size: 16px;
  font-weight: 600;
  margin: 12px 0 6px;
  color: #303133;
}
.message-content :deep(.md-h3) {
  font-size: 15px;
  font-weight: 600;
  margin: 10px 0 4px;
  color: #303133;
}
.message-content :deep(.md-h4) {
  font-size: 14px;
  font-weight: 600;
  margin: 8px 0 4px;
  color: #303133;
}
.message-content :deep(.md-ul) {
  padding-left: 20px;
  margin: 4px 0;
}
.message-content :deep(.md-li) {
  margin: 2px 0;
  list-style: disc;
}
.message-content :deep(strong) {
  font-weight: 600;
  color: #303133;
}
.message-item.user .message-content :deep(strong) {
  color: #fff;
}
</style>
