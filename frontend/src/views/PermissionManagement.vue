<template>
  <div class="permission-management" v-loading="loading">
    <div class="top-section">
      <div class="search-row">
        <el-input v-model="keyword" placeholder="搜索权限名称或编码" clearable style="width: 260px" @keyup.enter="handleSearch" @clear="handleSearch">
          <template #prefix><el-icon><Search /></el-icon></template>
        </el-input>
        <el-button type="primary" @click="handleSearch"><el-icon><Search /></el-icon> 搜索</el-button>
      </div>
      <div class="toolbar">
        <el-button type="primary" @click="openCreateDialog">
          <el-icon><Plus /></el-icon> 新增权限
        </el-button>
        <el-button type="success" @click="openBatchGrantDialog">
          <el-icon><UserFilled /></el-icon> 用户授权
        </el-button>
        <el-button type="warning" @click="openRoleGrantDialog">
          <el-icon><Lock /></el-icon> 角色授权
        </el-button>
        <el-button @click="fetchPermissions"><el-icon><RefreshRight /></el-icon> 刷新</el-button>
      </div>
    </div>

    <el-table :data="permissions" stripe border style="width: 100%; margin-top: 16px">
      <el-table-column prop="id" label="ID" width="60" />
      <el-table-column prop="name" label="权限名称" width="150" />
      <el-table-column prop="code" label="权限编码" width="170" />
      <el-table-column prop="description" label="描述" min-width="180" show-overflow-tooltip />
      <el-table-column label="关联角色" width="90">
        <template #default="{ row }">
          <el-tag size="small" type="warning">{{ row.roleCount }} 个</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="直接授权" width="90">
        <template #default="{ row }">
          <el-tag size="small" type="info">{{ row.userCount }} 个</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="300" fixed="right">
        <template #default="{ row }">
          <el-button size="small" type="primary" @click="openEditDialog(row)">编辑</el-button>
          <el-popconfirm title="删除权限将同时清除所有关联的角色和用户授权，确定继续？" @confirm="handleDelete(row.id)">
            <template #reference>
              <el-button size="small" type="danger">删除</el-button>
            </template>
          </el-popconfirm>
        </template>
      </el-table-column>
    </el-table>

    <div class="pagination-wrapper">
      <span class="total-info">共 {{ total }} 条</span>
      <el-pagination
        v-model:current-page="page"
        v-model:page-size="pageSize"
        :page-sizes="[10, 20, 50]"
        :total="total"
        layout="sizes, prev, pager, next, jumper"
        background
        @size-change="fetchPermissions"
        @current-change="fetchPermissions"
      />
    </div>

    <!-- 新增/编辑权限弹窗 -->
    <el-dialog v-model="dialogVisible" :title="isEdit ? '编辑权限' : '新增权限'" width="520px" :close-on-click-modal="false">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="90px">
        <el-form-item label="权限名称" prop="name">
          <el-input v-model="form.name" placeholder="如：用户查看" />
        </el-form-item>
        <el-form-item label="权限编码" prop="code">
          <el-input v-model="form.code" placeholder="如：user:view（格式：resource:action）" />
        </el-form-item>
        <el-form-item label="描述">
          <el-input v-model="form.description" type="textarea" :rows="2" placeholder="权限描述（可选）" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="submitLoading" @click="handleSubmit">保存</el-button>
      </template>
    </el-dialog>

    <!-- 批量授权用户弹窗：选择用户 → 批量勾选权限 -->
    <el-dialog v-model="batchGrantVisible" title="用户授权" width="720px" :close-on-click-modal="false">
      <!-- 第一步：选择目标用户 -->
      <div class="grant-step">
        <span class="grant-label">目标用户：</span>
        <el-select
          v-model="selectedUserId"
          filterable
          remote
          reserve-keyword
          placeholder="搜索并选择用户"
          :remote-method="searchGrantUsers"
          :loading="grantUserLoading"
          clearable
          style="width: 320px"
          @change="onGrantUserChange"
        >
          <el-option
            v-for="u in grantUserOptions"
            :key="u.id"
            :label="`${u.username} (${u.email})`"
            :value="u.id"
          />
        </el-select>
        <span v-if="grantUserRoleNames.length > 0" style="margin-left: 12px; color: #909399; font-size: 13px;">
          已有角色：<el-tag v-for="r in grantUserRoleNames" :key="r" size="small" type="success" style="margin-right: 4px;">{{ r }}</el-tag>
        </span>
      </div>

      <!-- 第二步：勾选权限 -->
      <div v-if="selectedUserId" style="margin-top: 16px;">
        <div class="grant-toolbar">
          <el-input v-model="permFilterKeyword" placeholder="过滤权限" clearable style="width: 200px" @input="filterGrantPerms" @clear="filterGrantPerms">
            <template #prefix><el-icon><Search /></el-icon></template>
          </el-input>
          <el-button size="small" text type="primary" @click="selectAllGrantPerms">全选当前页</el-button>
          <el-button size="small" text type="warning" @click="clearGrantPermSelection">清空</el-button>
          <span style="margin-left: auto; color: #909399; font-size: 13px;">
            已选 {{ grantedPermIds.length }} 项权限
          </span>
        </div>

        <el-table
          ref="grantPermTableRef"
          :data="filteredGrantPerms"
          row-key="id"
          max-height="360"
          stripe
          v-loading="grantPermLoading"
          @selection-change="onGrantPermSelectionChange"
        >
          <el-table-column type="selection" width="50" reserve-selection />
          <el-table-column prop="name" label="权限名称" width="160" />
          <el-table-column prop="code" label="权限编码" width="180" />
          <el-table-column prop="description" label="描述" show-overflow-tooltip />
          <el-table-column label="来源" width="80">
            <template #default="{ row: p }">
              <el-tag v-if="grantRolePermIds.includes(p.id)" size="small" type="success">角色</el-tag>
              <el-tag v-else-if="grantDirectPermIds.includes(p.id)" size="small" type="warning">直接</el-tag>
              <span v-else style="color: #c0c4cc;">-</span>
            </template>
          </el-table-column>
        </el-table>
      </div>

      <el-empty v-if="!selectedUserId" description="请先选择目标用户" :image-size="80" />

      <template #footer>
        <el-button @click="batchGrantVisible = false">取消</el-button>
        <el-button type="primary" :loading="batchGrantLoading" :disabled="!selectedUserId" @click="handleBatchGrantSubmit">
          {{ batchGrantLoading ? '保存中...' : '保存授权' }}
        </el-button>
      </template>
    </el-dialog>

    <!-- 角色授权弹窗：选择角色 → 权限树勾选 -->
    <el-dialog v-model="roleGrantVisible" title="角色授权" width="680px" :close-on-click-modal="false">
      <div class="grant-step">
        <span class="grant-label">目标角色：</span>
        <el-select
          v-model="selectedRoleId"
          filterable
          placeholder="选择角色"
          style="width: 280px"
          @change="onGrantRoleChange"
        >
          <el-option
            v-for="r in allRoles"
            :key="r.id"
            :label="r.name"
            :value="r.id"
          />
        </el-select>
      </div>

      <div v-if="selectedRoleId" style="margin-top: 16px;">
        <el-tree
          :key="roleTreeKey"
          ref="rolePermTreeRef"
          :data="rolePermTree"
          show-checkbox
          node-key="id"
          :default-checked-keys="roleDefaultChecked"
          :props="{ children: 'children', label: 'label' }"
          default-expand-all
          class="perm-tree"
        />
      </div>

      <el-empty v-if="!selectedRoleId" description="请先选择目标角色" :image-size="80" />

      <template #footer>
        <el-button @click="roleGrantVisible = false">取消</el-button>
        <el-button type="primary" :loading="roleGrantLoading" :disabled="!selectedRoleId" @click="handleRoleGrantSubmit">
          {{ roleGrantLoading ? '保存中...' : '保存授权' }}
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted, nextTick } from 'vue'
import { ElMessage } from 'element-plus'
import {
  getPermissionsPaginated, createPermission, updatePermission, deletePermission,
  grantUserPermission, revokeUserPermission,
  getUsers, getPermissions, getUserPermissionSummary,
  getRoles, updateRole, getPermissionTree,
} from '../api/auth'

