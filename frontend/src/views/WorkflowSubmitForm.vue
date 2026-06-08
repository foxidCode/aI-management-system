<template>
  <div class="submit-page" v-loading="loading">
    <!-- 顶部导航条 -->
    <div class="top-bar">
      <el-button text class="back-btn" @click="$router.back()">
        <el-icon :size="18"><ArrowLeft /></el-icon>
        <span>返回</span>
      </el-button>
      <div class="top-title">
        <el-icon :size="20" color="#409EFF"><DocumentAdd /></el-icon>
        <span>发起申请</span>
      </div>
    </div>

    <!-- 紧凑信息条 + 自选审批人 -->
    <div class="top-meta">
      <div class="info-compact">
        <el-icon :size="18"><List /></el-icon>
        <span class="info-name">{{ defName }}</span>
        <span class="info-sep">|</span>
        <span class="info-desc">{{ defRemark || '暂无说明' }}</span>
      </div>

      <!-- 错误提示 -->
      <el-alert
        v-if="submitError"
        :title="submitError"
        type="error"
        show-icon
        :closable="true"
        class="error-alert"
        @close="submitError = ''"
      />

      <!-- 自选审批人（紧凑） -->
      <div v-if="selfSelectNodes.length > 0" class="approver-row">
        <span class="approver-row-label"><el-icon :size="14"><Avatar /></el-icon> 选择审批人：</span>
        <div v-for="node in selfSelectNodes" :key="node.nodeId" class="approver-tag">
          <span class="at-label">{{ node.nodeDisplayName || node.nodeName }}</span>
          <el-select
            v-model="approverSelections[node.nodeId]"
            multiple
            filterable
            collapse-tags
            collapse-tags-tooltip
            placeholder="请选择"
            size="small"
            class="approver-select-inline"
            value-key="id"
          >
            <el-option
              v-for="u in userOptions"
              :key="u.id"
              :label="u.username"
              :value="{ id: u.id, name: u.username }"
            />
          </el-select>
        </div>
      </div>
    </div>

    <!-- 表单区域（占主体） -->
    <div v-if="hasFormJson" class="form-section">
      <div class="form-card">
        <FormRender
          ref="formRef"
          :lfFormData="formJsonStr"
          :lfFieldsData="formDataStr"
          :isPreview="false"
          :showSubmit="false"
        />
      </div>
      <!-- 提交按钮 -->
      <div class="submit-bar">
        <el-button size="large" @click="$router.back()">取 消</el-button>
        <el-button size="large" type="primary" :loading="submitting" @click="triggerSubmit">
          <el-icon :size="18"><Finished /></el-icon>
          提交申请
        </el-button>
      </div>
    </div>

    <!-- 无表单 -->
    <div v-else-if="!loading" class="empty-section">
      <el-empty description="该流程无表单配置">
        <el-button type="primary" size="large" @click="submitEmpty">直接发起</el-button>
      </el-empty>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, reactive, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { ArrowLeft, DocumentAdd, List, Avatar, Finished } from '@element-plus/icons-vue'
import { getDefinition, submitInstance, getUsers } from '@/workflow/api/workflow.js'
import FormRender from '@/workflow/components/workflow/dynamicForm/formRender.vue'

const route = useRoute()
const router = useRouter()
const loading = ref(false)
const defName = ref('')
const defRemark = ref('')
const formJsonStr = ref('{}')
const formDataStr = ref('{}')
const submitError = ref('')
const submitting = ref(false)
const formRef = ref(null)

const selfSelectNodes = ref([])
const approverSelections = reactive({})
const userOptions = ref([])

const hasFormJson = computed(() => {
  try { const o = JSON.parse(formJsonStr.value); return o?.rule?.length > 0 } catch { return false }
})

onMounted(async () => {
  loading.value = true
  try {
    const definitionId = Number(route.params.id)
    const res = await getDefinition(definitionId)
    const def = res.data.data || {}
    defName.value = def.name || '未知流程'
    defRemark.value = def.remark || ''
    formJsonStr.value = def.frmValue || '{}'
    formDataStr.value = '{}'

    try {
      const nodes = typeof def.nodes === 'string' ? JSON.parse(def.nodes) : (def.nodes || [])
      selfSelectNodes.value = nodes.filter(n => {
        if (!n.nodeProperty) return false
        const prop = typeof n.nodeProperty === 'string' ? JSON.parse(n.nodeProperty) : n.nodeProperty
        return prop.assigneeType === 8
      }).map(n => ({
        ...n,
        nodeProperty: typeof n.nodeProperty === 'string' ? JSON.parse(n.nodeProperty) : n.nodeProperty
      }))
      if (selfSelectNodes.value.length > 0) {
        try {
          const usersRes = await getUsers({ page: 1, pageSize: 1000 })
          userOptions.value = usersRes.data.data?.list || usersRes.data.data || []
        } catch { userOptions.value = [] }
      }
    } catch { /* ignore */ }
  } catch {
    ElMessage.error('加载流程定义失败')
  } finally {
    loading.value = false
  }
})

