<template>
  <div class="todo-card">
    <div class="card-stat" @click="goTodo">
      <span class="stat-num" :class="{ urgent: todoList.length > 0 }">{{ todoList.length }}</span>
      <span class="stat-label">待审批</span>
    </div>
    <div class="card-list" v-if="todoList.length > 0">
      <div
        v-for="item in todoList.slice(0, 5)"
        :key="item.id"
        class="card-item"
        @click="goApprove(item)"
      >
        <div class="item-info">
          <span class="item-title">{{ item.definitionName || '流程审批' }}</span>
          <span class="item-sub">{{ item.nodeName }} · {{ item.instanceModuleName }} #{{ item.instanceRelatedId }}</span>
        </div>
        <el-tag size="small" type="warning">{{ item.createdAt?.slice(11,16) || '' }}</el-tag>
      </div>
    </div>
    <div v-else class="card-empty">
      <el-icon :size="32" color="#c0c4cc"><Check /></el-icon>
      <span>暂无待审批任务</span>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { getTodoTasks } from '../api/auth'

const router = useRouter()
const todoList = ref([])

async function load() {
  try { const r = await getTodoTasks(); if (r.data.success) todoList.value = r.data.data } catch { }
}

function goTodo() { router.push('/dashboard/workflows/todo') }
function goApprove(item) { router.push('/dashboard/workflows/todo') }

onMounted(load)
</script>

<style scoped>
.todo-card { padding: 8px; height: 100%; display: flex; flex-direction: column; }
.card-stat { display: flex; align-items: baseline; gap: 8px; cursor: pointer; padding: 4px 0; }
.stat-num { font-size: 32px; font-weight: 700; color: #909399; }
.stat-num.urgent { color: #e6a23c; }
.stat-label { font-size: 14px; color: #909399; }
.card-list { flex: 1; overflow-y: auto; margin-top: 8px; }
.card-item { display: flex; align-items: center; justify-content: space-between; padding: 8px 4px; border-top: 1px solid #f5f7fa; cursor: pointer; transition: background 0.15s; }
.card-item:hover { background: #fdf6ec; }
.item-info { display: flex; flex-direction: column; gap: 2px; overflow: hidden; }
.item-title { font-size: 13px; color: #303133; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.item-sub { font-size: 11px; color: #c0c4cc; }
.card-empty { flex: 1; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 8px; color: #c0c4cc; font-size: 13px; }
</style>
