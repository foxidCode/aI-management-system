<template>
  <div class="monitor">
    <el-select v-model="filter" clearable placeholder="按状态筛选" @change="load" style="width:160px">
      <el-option label="运行中" value="running" />
      <el-option label="已通过" value="approved" />
      <el-option label="已驳回" value="rejected" />
      <el-option label="已撤回" value="recalled" />
    </el-select>
    <el-button @click="load" style="margin-left:12px"><el-icon><RefreshRight /></el-icon> 刷新</el-button>

    <el-table :data="instances" stripe border v-loading="loading" style="margin-top:16px">
      <el-table-column prop="id" label="ID" width="60" />
      <el-table-column prop="definitionName" label="流程名称" width="150" />
      <el-table-column prop="moduleName" label="模块" width="120" />
      <el-table-column prop="relatedId" label="关联ID" width="80" />
      <el-table-column label="状态" width="100">
        <template #default="{ row }">
          <el-tag :type="row.status==='running'?'warning':row.status==='approved'?'success':'danger'" size="small">{{ {running:'运行中',approved:'已通过',rejected:'已驳回',recalled:'已撤回'}[row.status] }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="startedByName" label="发起人" width="100" />
      <el-table-column prop="startedAt" label="发起时间" width="170" />
      <el-table-column prop="completedAt" label="完成时间" width="170" />
      <el-table-column label="操作" width="120">
        <template #default="{ row }">
          <el-button v-if="row.status==='running'" size="small" type="danger" plain @click="recall(row)">撤回</el-button>
          <el-button size="small" plain @click="viewDetail(row)">详情</el-button>
        </template>
      </el-table-column>
    </el-table>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { getWorkflowInstances, recallWorkflow } from '../api/auth'

const instances = ref([])
const loading = ref(false)
const filter = ref('')

async function load() {
  loading.value = true
  try { const r = await getWorkflowInstances({ status: filter.value || undefined }); if (r.data.success) instances.value = r.data.data } catch { } finally { loading.value = false }
}

async function recall(row) {
  try { await ElMessageBox.confirm('确定撤回该流程？', '撤回', { type: 'warning' }); await recallWorkflow(row.id); ElMessage.success('已撤回'); load() } catch { }
}

function viewDetail(row) {
  // TODO: navigate to detail page
  ElMessage.info(`实例 #${row.id}: ${row.definitionName}`)
}

onMounted(load)
</script>

<style scoped>
.monitor { background: #fff; border-radius: 6px; padding: 20px; min-height: calc(100vh - 160px); }
</style>
