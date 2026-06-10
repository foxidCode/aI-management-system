<template>
  <div class="knowledge-page">
    <el-card>
      <template #header>
        <div class="page-header">
          <span class="page-title">AI 知识库</span>
          <el-button type="primary" :icon="Plus" @click="openDialog()">添加条目</el-button>
        </div>
      </template>

      <!-- 搜索和筛选 -->
      <div class="toolbar">
        <el-select v-model="filterCategory" placeholder="全部分类" clearable style="width: 160px" @change="fetchData">
          <el-option v-for="cat in categories" :key="cat" :label="cat" :value="cat" />
        </el-select>
      </div>

      <el-table :data="entries" stripe v-loading="loading">
        <el-table-column prop="title" label="标题" min-width="180" />
        <el-table-column prop="category" label="分类" width="120">
          <template #default="{ row }">
            <el-tag size="small">{{ row.category }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="source" label="来源" width="100">
          <template #default="{ row }">
            <el-tag size="small" :type="row.source === 'system' ? 'info' : 'success'">
              {{ row.source === 'system' ? '系统内置' : 'AI总结' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="content" label="内容摘要" min-width="260" show-overflow-tooltip>
          <template #default="{ row }">
            {{ row.content?.substring(0, 80) }}{{ row.content?.length > 80 ? '...' : '' }}
          </template>
        </el-table-column>
        <el-table-column prop="isActive" label="状态" width="80" align="center">
          <template #default="{ row }">
            <el-tag size="small" :type="row.isActive ? 'success' : 'info'">
              {{ row.isActive ? '启用' : '禁用' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="updatedAt" label="更新时间" width="160" />
        <el-table-column label="操作" width="160" fixed="right">
          <template #default="{ row }">
            <el-button link type="primary" size="small" @click="openDialog(row)">编辑</el-button>
            <el-popconfirm title="删除？" @confirm="handleDelete(row.id)">
              <template #reference>
                <el-button link type="danger" size="small">删除</el-button>
              </template>
            </el-popconfirm>
          </template>
        </el-table-column>
      </el-table>
      <div style="margin-top: 16px; text-align: right">
        <el-pagination
          v-model:current-page="page"
          :page-size="pageSize"
          :total="total"
          layout="total, prev, pager, next"
          @current-change="fetchData"
        />
      </div>
    </el-card>

    <!-- 编辑弹窗 -->
    <el-dialog
      v-model="dialogVisible"
      :title="isEdit ? '编辑知识条目' : '添加知识条目'"
      width="600px"
      :close-on-click-modal="false"
    >
      <el-form :model="form" label-width="80px" label-position="left">
        <el-form-item label="标题" required>
          <el-input v-model="form.title" placeholder="知识条目标题" />
        </el-form-item>
        <el-form-item label="分类" required>
          <el-select v-model="form.category" style="width: 100%" allow-create filterable>
            <el-option label="getting-started" value="getting-started" />
            <el-option label="feature-guide" value="feature-guide" />
            <el-option label="faq" value="faq" />
            <el-option label="ai-summary" value="ai-summary" />
            <el-option label="general" value="general" />
          </el-select>
        </el-form-item>
        <el-form-item label="内容" required>
          <el-input v-model="form.content" type="textarea" :rows="8" placeholder="知识内容（支持 Markdown 格式）" />
        </el-form-item>
        <el-form-item label="启用">
          <el-switch v-model="form.isActive" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="handleSave" :loading="saving">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { Plus } from '@element-plus/icons-vue'
import {
  getKnowledgeEntries, getKnowledgeEntryById, createKnowledgeEntry,
  updateKnowledgeEntry, deleteKnowledgeEntry, getKnowledgeCategories
} from '../api/ai'

const entries = ref([])
const categories = ref([])
const loading = ref(false)
const filterCategory = ref('')
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)

const dialogVisible = ref(false)
const isEdit = ref(false)
const editingId = ref(null)
const saving = ref(false)
const form = ref({
  title: '',
  content: '',
  category: 'general',
  isActive: true
})

// ========== Data ==========
async function fetchData() {
  loading.value = true
  try {
    const [entriesRes, catsRes] = await Promise.all([
      getKnowledgeEntries({ category: filterCategory.value || undefined, page: page.value, pageSize: pageSize.value }),
      getKnowledgeCategories()
    ])
    if (entriesRes.data.success) {
      entries.value = entriesRes.data.data
      total.value = entriesRes.data.total
    }
    if (catsRes.data.success) categories.value = catsRes.data.data
  } catch { /* ignore */ }
  loading.value = false
}

// ========== Dialog ==========
async function openDialog(row) {
  if (row) {
    isEdit.value = true
    editingId.value = row.id
    try {
      const res = await getKnowledgeEntryById(row.id)
      if (res.data.success) {
        const d = res.data.data
        form.value = {
          title: d.title,
          content: d.content,
          category: d.category,
          isActive: d.isActive
        }
      }
    } catch { ElMessage.error('获取详情失败') }
  } else {
    isEdit.value = false
    editingId.value = null
    form.value = { title: '', content: '', category: 'general', isActive: true }
  }
  dialogVisible.value = true
}

async function handleSave() {
  if (!form.value.title || !form.value.content) {
    ElMessage.warning('请填写标题和内容')
    return
  }
  saving.value = true
  try {
    if (isEdit.value) {
      await updateKnowledgeEntry(editingId.value, form.value)
      ElMessage.success('更新成功')
    } else {
      await createKnowledgeEntry(form.value)
      ElMessage.success('添加成功')
    }
    dialogVisible.value = false
    await fetchData()
  } catch (err) {
    ElMessage.error(err.response?.data?.message || '操作失败')
  }
  saving.value = false
}

async function handleDelete(id) {
  try {
    await deleteKnowledgeEntry(id)
    ElMessage.success('已删除')
    await fetchData()
  } catch {
    ElMessage.error('删除失败')
  }
}

onMounted(fetchData)
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
.toolbar {
  margin-bottom: 16px;
}
</style>
