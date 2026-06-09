<template>
  <div class="permission-change-log" v-loading="loading">
    <div class="filter-section">
      <el-input v-model="keyword" placeholder="搜索用户名、权限编码" clearable style="width: 220px" @keyup.enter="handleSearch" @clear="handleSearch">
        <template #prefix><el-icon><Search /></el-icon></template>
      </el-input>
      <el-select v-model="changeTypeFilter" placeholder="变更类型" clearable style="width: 130px" @change="handleSearch">
        <el-option label="授予" value="Grant" />
        <el-option label="修改" value="Modify" />
        <el-option label="撤销" value="Revoke" />
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
      <el-table-column prop="targetUsername" label="目标用户" width="120">
        <template #default="{ row }">
          <span v-if="row.targetUsername">{{ row.targetUsername }}</span>
          <span v-else style="color: #999">-</span>
        </template>
      </el-table-column>
      <el-table-column prop="changeType" label="变更类型" width="90">
        <template #default="{ row }">
          <el-tag :type="changeTypeTag(row.changeType)" size="small">{{ row.changeType }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="permissionCode" label="权限编码" width="160" />
      <el-table-column prop="permissionName" label="权限名称" width="140" />
      <el-table-column prop="operatorName" label="操作者" width="120" />
      <el-table-column prop="operatorIp" label="操作者IP" width="140" />
      <el-table-column prop="detail" label="详情" min-width="180" show-overflow-tooltip />
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
import { getPermissionChangeLogs } from '../api/auth'

const logs = ref([])
const loading = ref(false)
const keyword = ref('')
const changeTypeFilter = ref('')
const dateRange = ref(null)
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)

function changeTypeTag(type) {
  return type === 'Grant' ? 'success' : type === 'Revoke' ? 'danger' : 'warning'
}

function handleSearch() { page.value = 1; fetchLogs() }

function resetFilter() {
  keyword.value = ''
  changeTypeFilter.value = ''
  dateRange.value = null
  page.value = 1
  fetchLogs()
}

async function fetchLogs() {
  loading.value = true
  try {
    const params = { page: page.value, pageSize: pageSize.value }
    if (keyword.value) params.keyword = keyword.value
    if (changeTypeFilter.value) params.changeType = changeTypeFilter.value
    if (dateRange.value && dateRange.value.length === 2) {
      params.startDate = dateRange.value[0]
      params.endDate = dateRange.value[1]
    }
    const res = await getPermissionChangeLogs(params)
    if (res.data.success) {
      logs.value = res.data.data
      total.value = res.data.total
    }
  } catch { ElMessage.error('获取权限变更日志失败') }
  finally { loading.value = false }
}

onMounted(fetchLogs)
</script>

<style scoped>
.permission-change-log { background: #fff; padding: 20px; border-radius: 4px; }
.filter-section { display: flex; align-items: center; gap: 12px; flex-wrap: wrap; }
.pagination-wrapper { display: flex; align-items: center; justify-content: space-between; margin-top: 16px; }
.total-info { font-size: 14px; color: #666; }
</style>
