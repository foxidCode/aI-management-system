<template>
  <div class="stats-card">
    <div class="stats-header">
      <span class="stats-title">🟢 系统在线</span>
      <el-button :icon="Refresh" circle size="small" text @click="fetchData" :loading="loading" />
    </div>

    <div v-if="loading" class="stats-body" v-loading="loading" />

    <div v-else class="stats-body">
      <div class="online-count">
        <span class="count-num">{{ onlineUsers.length }}</span>
        <span class="count-label">人在线</span>
      </div>

      <div class="user-list" v-if="onlineUsers.length > 0">
        <div class="user-tag" v-for="u in onlineUsers" :key="u.id">
          <el-icon :size="14"><UserFilled /></el-icon>
          <span class="user-name">{{ u.username }}</span>
          <!-- <span class="user-email">{{ u.email }}</span> -->
        </div>
      </div>

      <div v-else class="no-users">
        <span>暂无在线用户</span>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { Refresh, UserFilled } from '@element-plus/icons-vue'

const loading = ref(true)
const onlineUsers = ref([])

async function fetchData() {
  loading.value = true
  try {
    const { api } = await import('../api/auth')
    const res = await api.get('/user/online')
    if (res.data?.success) {
      onlineUsers.value = res.data.data || []
    }
  } catch {
    onlineUsers.value = []
  } finally {
    loading.value = false
  }
}

onMounted(fetchData)
</script>

<style scoped>
.stats-card {
  padding: 14px 16px;
  background: linear-gradient(135deg, #f0fdf4 0%, #ecfdf5 100%);
  border-radius: 8px;
  height: 100%;
  display: flex;
  flex-direction: column;
  box-sizing: border-box;
}
.stats-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 8px;
  flex-shrink: 0;
}
.stats-title {
  font-size: 14px;
  font-weight: 600;
  color: #065f46;
}
.stats-body {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-height: 0;
}
.online-count {
  display: flex;
  align-items: baseline;
  gap: 6px;
  margin-bottom: 12px;
}
.count-num {
  font-size: 36px;
  font-weight: 700;
  color: #059669;
}
.count-label {
  font-size: 14px;
  color: #6b7280;
}
.user-list {
  flex: 1;
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  align-content: flex-start;
  overflow-y: auto;
}
.user-tag {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  background: #d1fae5;
  color: #065f46;
  padding: 3px 10px;
  border-radius: 12px;
  font-size: 12px;
  white-space: nowrap;
}
.user-name {
  font-weight: 600;
}
.user-email {
  color: #6b7280;
  font-size: 11px;
}
.no-users {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #9ca3af;
  font-size: 13px;
}
</style>
