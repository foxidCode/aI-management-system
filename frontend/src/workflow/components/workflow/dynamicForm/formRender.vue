<template>
  <div class="form-render-container">
    <form-create
      v-model:api="fApi"
      v-model="formData"
      :rule="rule"
      :option="renderOption"
      :disabled="isPreview"
      @submit="onSubmit"
    />
    <div class="form-footer" v-if="!isPreview && props.showSubmit">
      <el-button type="primary" @click="handleSubmit">提交</el-button>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted, watch, computed } from 'vue'
import { ElMessage } from 'element-plus'

const emit = defineEmits(['submit'])

let props = defineProps({
  lfFormData: {// 表单设计 JSON 字符串 { rule: [...], option: {...} }
    type: String,
    default: '{}',
  },
  lfFieldsData: {// 表单字段值 JSON 字符串
    type: String,
    default: '{}',
  },
  showSubmit: {
    type: Boolean,
    default: false,
  },
  isPreview: {// true=只读
    type: Boolean,
    default: true,
  }
});

const fApi = ref(null)
const formData = reactive({})
const rule = ref([])
const renderOption = reactive({
  form: { labelWidth: '100px', labelPosition: 'left' },
  submitBtn: false,
  resetBtn: false,
})

function parseRule() {
  try {
    const parsed = JSON.parse(props.lfFormData || '{}')
    rule.value = parsed.rule || []
    if (parsed.option) {
      Object.assign(renderOption, parsed.option)
      renderOption.submitBtn = false  // 始终由自定义按钮控制
      renderOption.resetBtn = false
    }
  } catch {
    rule.value = []
  }
}

// 加载已有字段值
function loadFormData() {
  try {
    const data = JSON.parse(props.lfFieldsData || '{}')
    // 不覆盖 __approvers__ 等元数据
    Object.keys(data).forEach(k => {
      if (!k.startsWith('__')) formData[k] = data[k]
    })
  } catch { /* ignore */ }
}

onMounted(() => {
  parseRule()
  loadFormData()
})

// 监听 lfFormData 变化
watch(() => props.lfFormData, () => {
  parseRule()
})

const handleSubmit = () => {
  if (!fApi.value) return
  fApi.value.validate((valid) => {
    if (valid) {
      onSubmit(formData)
    }
  })
}

function onSubmit(data) {
  emit('submit', JSON.stringify(data))
}

defineExpose({
  handleValidate: () => {
    return new Promise((resolve, reject) => {
      if (!fApi.value) { resolve(true); return }
      fApi.value.validate((valid) => { valid ? resolve(true) : reject(false) })
    })
  },
  getFromData: () => {
    return Promise.resolve(JSON.stringify(formData))
  }
})
</script>

<style scoped>
.form-render-container {
  display: flex;
  flex-direction: column;
}
.form-footer {
  display: flex;
  justify-content: flex-end;
  padding: 16px 24px;
  background: #fff;
  border-top: 2px solid #f0f0f0;
  box-shadow: 0 1px 4px rgba(0, 21, 41, 0.08);
}
</style>
