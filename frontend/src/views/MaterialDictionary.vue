<template>
  <div class="material-dict">
    <div class="top-section">
      <el-input v-model="keyword" placeholder="搜索材料编码/名称" clearable style="width: 260px" @keyup.enter="handleSearch" @clear="handleSearch">
        <template #prefix><el-icon><Search /></el-icon></template>
      </el-input>
      <el-button type="primary" @click="handleSearch"><el-icon><Search /></el-icon> 搜索</el-button>
      <div class="toolbar">
        <el-button type="primary" :disabled="!canManage" @click="openCreate"><el-icon><Plus /></el-icon>新增</el-button>
        <el-button type="danger" :disabled="!canManage || selectedIds.length === 0" @click="handleBatchDelete"><el-icon><Delete /></el-icon>批量删除</el-button>
        <el-button @click="fetchData"><el-icon><RefreshRight /></el-icon>刷新</el-button>
      </div>
    </div>

    <el-table :data="list" stripe border v-loading="loading" style="width:100%;margin-top:16px" @selection-change="handleSelectionChange">
      <el-table-column type="selection" width="50" />
      <el-table-column prop="code" label="材料编码" width="140" />
      <el-table-column prop="name" label="材料名称" width="200" show-overflow-tooltip />
      <el-table-column prop="specification" label="规格" width="120" />
      <el-table-column prop="model" label="型号" width="120" />
      <el-table-column prop="unit" label="单位" width="80" />
      <el-table-column prop="remark" label="备注" min-width="150" show-overflow-tooltip />
      <el-table-column label="创建时间" width="170">
        <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="150" fixed="right">
        <template #default="{ row }">
          <el-button size="small" type="primary" :disabled="!canManage" @click="openEdit(row)">编辑</el-button>
          <el-button size="small" type="danger" :disabled="!canManage" @click="handleDelete(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <div class="pagination-wrapper">
      <span class="total-info">共 {{ total }} 条</span>
      <el-pagination v-model:current-page="page" v-model:page-size="pageSize" :page-sizes="[10,20,50,100]" :total="total" layout="sizes,prev,pager,next,jumper" background @size-change="fetchData" @current-change="fetchData" />
    </div>

    <el-dialog v-model="dialogVisible" :title="isEdit ? '编辑材料' : '新增材料'" width="500px" :close-on-click-modal="false">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="90px">
        <el-form-item label="材料编码" prop="code">
          <el-input v-model="form.code" :disabled="isEdit" placeholder="唯一编码" />
        </el-form-item>
        <el-form-item label="材料名称" prop="name">
          <el-input v-model="form.name" placeholder="材料名称" />
        </el-form-item>
        <el-form-item label="规格"><el-input v-model="form.specification" /></el-form-item>
        <el-form-item label="型号"><el-input v-model="form.model" /></el-form-item>
        <el-form-item label="单位"><el-input v-model="form.unit" /></el-form-item>
        <el-form-item label="备注"><el-input v-model="form.remark" type="textarea" :rows="2" /></el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="submitLoading" @click="handleSubmit">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted, computed } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { getMaterials, createMaterial, updateMaterial, deleteMaterial, batchDeleteMaterials } from '../api/auth'

// 权限
const perms = JSON.parse(localStorage.getItem('permissions') || '[]')
const canView = computed(() => perms.includes('material:view'))
const canManage = computed(() => perms.includes('material:manage'))

const list = ref([])
const selectedIds = ref([])
const loading = ref(false)
const total = ref(0)
const page = ref(1)
const pageSize = ref(10)
const keyword = ref('')

function handleSelectionChange(selection) {
  selectedIds.value = selection.map(s => s.id)
}
const dialogVisible = ref(false)
const isEdit = ref(false)
const editId = ref(null)
const formRef = ref(null)
const submitLoading = ref(false)

const form = reactive({ code: '', name: '', specification: '', model: '', unit: '', remark: '' })
const rules = {
  code: [{ required: true, message: '请输入材料编码', trigger: 'blur' }],
  name: [{ required: true, message: '请输入材料名称', trigger: 'blur' }],
}

function handleSearch() { page.value = 1; fetchData() }

function formatDate(dateStr) {
  if (!dateStr) return ''
  const d = new Date(dateStr)
  if (isNaN(d.getTime())) return dateStr
  const pad = n => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`
}

async function fetchData() {
  loading.value = true
  try {
    const res = await getMaterials({ keyword: keyword.value || undefined, page: page.value, pageSize: pageSize.value })
    if (res.data.success) { list.value = res.data.data; total.value = res.data.total }
  } catch { ElMessage.error('获取材料列表失败') }
  finally { loading.value = false }
}

function openCreate() {
  isEdit.value = false; editId.value = null
  form.code = ''; form.name = ''; form.specification = ''; form.model = ''; form.unit = ''; form.remark = ''
  formRef.value?.clearValidate()
  dialogVisible.value = true
}

function openEdit(row) {
  isEdit.value = true; editId.value = row.id
  form.code = row.code; form.name = row.name; form.specification = row.specification || ''
  form.model = row.model || ''; form.unit = row.unit || ''; form.remark = row.remark || ''
  formRef.value?.clearValidate()
  dialogVisible.value = true
}

async function handleSubmit() {
  const valid = await formRef.value.validate().catch(() => false)
  if (!valid) return
  submitLoading.value = true
  try {
    if (isEdit.value) {
      await updateMaterial(editId.value, { name: form.name, specification: form.specification, model: form.model, unit: form.unit, remark: form.remark })
      ElMessage.success('修改成功')
    } else {
      await createMaterial({ code: form.code, name: form.name, specification: form.specification, model: form.model, unit: form.unit, remark: form.remark })
      ElMessage.success('新增成功')
    }
    dialogVisible.value = false
    fetchData()
  } catch (err) { ElMessage.error(err.response?.data?.message || '操作失败') }
  finally { submitLoading.value = false }
}

async function handleDelete(row) {
  try {
    await ElMessageBox.confirm(`确定删除材料 "${row.name}" 吗？`, '删除确认', { type: 'warning' })
    await deleteMaterial(row.id)
    ElMessage.success('删除成功')
    fetchData()
  } catch { /* 取消 */ }
}

async function handleBatchDelete() {
  const count = selectedIds.value.length
  if (count === 0) return
  try {
    await ElMessageBox.confirm(`确定删除选中的 ${count} 条材料吗？`, '批量删除', { type: 'warning' })
    await batchDeleteMaterials(selectedIds.value)
    ElMessage.success(`已删除 ${count} 条材料`)
    selectedIds.value = []
    fetchData()
  } catch { /* 取消 */ }
}

onMounted(fetchData)
</script>

<style scoped>
.material-dict { background:#fff;padding:20px;border-radius:4px; }
.top-section { display:flex;align-items:center;gap:16px; }
.toolbar { display:flex;gap:10px;margin-left:auto; }
.pagination-wrapper { display:flex;align-items:center;justify-content:space-between;margin-top:16px; }
.total-info { font-size:14px;color:#666; }
</style>
