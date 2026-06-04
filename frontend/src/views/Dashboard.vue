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
        router
        @select="handleMenuSelect"
      >
        <template v-for="menu in menuList" :key="menu.id">
          <!-- 有子菜单 -->
          <el-sub-menu
            v-if="menu.children && menu.children.length > 0"
            :index="String(menu.id)"
          >
            <template #title>
              <el-icon v-if="menu.icon"><component :is="menu.icon" /></el-icon>
              <span>{{ menu.name }}</span>
            </template>
            <el-menu-item
              v-for="child in menu.children"
              :key="child.id"
              :index="child.path"
            >
              <el-icon v-if="child.icon"><component :is="child.icon" /></el-icon>
              <span>{{ child.name }}</span>
            </el-menu-item>
          </el-sub-menu>

          <!-- 无子菜单的叶子节点（必须有 path） -->
          <el-menu-item
            v-else-if="menu.path"
            :index="menu.path"
          >
            <el-icon v-if="menu.icon"><component :is="menu.icon" /></el-icon>
            <span>{{ menu.name }}</span>
          </el-menu-item>
        </template>
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
          <el-tooltip content="查看系统帮助文档" placement="bottom">
            <el-button type="primary" link size="small" @click="openHelp">
              <el-icon :size="18"><Notebook /></el-icon>
              <span style="margin-left:4px">帮助</span>
            </el-button>
          </el-tooltip>
          <el-divider direction="vertical" />
          <el-icon><UserFilled /></el-icon>
          <span class="username">{{ username }}</span>
          <el-button type="danger" size="small" @click="handleLogout">退出登录</el-button>
        </div>
      </el-header>

      <!-- 内容区 -->
      <el-main class="main-content">
        <router-view />
      </el-main>
    </el-container>
  </el-container>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { ElMessageBox } from 'element-plus'
import { Search } from '@element-plus/icons-vue'
import { getUserMenus } from '../api/auth'

const router = useRouter()
const route = useRoute()

const activeMenu = computed(() => route.path)

function handleMenuSelect(index) {
  // 外部链接（http/https）在新标签页打开，同时阻止vue-router改变当前页URL
  if (index && (index.startsWith('http://') || index.startsWith('https://'))) {
    window.open(index, '_blank')
    router.replace(route.path)
  }
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
    router.push('/login')
  } catch {
    // 用户取消
  }
}

onMounted(fetchMenus)

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
</style>