const permissions = ref([])
const loading = ref(false)
const keyword = ref('')
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)

// 新增/编辑
const dialogVisible = ref(false)
const isEdit = ref(false)
const editingId = ref(null)
const formRef = ref(null)
const submitLoading = ref(false)
const form = reactive({ name: '', code: '', description: '' })
const rules = {
  name: [{ required: true, message: '请输入权限名称', trigger: 'blur' }],
  code: [
    { required: true, message: '请输入权限编码', trigger: 'blur' },
    { pattern: /^[a-z]+:[a-z_]+$/, message: '格式：resource:action（小写字母+冒号）', trigger: 'blur' },
  ],
}

// ========== 批量授权用户 ==========
const batchGrantVisible = ref(false)
const batchGrantLoading = ref(false)
const selectedUserId = ref(null)
const grantUserLoading = ref(false)
const grantUserOptions = ref([])
const grantUserRoleNames = ref([])
const grantPermLoading = ref(false)
const allGrantPerms = ref([])
const filteredGrantPerms = ref([])
const permFilterKeyword = ref('')
const grantPermTableRef = ref(null)
const grantedPermIds = ref([])
const grantRolePermIds = ref([])
const grantDirectPermIds = ref([])
let isPermSync = false

function handleSearch() { page.value = 1; fetchPermissions() }

