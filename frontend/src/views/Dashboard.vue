<template>
  <el-container class="dashboard-container">
    <!-- 左侧菜单 -->
    <el-aside width="220px" class="sidebar">
      <div class="sidebar-header">
        <el-icon :size="28" color="#409EFF"><Monitor /></el-icon>
        <span class="system-title">管理系统</span>
      </div>

      <el-menu
        :default-active="activeMenu"
        background-color="#304156"
        text-color="#bfcbd9"
        active-text-color="#409EFF"
        @select="handleMenuSelect"
      >
        <MenuItem v-for="menu in menuList" :key="menu.id" :menu="menu" />
      </el-menu>
    </el-aside>

    <!-- 右侧区域 -->
    <el-container>
      <!-- 顶部栏 -->
      <el-header class="topbar">
        <div class="topbar-left">
          <el-popover
            placement="bottom-start"
            :width="320"
            trigger="focus"
            :visible="searchVisible"
            :popper-style="{ padding: '8px' }"
            :show-arrow="false"
            :offset="4"
          >
            <template #reference>
              <el-input
                ref="searchInputRef"
                v-model="searchKeyword"
                placeholder="搜索菜单..."
                :prefix-icon="Search"
                @input="handleSearchInput"
                @focus="searchVisible = true"
                @blur="onSearchBlur"
                @keydown.up.prevent="navigateResult(-1)"
                @keydown.down.prevent="navigateResult(1)"
                @keydown.enter.prevent="selectResult"
                @keydown.escape="searchVisible = false"
                class="menu-search-input"
              />
            </template>
            <div class="search-results" v-if="searchVisible">
              <div v-if="searchKeyword && filteredMenus.length === 0" class="no-result">无匹配菜单</div>
              <div
                v-for="(item, idx) in filteredMenus"
                :key="item.path"
                class="search-result-item"
                :class="{ 'is-active': idx === activeResultIndex }"
                @mousedown.prevent="goToMenu(item)"
                @mouseenter="activeResultIndex = idx"
              >
                <el-icon v-if="item.icon" :size="16"><component :is="item.icon" /></el-icon>
                <div class="result-info">
                  <span class="result-name">{{ item.name }}</span>
                  <span class="result-parent" v-if="item.parentName">{{ item.parentName }}</span>
                </div>
              </div>
            </div>
          </el-popover>
        </div>
        <div class="topbar-right">
          <!-- AI 助手按钮 -->
          <el-tooltip content="AI 助手 - 帮你了解系统" placement="bottom">
            <el-button link size="small" @click="showAiChat = true">
              <el-icon :size="20" color="#409EFF"><ChatDotRound /></el-icon>
            </el-button>
          </el-tooltip>
          <el-tooltip content="查看系统帮助文档" placement="bottom">
            <el-button link size="small" @click="openHelp">
              <el-icon :size="18"><Notebook /></el-icon>
            </el-button>
          </el-tooltip>
          <!-- 通知铃铛 -->
          <el-popover placement="bottom-end" :width="340" trigger="click" :offset="8">
            <template #reference>
              <el-badge :value="unreadCount" :hidden="unreadCount === 0" :max="99">
                <el-button link @click="markAllRead()">
                  <el-icon :size="20"><Bell /></el-icon>
                </el-button>
              </el-badge>
            </template>
            <div class="notify-list" v-if="notifications.length > 0">
              <div v-for="n in notifications.slice(0, 20)" :key="n.id"
                   class="notify-item" :class="{ unread: !n.read }"
                   @click="goToInstance(n)">
                <div class="notify-dot"></div>
                <div class="notify-body">
                  <p class="notify-msg">{{ n.message }}</p>
                  <span class="notify-time">{{ formatNotifyTime(n.timestamp) }}</span>
                </div>
              </div>
              <el-button v-if="notifications.length > 0" link type="danger" size="small" style="margin-top:8px" @click="clearAll()">
                清空全部通知
              </el-button>
            </div>
            <el-empty v-else description="暂无通知" :image-size="60" />
          </el-popover>

          <el-divider direction="vertical" />
          <el-icon><UserFilled /></el-icon>
          <span class="username">{{ username }}</span>
          <el-button type="danger" size="small" @click="handleLogout">退出登录</el-button>
        </div>
      </el-header>

      <!-- 内容区 -->
      <el-main class="main-content">
        <!-- iframe 内嵌模式 -->
        <div v-if="iframeUrl" class="iframe-container">
          <div class="iframe-toolbar">
            <span class="iframe-title">{{ iframeTitle || iframeUrl }}</span>
            <el-button size="small" @click="closeIframe">
              <el-icon><Close /></el-icon> 关闭
            </el-button>
          </div>
          <iframe :src="iframeUrl" class="iframe-view" frameborder="0" />
        </div>
        <router-view v-else />
      </el-main>
    </el-container>
  </el-container>

  <!-- AI 助手面板 -->
  <AiChatPanel v-model:visible="showAiChat" />
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { ElMessageBox } from 'element-plus'
import { Search, Bell, Close, ChatDotRound } from '@element-plus/icons-vue'
import { getUserMenus } from '../api/auth'
import MenuItem from './MenuItem.vue'
import AiChatPanel from './AiChatPanel.vue'
import { useSignalR } from '../composables/useSignalR.js'

