<template>
  <div class="inbound-order">
    <div class="top-section">
      <el-input v-model="keyword" placeholder="搜索单据编码/供应商/库房/合同" clearable style="width: 280px" @keyup.enter="handleSearch" @clear="handleSearch">
        <template #prefix><el-icon><Search /></el-icon></template>
      </el-input>
      <el-button type="primary" @click="handleSearch"><el-icon><Search /></el-icon> 搜索</el-button>
      <div class="toolbar">
        <el-button type="primary" :disabled="!canManage" @click="$router.push('/dashboard/inbound/detail')"><el-icon><Plus /></el-icon>新增</el-button>
        <el-button @click="fetchData"><el-icon><RefreshRight /></el-icon>刷新</el-button>
        <el-dropdown @command="handleExport">
          <el-button type="success"><el-icon><Download /></el-icon>导出<el-icon class="el-icon--right"><ArrowDown /></el-icon></el-button>
          <template #dropdown>
            <el-dropdown-menu>
              <el-dropdown-item command="page">导出当前页</el-dropdown-item>
              <el-dropdown-item command="all">导出全部数据</el-dropdown-item>
            </el-dropdown-menu>
          </template>
        </el-dropdown>
      </div>
    </div>

    <el-table :data="list" stripe border v-loading="loading" style="width:100%;margin-top:16px" @sort-change="handleSort" @selection-change="s => selectedIds = s.map(x => x.id)">
      <el-table-column type="selection" width="50" />
      <el-table-column label="单据编码" width="180" sortable="custom">
        <template #default="{ row }">
          <el-link type="primary" @click="$router.push(`/dashboard/inbound/detail/${row.id}`)">{{ row.orderCode }}</el-link>
        </template>
      </el-table-column>
      <el-table-column label="创建日期" width="170" sortable="custom">
        <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column prop="createdByName" label="创建人" width="120" />
      <el-table-column label="更新日期" width="170" sortable="custom">
        <template #default="{ row }">{{ formatDate(row.updatedAt) }}</template>
      </el-table-column>
      <el-table-column prop="remark" label="备注" min-width="150" show-overflow-tooltip />
      <el-table-column prop="warehouseName" label="库房名称" width="140" show-overflow-tooltip />
      <el-table-column prop="supplier" label="供应商" width="140" show-overflow-tooltip />
      <el-table-column prop="contract" label="合同" width="140" show-overflow-tooltip />
      <el-table-column prop="totalTaxIncludedAmount" label="含税金额" width="130" sortable="custom">
        <template #default="{ row }">{{ row.totalTaxIncludedAmount.toFixed(2) }}</template>
      </el-table-column>
      <el-table-column prop="totalCostAmount" label="计成本金额" width="130">
        <template #default="{ row }">{{ row.totalCostAmount.toFixed(2) }}</template>
      </el-table-column>
      <el-table-column prop="totalTaxAmount" label="税额" width="120">
        <template #default="{ row }">{{ row.totalTaxAmount.toFixed(2) }}</template>
      </el-table-column>
      <el-table-column prop="taxRate" label="税率(%)" width="90">
        <template #default="{ row }">{{ row.taxRate }}</template>
      </el-table-column>
      <el-table-column label="操作" width="200" fixed="right">
        <template #default="{ row }">
          <el-button size="small" type="primary" @click="$router.push(`/dashboard/inbound/detail/${row.id}`)">查看</el-button>
          <el-button size="small" type="danger" :disabled="!canManage" @click="handleDelete(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <div class="pagination-wrapper">
      <span class="total-info">共 {{ total }} 条</span>
      <el-pagination v-model:current-page="page" v-model:page-size="pageSize" :page-sizes="[10,20,50,100]" :total="total" layout="sizes,prev,pager,next,jumper" background @size-change="fetchData" @current-change="fetchData" />
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, computed } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { getInboundOrders, deleteInboundOrder, exportInboundOrders } from '../api/auth'

// 权限
const perms = JSON.parse(localStorage.getItem('permissions') || '[]')
const canManage = computed(() => perms.includes('inbound:manage'))

const list = ref([])
const loading = ref(false)
const total = ref(0)
const page = ref(1)
const pageSize = ref(10)
const keyword = ref('')
const sortField = ref('')
const sortOrder = ref('')
const selectedIds = ref([])

function handleSearch() { page.value = 1; fetchData() }

function formatDate(dateStr) {
  if (!dateStr) return ''
  const d = new Date(dateStr)
  if (isNaN(d.getTime())) return dateStr
  const pad = n => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`
}

function handleSort({ prop, order }) {
  sortField.value = prop || ''
  sortOrder.value = order || ''
  fetchData()
}

async function fetchData() {
  loading.value = true
  try {
    const params = { page: page.value, pageSize: pageSize.value }
    if (keyword.value) params.keyword = keyword.value
    if (sortField.value) { params.sortField = sortField.value; params.sortOrder = sortOrder.value }
    const res = await getInboundOrders(params)
    if (res.data.success) { list.value = res.data.data; total.value = res.data.total }
  } catch { ElMessage.error('获取入库单列表失败') }
  finally { loading.value = false }
}

async function handleDelete(row) {
  try {
    await ElMessageBox.confirm(`确定删除入库单 "${row.orderCode}" 吗？`, '删除确认', { type: 'warning' })
    await deleteInboundOrder(row.id)
    ElMessage.success('删除成功')
    fetchData()
  } catch { /* 取消 */ }
}

async function handleExport(mode) {
  try {
    const params = {}
    if (mode === 'page') {
      // 导出当前页：传当前分页参数
      params.page = page.value
      params.pageSize = pageSize.value
      if (keyword.value) params.keyword = keyword.value
      // 直接浏览器下载
      const res = await exportInboundOrders(params)
      downloadBlob(res.data, `入库单_${new Date().toISOString().slice(0,10)}.csv`)
      ElMessage.success('导出成功')
    } else {
      // 导出全部：只传 keyword
      if (keyword.value) params.keyword = keyword.value
      const res = await exportInboundOrders(params)
      downloadBlob(res.data, `入库单_全部_${new Date().toISOString().slice(0,10)}.csv`)
      ElMessage.success('导出成功')
    }
  } catch { ElMessage.error('导出失败') }
}

function downloadBlob(blob, filename) {
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url; a.download = filename; a.click()
  URL.revokeObjectURL(url)
}

onMounted(fetchData)
</script>

<style scoped>
.inbound-order { background:#fff;padding:20px;border-radius:4px; }
.top-section { display:flex;align-items:center;gap:16px; }
.toolbar { display:flex;gap:10px;margin-left:auto; }
.pagination-wrapper { display:flex;align-items:center;justify-content:space-between;margin-top:16px; }
.total-info { font-size:14px;color:#666; }
</style>
