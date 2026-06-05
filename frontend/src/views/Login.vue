<template>
  <div class="login-page">
    <el-card class="login-card" shadow="always">
      <template #header>
        <h2 class="login-title">用户登录</h2>
      </template>

      <el-form
        ref="loginFormRef"
        :model="loginForm"
        :rules="loginRules"
        label-position="top"
        @submit.prevent="handleLogin"
      >
        <el-form-item label="用户名" prop="username">
          <el-input
            v-model="loginForm.username"
            placeholder="请输入用户名1"
            :prefix-icon="User"
            size="large"
          />
        </el-form-item>

        <el-form-item label="密码" prop="password">
          <el-input
            v-model="loginForm.password"
            type="password"
            placeholder="请输入密码"
            :prefix-icon="Lock"
            size="large"
            show-password
            @keyup.enter="handleLogin"
          />
        </el-form-item>

        <el-alert v-if="loginError" :title="loginError" type="error" :closable="false" style="margin-bottom:16px" />

        <el-form-item>
          <el-button type="primary" size="large" :loading="loading" @click="handleLogin" style="width:100%">
            {{ loading ? '登录中...' : '登 录' }}
          </el-button>
        </el-form-item>
      </el-form>

      <el-divider>
        <span style="color: #909399; font-size: 13px;">或使用授权码登录</span>
      </el-divider>

      <div class="authcode-section">
        <el-input
          v-model="authCode"
          placeholder="请输入8位授权码"
          maxlength="8"
          clearable
          style="flex: 1"
          @keyup.enter="handleAuthCodeLogin"
        />
        <el-button
          type="success"
          :loading="authCodeLoading"
          @click="handleAuthCodeLogin"
        >
          授权码登录
        </el-button>
      </div>

      <div class="login-links">
        <router-link to="/register">还没有账号？立即注册</router-link>
        <el-button link type="primary" @click="openForgotDialog">忘记密码</el-button>
      </div>
    </el-card>

    <!-- 忘记密码 -->
    <el-dialog v-model="forgotVisible" title="找回密码" width="420px" :close-on-click-modal="false">
      <div v-if="forgotStep === 1">
        <el-form ref="emailFormRef" :model="forgotForm" :rules="emailRule" label-position="top">
          <el-form-item label="注册邮箱" prop="email">
            <el-input v-model="forgotForm.email" placeholder="请输入注册时使用的邮箱" @keyup.enter="handleSendCode" />
          </el-form-item>
        </el-form>
        <el-alert v-if="forgotError" :title="forgotError" type="error" :closable="false" style="margin-top:12px" />
      </div>

      <div v-else>
        <el-alert
          v-if="forgotMsg"
          :title="forgotMsg"
          :type="forgotMsgType"
          :closable="false"
          style="margin-bottom:16px"
        />
        <el-form ref="resetFormRef" :model="forgotForm" :rules="resetRules" label-position="top">
          <el-form-item label="验证码" prop="code">
            <el-input v-model="forgotForm.code" placeholder="6位数字验证码" maxlength="6" @keyup.enter="handleResetPassword" />
          </el-form-item>
          <el-form-item label="新密码" prop="newPassword">
            <el-input v-model="forgotForm.newPassword" type="password" placeholder="至少6位" show-password @keyup.enter="handleResetPassword" />
          </el-form-item>
        </el-form>
      </div>

      <template #footer>
        <el-button @click="forgotVisible = false">取消</el-button>
        <el-button
          v-if="forgotStep === 1"
          type="primary"
          :loading="sendingCode"
          :disabled="countdown > 0"
          @click="handleSendCode"
        >
          {{ countdown > 0 ? `${countdown}s` : '发送验证码' }}
        </el-button>
        <el-button v-else type="primary" :loading="resetting" @click="handleResetPassword">
          重置密码
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted, onUnmounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { User, Lock } from '@element-plus/icons-vue'
import { login, sendResetCode, forgotPassword } from '../api/auth'

const router = useRouter()
const route = useRoute()

// ========== 登录表单 ==========
const loginForm = reactive({ username: 'admin', password: 'password' })
const loginFormRef = ref(null)
const loginError = ref('')
const loading = ref(false)

const loginRules = {
  username: [{ required: true, message: '请输入用户名', trigger: 'blur' }],
  password: [{ required: true, message: '请输入密码', trigger: 'blur' }],
}

