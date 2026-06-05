<template>
  <div class="flow-viewer">
    <FlowNodeCard v-if="rootNode" :node="rootNode" :activeNodeId="activeNodeId" :completedNodeIds="completedNodeIds" />
    <el-empty v-else description="暂无流程数据" />
  </div>
</template>

<script setup>
import { computed } from 'vue'
import { FormatDisplayUtils } from '@/workflow/utils/workflow/formatDisplayData'
import FlowNodeCard from './flowNodeCard.vue'

const props = defineProps({
  nodesJson: { type: String, default: '[]' },
  activeNodeId: { type: String, default: '' },
  completedTaskNodeIds: { type: Array, default: () => [] }
})

const completedNodeIds = computed(() => new Set(props.completedTaskNodeIds))

const rootNode = computed(() => {
  try {
    let nodes = props.nodesJson
    if (typeof nodes === 'string') nodes = JSON.parse(nodes)
    if (!Array.isArray(nodes) || nodes.length === 0) return null
    const formatted = nodes.map(n => ({
      ...n,
      nodeProperty: typeof n.nodeProperty === 'string' ? JSON.parse(n.nodeProperty || '{}') : (n.nodeProperty || {})
    }))
    return FormatDisplayUtils.depthConverterToTree(formatted)
  } catch { return null }
})
</script>

<style scoped>
.flow-viewer {
  padding: 24px;
  background: #f5f5f7;
  border-radius: 8px;
  min-height: 300px;
  overflow-x: auto;
}
</style>
