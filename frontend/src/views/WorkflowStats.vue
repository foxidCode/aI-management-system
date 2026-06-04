<template>
  <div class="stats">
    <h3 style="margin-bottom:16px">流程统计</h3>
    <el-row :gutter="16">
      <el-col :span="6"><el-statistic-card title="流程定义" :value="stats.totalDefinitions" /></el-col>
      <el-col :span="6"><el-statistic-card title="已发布" :value="stats.publishedDefinitions" /></el-col>
      <el-col :span="6"><el-statistic-card title="运行中" :value="stats.runningInstances" /></el-col>
      <el-col :span="6"><el-statistic-card title="今日完成" :value="stats.completedToday" /></el-col>
    </el-row>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { getWorkflowStats } from '../api/auth'

const stats = ref({ totalDefinitions: 0, publishedDefinitions: 0, runningInstances: 0, completedToday: 0, myPendingTasks: 0 })

onMounted(async () => {
  try { const r = await getWorkflowStats(); if (r.data.success) stats.value = r.data.data } catch { }
})
</script>

<style scoped>
.stats { background: #fff; border-radius: 6px; padding: 20px; min-height: calc(100vh - 160px); }
</style>