const router = useRouter()
const route = useRoute()

const activeMenu = computed(() => route.path)
const showAiChat = ref(false)

// SignalR 通知
const { notifications, unreadCount, connect: connectSignalR, disconnect: disconnectSignalR, markAllRead, clearAll } = useSignalR()
function goToInstance(n) { markAllRead(); if (n.instanceId) router.push(`/dashboard/workflow/instance/${n.instanceId}`) }
function formatNotifyTime(ts) {
  if (!ts) return ''
  const d = new Date(ts), now = new Date(), diff = now - d
  if (diff < 60000) return '刚刚'
  if (diff < 3600000) return `${Math.floor(diff/60000)} 分钟前`
  return d.toLocaleTimeString('zh-CN')
}

// 路径→菜单映射（用于快速查找 openType）
const pathMenuMap = computed(() => {
  const map = {}
  function walk(nodes) {
    for (const m of nodes) {
      if (m.path) map[m.path] = m
      if (m.children?.length) walk(m.children)
    }
  }
  walk(menuList.value)
  return map
})

// iframe 状态
const iframeUrl = ref('')
const iframeTitle = ref('')

function handleMenuSelect(index) {
  // 查找菜单获取 openType
  const menu = pathMenuMap.value[index]

  if (menu?.openType === 'iframe') {
    iframeUrl.value = index
    iframeTitle.value = menu?.name || ''
    return
  }

  if (menu?.openType === 'blank') {
    window.open(index, '_blank')
    return
  }

  // 外部链接（http/https），默认新标签打开
  if (index && (index.startsWith('http://') || index.startsWith('https://'))) {
    window.open(index, '_blank')
    return
  }

  // 内部路径正常导航
  if (index) {
    router.push(index)
  }
}

function closeIframe() {
  iframeUrl.value = ''
  iframeTitle.value = ''
}

const menuList = ref([])

const user = JSON.parse(localStorage.getItem('user') || '{}')
const username = ref(user.username || '未知用户')

async function fetchMenus() {
  try {
    const res = await getUserMenus()
    if (res.data.success) {
      menuList.value = res.data.data
    }
  } catch {
    // 菜单加载失败，使用默认菜单
  }
}

function openHelp() {
  window.open('http://localhost:5174', '_blank')
}

async function handleLogout() {
  try {
    await ElMessageBox.confirm('确定要退出登录吗？', '提示', {
      confirmButtonText: '确定',
      cancelButtonText: '取消',
      type: 'warning',
    })
    try {
      const { api } = await import('../api/auth')
      await api.post('/auth/logout')
    } catch { /* 忽略网络错误 */ }
    localStorage.removeItem('token')
    localStorage.removeItem('user')
    localStorage.removeItem('permissions')
    localStorage.removeItem('refresh_token')
    localStorage.removeItem('id_token')
    disconnectSignalR()
    router.push('/login')
  } catch {
    // 用户取消
  }
}

onMounted(() => {
  fetchMenus()
  connectSignalR()
})

// ========== 菜单搜索 ==========
const searchVisible = ref(false)
const searchKeyword = ref('')
const searchInputRef = ref(null)
const activeResultIndex = ref(0)

// 扁平化所有可导航菜单项
const flatMenus = computed(() => {
  const result = []
  function walk(items, parentName) {
    for (const m of items) {
      if (m.children && m.children.length > 0) {
        walk(m.children, m.name)
      }
      if (m.path && !(m.path.startsWith('http://') || m.path.startsWith('https://'))) {
        result.push({ name: m.name, path: m.path, icon: m.icon, parentName })
      }
    }
  }
  walk(menuList.value, null)
  return result
})

