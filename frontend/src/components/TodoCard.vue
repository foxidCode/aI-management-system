<template>
  <div class="todo-card" v-loading="loading">
    <!-- 头部 -->
    <div class="card-top">
      <div class="card-title-row">
        <div class="title-left">
          <el-icon :size="18" color="#e6533b"><Clock /></el-icon>
          <span class="title-text">我的待办</span>
          <el-badge v-if="total > 0" :value="total" class="count-badge" :max="99" type="danger" />
        </div>
        <el-button :icon="Refresh" circle size="small" text @click="fetchData" :loading="loading" class="refresh-btn" />
      </div>
    </div>

    <!-- 列表 -->
    <div class="task-list" v-if="tasks.length > 0">
      <div
        v-for="task in tasks"
        :key="task.id"
        class="task-item"
        @click="goToInstance(task.instanceId)"
      >
        <div class="task-left">
          <span class="task-node">{{ task.instance?.definitionName || '-' }}</span>
          <span class="task-process">{{ task.nodeName }}</span>
        </div>
        <div class="task-right">
          <span class="task-applicant">{{ task.instance?.applicantName || task.assigneeName || '-' }}</span>
          <span class="task-time">{{ formatTime(task.createdAt) }}</span>
        </div>
      </div>
    </div>

    <!-- 空状态 -->
    <div v-else-if="!loading" class="empty-state">
      <el-icon :size="36" color="#c0c4cc"><DocumentChecked /></el-icon>
      <span>暂无待办任务</span>
    </div>

    <!-- 底部 -->
    <div class="card-bottom" v-if="total > 0">
      <el-button link type="primary" size="small" @click="goToList">
        查看全部 <el-icon :size="12"><ArrowRight /></el-icon>
      </el-button>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { Clock, Refresh, DocumentChecked, ArrowRight } from '@element-plus/icons-vue'
import { getMyTasks } from '../workflow/api/workflow.js'

const router = useRouter()
const loading = ref(true)
const tasks = ref([])
const total = ref(0)

async function fetchData() {
  loading.value = true
  try {
    const res = await getMyTasks({ page: 1, pageSize: 5 })
    tasks.value = res.data.data || []
    total.value = res.data.total || 0
  } catch {
    tasks.value = []
    total.value = 0
  } finally {
    loading.value = false
  }
}

function formatTime(d) {
  if (!d) return ''
  const date = new Date(d)
  const now = new Date()
  const diff = now - date
  if (diff < 60000) return '刚刚'
  if (diff < 3600000) return `${Math.floor(diff / 60000)}分钟前`
  if (diff < 86400000) return `${Math.floor(diff / 3600000)}小时前`
  return date.toLocaleDateString('zh-CN', { month: '2-digit', day: '2-digit' })
}

function goToInstance(id) {
  if (id) router.push(`/dashboard/workflow/instance/${id}`)
}

function goToList() {
  router.push('/dashboard/workflow/my-tasks')
}

onMounted(fetchData)
</script>

<style scoped>
.todo-card {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: linear-gradient(160deg, #fff5f5 0%, #fffbf0 100%);
  border-radius: 8px;
  overflow: hidden;
  box-sizing: border-box;
}

.card-top {
  padding: 10px 14px 8px;
  flex-shrink: 0;
}

.card-title-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.title-left {
  display: flex;
  align-items: center;
  gap: 6px;
}

.title-text {
  font-size: 14px;
  font-weight: 600;
  color: #303133;
}

.count-badge {
  margin-left: 2px;
}

.refresh-btn {
  color: #909399;
  transition: color 0.2s;
}
.refresh-btn:hover {
  color: #e6533b;
}

/* 任务列表 */
.task-list {
  flex: 1;
  overflow-y: auto;
  padding: 0 8px;
  min-height: 0;
}

.task-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 8px;
  border-radius: 6px;
  cursor: pointer;
  transition: background 0.15s;
  margin-bottom: 2px;
  gap: 8px;
}

.task-item:hover {
  background: rgba(230, 83, 59, 0.06);
}

.task-left {
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
  flex: 1;
}

.task-node {
  font-size: 13px;
  font-weight: 500;
  color: #303133;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.task-process {
  font-size: 11px;
  color: #909399;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.task-right {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 2px;
  flex-shrink: 0;
}

.task-applicant {
  font-size: 12px;
  color: #606266;
}

.task-time {
  font-size: 11px;
  color: #c0c4cc;
}

/* 空状态 */
.empty-state {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 8px;
  color: #c0c4cc;
  font-size: 13px;
}

/* 底部 */
.card-bottom {
  padding: 6px 14px 10px;
  text-align: center;
  flex-shrink: 0;
  border-top: 1px solid rgba(0, 0, 0, 0.04);
}
</style>
