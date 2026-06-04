<template>
  <div class="auth-container">
    <div class="auth-card">
      <h2>用户注册</h2>
      <form @submit.prevent="handleRegister">
        <div class="form-group">
          <label>用户名</label>
          <input
            v-model="form.username"
            type="text"
            name="username"
            autocomplete="username"
            placeholder="3-50个字符"
          />
          <p v-if="fieldErrors.username" class="field-error">{{ fieldErrors.username }}</p>
        </div>
        <div class="form-group">
          <label>邮箱</label>
          <input
            v-model="form.email"
            type="email"
            name="email"
            autocomplete="email"
            placeholder="请输入邮箱"
          />
          <p v-if="fieldErrors.email" class="field-error">{{ fieldErrors.email }}</p>
        </div>
        <div class="form-group">
          <label>密码</label>
          <input
            v-model="form.password"
            type="password"
            name="new-password"
            autocomplete="new-password"
            placeholder="至少6个字符"
          />
          <p v-if="fieldErrors.password" class="field-error">{{ fieldErrors.password }}</p>
        </div>
        <p v-if="error" class="error-msg">{{ error }}</p>
        <p v-if="success" class="success-msg">{{ success }}</p>
        <button type="submit" :disabled="loading">
          {{ loading ? '注册中...' : '注册' }}
        </button>
      </form>
      <p class="switch-link">
        已有账号？<router-link to="/login">立即登录</router-link>
      </p>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive } from 'vue'
import { register } from '../api/auth'

const form = reactive({ username: '', password: '', email: '' })
const fieldErrors = reactive({ username: '', email: '', password: '' })
const error = ref('')
const success = ref('')
const loading = ref(false)

function validate() {
  // 清除之前的字段错误
  Object.keys(fieldErrors).forEach(k => fieldErrors[k] = '')

  let valid = true
  if (!form.username || form.username.trim().length < 3) {
    fieldErrors.username = '用户名至少3个字符'
    valid = false
  }
  if (form.username && form.username.trim().length > 50) {
    fieldErrors.username = '用户名最多50个字符'
    valid = false
  }
  if (!form.email || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email)) {
    fieldErrors.email = '请输入有效的邮箱地址'
    valid = false
  }
  if (!form.password || form.password.length < 6) {
    fieldErrors.password = '密码至少6个字符'
    valid = false
  }
  return valid
}

async function handleRegister() {
  error.value = ''
  success.value = ''
  Object.keys(fieldErrors).forEach(k => fieldErrors[k] = '')

  if (!validate()) return

  loading.value = true
  try {
    const res = await register(form.username, form.password, form.email)
    if (res.data.success) {
      success.value = '注册成功！请前往登录。'
      form.username = ''
      form.password = ''
      form.email = ''
    }
  } catch (err) {
    const msg = err.response?.data?.message || ''
    if (msg) {
      // 尝试解析后端返回的字段级别错误
      if (msg.includes('用户名')) fieldErrors.username = msg
      else if (msg.includes('邮箱')) fieldErrors.email = msg
      else if (msg.includes('密码')) fieldErrors.password = msg
      else error.value = msg
    } else {
      error.value = '注册失败，请重试'
    }
  } finally {
    loading.value = false
  }
}
</script>