const triggerSubmit = () => {
  if (submitting.value) return

  // 若 FormRender(FormCreate) 无 fApi 则直接提交
  if (!formRef.value?.handleValidate || !formRef.value?.getFromData) {
    handleFormSubmit('{}')
    return
  }

  formRef.value.handleValidate()
    .then(() => formRef.value.getFromData())
    .then((data) => handleFormSubmit(data))
    .catch(() => { /* 表单校验不通过，FormCreate 会自动提示 */ })
}

const handleFormSubmit = async (formDataJson) => {
  if (submitting.value) return
  submitting.value = true
  submitError.value = ''

  for (const node of selfSelectNodes.value) {
    const sel = approverSelections[node.nodeId]
    if (!sel || sel.length === 0) {
      submitError.value = `请为「${node.nodeDisplayName || node.nodeName}」选择至少一位审批人`
      submitting.value = false
      return
    }
  }

  try {
    const definitionId = Number(route.params.id)
    let formData = formDataJson
    if (selfSelectNodes.value.length > 0) {
      const parsed = JSON.parse(formDataJson)
      parsed.__approvers__ = {}
      for (const node of selfSelectNodes.value) {
        parsed.__approvers__[node.nodeId] = approverSelections[node.nodeId] || []
      }
      formData = JSON.stringify(parsed)
    }
    await submitInstance({ definitionId, formData })
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
    let formData = '{}'
    if (selfSelectNodes.value.length > 0) {
      const parsed = {}
      parsed.__approvers__ = {}
      for (const node of selfSelectNodes.value) {
        parsed.__approvers__[node.nodeId] = approverSelections[node.nodeId] || []
      }
      formData = JSON.stringify(parsed)
    }
    await submitInstance({ definitionId, formData })
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
.submit-page {
  min-height: 100vh;
  background: linear-gradient(160deg, #f5f7fa 0%, #e8ecf1 100%);
  padding-bottom: 40px;
}

/* ---- 顶部导航 ---- */
.top-bar {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 24px;
  background: #fff;
  border-bottom: 1px solid #ebeef5;
  position: sticky;
  top: 0;
  z-index: 20;
  box-shadow: 0 1px 4px rgba(0,0,0,.04);
}
.back-btn { color: #606266; font-size: 14px; }
.back-btn:hover { color: #409EFF; }
.top-title {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 16px;
  font-weight: 600;
  color: #303133;
}

/* ---- 紧凑元信息区域（流程名称 + 审批人） ---- */
.top-meta {
  max-width: 1100px;
  margin: 12px auto 0;
  padding: 0 24px;
}
.info-compact {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 16px;
  background: #fff;
  border-radius: 8px;
  font-size: 14px;
  color: #303133;
  box-shadow: 0 1px 6px rgba(0,0,0,.04);
}
.info-compact .el-icon { color: #409EFF; flex-shrink: 0; }
.info-name { font-weight: 600; }
.info-sep { color: #dcdfe6; }
.info-desc { color: #909399; font-size: 13px; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }

.error-alert { margin-top: 8px; border-radius: 8px; }

/* 自选审批人（行内紧凑） */
.approver-row {
  display: flex;
  align-items: center;
  gap: 16px;
  flex-wrap: wrap;
  margin-top: 8px;
  padding: 8px 16px;
  background: #fff;
  border-radius: 8px;
  box-shadow: 0 1px 6px rgba(0,0,0,.04);
  font-size: 13px;
}
.approver-row-label {
  display: flex;
  align-items: center;
  gap: 4px;
  color: #606266;
  font-weight: 500;
  white-space: nowrap;
}
.approver-row-label .el-icon { color: #409EFF; }
.approver-tag {
  display: flex;
  align-items: center;
  gap: 6px;
}
.at-label { color: #303133; white-space: nowrap; }
.approver-select-inline { width: 220px; }

/* ---- 表单区域（主体，占用大部分界面） ---- */
.form-section {
  max-width: 1100px;
  margin: 16px auto 0;
  padding: 0 24px;
}
.form-card {
  background: #fff;
  border-radius: 12px;
  box-shadow: 0 2px 14px rgba(0,0,0,.06);
  padding: 32px 40px;
  min-height: 60vh;
}

/* ---- 提交按钮栏 ---- */
.submit-bar {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
  margin-top: 20px;
}

/* ---- 无表单 ---- */
.empty-section {
  max-width: 780px;
  margin: 60px auto 0;
  background: #fff;
  border-radius: 10px;
  box-shadow: 0 2px 12px rgba(0,0,0,.06);
  padding: 60px 20px;
}

/* ---- Element Plus 选择器微调 ---- */
:deep(.approver-select-inline .el-tag) {
  background: #ecf5ff;
  border-color: #d9ecff;
  color: #409EFF;
}
</style>
