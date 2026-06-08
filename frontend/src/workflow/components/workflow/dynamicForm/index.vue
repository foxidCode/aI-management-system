<template>
  <div class="main-container">
    <div class="designer-container">
      <fc-designer ref="designerRef" />
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { useWorkflowStore } from '@/workflow/store/modules/workflow'

let store = useWorkflowStore()
let props = defineProps({
  lfFormData: {
    type: String,
    default: null,
  }
});

const designerRef = ref(null)

function getRule() {
  return designerRef.value?.getRule() || []
}

function getOption() {
  return designerRef.value?.getOption() || {}
}

onMounted(() => {
  if (props.lfFormData) {
    try {
      const parsed = JSON.parse(props.lfFormData)
      if (parsed.rule && parsed.rule.length > 0) {
        designerRef.value?.setRule(parsed.rule)
      }
      if (parsed.option) {
        designerRef.value?.setOption(parsed.option)
      }
      // 同步字段到 store
      syncToStore()
    } catch {
      console.warn('表单数据解析失败，使用空白设计器')
    }
  }
})

function syncToStore() {
  try {
    const rule = getRule()
    const fields = rule.map(r => ({
      fieldId: r.field,
      name: r.field,
      title: r.title || r.field,
      type: r.type
    }))
    store.setLowCodeFormField({ formFields: fields })
  } catch { /* 忽略 */ }
}

const getData = () => {
  return new Promise((resolve, reject) => {
    try {
      // 先同步字段到 store
      syncToStore()
      const data = {
        rule: getRule(),
        option: getOption()
      }
      resolve({ formData: data })
    } catch (err) {
      reject(new Error('获取表单数据失败'))
    }
  })
}

const getFieldList = () => {
  const rule = getRule()
  const fields = rule.map(r => ({
    fieldId: r.field,
    name: r.field,
    title: r.title || r.field,
    type: r.type
  }))
  return Promise.resolve({ formData: fields })
}

defineExpose({ getData, getFieldList })
</script>

<style scoped>
.main-container {
  margin-left: 0 !important;
  height: calc(100vh - 65px);
  display: flex;
  flex-direction: column;
}

.designer-container {
  flex: 1;
  background: #fff;
  overflow: hidden;
}

.designer-container :deep(.fc-designer) {
  height: 100%;
}
</style>
