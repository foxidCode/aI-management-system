<template>
  <div class="wf-mgmt">
    <div class="top-section">
      <el-button type="primary" @click="openDesigner"><el-icon><Plus /></el-icon> 新建流程</el-button>
      <el-button @click="loadData"><el-icon><RefreshRight /></el-icon> 刷新</el-button>
      <el-select v-model="filterCategory" clearable placeholder="按分类筛选" style="width:180px" @change="loadData">
        <el-option label="入库单审批" value="InboundOrder" />
        <el-option label="账号申请" value="AccountRequest" />
      </el-select>
    </div>
    <el-table :data="defs" stripe border v-loading="loading" style="margin-top:16px">
      <el-table-column prop="id" label="ID" width="60" />
      <el-table-column prop="name" label="名称" min-width="140" />
      <el-table-column prop="category" label="分类" width="120" />
      <el-table-column prop="version" label="版本" width="70" align="center" />
      <el-table-column label="状态" width="100">
        <template #default="{ row }">
          <el-tag :type="row.status==='published'?'success':row.status==='draft'?'info':'warning'" size="small">{{ {draft:'草稿',published:'已发布',archived:'已归档'}[row.status] || row.status }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="runningInstanceCount" label="运行中" width="80" align="center" />
      <el-table-column prop="updatedAt" label="更新时间" width="170" />
      <el-table-column label="操作" width="280" fixed="right">
        <template #default="{ row }">
          <el-button size="small" type="primary" plain @click="openDesigner(row)"><el-icon><Edit /></el-icon>编辑</el-button>
          <el-button v-if="row.status==='draft'" size="small" type="success" plain @click="publishDef(row)">发布</el-button>
          <el-button v-if="row.status==='published'" size="small" type="warning" plain @click="archiveDef(row)">归档</el-button>
          <el-button size="small" type="danger" plain @click="deleteDef(row)" :disabled="row.runningInstanceCount>0">删除</el-button>
        </template>
      </el-table-column>
    </el-table>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { getWorkflowDefinitions, publishWorkflow, updateWorkflowDefinition, deleteWorkflowDefinition } from '../api/auth'

const router = useRouter()
const defs = ref([])
const loading = ref(false)
const filterCategory = ref('')

async function loadData() {
  loading.value = true
  try {
    const res = await getWorkflowDefinitions({ category: filterCategory.value || undefined })
    if (res.data.success) defs.value = res.data.data
  } catch { ElMessage.error('加载失败') } finally { loading.value = false }
}

function openDesigner(row) { router.push(row ? `/dashboard/workflows/design/${row.id}` : '/dashboard/workflows/design') }

async function publishDef(row) {
  try { await publishWorkflow(row.id); ElMessage.success('已发布'); loadData() } catch { ElMessage.error('发布失败') }
}

async function archiveDef(row) {
  try { await updateWorkflowDefinition(row.id, { name: row.name, category: row.category, nodeData: row.nodeData }); ElMessage.success('操作成功'); loadData() } catch { ElMessage.error('操作失败') }
}

async function deleteDef(row) {
  try {
    await ElMessageBox.confirm('确定删除？', '删除', { type: 'warning' })
    await deleteWorkflowDefinition(row.id); ElMessage.success('已删除'); loadData()
  } catch { }
}

onMounted(loadData)
</script>

<style scoped>
.wf-mgmt { background: #fff; border-radius: 6px; padding: 20px; min-height: calc(100vh - 160px); }
.top-section { display: flex; gap: 12px; align-items: center; }
</style>
