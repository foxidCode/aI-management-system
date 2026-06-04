<template>
  <div class="todo-center">
    <div class="stats-row">
      <el-statistic title="待审批" :value="todoList.length" />
      <el-button type="primary" @click="load"><el-icon><RefreshRight /></el-icon> 刷新</el-button>
    </div>
    <el-table :data="todoList" stripe border v-loading="loading" style="margin-top:16px" empty-text="暂无待审批任务">
      <el-table-column prop="definitionName" label="流程名称" width="150" />
      <el-table-column prop="nodeName" label="当前节点" width="120" />
      <el-table-column prop="instanceModuleName" label="业务模块" width="110" />
      <el-table-column prop="instanceRelatedId" label="关联ID" width="90" />
      <el-table-column prop="createdAt" label="到达时间" width="170" />
      <el-table-column label="操作" width="300" fixed="right">
        <template #default="{ row }">
          <el-button size="small" type="success" @click="handleApprove(row)">通过</el-button>
          <el-button size="small" type="danger" @click="handleReject(row)">驳回</el-button>
          <el-button size="small" plain @click="viewDetail(row)">查看</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-dialog v-model="approveVisible" title="审批" width="420px">
      <el-form label-width="80px">
        <el-form-item label="审批意见">
          <el-input v-model="comment" type="textarea" :rows="3" placeholder="可选" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="approveVisible=false">取消</el-button>
        <el-button type="success" @click="submitApprove">通过</el-button>
        <el-button type="danger" @click="submitReject">驳回</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { getTodoTasks, approveWorkflow, rejectWorkflow } from '../api/auth'

const router = useRouter()
const todoList = ref([])
const loading = ref(false)
const approveVisible = ref(false)
const comment = ref('')
let currentTask = null

async function load() {
  loading.value = true
  try { const r = await getTodoTasks(); if (r.data.success) todoList.value = r.data.data } catch { } finally { loading.value = false }
}

function handleApprove(row) { currentTask = row; comment.value = ''; approveVisible.value = true }
function handleReject(row) { currentTask = row; comment.value = ''; approveVisible.value = true }
function viewDetail(row) { router.push(`/dashboard/workflows/monitor?instanceId=${row.instanceId}`) }

async function submitApprove() {
  try { await approveWorkflow(currentTask.instanceId, { nodeId: currentTask.nodeId, comment: comment.value }); ElMessage.success('已通过'); approveVisible.value = false; load() } catch (e) { ElMessage.error(e.response?.data?.message || '操作失败') }
}
async function submitReject() {
  try { await rejectWorkflow(currentTask.instanceId, { nodeId: currentTask.nodeId, comment: comment.value }); ElMessage.success('已驳回'); approveVisible.value = false; load() } catch (e) { ElMessage.error(e.response?.data?.message || '操作失败') }
}

onMounted(load)
</script>

<style scoped>
.todo-center { background: #fff; border-radius: 6px; padding: 20px; min-height: calc(100vh - 160px); }
.stats-row { display: flex; align-items: center; gap: 24px; }
</style>
