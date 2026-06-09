<template>
  <div class="audit-log" v-loading="loading">
    <div class="filter-section">
      <el-input v-model="keyword" placeholder="搜索用户名、操作、目标名称" clearable style="width: 240px" @keyup.enter="handleSearch" @clear="handleSearch">
        <template #prefix><el-icon><Search /></el-icon></template>
      </el-input>
      <el-select v-model="actionFilter" placeholder="操作类型" clearable style="width: 160px" @change="handleSearch">
        <el-option label="创建" value="permission:create" />
        <el-option label="更新" value="permission:update" />
        <el-option label="删除" value="permission:delete" />
        <el-option label="授予用户权限" value="permission:grant_user" />
        <el-option label="撤销用户权限" value="permission:revoke_user" />
        <el-option label="角色管理" value="post:api:role" />
        <el-option label="菜单管理" value="post:api:menu" />
        <el-option label="用户管理" value="post:api:usermanagement" />
      </el-select>
      <el-date-picker
        v-model="dateRange"
        type="daterange"
        range-separator="至"
        start-placeholder="开始日期"
        end-placeholder="结束日期"
        format="YYYY-MM-DD"
        value-format="YYYY-MM-DD"
        style="width: 260px"
        @change="handleSearch"
      />
      <el-button type="primary" @click="handleSearch"><el-icon><Search /></el-icon> 查询</el-button>
      <el-button @click="resetFilter">重置</el-button>
      <el-button @click="fetchLogs"><el-icon><RefreshRight /></el-icon> 刷新</el-button>
    </div>

    <el-table :data="logs" stripe border style="width: 100%; margin-top: 16px">
      <el-table-column prop="createdAt" label="时间" width="170" />
      <el-table-column prop="username" label="操作者" width="120" />
      <el-table-column prop="ipAddress" label="IP地址" width="140" />
      <el-table-column prop="action" label="操作类型" width="180" show-overflow-tooltip>
        <template #default="{ row }">
          <el-tag :type="row.isSensitive ? 'danger' : 'info'" size="small">{{ row.action }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="targetType" label="目标类型" width="100" />
      <el-table-column label="目标" min-width="160" show-overflow-tooltip>
        <template #default="{ row }">
          <span v-if="row.targetName">{{ row.targetName }}</span>
          <span v-else-if="row.targetId">ID: {{ row.targetId }}</span>
          <span v-else style="color: #999">-</span>
        </template>
      </el-table-column>
      <el-table-column prop="detail" label="详情" min-width="200" show-overflow-tooltip />
    </el-table>

    <div class="pagination-wrapper">
      <span class="total-info">共 {{ total }} 条记录</span>
      <el-pagination
        v-model:current-page="page"
        v-model:page-size="pageSize"
        :page-sizes="[10, 20, 50]"
        :total="total"
        layout="sizes, prev, pager, next, jumper"
        background
        @size-change="fetchLogs"
        @current-change="fetchLogs"
      />
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { getOperationLogs } from '../api/auth'

const logs = ref([])
const loading = ref(false)
const keyword = ref('')
const actionFilter = ref('')
const dateRange = ref(null)
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)

function handleSearch() { page.value = 1; fetchLogs() }

function resetFilter() {
  keyword.value = ''
  actionFilter.value = ''
  dateRange.value = null
  page.value = 1
  fetchLogs()
}

async function fetchLogs() {
  loading.value = true
  try {
    const params = { page: page.value, pageSize: pageSize.value }
    if (keyword.value) params.keyword = keyword.value
    if (actionFilter.value) params.action = actionFilter.value
    if (dateRange.value && dateRange.value.length === 2) {
      params.startDate = dateRange.value[0]
      params.endDate = dateRange.value[1]
    }
    const res = await getOperationLogs(params)
    if (res.data.success) {
      logs.value = res.data.data
      total.value = res.data.total
    }
  } catch { ElMessage.error('获取操作日志失败') }
  finally { loading.value = false }
}

onMounted(fetchLogs)
</script>

<style scoped>
.audit-log { background: #fff; padding: 20px; border-radius: 4px; }
.filter-section { display: flex; align-items: center; gap: 12px; flex-wrap: wrap; }
.pagination-wrapper { display: flex; align-items: center; justify-content: space-between; margin-top: 16px; }
.total-info { font-size: 14px; color: #666; }
</style>
