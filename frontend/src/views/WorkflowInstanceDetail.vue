<template>
  <div class="instance-detail" v-loading="loading">
    <div class="header-bar">
      <el-button text @click="$router.back()"><el-icon><ArrowLeft /></el-icon> 返回</el-button>
      <span class="title">{{ detail.definitionName || '流程详情' }}</span>
      <el-tag :type="statusTag(detail.status)" size="default">{{ statusLabel(detail.status) }}</el-tag>
    </div>

    <el-descriptions :column="3" border style="margin-bottom: 20px">
      <el-descriptions-item label="申请人">{{ detail.applicantName }}</el-descriptions-item>
      <el-descriptions-item label="提交时间">{{ formatDate(detail.createdAt) }}</el-descriptions-item>
      <el-descriptions-item label="完成时间">{{ formatDate(detail.completedAt) || '-' }}</el-descriptions-item>
    </el-descriptions>

    <el-tabs v-model="activeTab">
      <el-tab-pane label="表单数据" name="form">
        <div v-if="hasFormJson" style="max-width: 900px; margin: 0 auto;">
          <FormRender
            :lfFormData="formJsonStr"
            :lfFieldsData="formDataStr"
            :isPreview="true"
            :showSubmit="false"
          />
        </div>
        <el-card v-else-if="detail.formData">
          <pre class="json-display">{{ formatJson(detail.formData) }}</pre>
        </el-card>
        <el-empty v-else description="无表单数据" />
      </el-tab-pane>

      <el-tab-pane label="流程图" name="flow">
        <FlowViewer
          :nodesJson="definitionNodesJson"
          :activeNodeId="detail.currentNodeId || ''"
          :completedTaskNodeIds="completedTaskNodeIds"
        />
      </el-tab-pane>

      <el-tab-pane label="审批历史" name="history">
        <el-timeline v-if="detail.histories?.length">
          <el-timeline-item
            v-for="h in detail.histories"
            :key="h.id"
            :timestamp="formatDate(h.createdAt)"
            :type="timelineType(h.actionType)"
            :color="timelineColor(h.actionType)"
          >
            <p>
              <strong>{{ h.actorName }}</strong>
              {{ actionLabel(h.actionType) }}
              <span v-if="h.comment"> — "{{ h.comment }}"</span>
            </p>
          </el-timeline-item>
        </el-timeline>
        <el-empty v-else description="无审批记录" />
      </el-tab-pane>

      <el-tab-pane label="任务列表" name="tasks">
        <el-table :data="detail.tasks || []" stripe border>
          <el-table-column prop="nodeName" label="节点" width="130" />
          <el-table-column prop="assigneeName" label="处理人" width="100" />
          <el-table-column label="状态" width="90">
            <template #default="{ row }">
              <el-tag :type="row.status === 'Completed' ? 'success' : (row.status === 'Pending' ? 'warning' : 'info')" size="small">
                {{ row.status === 'Pending' ? '待处理' : (row.status === 'Completed' ? '已完成' : row.status) }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column prop="comment" label="意见" min-width="150" show-overflow-tooltip />
          <el-table-column prop="createdAt" label="创建时间" width="170">
            <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
          </el-table-column>
        </el-table>
      </el-tab-pane>
    </el-tabs>

    <!-- 操作栏（如有待办任务） -->
    <div class="action-bar" v-if="hasPendingTask">
      <el-input v-model="actionComment" type="textarea" :rows="2" placeholder="审批意见（可选）" class="action-input" />
      <div class="action-btns">
        <el-button type="success" @click="doApprove">通过</el-button>
        <el-button type="danger" @click="doReject">驳回</el-button>
        <el-button type="warning" @click="showTransfer">转办</el-button>
      </div>
    </div>

    <!-- 转办对话框 -->
    <el-dialog v-model="transferVisible" title="转办" width="400px" append-to-body>
      <el-select v-model="transferUserId" placeholder="选择用户" filterable style="width: 100%">
        <el-option v-for="u in userOptions" :key="u.id" :label="u.username" :value="u.id" />
      </el-select>
      <template #footer>
        <el-button @click="transferVisible = false">取消</el-button>
        <el-button type="primary" @click="doTransfer">确定</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { ElMessage } from 'element-plus'
import { ArrowLeft } from '@element-plus/icons-vue'
import { getInstanceDetail, getDefinition, approveTask, rejectTask, transferTask } from '@/workflow/api/workflow.js'
import { getUsers } from '@/workflow/api/workflow.js'
import FormRender from '@/workflow/components/workflow/dynamicForm/formRender.vue'
import FlowViewer from '@/workflow/components/workflow/flowDesign/flowViewer.vue'

const route = useRoute()
const loading = ref(false)
const detail = ref({})
const activeTab = ref('form')
const actionComment = ref('')
const transferVisible = ref(false)
const transferUserId = ref(null)
const userOptions = ref([])

// 表单渲染数据
const formJsonStr = ref('{}')
const formDataStr = ref('{}')
const hasFormJson = computed(() => {
  try { const o = JSON.parse(formJsonStr.value); return o && (o.widgetList || o.formConfig) } catch { return false }
})

// 流程图数据
const definitionNodesJson = ref('[]')
const completedTaskNodeIds = computed(() =>
  (detail.value.tasks || []).filter(t => t.status === 'Completed').map(t => t.nodeId)
)

const pendingTask = computed(() => (detail.value.tasks || []).find(t => t.status === 'Pending'))
const hasPendingTask = computed(() => !!pendingTask.value)

const fetchDetail = async () => {
  loading.value = true
  try {
    const res = await getInstanceDetail(route.params.id)
    detail.value = res.data.data || {}
    formDataStr.value = detail.value.formData || '{}'

    // 加载流程定义获取表单结构和节点
    if (detail.value.definitionId) {
      try {
        const defRes = await getDefinition(detail.value.definitionId)
        formJsonStr.value = defRes.data.data?.frmValue || '{}'
        definitionNodesJson.value = defRes.data.data?.nodes || '[]'
      } catch { formJsonStr.value = '{}'; definitionNodesJson.value = '[]' }
    }
  } catch (e) {
    ElMessage.error('加载实例详情失败')
  } finally { loading.value = false }
}

const doApprove = async () => {
  if (!pendingTask.value) return
  try {
    await approveTask(pendingTask.value.id, { comment: actionComment.value })
    ElMessage.success('已通过')
    actionComment.value = ''
    fetchDetail()
  } catch (e) { ElMessage.error(e.response?.data?.message || '操作失败') }
}

const doReject = async () => {
  if (!pendingTask.value) return
  if (!actionComment.value) { ElMessage.warning('请填写驳回意见'); return }
  try {
    await rejectTask(pendingTask.value.id, { comment: actionComment.value })
    ElMessage.success('已驳回')
    actionComment.value = ''
    fetchDetail()
  } catch (e) { ElMessage.error(e.response?.data?.message || '操作失败') }
}

const showTransfer = async () => {
  transferVisible.value = true
  transferUserId.value = null
  try {
    const res = await getUsers({ page: 1, pageSize: 1000 })
    userOptions.value = res.data.data?.list || []
  } catch { userOptions.value = [] }
}

const doTransfer = async () => {
  if (!transferUserId.value || !pendingTask.value) { ElMessage.warning('请选择目标用户'); return }
  try {
    await transferTask(pendingTask.value.id, { toUserId: transferUserId.value, comment: actionComment.value })
    ElMessage.success('已转办')
    transferVisible.value = false
    fetchDetail()
  } catch (e) { ElMessage.error(e.response?.data?.message || '转办失败') }
}

const statusTag = (s) => ({ Running: 'warning', Completed: 'success', Rejected: 'danger', Cancelled: 'info' }[s] || 'info')
const statusLabel = (s) => ({ Running: '进行中', Completed: '已完成', Rejected: '已驳回', Cancelled: '已取消' }[s] || s)

const actionLabel = (a) => ({
  submitted: '提交申请', approved: '审批通过', rejected: '驳回', transferred: '转办', cancelled: '取消'
}[a] || a)

const timelineType = (a) => ({ submitted: 'primary', approved: 'success', rejected: 'danger', transferred: 'warning' }[a] || 'info')
const timelineColor = (a) => ({ submitted: '#409EFF', approved: '#67C23A', rejected: '#F56C6C', transferred: '#E6A23C' }[a])

const formatDate = (d) => d ? new Date(d).toLocaleString('zh-CN') : ''
const formatJson = (s) => {
  try { return JSON.stringify(JSON.parse(s), null, 2) } catch { return s }
}

onMounted(fetchDetail)
</script>

<style scoped>
.instance-detail { background: #fff; padding: 20px; border-radius: 4px; min-height: 400px; }
.header-bar { display: flex; align-items: center; gap: 16px; margin-bottom: 20px; }
.title { font-size: 18px; font-weight: 600; }
.json-display { background: #f5f7fa; padding: 16px; border-radius: 4px; font-size: 13px; white-space: pre-wrap; word-break: break-all; }
.action-bar { margin-top: 24px; padding: 16px; background: #f5f7fa; border-radius: 4px; display: flex; gap: 12px; align-items: flex-start; }
.action-input { flex: 1; }
.action-btns { display: flex; gap: 8px; white-space: nowrap; }
</style>
