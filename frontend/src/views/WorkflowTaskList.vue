<template>
  <div class="task-management">
    <el-tabs v-model="activeTab" @tab-change="handleTabChange">
      <el-tab-pane label="待审批" name="pending" />
      <el-tab-pane label="已处理" name="history" />
    </el-tabs>

    <el-table :data="list" v-loading="loading" stripe border style="width: 100%">
      <el-table-column prop="nodeName" label="审批节点" width="130" show-overflow-tooltip />
      <el-table-column label="流程名称" min-width="150">
        <template #default="{ row }">{{ row.instance?.definitionName || '-' }}</template>
      </el-table-column>
      <el-table-column label="申请人" width="100">
        <template #default="{ row }">{{ row.instance?.applicantName || row.assigneeName || '-' }}</template>
      </el-table-column>
      <el-table-column prop="createdAt" label="创建时间" width="170">
        <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="280" fixed="right">
        <template #default="{ row }">
          <template v-if="activeTab === 'pending'">
            <el-button link type="success" size="small" @click="handleApprove(row)">通过</el-button>
            <el-button link type="danger" size="small" @click="handleReject(row)">驳回</el-button>
            <el-button link type="warning" size="small" @click="handleTransfer(row)">转办</el-button>
            <el-button link type="primary" size="small" @click="viewDetail(row.instanceId)">查看</el-button>
          </template>
          <template v-else>
            <el-button link type="primary" size="small" @click="viewDetail(row.instanceId)">查看详情</el-button>
          </template>
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

    <!-- 转办对话框 -->
    <el-dialog v-model="transferVisible" title="转办任务" width="400px" append-to-body>
      <el-form>
        <el-form-item label="转给用户">
          <el-select v-model="transferUserId" placeholder="选择用户" filterable style="width: 100%">
            <el-option v-for="u in userOptions" :key="u.id" :label="u.username" :value="u.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="转办意见">
          <el-input v-model="transferComment" type="textarea" :rows="3" placeholder="选填" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="transferVisible = false">取消</el-button>
        <el-button type="primary" @click="doTransfer">确定转办</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { getMyTasks, getTaskHistory, approveTask, rejectTask, transferTask } from '@/workflow/api/workflow.js'
import { getUsers } from '@/workflow/api/workflow.js'

const router = useRouter()
const loading = ref(false)
const list = ref([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(10)
const activeTab = ref('pending')

// 转办相关
const transferVisible = ref(false)
const transferUserId = ref(null)
const transferComment = ref('')
const transferTaskId = ref(null)
const userOptions = ref([])

const fetchList = async () => {
  loading.value = true
  try {
    const fn = activeTab.value === 'pending' ? getMyTasks : getTaskHistory
    const res = await fn({ page: page.value, pageSize: pageSize.value })
    list.value = res.data.data || []
    total.value = res.data.total || 0
  } catch (e) {
    ElMessage.error('加载任务列表失败')
  } finally { loading.value = false }
}

const handleTabChange = () => { page.value = 1; fetchList() }

const handleApprove = async (row) => {
  try {
    const comment = await ElMessageBox.prompt('审批意见（可选）', '审批通过', { confirmButtonText: '通过', cancelButtonText: '取消', inputType: 'textarea' })
    await approveTask(row.id, { comment: comment.value || '' })
    ElMessage.success('已通过')
    fetchList()
  } catch { /* cancelled */ }
}

const handleReject = async (row) => {
  try {
    const comment = await ElMessageBox.prompt('驳回意见', '驳回', { confirmButtonText: '驳回', cancelButtonText: '取消', inputType: 'textarea' })
    await rejectTask(row.id, { comment: comment.value || '' })
    ElMessage.success('已驳回')
    fetchList()
  } catch { /* cancelled */ }
}

const handleTransfer = async (row) => {
  transferTaskId.value = row.id
  transferVisible.value = true
  transferUserId.value = null
  transferComment.value = ''
  // 加载用户列表
  try {
    const res = await getUsers({ page: 1, pageSize: 1000 })
    userOptions.value = res.data.data?.list || []
  } catch { userOptions.value = [] }
}

const doTransfer = async () => {
  if (!transferUserId.value) { ElMessage.warning('请选择目标用户'); return }
  try {
    await transferTask(transferTaskId.value, { toUserId: transferUserId.value, comment: transferComment.value })
    ElMessage.success('已转办')
    transferVisible.value = false
    fetchList()
  } catch (e) {
    ElMessage.error(e.response?.data?.message || '转办失败')
  }
}

const viewDetail = (id) => { router.push(`/dashboard/workflow/instance/${id}`) }
const formatDate = (d) => d ? new Date(d).toLocaleString('zh-CN') : ''

onMounted(fetchList)
</script>

<style scoped>
.task-management { background: #fff; padding: 20px; border-radius: 4px; }
.pagination-wrapper { display: flex; align-items: center; justify-content: space-between; margin-top: 16px; }
.total-text { color: #666; font-size: 14px; }
</style>
