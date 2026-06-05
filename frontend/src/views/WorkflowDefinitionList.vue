<template>
  <div class="definition-management">
    <div class="top-section">
      <el-input
        v-model="keyword"
        placeholder="搜索流程名称、Key"
        clearable
        style="width: 280px"
        @keyup.enter="handleSearch"
        @clear="handleSearch"
      >
        <template #prefix><el-icon><Search /></el-icon></template>
      </el-input>
      <el-button type="primary" @click="handleSearch"><el-icon><Search /></el-icon> 搜索</el-button>
      <div class="toolbar">
        <el-button type="primary" @click="openDesigner"><el-icon><Plus /></el-icon> 新建流程</el-button>
        <el-button @click="fetchList"><el-icon><RefreshRight /></el-icon> 刷新</el-button>
      </div>
    </div>

    <el-table :data="list" v-loading="loading" stripe border style="width: 100%">
      <el-table-column prop="name" label="流程名称" min-width="150" show-overflow-tooltip />
      <el-table-column prop="key" label="Key" width="140" show-overflow-tooltip />
      <el-table-column prop="flowCode" label="流程编码" width="120" />
      <el-table-column prop="version" label="版本" width="80" />
      <el-table-column label="状态" width="90">
        <template #default="{ row }">
          <el-tag :type="row.isActive ? 'success' : 'info'" size="small">
            {{ row.isActive ? '已发布' : '未发布' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="updatedAt" label="更新时间" width="170">
        <template #default="{ row }">{{ formatDate(row.updatedAt) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="260" fixed="right">
        <template #default="{ row }">
          <el-button link type="primary" size="small" @click="openDesigner(row.id)">编辑</el-button>
          <el-button link type="success" size="small" @click="handlePublish(row)">发布</el-button>
          <el-button link type="danger" size="small" @click="handleDelete(row)">删除</el-button>
          <el-button link type="primary" size="small" @click="handleSubmitInstance(row)">发起</el-button>
        </template>
      </el-table-column>
    </el-table>

    <div class="pagination-wrapper">
      <span class="total-text">共 {{ total }} 条</span>
      <el-pagination
        v-model:current-page="page"
        v-model:page-size="pageSize"
        :page-sizes="[10, 20, 50]"
        :total="total"
        layout="sizes, prev, pager, next"
        @current-change="fetchList"
        @size-change="fetchList"
      />
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Search, Plus, RefreshRight } from '@element-plus/icons-vue'
import { getDefinitions, deleteDefinition, saveDefinition } from '@/workflow/api/workflow.js'

const router = useRouter()
const loading = ref(false)
const list = ref([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(10)
const keyword = ref('')

const fetchList = async () => {
  loading.value = true
  try {
    const res = await getDefinitions({ keyword: keyword.value, page: page.value, pageSize: pageSize.value })
    list.value = res.data.data || []
    total.value = res.data.total || 0
  } catch (e) {
    ElMessage.error('加载流程列表失败')
  } finally { loading.value = false }
}

const handleSearch = () => { page.value = 1; fetchList() }

const openDesigner = (id) => {
  router.push(id ? `/workflow/designer/${id}` : '/workflow/designer/new')
}

const handlePublish = async (row) => {
  try {
    await saveDefinition({ ...row, isActive: true })
    ElMessage.success('流程已发布')
    fetchList()
  } catch (e) {
    ElMessage.error(e.response?.data?.message || '发布失败')
  }
}

const handleDelete = async (row) => {
  try {
    await ElMessageBox.confirm(`确定删除流程 "${row.name}" 吗？`, '确认删除', { type: 'warning' })
    await deleteDefinition(row.id)
    ElMessage.success('已删除')
    fetchList()
  } catch { /* cancelled */ }
}

const handleSubmitInstance = (row) => {
  router.push(`/dashboard/workflow/submit/${row.id}`)
}

const formatDate = (d) => d ? new Date(d).toLocaleString('zh-CN') : ''

onMounted(fetchList)
</script>

<style scoped>
.definition-management { background: #fff; padding: 20px; border-radius: 4px; }
.top-section { display: flex; align-items: center; gap: 10px; margin-bottom: 16px; flex-wrap: wrap; }
.toolbar { margin-left: auto; display: flex; gap: 8px; }
.pagination-wrapper { display: flex; align-items: center; justify-content: space-between; margin-top: 16px; }
.total-text { color: #666; font-size: 14px; }
</style>
