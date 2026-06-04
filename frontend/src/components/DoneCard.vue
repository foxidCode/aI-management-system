<template>
  <div class="done-card">
    <div class="card-stat" @click="goDone">
      <span class="stat-num">{{ doneList.length }}</span>
      <span class="stat-label">已审批</span>
    </div>
    <div class="card-list" v-if="doneList.length > 0">
      <div
        v-for="item in doneList.slice(0, 5)"
        :key="item.id"
        class="card-item"
        @click="goDone"
      >
        <div class="item-info">
          <span class="item-title">{{ item.definitionName || '流程审批' }}</span>
          <span class="item-sub">{{ item.nodeName }}</span>
        </div>
        <el-tag :type="item.status === 'approved' ? 'success' : 'danger'" size="small">
          {{ item.status === 'approved' ? '已通过' : '已驳回' }}
        </el-tag>
      </div>
    </div>
    <div v-else class="card-empty">
      <el-icon :size="32" color="#c0c4cc"><Clock /></el-icon>
      <span>暂无已办记录</span>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { getDoneTasks } from '../api/auth'

const router = useRouter()
const doneList = ref([])

async function load() {
  try { const r = await getDoneTasks(); if (r.data.success) doneList.value = r.data.data } catch { }
}

function goDone() { router.push('/dashboard/workflows/done') }

onMounted(load)
</script>

<style scoped>
.done-card { padding: 8px; height: 100%; display: flex; flex-direction: column; }
.card-stat { display: flex; align-items: baseline; gap: 8px; cursor: pointer; padding: 4px 0; }
.stat-num { font-size: 32px; font-weight: 700; color: #67c23a; }
.stat-label { font-size: 14px; color: #909399; }
.card-list { flex: 1; overflow-y: auto; margin-top: 8px; }
.card-item { display: flex; align-items: center; justify-content: space-between; padding: 8px 4px; border-top: 1px solid #f5f7fa; cursor: pointer; transition: background 0.15s; }
.card-item:hover { background: #f0f9eb; }
.item-info { display: flex; flex-direction: column; gap: 2px; overflow: hidden; }
.item-title { font-size: 13px; color: #303133; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.item-sub { font-size: 11px; color: #c0c4cc; }
.card-empty { flex: 1; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 8px; color: #c0c4cc; font-size: 13px; }
</style>
