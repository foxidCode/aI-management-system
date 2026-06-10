<template>
  <div class="summary-page">
    <el-card>
      <template #header>
        <div class="page-header">
          <span class="page-title">AI 每日总结管理</span>
          <div class="header-actions">
            <el-date-picker
              v-model="generateDate"
              type="date"
              placeholder="选择日期（默认今天）"
              value-format="YYYY-MM-DD"
              style="width: 180px; margin-right: 8px"
              size="small"
            />
            <el-button type="primary" :icon="Refresh" @click="handleGenerate" :loading="generating">
              生成总结
            </el-button>
          </div>
        </div>
      </template>

      <el-table :data="summaries" stripe v-loading="loading">
        <el-table-column prop="summaryDate" label="日期" width="120" />
        <el-table-column prop="status" label="状态" width="100" align="center">
          <template #default="{ row }">
            <el-tag size="small" :type="statusType(row.status)">
              {{ statusLabel(row.status) }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="sessionCount" label="会话数" width="80" align="center" />
        <el-table-column prop="messageCount" label="消息数" width="80" align="center" />
        <el-table-column prop="content" label="内容摘要" min-width="300" show-overflow-tooltip>
          <template #default="{ row }">
            {{ row.content?.substring(0, 100) }}{{ row.content?.length > 100 ? '...' : '' }}
          </template>
        </el-table-column>
        <el-table-column prop="reviewerName" label="审批人" width="100" />
        <el-table-column prop="createdAt" label="创建时间" width="160" />
        <el-table-column label="操作" width="240" fixed="right">
          <template #default="{ row }">
            <el-button link type="primary" size="small" @click="showDetail(row)">详情</el-button>
            <el-button
              v-if="row.status === 'pending'"
              link type="success" size="small" @click="handleReview(row, 'approved')"
            >
              通过
            </el-button>
            <el-button
              v-if="row.status === 'pending'"
              link type="danger" size="small" @click="handleReview(row, 'rejected')"
            >
              拒绝
            </el-button>
            <el-popconfirm title="删除？" @confirm="handleDelete(row.id)">
              <template #reference>
                <el-button link type="danger" size="small">删除</el-button>
              </template>
            </el-popconfirm>
          </template>
        </el-table-column>
      </el-table>
      <div style="margin-top: 16px; text-align: right">
        <el-pagination
          v-model:current-page="page"
          :page-size="pageSize"
          :total="total"
          layout="total, prev, pager, next"
          @current-change="fetchData"
        />
      </div>
    </el-card>

    <!-- 详情弹窗 -->
    <el-dialog v-model="detailVisible" title="总结详情" width="640px">
      <div v-if="detail" class="summary-detail">
        <div class="detail-meta">
          <el-tag :type="statusType(detail.status)">{{ statusLabel(detail.status) }}</el-tag>
          <span>日期：{{ detail.summaryDate }}</span>
          <span>会话数：{{ detail.sessionCount }}</span>
          <span>消息数：{{ detail.messageCount }}</span>
          <span v-if="detail.reviewerName">审批人：{{ detail.reviewerName }}</span>
        </div>
        <div class="detail-content" v-html="renderSimpleMarkdown(detail.content)" />
        <div v-if="detail.reviewComment" class="review-comment">
          <strong>审批意见：</strong>{{ detail.reviewComment }}
        </div>
      </div>
    </el-dialog>

    <!-- 拒绝原因 -->
    <el-dialog v-model="rejectVisible" title="拒绝原因（可选）" width="420px">
      <el-input v-model="rejectComment" type="textarea" :rows="3" placeholder="输入拒绝原因（可选）" />
      <template #footer>
        <el-button @click="rejectVisible = false">取消</el-button>
        <el-button type="danger" @click="confirmReject">确认拒绝</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { Refresh } from '@element-plus/icons-vue'
import {
  getDailySummaries, generateDailySummary, reviewDailySummary, deleteDailySummary
} from '../api/ai'

const summaries = ref([])
const loading = ref(false)
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)

const generateDate = ref(new Date().toISOString().slice(0, 10))
const generating = ref(false)

const detailVisible = ref(false)
const detail = ref(null)

const rejectVisible = ref(false)
const rejectComment = ref('')
const rejectTarget = ref(null)

// ========== Data ==========
async function fetchData() {
  loading.value = true
  try {
    const res = await getDailySummaries({ page: page.value, pageSize: pageSize.value })
    if (res.data.success) {
      summaries.value = res.data.data
      total.value = res.data.total
    }
  } catch { /* ignore */ }
  loading.value = false
}

// ========== Generate ==========
async function handleGenerate() {
  generating.value = true
  try {
    const res = await generateDailySummary(generateDate.value || null)
    if (res.data.success) {
      ElMessage.success(res.data.message || '总结生成成功')
      await fetchData()
    } else {
      ElMessage.warning(res.data.message || '生成失败')
    }
  } catch (err) {
    ElMessage.error(err.response?.data?.message || '生成失败')
  }
  generating.value = false
}

// ========== Review ==========
function handleReview(row, status) {
  if (status === 'rejected') {
    rejectTarget.value = row
    rejectComment.value = ''
    rejectVisible.value = true
    return
  }
  doReview(row.id, 'approved', '')
}

async function confirmReject() {
  if (rejectTarget.value) {
    await doReview(rejectTarget.value.id, 'rejected', rejectComment.value)
    rejectVisible.value = false
  }
}

async function doReview(id, status, comment) {
  try {
    const res = await reviewDailySummary(id, { status, reviewComment: comment })
    if (res.data.success) {
      ElMessage.success(res.data.message || '操作成功')
      await fetchData()
    }
  } catch (err) {
    ElMessage.error(err.response?.data?.message || '操作失败')
  }
}

// ========== Detail ==========
function showDetail(row) {
  detail.value = row
  detailVisible.value = true
}

// ========== Delete ==========
async function handleDelete(id) {
  try {
    await deleteDailySummary(id)
    ElMessage.success('已删除')
    await fetchData()
  } catch {
    ElMessage.error('删除失败')
  }
}

// ========== Utils ==========
function statusType(status) {
  return { pending: 'warning', approved: 'success', rejected: 'danger' }[status] || 'info'
}
function statusLabel(status) {
  return { pending: '待审批', approved: '已通过', rejected: '已拒绝' }[status] || status
}

function renderSimpleMarkdown(text) {
  if (!text) return ''
  return text
    .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
    .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
    .replace(/\n/g, '<br/>')
}

onMounted(fetchData)
</script>

<style scoped>
.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.page-title {
  font-size: 16px;
  font-weight: 600;
}
.header-actions {
  display: flex;
  align-items: center;
}
.summary-detail {
  max-height: 60vh;
  overflow-y: auto;
}
.detail-meta {
  display: flex;
  gap: 16px;
  align-items: center;
  margin-bottom: 16px;
  font-size: 13px;
  color: #606266;
  flex-wrap: wrap;
}
.detail-content {
  background: #f5f7fa;
  padding: 16px;
  border-radius: 8px;
  line-height: 1.8;
  font-size: 14px;
}
.review-comment {
  margin-top: 12px;
  padding: 10px;
  background: #fef0f0;
  border-radius: 6px;
  font-size: 13px;
  color: #f56c6c;
}
</style>
