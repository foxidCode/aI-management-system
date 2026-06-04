<template>
  <div class="callback-page">
    <div class="callback-container">
      <template v-if="error">
        <el-result icon="error" title="登录失败" :sub-title="error">
          <template #extra>
            <el-button type="primary" @click="goLogin">返回登录页</el-button>
          </template>
        </el-result>
      </template>
      <template v-else>
        <el-result icon="success" title="登录成功" sub-title="正在跳转...">
          <template #extra>
            <el-icon class="loading-icon" :size="32"><Loading /></el-icon>
          </template>
        </el-result>
      </template>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { Loading } from '@element-plus/icons-vue'
import { handleOAuthCallback } from '../composables/useOAuth'

const router = useRouter()
const error = ref('')

async function processCallback() {
  try {
    await handleOAuthCallback()
    // 跳转到 dashboard
    router.replace('/dashboard')
  } catch (err) {
    error.value = err.message || 'OAuth 回调处理失败'
    console.error('OAuth callback error:', err)
  }
}

function goLogin() {
  router.replace('/login')
}

onMounted(() => {
  processCallback()
})
</script>

<style scoped>
.callback-page {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 100vh;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
}
.callback-container {
  background: white;
  border-radius: 16px;
  padding: 40px;
  min-width: 400px;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.15);
}
.loading-icon {
  animation: spin 1s linear infinite;
}
@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}
</style>
