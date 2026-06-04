<template>
  <div class="attachment-management">
    <div class="top-section">
      <div class="search-row">
        <el-input v-model="keyword" placeholder="搜索文件名或关联记录" clearable style="width: 260px" @keyup.enter="handleSearch" @clear="handleSearch">
          <template #prefix><el-icon><Search /></el-icon></template>
        </el-input>
        <el-select v-model="filterModule" placeholder="按模块筛选" clearable style="width: 160px" @change="handleSearch">
          <el-option v-for="m in moduleOptions" :key="m.moduleName" :label="m.displayName" :value="m.moduleName" />
        </el-select>
        <el-button type="primary" @click="handleSearch"><el-icon><Search /></el-icon> 搜索</el-button>
      </div>
      <div class="toolbar">
        <el-button type="primary" @click="openUploadDialog">
          <el-icon><Upload /></el-icon> 上传附件
        </el-button>
        <el-button @click="fetchAttachments">
          <el-icon><RefreshRight /></el-icon> 刷新
        </el-button>
      </div>
    </div>

    <el-table
      :data="attachments"
      stripe
      border
      style="width: 100%; margin-top: 16px"
      v-loading="loading"
    >
      <el-table-column prop="id" label="ID" width="70" />
      <el-table-column prop="fileName" label="文件名" min-width="220" show-overflow-tooltip />
      <el-table-column label="业务模块" width="120">
        <template #default="{ row }">
          <el-tag size="small" type="primary">{{ row.moduleDisplay || row.moduleName }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="关联记录" width="180" show-overflow-tooltip>
        <template #default="{ row }">
          <span v-if="row.relatedName">{{ row.relatedName }}</span>
          <span v-else style="color: #999">{{ row.relatedId }}</span>
        </template>
      </el-table-column>
      <el-table-column label="文件大小" width="110" align="right">
        <template #default="{ row }">{{ formatFileSize(row.fileSize) }}</template>
      </el-table-column>
      <el-table-column prop="uploadedByName" label="上传人" width="110" />
      <el-table-column label="上传时间" width="170">
        <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="160" fixed="right">
        <template #default="{ row }">
          <el-button size="small" type="primary" @click="handleDownload(row)">下载</el-button>
          <el-popconfirm
            title="确定要删除该附件吗？"
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
        :page-sizes="[10, 20, 50]"
        :total="total"
        layout="sizes, prev, pager, next, jumper"
        background
        @size-change="fetchAttachments"
        @current-change="fetchAttachments"
      />
    </div>

    <!-- 上传对话框 -->
    <el-dialog
      v-model="uploadDialogVisible"
      title="上传附件"
      width="560px"
      :close-on-click-modal="false"
    >
      <el-form ref="uploadFormRef" :model="uploadForm" :rules="uploadRules" label-width="90px">
        <el-form-item label="业务模块" prop="moduleName">
          <el-select v-model="uploadForm.moduleName" placeholder="请选择业务模块" style="width: 100%">
            <el-option
              v-for="m in uploadModuleOptions"
              :key="m.moduleName"
              :label="m.displayName"
              :value="m.moduleName"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="关联记录ID" prop="relatedId">
          <el-input v-model="uploadForm.relatedId" placeholder="如：入库单ID" />
        </el-form-item>
        <el-form-item label="关联记录名称">
          <el-input v-model="uploadForm.relatedName" placeholder="如：RK-20260602-0001（可选）" />
        </el-form-item>
        <el-form-item label="选择文件">
          <el-upload
            ref="uploadRef"
            :auto-upload="false"
            :multiple="true"
            :limit="20"
          >
            <el-button type="primary"><el-icon><Upload /></el-icon> 选择文件</el-button>
            <template #tip>
              <div class="upload-tip">支持多文件上传，单文件最大 50MB</div>
            </template>
          </el-upload>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="uploadDialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="uploadLoading" @click="handleUpload">
          {{ uploadLoading ? '上传中...' : '上传' }}
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import {
  getUnifiedAttachments, getAttachmentModules,
  uploadUnifiedAttachment, deleteUnifiedAttachment,
} from '../api/auth'

// 内置模块选项（确保始终可选）
const BUILTIN_MODULES = [
  { moduleName: 'InboundOrder', displayName: '入库单' },
]

const attachments = ref([])
const moduleOptions = ref([])
const uploadModuleOptions = ref(BUILTIN_MODULES)
const loading = ref(false)
const uploadDialogVisible = ref(false)
const uploadLoading = ref(false)
const uploadFormRef = ref(null)
const uploadRef = ref(null)
const keyword = ref('')
const filterModule = ref('')
const page = ref(1)
const pageSize = ref(10)
const total = ref(0)

const uploadForm = reactive({
  moduleName: '',
  relatedId: '',
  relatedName: '',
})

const uploadRules = {
  moduleName: [{ required: true, message: '请选择业务模块', trigger: 'change' }],
  relatedId: [{ required: true, message: '请输入关联记录ID', trigger: 'blur' }],
}

function formatDate(dateStr) {
  if (!dateStr) return ''
  const d = new Date(dateStr)
  if (isNaN(d.getTime())) return dateStr
  const pad = n => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`
}

function formatFileSize(bytes) {
  if (!bytes || bytes === 0) return '0 B'
  const units = ['B', 'KB', 'MB', 'GB']
  let i = 0
  let size = bytes
  while (size >= 1024 && i < units.length - 1) {
    size /= 1024
    i++
  }
  return size.toFixed(i === 0 ? 0 : 2) + ' ' + units[i]
}

function handleSearch() {
  page.value = 1
  fetchAttachments()
}

async function fetchAttachments() {
  loading.value = true
  try {
    const [attRes, modRes] = await Promise.all([
      getUnifiedAttachments({
        keyword: keyword.value || undefined,
        moduleName: filterModule.value || undefined,
        page: page.value,
        pageSize: pageSize.value,
      }),
      getAttachmentModules(),
    ])
    if (attRes.data.success) {
      attachments.value = attRes.data.data
      total.value = attRes.data.total
    }
    if (modRes.data.success) {
      // 合并内置模块和动态模块
      const dynamic = modRes.data.data || []
      const merged = [...BUILTIN_MODULES]
      for (const d of dynamic) {
        if (!merged.find(m => m.moduleName === d.moduleName)) {
          merged.push(d)
        }
      }
      moduleOptions.value = merged
      uploadModuleOptions.value = merged
    }
  } catch {
    ElMessage.error('获取附件列表失败')
  } finally {
    loading.value = false
  }
}

function openUploadDialog() {
  uploadForm.moduleName = ''
  uploadForm.relatedId = ''
  uploadForm.relatedName = ''
  uploadRef.value?.clearFiles()
  uploadFormRef.value?.clearValidate()
  uploadDialogVisible.value = true
}

async function handleUpload() {
  const valid = await uploadFormRef.value.validate().catch(() => false)
  if (!valid) return

  const uploadFiles = uploadRef.value?.uploadFiles || []
  if (uploadFiles.length === 0) {
    ElMessage.warning('请选择要上传的文件')
    return
  }

  uploadLoading.value = true
  try {
    const formData = new FormData()
    formData.append('moduleName', uploadForm.moduleName)
    formData.append('relatedId', uploadForm.relatedId)
    if (uploadForm.relatedName) {
      formData.append('relatedName', uploadForm.relatedName)
    }
    for (const f of uploadFiles) {
      formData.append('files', f.raw)
    }

    await uploadUnifiedAttachment(formData)
    ElMessage.success('上传成功')
    uploadDialogVisible.value = false
    await fetchAttachments()
  } catch (err) {
    ElMessage.error(err.response?.data?.message || '上传失败')
  } finally {
    uploadLoading.value = false
  }
}

async function handleDownload(row) {
  try {
    const { api } = await import('../api/auth')
    const res = await api.get(`/attachment/${row.id}/download`, { responseType: 'blob' })
    const url = URL.createObjectURL(res.data)
    const a = document.createElement('a')
    a.href = url
    a.download = row.fileName
    a.click()
    URL.revokeObjectURL(url)
    ElMessage.success('下载成功')
  } catch {
    ElMessage.error('下载失败')
  }
}

async function handleDelete(id) {
  try {
    await deleteUnifiedAttachment(id)
    ElMessage.success('附件已删除')
    await fetchAttachments()
  } catch (err) {
    ElMessage.error(err.response?.data?.message || '删除失败')
  }
}

onMounted(fetchAttachments)
</script>

<style scoped>
.attachment-management {
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
.upload-tip {
  font-size: 12px;
  color: #999;
  margin-top: 4px;
}
</style>
