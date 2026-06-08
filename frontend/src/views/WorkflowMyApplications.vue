<template>
  <div class="application-management">
    <div class="top-section">
      <el-select v-model="statusFilter" placeholder="状态筛选" clearable style="width: 180px" @change="handleSearch">
        <el-option label="进行中" value="Running" />
        <el-option label="已完成" value="Completed" />
        <el-option label="已驳回" value="Rejected" />
        <el-option label="已取消" value="Cancelled" />
      </el-select>
      <el-button type="primary" @click="handleSearch"><el-icon><Search /></el-icon> 搜索</el-button>
      <el-button @click="fetchList"><el-icon><RefreshRight /></el-icon> 刷新</el-button>
    </div>

    <el-table :data="list" v-loading="loading" stripe border style="width: 100%">
      <el-table-column prop="definitionName" label="流程名称" min-width="150" show-overflow-tooltip />
      <el-table-column prop="applicantName" label="申请人" width="100" />
      <el-table-column label="状态" width="100">
        <template #default="{ row }">
          <el-tag :type="statusTag(row.status)" size="small">{{ statusLabel(row.status) }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="createdAt" label="提交时间" width="170">
        <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column prop="completedAt" label="完成时间" width="170">
        <template #default="{ row }">{{ formatDate(row.completedAt) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="120">
        <template #default="{ row }">
          <el-button link type="primary" size="small" @click="viewDetail(row.id)">查看详情</el-button>
        </template>
      </el-table-column>
    </el-table>

    <div class="pagination-wrapper">
      <span class="total-text">共 {{ total }} 条</span>
      <el-pagination
        v-model:current-page="page" v-model:page-size="pageSize"
        :page-sizes="[10, 20, 50]" :total="total"
        layout="sizes, prev, pager, next"
        @current-change="fetchList" @size-change="fetchList"
      />
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { Search, RefreshRight } from '@element-plus/icons-vue'
import { getMyInstances } from '@/workflow/api/workflow.js'

const router = useRouter()
const loading = ref(false)
const list = ref([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(10)
const statusFilter = ref('')

const fetchList = async () => {
  loading.value = true
  try {
    const res = await getMyInstances({ status: statusFilter.value, page: page.value, pageSize: pageSize.value })
    list.value = res.data.data || []
    total.value = res.data.total || 0
  } catch (e) {
    ElMessage.error('加载申请列表失败')
  } finally { loading.value = false }
}

const handleSearch = () => { page.value = 1; fetchList() }
const viewDetail = (id) => { router.push(`/dashboard/workflow/instance/${id}`) }

const statusTag = (s) => ({ Running: 'warning', Completed: 'success', Rejected: 'danger', Cancelled: 'info' }[s] || 'warning')
const statusLabel = (s) => ({ Running: '进行中', Completed: '已完成', Rejected: '已驳回', Cancelled: '已取消' }[s] || s)
const formatDate = (d) => d ? new Date(d).toLocaleString('zh-CN') : ''

onMounted(fetchList)
</script>

<style scoped>
.application-management { background: #fff; padding: 20px; border-radius: 4px; }
.top-section { display: flex; align-items: center; gap: 10px; margin-bottom: 16px; }
.pagination-wrapper { display: flex; align-items: center; justify-content: space-between; margin-top: 16px; }
.total-text { color: #666; font-size: 14px; }
</style>
