<template>
  <div class="menu-management" v-loading="loading">
    <div class="top-section">
      <div class="toolbar-left">
        <el-button type="primary" @click="openCreate(null)">
          <el-icon><Plus /></el-icon> 新增顶级菜单
        </el-button>
        <el-button @click="saveOrder" :loading="saving">
          <el-icon><Check /></el-icon> 保存排序
        </el-button>
        <el-button @click="fetchMenus">
          <el-icon><RefreshRight /></el-icon> 刷新
        </el-button>
      </div>
      <span class="hint">拖拽节点可调整顺序和父子关系，调整后点击「保存排序」生效</span>
    </div>

    <div class="content-area">
      <!-- 左侧：菜单树 -->
      <div class="tree-panel">
        <el-tree
          ref="treeRef"
          :data="treeData"
          :props="{ label: 'name', children: 'children' }"
          node-key="id"
          draggable
          :allow-drop="allowDrop"
          :allow-drag="allowDrag"
          :expand-on-click-node="false"
          default-expand-all
          @node-drop="onNodeDrop"
        >
          <template #default="{ data }">
            <div class="tree-node">
              <el-icon v-if="data.icon && getIcon(data.icon)" :size="16" class="node-icon">
                <component :is="getIcon(data.icon)" />
              </el-icon>
              <span class="node-name">{{ data.name }}</span>
              <span class="node-path">{{ data.path || '(无路径)' }}</span>
              <el-tag v-if="data.isBuiltIn" size="small" type="warning" class="node-tag">内置</el-tag>
              <el-tag v-if="!data.isVisible" size="small" type="info" class="node-tag">已隐藏</el-tag>
              <el-tag
                v-if="data.openType && data.openType !== 'self'"
                size="small"
                :type="data.openType === 'iframe' ? 'warning' : 'primary'"
                class="node-tag"
              >
                {{ data.openType === 'blank' ? '新标签' : 'iframe' }}
              </el-tag>
              <div class="node-actions" @click.stop>
                <el-button link size="small" type="primary" @click="openEdit(data)">
                  <el-icon><Edit /></el-icon>
                </el-button>
                <el-button link size="small" type="success" @click="openCreate(data)">
                  <el-icon><Plus /></el-icon>
                </el-button>
                <el-popconfirm
                  v-if="!data.isBuiltIn"
                  title="删除此菜单？有子菜单将一并删除"
                  @confirm="doDelete(data)"
                >
                  <template #reference>
                    <el-button link size="small" type="danger">
                      <el-icon><Delete /></el-icon>
                    </el-button>
                  </template>
                </el-popconfirm>
                <el-tooltip
                  v-else
                  content="内置菜单不可删除，仅可调整顺序和层级"
                  placement="top"
                >
                  <el-button link size="small" type="info" disabled>
                    <el-icon><Delete /></el-icon>
                  </el-button>
                </el-tooltip>
              </div>
            </div>
          </template>
        </el-tree>
      </div>
    </div>

    <!-- 编辑抽屉 -->
    <el-drawer
      v-model="drawerVisible"
      :title="editingId ? '编辑菜单' : '新增菜单'"
      size="480px"
      append-to-body
    >
      <el-form :model="form" label-width="80px" class="menu-form">
        <el-form-item label="名称" required>
          <el-input v-model="form.name" placeholder="菜单显示名称" />
        </el-form-item>
        <el-form-item label="路径">
          <el-input v-model="form.path" placeholder="内部路由 或 https://外部地址" />
        </el-form-item>
        <el-form-item label="图标">
          <div class="menu-icon-picker">
            <div class="menu-icon-current" v-if="form.icon">
              <el-icon :size="20"><component :is="getIcon(form.icon)" /></el-icon>
              <span>{{ form.icon }}</span>
              <el-button link size="small" type="danger" @click="form.icon = ''">清除</el-button>
            </div>
            <el-popover placement="bottom-start" :width="420" trigger="click">
              <template #reference>
                <el-button size="small">
                  {{ form.icon ? '更换图标' : '选择图标' }}
                </el-button>
              </template>
              <div class="menu-icon-grid">
                <div
                  v-for="ic in iconList"
                  :key="ic"
                  class="menu-icon-cell"
                  :class="{ selected: form.icon === ic }"
                  @click="form.icon = ic"
                >
                  <el-icon :size="18">
                    <component :is="getIcon(ic)" />
                  </el-icon>
                  <span class="menu-icon-label">{{ ic }}</span>
                </div>
              </div>
            </el-popover>
          </div>
        </el-form-item>
        <el-form-item label="打开方式">
          <el-radio-group v-model="form.openType">
            <el-radio value="self">当前页</el-radio>
            <el-radio value="blank">新标签</el-radio>
            <el-radio value="iframe">内嵌 iframe</el-radio>
          </el-radio-group>
        </el-form-item>
        <el-form-item label="权限编码">
          <el-select v-model="form.permissionCode" placeholder="选择权限（可留空）" clearable filterable style="width: 100%">
            <el-option
              v-for="p in permissionOptions"
              :key="p.code"
              :label="`${p.name} (${p.code})`"
              :value="p.code"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="组件名">
          <el-input v-model="form.component" placeholder="Vue 组件名（可选）" />
        </el-form-item>
        <el-form-item label="可见">
          <el-switch v-model="form.isVisible" />
        </el-form-item>
        <el-form-item label="父菜单">
          <span v-if="form.parentId">{{ parentName }}</span>
          <span v-else style="color:#909399">顶级菜单</span>
        </el-form-item>
        <el-form-item>
          <el-button type="primary" @click="doSave" :loading="saving">保存</el-button>
          <el-button @click="drawerVisible = false">取消</el-button>
        </el-form-item>
      </el-form>
    </el-drawer>
  </div>
