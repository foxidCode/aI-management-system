<template>
  <div class="submit-form-page" v-loading="loading">
    <div class="header-bar">
      <el-button text @click="$router.back()"><el-icon><ArrowLeft /></el-icon> 返回</el-button>
      <span class="title">发起申请 — {{ defName }}</span>
    </div>

    <div v-if="hasFormJson" class="form-area">
      <el-alert
        v-if="submitError"
        :title="submitError"
        type="error"
        show-icon
        :closable="true"
        style="margin-bottom: 16px"
        @close="submitError = ''"
      />
      <FormRender
        ref="formRef"
        :lfFormData="formJsonStr"
        :lfFieldsData="formDataStr"
        :isPreview="false"
        :showSubmit="true"
        @submit="handleFormSubmit"
      />
    </div>

    <el-card v-else-if="!loading" class="fallback-card">
      <el-empty description="该流程无表单配置">
        <el-button type="primary" @click="submitEmpty">直接发起（无表单）</el-button>
      </el-empty>
    </el-card>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { ArrowLeft } from '@element-plus/icons-vue'
import { getDefinition, submitInstance } from '@/workflow/api/workflow.js'
import FormRender from '@/workflow/components/workflow/dynamicForm/formRender.vue'

const route = useRoute()
const router = useRouter()
const loading = ref(false)
const defName = ref('')
const formJsonStr = ref('{}')
const formDataStr = ref('{}')
const submitError = ref('')
const submitting = ref(false)
const formRef = ref(null)

const hasFormJson = computed(() => {
  try {
    const o = JSON.parse(formJsonStr.value)
    return o && (o.widgetList?.length > 0 || o.formConfig)
  } catch { return false }
})

onMounted(async () => {
  loading.value = true
  try {
    const definitionId = Number(route.params.id)
    const res = await getDefinition(definitionId)
    const def = res.data.data || {}
    defName.value = def.name || '未知流程'
    formJsonStr.value = def.frmValue || '{}'
    formDataStr.value = '{}'  // 空表单，用户填写
  } catch (e) {
    ElMessage.error('加载流程定义失败')
  } finally {
    loading.value = false
  }
})

const handleFormSubmit = async (formDataJson) => {
  if (submitting.value) return
  submitting.value = true
  submitError.value = ''
  try {
    const definitionId = Number(route.params.id)
    await submitInstance({ definitionId, formData: formDataJson })
    ElMessage.success('申请已提交')
    router.push('/dashboard/workflow/my-applications')
  } catch (e) {
    submitError.value = e.response?.data?.message || '提交失败，请重试'
  } finally {
    submitting.value = false
  }
}

const submitEmpty = async () => {
  if (submitting.value) return
  submitting.value = true
  try {
    const definitionId = Number(route.params.id)
    await submitInstance({ definitionId, formData: '{}' })
    ElMessage.success('申请已提交')
    router.push('/dashboard/workflow/my-applications')
  } catch (e) {
    ElMessage.error(e.response?.data?.message || '提交失败')
  } finally {
    submitting.value = false
  }
}
</script>

<style scoped>
.submit-form-page { background: #fff; padding: 20px; border-radius: 4px; min-height: 400px; }
.header-bar { display: flex; align-items: center; gap: 16px; margin-bottom: 20px; }
.title { font-size: 18px; font-weight: 600; }
.form-area { max-width: 900px; margin: 0 auto; }
.fallback-card { margin-top: 40px; text-align: center; }
</style>