const filteredMenus = computed(() => {
  if (!searchKeyword.value) return flatMenus.value.slice(0, 5)
  const kw = searchKeyword.value.toLowerCase()
  return flatMenus.value
    .filter(m => m.name.toLowerCase().includes(kw) || (m.parentName && m.parentName.toLowerCase().includes(kw)))
    .slice(0, 8)
})

function handleSearchInput() {
  searchVisible.value = true
  activeResultIndex.value = 0
}

function onSearchBlur() {
  // 延迟关闭让 mousedown 事件先触发
  setTimeout(() => { searchVisible.value = false }, 150)
}

function navigateResult(delta) {
  const len = filteredMenus.value.length
  if (len === 0) return
  activeResultIndex.value = ((activeResultIndex.value + delta) % len + len) % len
}

function selectResult() {
  const item = filteredMenus.value[activeResultIndex.value]
  if (item) goToMenu(item)
}

function goToMenu(item) {
  searchVisible.value = false
  searchKeyword.value = ''
  activeResultIndex.value = 0
  if (item.path.startsWith('http://') || item.path.startsWith('https://')) {
    window.open(item.path, '_blank')
  } else {
    router.push(item.path)
  }
}

</script>

<style scoped>
.dashboard-container {
  height: 100vh;
}

.sidebar {
  background-color: #304156;
  overflow-y: auto;
}

.sidebar-header {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 20px 16px;
  color: #fff;
  border-bottom: 1px solid rgba(255,255,255,0.1);
}

.system-title {
  font-size: 18px;
  font-weight: 600;
  white-space: nowrap;
}

.sidebar .el-menu {
  border-right: none;
}

.topbar {
  --el-header-height: 56px;
  background: #fff;
  display: flex;
  align-items: center;
  justify-content: space-between;
  border-bottom: 1px solid #e6e6e6;
  padding: 0 20px;
  height: 56px;
}

.topbar-left {
  display: flex;
  align-items: center;
}

/* ===== 菜单搜索 ===== */
.menu-search-input {
  width: 240px;
}
.search-results {
  max-height: 340px;
  overflow-y: auto;
}
.no-result {
  text-align: center;
  color: #909399;
  padding: 24px 0;
  font-size: 13px;
}
.search-result-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 12px;
  border-radius: 6px;
  cursor: pointer;
  transition: background 0.15s;
}
.search-result-item:hover,
.search-result-item.is-active {
  background: #ecf5ff;
}
.result-info {
  display: flex;
  flex-direction: column;
  gap: 2px;
  overflow: hidden;
}
.result-name {
  font-size: 14px;
  color: #303133;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.result-parent {
  font-size: 12px;
  color: #909399;
}

.topbar-right {
  display: flex;
  align-items: center;
  gap: 10px;
  font-size: 14px;
  color: #333;
}

.username {
  font-weight: 500;
  display: inline-flex;
  align-items: center;
}

.main-content {
  background: #f0f2f5;
  padding: 20px;
}

/* 通知列表样式 */
.notify-list { max-height: 360px; overflow-y: auto; }
.notify-item { display: flex; align-items: flex-start; gap: 10px; padding: 10px 8px; border-bottom: 1px solid #f0f0f0; cursor: pointer; border-radius: 4px; transition: background 0.15s; }
.notify-item:hover { background: #f5f7fa; }
.notify-item.unread { background: #ecf5ff; }
.notify-dot { width: 8px; height: 8px; border-radius: 50%; margin-top: 6px; flex-shrink: 0; background: #409eff; }
.notify-item:not(.unread) .notify-dot { background: #c0c4cc; }
.notify-body { flex: 1; min-width: 0; }
.notify-msg { margin: 0; font-size: 13px; color: #303133; line-height: 1.5; }
.notify-time { font-size: 11px; color: #909399; }

/* iframe 内嵌 */
.iframe-container {
  display: flex;
  flex-direction: column;
  height: calc(100vh - 76px);
}
.iframe-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 16px;
  background: #fff;
  border-bottom: 1px solid #e6e6e6;
  flex-shrink: 0;
}
.iframe-title {
  font-size: 14px;
  font-weight: 500;
  color: #303133;
}
.iframe-view {
  flex: 1;
  border: none;
  width: 100%;
}
</style>