</template>

<script setup>
import { ref, reactive, computed, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Plus, Check, RefreshRight, Edit, Delete } from '@element-plus/icons-vue'
import * as Icons from '@element-plus/icons-vue'
import { getAllMenus, createMenu, updateMenu, deleteMenu, batchUpdateMenus, getPermissions } from '../api/auth'

const loading = ref(false)
const saving = ref(false)
const treeRef = ref(null)
const rawList = ref([])       // 扁平原始列表
const treeData = ref([])      // 树结构
const drawerVisible = ref(false)
const editingId = ref(null)
const draggedNode = ref(null)

const form = reactive({
  name: '',
  path: '',
  icon: '',
  openType: 'self',
  permissionCode: '',
  component: '',
  isVisible: true,
  parentId: null,
})

const permissionOptions = ref([])

// 根据名称获取图标组件实例
function getIcon(name) {
  return Icons[name]
}

// 常用 Element Plus 图标（所有名称均来自 @element-plus/icons-vue 实际导出）
const iconList = [
  'HomeFilled', 'Setting', 'List', 'Lock', 'Document', 'Promotion',
  'Edit', 'Select', 'Menu', 'Link', 'Key', 'Notebook',
  'Clock', 'Monitor', 'Search', 'User', 'UserFilled', 'Avatar',
  'Bell', 'PieChart', 'Share', 'Star', 'Warning', 'Filter',
  'InfoFilled', 'CirclePlus', 'Help', 'Calendar', 'DataAnalysis',
  'Histogram', 'Box', 'Folder', 'Goods', 'Message',
  'Money', 'OfficeBuilding', 'Picture', 'TrophyBase', 'VideoCamera',
  'View', 'ChatDotSquare', 'Files', 'Guide', 'Discount',
  'Timer', 'TrendCharts', 'Postcard', 'TakeawayBox', 'Tickets',
  'Van', 'Reading', 'School', 'Coin', 'Paperclip',
  'Connection', 'Grid', 'Checked', 'Collection', 'Management',
  'Medal', 'ShoppingCart', 'Refresh', 'Pointer', 'Tools',
]

const parentName = computed(() => {
  if (!form.parentId) return ''
  const p = rawList.value.find(m => m.id === form.parentId)
  return p ? p.name : ''
})

function buildTree(flat) {
  const map = {}, roots = []
  flat.forEach(m => { map[m.id] = { ...m, children: [] } })
  flat.forEach(m => {
    if (m.parentId && map[m.parentId]) {
      map[m.parentId].children.push(map[m.id])
    } else {
      roots.push(map[m.id])
    }
  })
  // 按 sortOrder 排序
  const sortFn = arr => { arr.sort((a, b) => a.sortOrder - b.sortOrder); arr.forEach(m => { if (m.children.length) sortFn(m.children) }) }
  sortFn(roots)
  return roots
}

async function fetchMenus() {
  loading.value = true
  try {
    const res = await getAllMenus()
    // API 返回树结构，需要先展平
    const tree = res.data.data || []
    rawList.value = flattenTree(tree)
    treeData.value = tree
  } catch { ElMessage.error('加载菜单失败') }
  finally { loading.value = false }
}

function flattenTree(tree) {
  const result = []
  function walk(nodes) {
    for (const n of nodes) {
      result.push({ id: n.id, name: n.name, path: n.path, icon: n.icon, parentId: n.parentId, sortOrder: n.sortOrder, permissionCode: n.permissionCode, component: n.component, openType: n.openType, isVisible: n.isVisible, isBuiltIn: n.isBuiltIn })
      if (n.children?.length) walk(n.children)
    }
  }
  walk(tree)
  return result
}

// 收集当前树中所有节点的 id / parentId / sortOrder
function collectSortData(nodes, parentId = null, startOrder = 0) {
  const items = []
  nodes.forEach((n, i) => {
    items.push({ id: n.id, parentId, sortOrder: startOrder + i })
    if (n.children?.length) {
      items.push(...collectSortData(n.children, n.id, 0))
    }
  })
  return items
}

async function saveOrder() {
  saving.value = true
  try {
    const items = collectSortData(treeData.value)
    await batchUpdateMenus({ menus: items })
    ElMessage.success('菜单排序已保存')
    await fetchMenus()
  } catch (e) {
    ElMessage.error(e.response?.data?.message || '保存失败')
  } finally { saving.value = false }
}

