<template>
  <div class="kv-editor">
    <div v-for="(item, idx) in items" :key="idx" class="kv-row">
      <el-input v-model="item.key" size="small" placeholder="参数名" style="width:200px" @input="onChange" />
      <el-input v-model="item.value" size="small" placeholder="参数值" style="flex:1" @input="onChange" />
      <el-button size="small" type="danger" text @click="remove(idx)"><el-icon><Delete /></el-icon></el-button>
    </div>
    <el-button size="small" style="margin-top:8px" @click="add"><el-icon><Plus /></el-icon> {{ addLabel }}</el-button>
  </div>
</template>

<script setup>
import { ref, watch } from 'vue'

const props = defineProps({
  modelValue: { type: [Array, Object], default: () => [] },
  addLabel: { type: String, default: '添加' },
})
const emit = defineEmits(['update:modelValue'])

const items = ref([])
let ignoreWatch = false

watch(() => props.modelValue, (v) => {
  if (ignoreWatch) return
  items.value = toArray(v)
}, { immediate: true, deep: false })

function toArray(v) {
  if (!v) return []
  if (Array.isArray(v)) return v.map(i => ({ key: i.key || '', value: i.value || '' }))
  if (typeof v === 'object') return Object.entries(v).map(([k, val]) => ({ key: k, value: val ?? '' }))
  return []
}

function onChange() {
  ignoreWatch = true
  emit('update:modelValue', items.value.map(i => ({ key: i.key, value: i.value })))
  setTimeout(() => { ignoreWatch = false }, 0)
}

function add() { items.value.push({ key: '', value: '' }); onChange() }
function remove(idx) { items.value.splice(idx, 1); onChange() }
</script>

<style scoped>
.kv-row { display: flex; gap: 8px; align-items: center; margin-bottom: 6px; }
</style>
