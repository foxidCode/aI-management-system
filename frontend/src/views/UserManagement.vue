<template>
  <div class="user-management">
    <!-- 顶部：搜索 + 操作按钮 -->
    <div class="top-section">
      <el-input
        v-model="keyword"
        placeholder="搜索用户名、邮箱、工号、身份证号"
        clearable
        style="width: 300px"
        @keyup.enter="handleSearch"
        @clear="handleSearch"
      >
        <template #prefix>
          <el-icon><Search /></el-icon>
        </template>
      </el-input>
      <el-button type="primary" @click="handleSearch">
        <el-icon><Search /></el-icon> 搜索
      </el-button>
      <div class="toolbar">
        <el-button type="primary" @click="openCreateDialog">
          <el-icon><Plus /></el-icon> 新增
        </el-button>
        <el-button @click="fetchUsers">
          <el-icon><RefreshRight /></el-icon> 刷新
        </el-button>
        <el-button type="warning" :disabled="selectedIds.length === 0" @click="handleBatchResetPwd">
          <el-icon><Refresh /></el-icon> 重置密码
        </el-button>
        <el-button type="info" :disabled="selectedIds.length === 0" @click="handleBatchFreeze(true)">
          <el-icon><Lock /></el-icon> 批量冻结
        </el-button>
        <el-button type="success" :disabled="selectedIds.length === 0" @click="handleBatchFreeze(false)">
          <el-icon><Unlock /></el-icon> 批量解冻
        </el-button>
        <el-button type="danger" :disabled="selectedIds.length === 0" @click="handleBatchKick">
          <el-icon><SwitchButton /></el-icon> 批量踢出
        </el-button>
        <el-button type="danger" :disabled="selectedIds.length === 0" @click="handleBatchDelete" plain>
          <el-icon><Delete /></el-icon> 批量删除
        </el-button>
      </div>
    </div>

    <!-- 用户表格 -->
    <el-table
      :data="users"
      stripe
      border
      style="width: 100%; margin-top: 16px"
      @selection-change="handleSelectionChange"
      @sort-change="handleSortChange"
      v-loading="loading"
    >
      <el-table-column type="selection" width="50" />
      <el-table-column prop="username" label="用户名" width="120" sortable="custom" />
      <el-table-column prop="email" label="邮箱" width="180" sortable="custom" />
      <el-table-column prop="gender" label="性别" width="80" sortable="custom" />
      <el-table-column prop="idCard" label="身份证号" width="180" sortable="custom" />
      <el-table-column prop="employeeId" label="工号" width="120" sortable="custom" />
      <el-table-column prop="remark" label="备注" min-width="150" show-overflow-tooltip />
      <el-table-column prop="isFrozen" label="状态" width="90" sortable="custom">
        <template #default="{ row }">
          <el-tag :type="row.isFrozen ? 'danger' : 'success'" size="small">
            {{ row.isFrozen ? '已冻结' : '正常' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column label="在线" width="70">
        <template #default="{ row }">
          <span v-if="row.isOnline" class="online-dot" title="在线">🟢</span>
          <span v-else class="offline-dot" title="离线">⚫</span>
        </template>
      </el-table-column>
      <el-table-column label="角色" width="150" show-overflow-tooltip>
        <template #default="{ row }">
          <el-tag
            v-for="(name, idx) in row.roleNames"
            :key="idx"
            size="small"
            type="warning"
            style="margin-right: 4px"
          >
            {{ name }}
          </el-tag>
          <span v-if="!row.roleNames || row.roleNames.length === 0" style="color: #999">未分配</span>
        </template>
      </el-table-column>
      <el-table-column prop="createdAt" label="创建时间" width="170" sortable="custom" />
      <el-table-column prop="updatedAt" label="更新时间" width="170" sortable="custom" />
      <el-table-column label="操作" width="340" fixed="right">
        <template #default="{ row }">
          <el-button size="small" type="primary" @click="openEditDialog(row)">修改</el-button>
          <el-button v-if="canAssignRole" size="small" type="warning" @click="openRoleDialog(row)">分配角色</el-button>
          <el-button v-if="canAssignRole" size="small" type="success" @click="openDirectPermDialog(row)">直接授权</el-button>
          <el-button
            v-if="row.username !== 'admin'"
            size="small"
            :type="row.isFrozen ? 'success' : 'info'"
            @click="handleToggleFreeze(row)"
          >
            {{ row.isFrozen ? '解冻' : '冻结' }}
          </el-button>
          <el-button
            v-if="row.isOnline && row.username !== currentUsername"
            size="small"
            type="danger"
            @click="handleKick([row.id])"
          >
            踢出
          </el-button>
          <el-button
            v-if="row.username !== 'admin' && row.username !== currentUsername"
            size="small"
            type="danger"
            plain
            @click="handleDelete(row)"
          >
            删除
          </el-button>
        </template>
      </el-table-column>
    </el-table>

    <!-- 分页 -->
    <div class="pagination-wrapper">
      <span class="total-info">共 {{ total }} 条</span>
      <el-pagination
        v-model:current-page="currentPage"
        v-model:page-size="pageSize"
        :page-sizes="[10, 20, 50, 100]"
        :total="total"
        layout="sizes, prev, pager, next, jumper"
        background
        @size-change="fetchUsers"
        @current-change="fetchUsers"
      />
    </div>

    <!-- 新增/编辑弹窗 -->
    <el-dialog
      v-model="dialogVisible"
      :title="isEdit ? '修改用户信息' : '新增用户'"
      width="520px"
      :close-on-click-modal="false"
    >
      <el-form ref="formRef" :model="form" :rules="rules" label-width="90px">
        <el-form-item label="用户名" prop="username">
          <el-input v-model="form.username" :disabled="isEdit" placeholder="请输入用户名" />
        </el-form-item>
        <el-form-item label="邮箱" prop="email">
          <el-input v-model="form.email" placeholder="请输入邮箱" />
        </el-form-item>
        <el-form-item label="性别">
          <el-select v-model="form.gender" placeholder="请选择" clearable style="width: 100%">
            <el-option label="男" value="男" />
            <el-option label="女" value="女" />
          </el-select>
        </el-form-item>
        <el-form-item label="身份证号">
          <el-input v-model="form.idCard" placeholder="请输入身份证号" />
        </el-form-item>
        <el-form-item label="工号">
          <el-input v-model="form.employeeId" placeholder="请输入工号" />
        </el-form-item>
        <el-form-item label="备注">
          <el-input v-model="form.remark" type="textarea" :rows="3" placeholder="请输入备注" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="submitLoading" @click="handleSubmit">
          {{ submitLoading ? '保存中...' : '保存' }}
        </el-button>
      </template>
    </el-dialog>

    <!-- 分配角色弹窗 -->
    <el-dialog
      v-model="roleDialogVisible"
      title="分配角色"
      width="560px"
      :close-on-click-modal="false"
    >
      <p style="margin-bottom: 12px; color: #666;">
        为用户 <strong>{{ roleUser.username }}</strong> 分配角色
        <span style="margin-left: 12px; color: #409eff">
          已选 {{ selectedRoleIds.length }} 个角色
        </span>
      </p>

      <!-- 搜索 + 快捷操作 -->
      <div class="role-toolbar">
        <el-input
          v-model="roleSearchKeyword"
          placeholder="搜索角色名称或描述"
          clearable
          style="flex: 1"
          @input="handleRoleSearch"
          @clear="handleRoleSearch"
        >
          <template #prefix>
            <el-icon><Search /></el-icon>
          </template>
        </el-input>
        <el-button size="small" text type="primary" @click="selectAllRoles">全选</el-button>
        <el-button size="small" text type="warning" @click="clearRoleSelection">清空</el-button>
      </div>

      <!-- 角色列表（服务端搜索 + 分页） -->
      <el-table
        ref="roleTableRef"
        :data="allRoles"
        row-key="id"
        max-height="360"
        stripe
        v-loading="roleLoading"
        @selection-change="handleRoleSelectionChange"
        v-if="roleTotal > 0 || roleLoading"
      >
        <el-table-column type="selection" width="50" reserve-selection />
        <el-table-column prop="name" label="角色名称" width="140" />
        <el-table-column prop="description" label="描述" show-overflow-tooltip />
      </el-table>
      <el-empty
        v-if="!roleLoading && roleTotal === 0 && roleSearchKeyword"
        description="未找到匹配的角色"
        :image-size="80"
      />
      <el-empty
        v-if="!roleLoading && roleTotal === 0 && !roleSearchKeyword"
        description="暂无可分配角色，请先创建角色"
        :image-size="80"
      />

      <!-- 分页（服务端） -->
      <div class="role-pagination" v-if="roleTotal > rolePageSize">
        <el-pagination
          v-model:current-page="rolePage"
          :page-size="rolePageSize"
          :total="roleTotal"
          layout="total, prev, pager, next"
          background
          small
          @current-change="handleRolePageChange"
        />
      </div>

      <template #footer>
        <el-button @click="roleDialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="roleSubmitLoading" @click="handleRoleSubmit">
          {{ roleSubmitLoading ? '保存中...' : '保存' }}
        </el-button>
      </template>
    </el-dialog>

    <!-- 直接授权弹窗 -->
    <el-dialog
      v-model="directPermDialogVisible"
      title="直接授权"
      width="640px"
      :close-on-click-modal="false"
    >
      <p style="margin-bottom: 12px; color: #666;">
        为用户 <strong>{{ directPermUser.username }}</strong> 直接授予权限（跳过角色，与角色权限合并生效）
      </p>
      <div class="perm-toolbar">
        <el-input
          v-model="permSearchKeyword"
          placeholder="搜索权限名称或编码"
          clearable
          style="flex: 1"
          @input="filterPermOptions"
          @clear="filterPermOptions"
        >
          <template #prefix><el-icon><Search /></el-icon></template>
        </el-input>
        <el-button size="small" text type="primary" @click="selectAllPerms">全选</el-button>
        <el-button size="small" text type="warning" @click="clearPermSelection">清空</el-button>
      </div>

      <el-table
        ref="permTableRef"
        :data="filteredPermOptions"
        row-key="id"
        max-height="400"
        stripe
        v-loading="permLoading"
        @selection-change="handlePermSelectionChange"
      >
        <el-table-column type="selection" width="50" reserve-selection />
        <el-table-column prop="name" label="权限名称" width="160" />
        <el-table-column prop="code" label="权限编码" width="180" />
        <el-table-column prop="description" label="描述" show-overflow-tooltip />
      </el-table>

      <el-empty
        v-if="!permLoading && filteredPermOptions.length === 0"
        description="暂无可用权限"
        :image-size="80"
      />

      <template #footer>
        <el-button @click="directPermDialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="directPermSubmitLoading" @click="handleDirectPermSubmit">
          {{ directPermSubmitLoading ? '保存中...' : '保存' }}
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, computed, onMounted, nextTick } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import {
  getUsers,
  createUser,
  updateUser,
  resetPassword,
  batchFreeze,
  kickUsers,
  deleteUser,
  batchDeleteUsers,
  getRoles,
  getUserRoles,
  assignUserRoles,
  getPermissions,
  getUserDirectPermissions,
  grantUserPermission,
  revokeUserPermission,
} from '../api/auth'

const users = ref([])
const selectedIds = ref([])
const loading = ref(false)

// 权限检查
const currentUsername = (JSON.parse(localStorage.getItem('user') || '{}')).username || ''
const permissions = JSON.parse(localStorage.getItem('permissions') || '[]')
const canAssignRole = computed(() => permissions.includes('role:manage'))
const dialogVisible = ref(false)
const isEdit = ref(false)
const editingId = ref(null)
const formRef = ref(null)
const submitLoading = ref(false)

// 分页 & 搜索 & 排序
const currentPage = ref(1)
const pageSize = ref(10)
const total = ref(0)
const keyword = ref('')
const sortField = ref('')
const sortOrder = ref('')

const form = reactive({
  username: '',
  email: '',
  gender: '',
  idCard: '',
  employeeId: '',
  remark: '',
})

const rules = {
  username: [
    { required: true, message: '请输入用户名', trigger: 'blur' },
    { min: 3, message: '用户名至少3个字符', trigger: 'blur' },
  ],
  email: [
    { required: true, message: '请输入邮箱', trigger: 'blur' },
    { type: 'email', message: '邮箱格式不正确', trigger: 'blur' },
  ],
}

function resetForm() {
  form.username = ''
  form.email = ''
  form.gender = ''
  form.idCard = ''
  form.employeeId = ''
  form.remark = ''
  formRef.value?.clearValidate()
}

function openCreateDialog() {
  isEdit.value = false
  editingId.value = null
  resetForm()
  dialogVisible.value = true
}

function openEditDialog(row) {
  isEdit.value = true
  editingId.value = row.id
  form.username = row.username
  form.email = row.email
  form.gender = row.gender || ''
  form.idCard = row.idCard || ''
  form.employeeId = row.employeeId || ''
  form.remark = row.remark || ''
  dialogVisible.value = true
}

function handleSelectionChange(selection) {
  selectedIds.value = selection.map(s => s.id)
}

function handleSearch() {
  currentPage.value = 1
  fetchUsers()
}

function handleSortChange({ prop, order }) {
  sortField.value = prop || ''
  sortOrder.value = order || ''
  fetchUsers()
}

async function fetchUsers() {
  loading.value = true
  try {
    const params = {
      page: currentPage.value,
      pageSize: pageSize.value,
    }
    if (keyword.value) params.keyword = keyword.value
    if (sortField.value) params.sortField = sortField.value
    if (sortOrder.value) params.sortOrder = sortOrder.value

    const res = await getUsers(params)
    if (res.data.success) {
      users.value = res.data.data.list
      total.value = res.data.data.total
    }
  } catch {
    ElMessage.error('获取用户列表失败')
  } finally {
    loading.value = false
  }
}

async function handleSubmit() {
  const valid = await formRef.value.validate().catch(() => false)
  if (!valid) return

  submitLoading.value = true
  try {
    if (isEdit.value) {
      await updateUser(editingId.value, {
        email: form.email,
        gender: form.gender || null,
        idCard: form.idCard || null,
        employeeId: form.employeeId || null,
        remark: form.remark || null,
      })
      ElMessage.success('修改成功')
    } else {
      await createUser({
        username: form.username,
        email: form.email,
        gender: form.gender || null,
        idCard: form.idCard || null,
        employeeId: form.employeeId || null,
        remark: form.remark || null,
      })
      ElMessage.success('新增成功，默认密码为 password')
    }
    dialogVisible.value = false
    await fetchUsers()
  } catch (err) {
    ElMessage.error(err.response?.data?.message || '操作失败')
  } finally {
    submitLoading.value = false
  }
}

async function handleBatchResetPwd() {
  try {
    await ElMessageBox.confirm(
      `确定要将选中的 ${selectedIds.value.length} 个用户的密码重置为 password 吗？`,
      '重置密码',
      { type: 'warning' }
    )
    const res = await resetPassword(selectedIds.value)
    ElMessage.success(res.data.message)
    await fetchUsers()
  } catch { /* 取消操作 */ }
}

async function handleToggleFreeze(row) {
  const action = row.isFrozen ? '解冻' : '冻结'
  try {
    await ElMessageBox.confirm(`确定要${action}用户 "${row.username}" 吗？`, `${action}用户`, { type: 'warning' })
    // 单个用户冻结用 toggleFreeze API（已废弃改用 batch）
    await batchFreeze([row.id], !row.isFrozen)
    ElMessage.success(`用户已${action}`)
    await fetchUsers()
  } catch { /* 取消操作 */ }
}

async function handleBatchFreeze(freeze) {
  const action = freeze ? '冻结' : '解冻'
  try {
    await ElMessageBox.confirm(
      `确定要${action}选中的 ${selectedIds.value.length} 个用户吗？`,
      `批量${action}`,
      { type: 'warning' }
    )
    const res = await batchFreeze(selectedIds.value, freeze)
    ElMessage.success(res.data.message)
    await fetchUsers()
  } catch { /* 取消操作 */ }
}

// ========== 踢出用户 ==========

async function handleKick(userIds) {
  try {
    await ElMessageBox.confirm(
      '确定要强制踢出该用户吗？踢出后用户需重新登录。',
      '踢出确认',
      { type: 'warning' }
    )
    await kickUsers(userIds)
    ElMessage.success('已踢出')
    await fetchUsers()
  } catch { /* 取消 */ }
}

async function handleBatchKick() {
  // 过滤掉自己
  const ids = selectedIds.value.filter(id => {
    const user = users.value.find(u => u.id === id)
    return user && user.username !== currentUsername
  })
  if (ids.length === 0) {
    ElMessage.warning('不能踢出自己')
    return
  }
  try {
    await ElMessageBox.confirm(
      `确定要踢出选中的 ${ids.length} 个用户吗？`,
      '批量踢出',
      { type: 'warning' }
    )
    await kickUsers(ids)
    ElMessage.success('已批量踢出')
    await fetchUsers()
  } catch { /* 取消 */ }
}

// ========== 删除用户 ==========

async function handleDelete(row) {
  try {
    await ElMessageBox.confirm(
      `确定要删除用户 "${row.username}" 吗？此操作不可撤销。`,
      '删除用户',
      { type: 'error', confirmButtonText: '确认删除' }
    )
    const res = await deleteUser(row.id)
    ElMessage.success(res.data.message)
    await fetchUsers()
  } catch { /* 取消操作 */ }
}

async function handleBatchDelete() {
  // 过滤掉 admin 和自己
  const ids = selectedIds.value.filter(id => {
    const user = users.value.find(u => u.id === id)
    return user && user.username !== 'admin' && user.username !== currentUsername
  })
  if (ids.length === 0) {
    ElMessage.warning('所选用户均不可删除（admin 或当前用户）')
    return
  }
  try {
    await ElMessageBox.confirm(
      `确定要删除选中的 ${ids.length} 个用户吗？此操作不可撤销。`,
      '批量删除',
      { type: 'error', confirmButtonText: '确认删除' }
    )
    const res = await batchDeleteUsers(ids)
    ElMessage.success(res.data.message)
    await fetchUsers()
  } catch { /* 取消 */ }
}

// ========== 角色分配 ==========

const roleDialogVisible = ref(false)
const roleSubmitLoading = ref(false)
const roleLoading = ref(false)
const selectedRoleIds = ref([])
const allRoles = ref([])
const roleTotal = ref(0)
const roleUser = reactive({ id: 0, username: '' })
const roleSearchKeyword = ref('')
const roleTableRef = ref(null)
const rolePage = ref(1)
const rolePageSize = ref(5)
const isSyncing = ref(false)
let searchTimer = null

// 搜索输入防抖 300ms 后请求后端
function handleRoleSearch() {
  clearTimeout(searchTimer)
  searchTimer = setTimeout(() => {
    rolePage.value = 1
    fetchRoles()
  }, 300)
}

function handleRolePageChange() {
  fetchRoles()
}

function handleRoleSelectionChange(selection) {
  if (isSyncing.value) return
  // 合并：保留其他页已选 + 当前页勾选
  const pageIds = new Set(allRoles.value.map(r => r.id))
  const otherSelected = selectedRoleIds.value.filter(id => !pageIds.has(id))
  selectedRoleIds.value = [...otherSelected, ...selection.map(r => r.id)]
}

function selectAllRoles() {
  // 全选当前页
  isSyncing.value = true
  const pageIds = new Set(allRoles.value.map(r => r.id))
  const otherSelected = selectedRoleIds.value.filter(id => !pageIds.has(id))
  selectedRoleIds.value = [...otherSelected, ...allRoles.value.map(r => r.id)]
  allRoles.value.forEach(r => {
    roleTableRef.value?.toggleRowSelection(r, true)
  })
  nextTick(() => { isSyncing.value = false })
}

function clearRoleSelection() {
  selectedRoleIds.value = []
  roleTableRef.value?.clearSelection()
}

async function fetchRoles() {
  roleLoading.value = true
  try {
    const res = await getRoles({
      keyword: roleSearchKeyword.value || undefined,
      page: rolePage.value,
      pageSize: rolePageSize.value,
    })
    if (res.data.success) {
      allRoles.value = res.data.data
      roleTotal.value = res.data.total
      await nextTick()
      syncTableSelection()
    }
  } catch {
    ElMessage.error('获取角色列表失败')
  } finally {
    roleLoading.value = false
  }
}

function syncTableSelection() {
  if (!roleTableRef.value) return
  isSyncing.value = true
  roleTableRef.value.clearSelection()
  allRoles.value.forEach(r => {
    if (selectedRoleIds.value.includes(r.id)) {
      roleTableRef.value.toggleRowSelection(r, true)
    }
  })
  nextTick(() => { isSyncing.value = false })
}

async function openRoleDialog(row) {
  roleUser.id = row.id
  roleUser.username = row.username
  selectedRoleIds.value = [...(row.roleIds || [])]
  roleSearchKeyword.value = ''
  rolePage.value = 1
  roleTotal.value = 0
  allRoles.value = []

  roleDialogVisible.value = true
  await fetchRoles()
}

async function handleRoleSubmit() {
  roleSubmitLoading.value = true
  try {
    await assignUserRoles(roleUser.id, selectedRoleIds.value)
    ElMessage.success('角色分配成功')
    roleDialogVisible.value = false
    await fetchUsers()
  } catch (err) {
    ElMessage.error(err.response?.data?.message || '角色分配失败')
  } finally {
    roleSubmitLoading.value = false
  }
}

// ========== 直接授权 ==========

const directPermDialogVisible = ref(false)
const directPermSubmitLoading = ref(false)
const permLoading = ref(false)
const selectedPermIds = ref([])
const allPermOptions = ref([])
const filteredPermOptions = ref([])
const permSearchKeyword = ref('')
const directPermUser = reactive({ id: 0, username: '' })
const permTableRef = ref(null)
const isPermSyncing = ref(false)

function filterPermOptions() {
  if (!permSearchKeyword.value) {
    filteredPermOptions.value = allPermOptions.value
  } else {
    const kw = permSearchKeyword.value.toLowerCase()
    filteredPermOptions.value = allPermOptions.value.filter(
      p => p.name.toLowerCase().includes(kw) || p.code.toLowerCase().includes(kw)
    )
  }
}

function handlePermSelectionChange(selection) {
  if (isPermSyncing.value) return
  const pageIds = new Set(filteredPermOptions.value.map(p => p.id))
  const otherSelected = selectedPermIds.value.filter(id => !pageIds.has(id))
  selectedPermIds.value = [...otherSelected, ...selection.map(p => p.id)]
}

function selectAllPerms() {
  isPermSyncing.value = true
  const pageIds = new Set(filteredPermOptions.value.map(p => p.id))
  const otherSelected = selectedPermIds.value.filter(id => !pageIds.has(id))
  selectedPermIds.value = [...otherSelected, ...filteredPermOptions.value.map(p => p.id)]
  filteredPermOptions.value.forEach(p => {
    permTableRef.value?.toggleRowSelection(p, true)
  })
  nextTick(() => { isPermSyncing.value = false })
}

function clearPermSelection() {
  selectedPermIds.value = []
  permTableRef.value?.clearSelection()
}

async function openDirectPermDialog(row) {
  directPermUser.id = row.id
  directPermUser.username = row.username
  selectedPermIds.value = []
  permSearchKeyword.value = ''

  permLoading.value = true
  directPermDialogVisible.value = true

  try {
    const [permsRes, directRes] = await Promise.all([
      getPermissions(),
      getUserDirectPermissions(row.id),
    ])
    if (permsRes.data.success) {
      allPermOptions.value = permsRes.data.data || []
      filteredPermOptions.value = allPermOptions.value
    }
    if (directRes.data.success) {
      selectedPermIds.value = directRes.data.data || []
    }
    await nextTick()
    syncPermTableSelection()
  } catch {
    ElMessage.error('获取权限数据失败')
  } finally {
    permLoading.value = false
  }
}

function syncPermTableSelection() {
  if (!permTableRef.value) return
  isPermSyncing.value = true
  permTableRef.value.clearSelection()
  filteredPermOptions.value.forEach(p => {
    if (selectedPermIds.value.includes(p.id)) {
      permTableRef.value.toggleRowSelection(p, true)
    }
  })
  nextTick(() => { isPermSyncing.value = false })
}

async function handleDirectPermSubmit() {
  directPermSubmitLoading.value = true
  try {
    // 获取当前已有的直接权限
    const currentRes = await getUserDirectPermissions(directPermUser.id)
    const currentIds = currentRes.data.success ? (currentRes.data.data || []) : []

    const toGrant = selectedPermIds.value.filter(id => !currentIds.includes(id))
    const toRevoke = currentIds.filter(id => !selectedPermIds.value.includes(id))

    if (toGrant.length > 0) {
      await grantUserPermission({ userId: directPermUser.id, permissionIds: toGrant })
    }
    if (toRevoke.length > 0) {
      await revokeUserPermission({ userId: directPermUser.id, permissionIds: toRevoke })
    }

    ElMessage.success('直接权限已更新')
    directPermDialogVisible.value = false
  } catch (err) {
    ElMessage.error(err.response?.data?.message || '权限更新失败')
  } finally {
    directPermSubmitLoading.value = false
  }
}

onMounted(fetchUsers)
</script>

<style scoped>
.user-management {
  background: #fff;
  padding: 20px;
  border-radius: 4px;
}

.toolbar {
  display: flex;
  gap: 10px;
  flex-wrap: wrap;
  margin-left: auto;
}

.top-section {
  display: flex;
  align-items: center;
  gap: 16px;
}

.pagination-wrapper {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: 16px;
}

.total-info {
  font-size: 14px;
  color: #666;
}

/* ========== 角色分配弹窗 ========== */

.role-toolbar {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 14px;
}

.role-pagination {
  display: flex;
  justify-content: center;
  margin-top: 12px;
}

/* ========== 直接授权弹窗 ========== */

.perm-toolbar {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 14px;
}
</style>
