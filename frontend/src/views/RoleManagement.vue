<template>
  <div class="role-management">
    <div class="top-section">
      <div class="search-row">
        <el-input v-model="keyword" placeholder="搜索角色名称或描述" clearable style="width: 260px" @keyup.enter="handleSearch" @clear="handleSearch">
          <template #prefix><el-icon><Search /></el-icon></template>
        </el-input>
        <el-button type="primary" @click="handleSearch"><el-icon><Search /></el-icon> 搜索</el-button>
      </div>
      <div class="toolbar">
        <el-button type="primary" @click="openCreateDialog">
          <el-icon><Plus /></el-icon> 新增角色
        </el-button>
        <el-button @click="fetchRoles">
          <el-icon><RefreshRight /></el-icon> 刷新
        </el-button>
      </div>
    </div>

    <el-table
      :data="roles"
      stripe
      border
      style="width: 100%; margin-top: 16px"
      v-loading="loading"
    >
      <el-table-column prop="id" label="ID" width="60" />
      <el-table-column prop="name" label="角色名称" width="150" />
      <el-table-column prop="description" label="描述" min-width="200" show-overflow-tooltip />
      <el-table-column label="创建时间" width="170">
        <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column label="关联权限" min-width="300" show-overflow-tooltip>
        <template #default="{ row }">
          <el-tag
            v-for="pid in row.permissionIds"
            :key="pid"
            size="small"
            type="info"
            style="margin-right: 4px"
          >
            {{ getPermissionName(pid) }}
          </el-tag>
          <span v-if="row.permissionIds.length === 0" style="color: #999">无权限</span>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="160" fixed="right">
        <template #default="{ row }">
          <el-button size="small" type="primary" @click="openEditDialog(row)">编辑</el-button>
          <el-popconfirm
            title="确定要删除该角色吗？"
            confirm-button-text="确定"
            cancel-button-text="取消"
            @confirm="handleDelete(row.id)"
          >
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
        :page-sizes="[5, 10, 20, 50]"
        :total="total"
        layout="sizes, prev, pager, next, jumper"
        background
        @size-change="fetchRoles"
        @current-change="fetchRoles"
      />
    </div>

    <el-dialog
      v-model="dialogVisible"
      :title="isEdit ? '编辑角色' : '新增角色'"
      width="720px"
      :close-on-click-modal="false"
    >
      <el-form ref="formRef" :model="form" :rules="rules" label-width="90px">
        <el-form-item label="角色名称" prop="name">
          <el-input v-model="form.name" placeholder="请输入角色名称" />
        </el-form-item>
        <el-form-item label="角色描述">
          <el-input v-model="form.description" placeholder="请输入角色描述" />
        </el-form-item>
        <el-form-item label="分配权限">
          <el-tree
            :key="treeKey"
            ref="treeRef"
            :data="permissionTree"
            show-checkbox
            node-key="id"
            :default-checked-keys="defaultChecked"
            :props="{ children: 'children', label: 'label' }"
            default-expand-all
            class="perm-tree"
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="submitLoading" @click="handleSubmit">
          {{ submitLoading ? '保存中...' : '保存' }}
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { getRoles, createRole, updateRole, deleteRole, getPermissions, getPermissionTree } from '../api/auth'

const roles = ref([])
const allPermissions = ref([])
const permissionTree = ref([])
const treeRef = ref(null)
const loading = ref(false)
const dialogVisible = ref(false)
const isEdit = ref(false)
const editingId = ref(null)
const formRef = ref(null)
const submitLoading = ref(false)
const treeKey = ref(0)
const defaultChecked = ref([])

const keyword = ref('')
const page = ref(1)
const pageSize = ref(10)
const total = ref(0)

function handleSearch() { page.value = 1; fetchRoles() }

function formatDate(dateStr) {
  if (!dateStr) return ''
  const d = new Date(dateStr)
  if (isNaN(d.getTime())) return dateStr
  const pad = n => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`
}

const form = reactive({
  name: '',
  description: '',
})

const rules = {
  name: [{ required: true, message: '请输入角色名称', trigger: 'blur' }],
}

function getPermissionName(pid) {
  const perm = allPermissions.value.find(p => p.id === pid)
  return perm ? perm.name : `ID:${pid}`
}

function openCreateDialog() {
  isEdit.value = false
  editingId.value = null
  form.name = ''
  form.description = ''
  defaultChecked.value = []
  treeKey.value++
  formRef.value?.clearValidate()
  dialogVisible.value = true
}

function openEditDialog(row) {
  isEdit.value = true
  editingId.value = row.id
  form.name = row.name
  form.description = row.description || ''
  defaultChecked.value = row.permissionIds.map(String)
  treeKey.value++
  dialogVisible.value = true
}

async function fetchRoles() {
  loading.value = true
  try {
    const [rolesRes, permsRes, treeRes] = await Promise.all([
      getRoles({ keyword: keyword.value || undefined, page: page.value, pageSize: pageSize.value }),
      getPermissions(),
      getPermissionTree(),
    ])
    if (rolesRes.data.success) {
      roles.value = rolesRes.data.data
      total.value = rolesRes.data.total
    }
    if (permsRes.data.success) allPermissions.value = permsRes.data.data
    if (treeRes.data.success) permissionTree.value = treeRes.data.data
  } catch {
    ElMessage.error('获取数据失败')
  } finally {
    loading.value = false
  }
}

async function handleSubmit() {
  const valid = await formRef.value.validate().catch(() => false)
  if (!valid) return

  const checkedKeys = treeRef.value?.getCheckedKeys() || []
  const permIds = checkedKeys
    .filter(k => !String(k).startsWith('menu_'))
    .map(Number)

  submitLoading.value = true
  try {
    if (isEdit.value) {
      await updateRole(editingId.value, {
        name: form.name,
        description: form.description || null,
        permissionIds: permIds,
      })
      ElMessage.success('角色更新成功')
    } else {
      await createRole({
        name: form.name,
        description: form.description || null,
        permissionIds: permIds,
      })
      ElMessage.success('角色创建成功')
    }
    dialogVisible.value = false
    await fetchRoles()
  } catch (err) {
    ElMessage.error(err.response?.data?.message || '操作失败')
  } finally {
    submitLoading.value = false
  }
}

async function handleDelete(id) {
  try {
    await deleteRole(id)
    ElMessage.success('角色已删除')
    await fetchRoles()
  } catch (err) {
    ElMessage.error(err.response?.data?.message || '删除失败')
  }
}

onMounted(fetchRoles)
</script>

<style scoped>
.role-management {
  background: #fff;
  padding: 20px;
  border-radius: 4px;
}
.top-section {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 12px;
}
.search-row {
  display: flex;
  align-items: center;
  gap: 10px;
}
.toolbar {
  display: flex;
  gap: 10px;
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
.perm-tree {
  max-height: 400px;
  overflow-y: auto;
  width: 100%;
}
.perm-tree :deep(.el-tree-node__content) {
  height: 32px;
}
.perm-tree :deep(.el-tree-node__label) {
  font-size: 14px;
  white-space: nowrap;
  overflow: visible;
}
</style>