async function fetchPermissions() {
  loading.value = true
  try {
    const res = await getPermissionsPaginated({ keyword: keyword.value || undefined, page: page.value, pageSize: pageSize.value })
    if (res.data.success) {
      permissions.value = res.data.data
      total.value = res.data.total
    }
  } catch { ElMessage.error('获取权限列表失败') }
  finally { loading.value = false }
}

function openCreateDialog() {
  isEdit.value = false; editingId.value = null
  form.name = ''; form.code = ''; form.description = ''
  formRef.value?.clearValidate()
  dialogVisible.value = true
}

function openEditDialog(row) {
  isEdit.value = true; editingId.value = row.id
  form.name = row.name; form.code = row.code; form.description = row.description || ''
  dialogVisible.value = true
}

async function handleSubmit() {
  const valid = await formRef.value.validate().catch(() => false)
  if (!valid) return
  submitLoading.value = true
  try {
    if (isEdit.value) {
      await updatePermission(editingId.value, { name: form.name, code: form.code, description: form.description || null })
      ElMessage.success('权限更新成功')
    } else {
      await createPermission({ name: form.name, code: form.code, description: form.description || null })
      ElMessage.success('权限创建成功')
    }
    dialogVisible.value = false
    await fetchPermissions()
  } catch (err) {
    ElMessage.error(err.response?.data?.message || '操作失败')
  } finally { submitLoading.value = false }
}

async function handleDelete(id) {
  try {
    await deletePermission(id)
    ElMessage.success('权限已删除')
    await fetchPermissions()
  } catch (err) {
    ElMessage.error(err.response?.data?.message || '删除失败')
  }
}

// ========== 批量授权用户逻辑 ==========

async function searchGrantUsers(query) {
  if (!query || query.length < 1) { grantUserOptions.value = []; return }
  grantUserLoading.value = true
  try {
    const res = await getUsers({ keyword: query, pageSize: 20 })
    if (res.data.success) {
      grantUserOptions.value = res.data.data.list || []
    }
  } catch { grantUserOptions.value = [] }
  finally { grantUserLoading.value = false }
}

