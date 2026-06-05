<template>
  <WorkflowShell :definitionId="definitionId" />
</template>

<script setup>
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import WorkflowShell from '@/workflow/components/workflow/index.vue'

const route = useRoute()
const definitionId = computed(() => {
  const id = route.params.id
  return id && id !== 'new' ? Number(id) : null
})
</script>
