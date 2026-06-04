<template>
  <div class="profile-page">
    <el-card class="profile-card">
      <template #header>
        <span class="card-title">修改用户信息</span>
      </template>

      <el-form
        ref="formRef"
        :model="form"
        :rules="rules"
        label-width="80px"
        style="max-width: 500px"
      >
        <el-form-item label="用户名">
          <el-input v-model="form.username" disabled />
        </el-form-item>

        <el-form-item label="邮箱" prop="email">
          <el-input v-model="form.email" placeholder="请输入新邮箱" />
        </el-form-item>

        <el-form-item label="新密码" prop="newPassword">
          <el-input
            v-model="form.newPassword"
            type="password"
            placeholder="留空则不修改密码"
            show-password
          />
        </el-form-item>

        <el-form-item label="当前密码" prop="currentPassword">
          <el-input
            v-model="form.currentPassword"
            type="password"
            placeholder="请输入当前密码以确认修改"
            show-password
          />
        </el-form-item>
      </el-form>

      <div style="padding-left: 80px">
        <el-button type="primary" :loading="loading" @click="handleSubmit">
          {{ loading ? '保存中...' : '保存修改' }}
        </el-button>
        <el-button @click="handleReset">重置</el-button>
      </div>
    </el-card>

    <el-card class="info-card" style="margin-top: 20px">
      <template #header>
        <span class="card-title">账号信息</span>
      </template>
      <el-descriptions :column="1" border>
        <el-descriptions-item label="用户名">{{ profile.username }}</el-descriptions-item>
        <el-descriptions-item label="邮箱">{{ profile.email }}</el-descriptions-item>
        <el-descriptions-item label="注册时间">{{ profile.createdAt }}</el-descriptions-item>
      </el-descriptions>
    </el-card>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { getUserProfile, updateUserProfile } from '../api/auth'

const formRef = ref(null)
const loading = ref(false)
const profile = reactive({ username: '', email: '', createdAt: '' })

const form = reactive({
  username: '',
  email: '',
  newPassword: '',
  currentPassword: '',
})

const rules = {
  email: [
    { required: true, message: '请输入邮箱', trigger: 'blur' },
    { type: 'email', message: '邮箱格式不正确', trigger: 'blur' },
  ],
  currentPassword: [
    { required: true, message: '请输入当前密码以确认修改', trigger: 'blur' },
  ],
  newPassword: [
    { required: true, min: 6, message: '新密码至少6个字符', trigger: 'blur' },
  ],
}

onMounted(async () => {
  try {
    const res = await getUserProfile()
    if (res.data.success) {
      const data = res.data.data
      profile.username = data.username
      profile.email = data.email
      profile.createdAt = data.createdAt
      form.username = data.username
      form.email = data.email
      // 同步更新 localStorage
      localStorage.setItem('user', JSON.stringify(data))
    }
  } catch (err) {
    ElMessage.error('获取用户信息失败')
  }
})

function handleReset() {
  form.email = profile.email
  form.newPassword = ''
  form.currentPassword = ''
  formRef.value?.clearValidate()
}

async function handleSubmit() {
  const valid = await formRef.value.validate().catch(() => false)
  if (!valid) return

  loading.value = true
  try {
    const res = await updateUserProfile({
      email: form.email,
      newPassword: form.newPassword || null,
      currentPassword: form.currentPassword,
    })
    if (res.data.success) {
      ElMessage.success('修改成功')
      profile.email = form.email
      form.currentPassword = ''
      form.newPassword = ''
      const user = JSON.parse(localStorage.getItem('user') || '{}')
      user.email = form.email
      localStorage.setItem('user', JSON.stringify(user))
    }
  } catch (err) {
    ElMessage.error(err.response?.data?.message || '修改失败')
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
.profile-page {
  max-width: 800px;
}

.card-title {
  font-size: 16px;
  font-weight: 600;
}
</style>