async function onGrantUserChange(userId) {
  if (!userId) {
    grantUserRoleNames.value = []
    allGrantPerms.value = []
    filteredGrantPerms.value = []
    grantedPermIds.value = []
    grantRolePermIds.value = []
    grantDirectPermIds.value = []
    return
  }

  // 加载用户权限汇总 + 所有权限项
  grantPermLoading.value = true
  try {
    const [summaryRes, permsRes] = await Promise.all([
      getUserPermissionSummary(userId),
      getPermissions(),
    ])

    if (summaryRes.data.success) {
      const s = summaryRes.data.data
      grantRolePermIds.value = s.rolePermissionIds || []
      grantDirectPermIds.value = s.directPermissionIds || []
      grantedPermIds.value = [...s.allPermissionIds]
      // 获取角色名
      grantUserRoleNames.value = s.rolePermissionIds?.length > 0 ? ['角色权限'] : []
    }

    if (permsRes.data.success) {
      allGrantPerms.value = permsRes.data.data || []
      filteredGrantPerms.value = allGrantPerms.value
    }

    await nextTick()
    syncGrantPermTable()
  } catch { ElMessage.error('加载权限数据失败') }
  finally { grantPermLoading.value = false }
}

function filterGrantPerms() {
  if (!permFilterKeyword.value) {
    filteredGrantPerms.value = allGrantPerms.value
  } else {
    const kw = permFilterKeyword.value.toLowerCase()
    filteredGrantPerms.value = allGrantPerms.value.filter(
      p => p.name.toLowerCase().includes(kw) || p.code.toLowerCase().includes(kw)
    )
  }
  nextTick(() => syncGrantPermTable())
}

function syncGrantPermTable() {
  if (!grantPermTableRef.value) return
  isPermSync = true
  grantPermTableRef.value.clearSelection()
  filteredGrantPerms.value.forEach(p => {
    if (grantedPermIds.value.includes(p.id)) {
      grantPermTableRef.value.toggleRowSelection(p, true)
    }
  })
  nextTick(() => { isPermSync = false })
}

function onGrantPermSelectionChange(selection) {
  if (isPermSync) return
  const pageIds = new Set(filteredGrantPerms.value.map(p => p.id))
  const otherSelected = grantedPermIds.value.filter(id => !pageIds.has(id))
  grantedPermIds.value = [...otherSelected, ...selection.map(p => p.id)]
}

function selectAllGrantPerms() {
  isPermSync = true
  const pageIds = new Set(filteredGrantPerms.value.map(p => p.id))
  const otherSelected = grantedPermIds.value.filter(id => !pageIds.has(id))
  grantedPermIds.value = [...otherSelected, ...filteredGrantPerms.value.map(p => p.id)]
  filteredGrantPerms.value.forEach(p => grantPermTableRef.value?.toggleRowSelection(p, true))
  nextTick(() => { isPermSync = false })
}

function clearGrantPermSelection() {
  const pageIds = new Set(filteredGrantPerms.value.map(p => p.id))
  grantedPermIds.value = grantedPermIds.value.filter(id => !pageIds.has(id))
  grantPermTableRef.value?.clearSelection()
}

async function openBatchGrantDialog() {
  selectedUserId.value = null
  grantUserOptions.value = []
  grantUserRoleNames.value = []
  allGrantPerms.value = []
  filteredGrantPerms.value = []
  grantedPermIds.value = []
  grantRolePermIds.value = []
  grantDirectPermIds.value = []
  permFilterKeyword.value = ''
  batchGrantVisible.value = true
}

async function handleBatchGrantSubmit() {
  if (!selectedUserId.value) return
  batchGrantLoading.value = true
  try {
    // 计算差异：需要授予的 = 当前勾选但非直接权限的, 需要撤销的 = 原有直接权限但当前未勾选的
    const toGrant = grantedPermIds.value.filter(id => !grantDirectPermIds.value.includes(id))
    const toRevoke = grantDirectPermIds.value.filter(id => !grantedPermIds.value.includes(id))

    if (toGrant.length > 0) {
      await grantUserPermission({ userId: selectedUserId.value, permissionIds: toGrant })
    }
    if (toRevoke.length > 0) {
      await revokeUserPermission({ userId: selectedUserId.value, permissionIds: toRevoke })
    }

    ElMessage.success(`授权已更新（授予 ${toGrant.length} 项，撤销 ${toRevoke.length} 项）`)
    batchGrantVisible.value = false
    await fetchPermissions()
  } catch (err) {
    ElMessage.error(err.response?.data?.message || '授权失败')
  } finally { batchGrantLoading.value = false }
}