function openCreate(parent) {
  editingId.value = null
  form.name = ''
  form.path = ''
  form.icon = ''
  form.openType = 'self'
  form.permissionCode = ''
  form.component = ''
  form.isVisible = true
  form.parentId = parent ? parent.id : null
  drawerVisible.value = true
}

function openEdit(node) {
  editingId.value = node.id
  form.name = node.name
  form.path = node.path || ''
  form.icon = node.icon || ''
  form.openType = node.openType || 'self'
  form.permissionCode = node.permissionCode || ''
  form.component = node.component || ''
  form.isVisible = node.isVisible !== false
  form.parentId = node.parentId !== undefined ? node.parentId : null
  drawerVisible.value = true
}

async function doSave() {
  if (!form.name.trim()) { ElMessage.warning('请输入菜单名称'); return }
  saving.value = true
  try {
    const data = { ...form, name: form.name.trim() }
    if (editingId.value) {
      await updateMenu(editingId.value, data)
      ElMessage.success('菜单已更新')
    } else {
      await createMenu(data)
      ElMessage.success('菜单已创建')
    }
    drawerVisible.value = false
    await fetchMenus()
  } catch (e) {
    ElMessage.error(e.response?.data?.message || '保存失败')
  } finally { saving.value = false }
}

async function doDelete(node) {
  try {
    await deleteMenu(node.id)
    ElMessage.success('已删除')
    await fetchMenus()
  } catch (e) {
    ElMessage.error(e.response?.data?.message || '删除失败')
  }
}

// 拖拽控制
function allowDrag(node) {
  return true
}

function allowDrop(dragNode, dropNode, type) {
  // 不能拖到自己或自己的子节点上
  if (dragNode.data.id === dropNode.data.id) return false
  // within: 成为 dropNode 的子节点
  // before/after: 与 dropNode 同级
  return true
}

function onNodeDrop() {
  // el-tree draggable 会就地修改数据，从 store 中重建树结构以确保一致性
  rebuildTreeFromStore()
}

function rebuildTreeFromStore() {
  const root = treeRef.value?.store?.root
  if (!root || !root.childNodes) return
  const build = (nodes) => {
    if (!nodes) return []
    return nodes.map(n => ({
      ...n.data,
      children: build(n.childNodes)
    }))
  }
  treeData.value = build(root.childNodes)
}

async function loadPermissions() {
  try {
    const res = await getPermissions()
    permissionOptions.value = res.data.data || []
  } catch { permissionOptions.value = [] }
}

onMounted(() => {
  fetchMenus()
  loadPermissions()
})
</script>

<style scoped>
.menu-management {
  background: #fff;
  padding: 20px;
  border-radius: 4px;
  min-height: 70vh;
}
.top-section {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  margin-bottom: 20px;
  flex-wrap: wrap;
}
.toolbar-left {
  display: flex;
  gap: 8px;
}
.hint {
  color: #909399;
  font-size: 13px;
}
.content-area {
  display: flex;
  gap: 20px;
}
.tree-panel {
  flex: 1;
  border: 1px solid #ebeef5;
  border-radius: 6px;
  padding: 12px 16px;
  min-height: 50vh;
  background: #fafafa;
}

/* 树节点样式 */
.tree-node {
  display: flex;
  align-items: center;
  gap: 8px;
  flex: 1;
  min-width: 0;
  padding: 4px 0;
}
.tree-node .node-icon { color: #409EFF; flex-shrink: 0; }
.node-name { font-weight: 500; white-space: nowrap; }
.node-path {
  font-size: 12px;
  color: #909399;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 200px;
}
.node-tag { flex-shrink: 0; margin-left: 4px; }
.node-actions {
  display: flex;
  gap: 2px;
  margin-left: auto;
  flex-shrink: 0;
}

/* 使 el-tree 节点支持 flex 布局 */
:deep(.el-tree-node__content) {
  height: auto;
  min-height: 36px;
  padding: 4px 8px;
}
:deep(.el-tree-node__label) {
  flex: 1;
  min-width: 0;
}
:deep(.el-tree) {
  background: transparent;
}

/* 表单 */
.menu-form {
  padding: 0 12px;
}

/* 图标选择器（menu-icon- 前缀避免与 form-create 的 icon.css 类名冲突） */
.menu-icon-picker { width: 100%; }
.menu-icon-current {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 8px;
  padding: 6px 10px;
  background: #f5f7fa;
  border-radius: 4px;
  font-size: 13px;
}
.menu-icon-grid {
  display: grid;
  grid-template-columns: repeat(5, 1fr);
  gap: 2px;
  max-height: 340px;
  overflow-y: auto;
}
.menu-icon-cell {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 2px;
  padding: 6px 2px;
  border-radius: 4px;
  cursor: pointer;
  transition: background 0.15s;
  min-height: 52px;
}
.menu-icon-cell:hover { background: #ecf5ff; }
.menu-icon-cell.selected { background: #d9ecff; color: #409EFF; }
.menu-icon-label {
  font-size: 9px;
  color: #909399;
  text-align: center;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  max-width: 100%;
}
</style>
