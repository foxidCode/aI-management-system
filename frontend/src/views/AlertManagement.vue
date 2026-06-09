<template>
  <div class="alert-management" v-loading="loading">
    <div class="top-section">
      <div class="toolbar">
        <el-button type="primary" @click="detectAnomalies">
          <el-icon><Search /></el-icon> 检测异常
        </el-button>
        <el-button @click="detectAnomalies">
          <el-icon><RefreshRight /></el-icon> 刷新
        </el-button>
      </div>
      <div class="stats-cards">
        <el-card shadow="hover" class="stat-card">
          <div class="stat-value danger">{{ anomalyCount }}</div>
          <div class="stat-label">异常告警</div>
        </el-card>
        <el-card shadow="hover" class="stat-card">
          <div class="stat-value">{{ todayOps }}</div>
          <div class="stat-label">今日操作</div>
        </el-card>
        <el-card shadow="hover" class="stat-card">
          <div class="stat-value warning">{{ sensitiveOps }}</div>
          <div class="stat-label">敏感操作</div>
        </el-card>
      </div>
    </div>

    <el-empty v-if="alerts.length === 0 && !loading" description="暂无异常告警，系统运行正常" :image-size="120" />

    <div v-if="alerts.length > 0" class="alert-list" style="margin-top: 20px">
      <el-alert
        v-for="(alert, idx) in alerts"
        :key="idx"
        :title="alert.alertType"
        :type="alert.severity === 'danger' ? 'error' : 'warning'"
        :description="alert.message"
        show-icon
        :closable="false"
        style="margin-bottom: 12px"
      >
        <template #default>
          <div class="alert-meta">
            <span>检测时间：{{ alert.detectedAt }}</span>
            <span v-if="alert.relatedUsername" style="margin-left: 16px">涉及用户：{{ alert.relatedUsername }}</span>
          </div>
        </template>
      </el-alert>
    </div>

    <!-- 告警规则说明 -->
    <el-divider style="margin-top: 32px" />
    <h4 style="margin-bottom: 12px; color: #666;">告警规则说明</h4>
    <el-descriptions :column="1" border size="small">
      <el-descriptions-item label="非工作时间敏感操作">
        在 22:00 - 06:00 时段内执行权限变更、删除等敏感操作时触发告警
      </el-descriptions-item>
      <el-descriptions-item label="频繁权限变更">
        10 分钟内执行超过 10 次权限变更操作时触发告警
      </el-descriptions-item>
      <el-descriptions-item label="频繁导出数据">
        1 小时内执行超过 5 次数据导出操作时触发告警
      </el-descriptions-item>
      <el-descriptions-item label="批量删除操作">
        执行任何批量删除操作时触发告警
      </el-descriptions-item>
    </el-descriptions>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { getAnomalies, getOperationStats } from '../api/auth'

const alerts = ref([])
const loading = ref(false)
const anomalyCount = ref(0)
const todayOps = ref(0)
const sensitiveOps = ref(0)

async function detectAnomalies() {
  loading.value = true
  try {
    const [anomalyRes, statsRes] = await Promise.all([
      getAnomalies(),
      getOperationStats(7),
    ])
    if (anomalyRes.data.success) {
      alerts.value = anomalyRes.data.data || []
      anomalyCount.value = alerts.value.length
    }
    if (statsRes.data.success) {
      todayOps.value = statsRes.data.data.todayOperations || 0
      sensitiveOps.value = statsRes.data.data.sensitiveOperations || 0
    }
  } catch { ElMessage.error('检测异常失败') }
  finally { loading.value = false }
}

onMounted(detectAnomalies)
</script>

<style scoped>
.alert-management { background: #fff; padding: 20px; border-radius: 4px; min-height: 60vh; }
.top-section { display: flex; flex-direction: column; gap: 16px; }
.toolbar { display: flex; gap: 10px; }
.stats-cards { display: flex; gap: 16px; }
.stat-card { flex: 1; max-width: 200px; text-align: center; }
.stat-value { font-size: 32px; font-weight: bold; color: #409eff; }
.stat-value.danger { color: #f56c6c; }
.stat-value.warning { color: #e6a23c; }
.stat-label { font-size: 14px; color: #909399; margin-top: 4px; }
.alert-meta { font-size: 12px; color: #909399; margin-top: 4px; }
</style>