// ========== 角色授权 ==========
const roleGrantVisible = ref(false)
const roleGrantLoading = ref(false)
const selectedRoleId = ref(null)
const allRoles = ref([])
const rolePermTree = ref([])
const roleTreeKey = ref(0)
const roleDefaultChecked = ref([])
const rolePermTreeRef = ref(null)

async function openRoleGrantDialog() {
  selectedRoleId.value = null
  rolePermTree.value = []
  roleDefaultChecked.value = []
  roleTreeKey.value++
  roleGrantVisible.value = true

  // 加载角色列表
  try {
    const res = await getRoles({ pageSize: 100 })
    if (res.data.success) allRoles.value = res.data.data
  } catch { allRoles.value = [] }
}

async function onGrantRoleChange(roleId) {
  if (!roleId) {
    rolePermTree.value = []
    roleDefaultChecked.value = []
    return
  }

  // 加载权限树 + 角色已有的权限
  try {
    const [treeRes, roleRes] = await Promise.all([
      getPermissionTree(),
      getRoles({ pageSize: 100 }),
    ])
    if (treeRes.data.success) {
      rolePermTree.value = treeRes.data.data
    }
    // 找到该角色的 permissionIds
    const roles = roleRes.data.success ? (roleRes.data.data || []) : []
    const role = roles.find(r => r.id === roleId)
    if (role) {
      roleDefaultChecked.value = (role.permissionIds || []).map(String)
    } else {
      roleDefaultChecked.value = []
    }
    roleTreeKey.value++
  } catch {
    ElMessage.error('加载权限树失败')
  }
}

async function handleRoleGrantSubmit() {
  if (!selectedRoleId.value) return
  roleGrantLoading.value = true
  try {
    const checkedKeys = rolePermTreeRef.value?.getCheckedKeys() || []
    const halfCheckedKeys = rolePermTreeRef.value?.getHalfCheckedKeys() || []
    const allChecked = [...checkedKeys, ...halfCheckedKeys]
    const permIds = allChecked
      .filter(k => !String(k).startsWith('menu_'))
      .map(Number)

    const role = allRoles.value.find(r => r.id === selectedRoleId.value)
    await updateRole(selectedRoleId.value, {
      name: role?.name || '',
      description: role?.description || null,
      permissionIds: permIds,
    })

    ElMessage.success(`角色「${role?.name}」权限已更新（${permIds.length} 项）`)
    roleGrantVisible.value = false
    await fetchPermissions()
  } catch (err) {
    ElMessage.error(err.response?.data?.message || '角色授权失败')
  } finally {
    roleGrantLoading.value = false
  }
}

onMounted(fetchPermissions)
</script>

<style scoped>
.permission-management { background: #fff; padding: 20px; border-radius: 4px; }
.top-section { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 12px; }
.search-row { display: flex; align-items: center; gap: 10px; }
.toolbar { display: flex; gap: 10px; }
.pagination-wrapper { display: flex; align-items: center; justify-content: space-between; margin-top: 16px; }
.total-info { font-size: 14px; color: #666; }

/* 批量授权 */
.grant-step { display: flex; align-items: center; flex-wrap: wrap; gap: 8px; margin-bottom: 8px; }
.grant-label { font-weight: 500; white-space: nowrap; }
.grant-toolbar { display: flex; align-items: center; gap: 10px; margin-bottom: 12px; flex-wrap: wrap; }

/* 角色授权权限树 */
.perm-tree {
  max-height: 420px;
  overflow-y: auto;
  width: 100%;
  border: 1px solid #ebeef5;
  border-radius: 4px;
  padding: 8px;
}
.perm-tree :deep(.el-tree-node__content) {
  height: 32px;
}
.perm-tree :deep(.el-tree-node__label) {
  font-size: 14px;
}
</style>
