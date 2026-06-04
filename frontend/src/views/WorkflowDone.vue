<template>
  <div class="done-center">
    <el-tabs v-model="activeTab" @tab-change="load">
      <el-tab-pane label="我已审批" name="done" />
      <el-tab-pane label="我发起的" name="myapps" />
    </el-tabs>
    <el-table :data="list" stripe border v-loading="loading" empty-text="暂无记录">
      <el-table-column prop="definitionName" label="流程名称" width="150" />
      <el-table-column prop="nodeName" label="节点" width="120" />
      <el-table-column label="操作" width="80">
        <template #default="{ row }">
          <el-tag :type="row.status==='approved'?'success':'danger'" size="small">{{ row.status==='approved'?'通过':'驳回' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="comment" label="意见" min-width="150" show-overflow-tooltip />
      <el-table-column prop="instanceModuleName" label="模块" width="100" />
      <el-table-column prop="createdAt" label="时间" width="170" />
    </el-table>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { getDoneTasks, getMyApplications } from '../api/auth'

const list = ref([])
const loading = ref(false)
const activeTab = ref('done')

async function load() {
  loading.value = true
  try {
    const r = activeTab.value === 'done' ? await getDoneTasks() : await getMyApplications()
    if (r.data.success) list.value = r.data.data
  } catch { } finally { loading.value = false }
}

onMounted(load)
</script>

<style scoped>
.done-center { background: #fff; border-radius: 6px; padding: 20px; min-height: calc(100vh - 160px); }
</style>