async function handleLogin() {
  loginError.value = ''
  const valid = await loginFormRef.value?.validate().catch(() => false)
  if (!valid) return

  loading.value = true
  try {
    const res = await login(loginForm.username, loginForm.password)
    if (res.data.success) {
      localStorage.setItem('token', res.data.data.token)
      localStorage.setItem('user', JSON.stringify(res.data.data))
      localStorage.setItem('permissions', JSON.stringify(res.data.data.permissions || []))

      // OAuth 流程：检查 return_url 参数
      const returnUrl = route.query.return_url
      if (returnUrl) {
        window.location.href = returnUrl
        return
      }

      router.push('/dashboard')
    }
  } catch (err) {
    loginError.value = err.response?.data?.message || '登录失败，请重试'
  } finally {
    loading.value = false
  }
}

// ========== 忘记密码 ==========
const forgotVisible = ref(false)
const forgotStep = ref(1)
const forgotMsg = ref('')
const forgotMsgType = ref('success')
const forgotError = ref('')
const sendingCode = ref(false)
const resetting = ref(false)
const countdown = ref(0)
let timer = null
const emailFormRef = ref(null)
const resetFormRef = ref(null)

const forgotForm = reactive({ email: '', code: '', newPassword: '' })

const emailRule = {
  email: [
    { required: true, message: '请输入邮箱', trigger: 'blur' },
    { type: 'email', message: '邮箱格式不正确', trigger: 'blur' },
  ],
}

const resetRules = {
  code: [{ required: true, message: '请输入验证码', trigger: 'blur' }],
  newPassword: [
    { required: true, message: '请输入新密码', trigger: 'blur' },
    { min: 6, message: '密码至少6个字符', trigger: 'blur' },
  ],
}

function openForgotDialog() {
  forgotForm.email = ''
  forgotForm.code = ''
  forgotForm.newPassword = ''
  forgotMsg.value = ''
  forgotError.value = ''
  forgotStep.value = 1
  countdown.value = 0
  if (timer) clearInterval(timer)
  forgotVisible.value = true
}

async function handleSendCode() {
  const valid = await emailFormRef.value?.validate().catch(() => false)
  if (!valid) return

  sendingCode.value = true
  forgotError.value = ''
  try {
    const res = await sendResetCode(forgotForm.email)
    forgotStep.value = 2
    const devCode = res.data?.data?.devCode
    forgotMsg.value = devCode
      ? `验证码已发送（开发模式，验证码：${devCode}）`
      : '验证码已发送，请查收邮件'
    countdown.value = 60
    timer = setInterval(() => { if (--countdown.value <= 0) clearInterval(timer) }, 1000)
  } catch (err) {
    forgotError.value = err.response?.data?.message || '发送失败'
  } finally {
    sendingCode.value = false
  }
}

async function handleResetPassword() {
  const valid = await resetFormRef.value?.validate().catch(() => false)
  if (!valid) return

  resetting.value = true
  forgotMsg.value = ''
  try {
    const res = await forgotPassword(forgotForm.email, forgotForm.code, forgotForm.newPassword)
    forgotMsg.value = res.data.message
    forgotMsgType.value = 'success'
    setTimeout(() => { forgotVisible.value = false }, 1500)
  } catch (err) {
    forgotMsg.value = err.response?.data?.message || '重置失败'
    forgotMsgType.value = 'error'
  } finally {
    resetting.value = false
  }
}

// ========== 授权码登录 ==========
const authCode = ref('')
const authCodeLoading = ref(false)

async function handleAuthCodeLogin() {
  const code = authCode.value.trim().toUpperCase()
  if (!code || code.length !== 8) {
    loginError.value = '请输入8位授权码'
    return
  }
  authCodeLoading.value = true
  loginError.value = ''
  try {
    const { loginByAuthCode } = await import('../api/auth')
    const res = await loginByAuthCode(code)
    if (res.data.success) {
      localStorage.setItem('token', res.data.data.token)
      localStorage.setItem('user', JSON.stringify(res.data.data))
      localStorage.setItem('permissions', JSON.stringify(res.data.data.permissions || []))
      router.push('/dashboard')
    }
  } catch (err) {
    loginError.value = err.response?.data?.message || '授权码无效或已过期'
  } finally {
    authCodeLoading.value = false
  }
}

// SSO 错误提示
onMounted(() => {
  if (route.query.sso_error) {
    loginError.value = route.query.sso_error
    router.replace({ path: '/login', query: {} })
  }
})

onUnmounted(() => { if (timer) clearInterval(timer) })
</script>

<style scoped>
.login-page {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 100vh;
  padding: 20px;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
}

.login-card {
  width: 420px;
  border-radius: 12px;
}

.login-title {
  margin: 0;
  text-align: center;
  color: #303133;
  font-size: 22px;
}

.authcode-section {
  display: flex;
  gap: 10px;
  margin-bottom: 16px;
}

.login-links {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 14px;
}

.login-links a {
  color: #909399;
  text-decoration: none;
}

.login-links a:hover {
  color: #409eff;
}
</style>
