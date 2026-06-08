<template>
  <div class="home-page">
    <div class="grid-container" :style="gridStyle">
      <div
        v-for="(card, idx) in cards"
        :key="idx"
        class="grid-card"
        :style="getCardStyle(card)"
      >
        <div class="card-header">
          <span>{{ card.title }}</span>
        </div>
        <div class="card-body">
          <component :is="card.component" />
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, markRaw, onMounted } from 'vue'
import WeatherCard from '../components/WeatherCard.vue'
import InboundChartCard from '../components/InboundChartCard.vue'
import StatsCard from '../components/StatsCard.vue'
import ClockCard from '../components/ClockCard.vue'
import TodoCard from '../components/TodoCard.vue'
import DoneCard from '../components/DoneCard.vue'
import { getHomeConfig } from '../api/auth'

// ========== 卡片注册表 ==========
const CARD_REGISTRY = {
  weather: { title: '天气预报', component: WeatherCard },
  inboundChart: { title: '入库统计', component: InboundChartCard },
  stats: { title: '系统统计', component: StatsCard },
  clock: { title: '时钟', component: ClockCard },
  todo: { title: '我的待办', component: TodoCard },
  done: { title: '我的已办', component: DoneCard },
}

function loadComponent(type) {
  const def = CARD_REGISTRY[type]
  if (def) return markRaw(def.component)
  return null
}

// ========== 加载配置 ==========
const gridCols = ref(3)
const gridRows = ref(2)
const cards = ref([])

function getUserKey() {
  const user = JSON.parse(localStorage.getItem('user') || '{}')
  return `home_card_config_${user.username || 'default'}`
}

async function loadConfig() {
  // 优先从后端加载
  try {
    const res = await getHomeConfig()
    if (res.data.success && res.data.data) {
      const parsed = JSON.parse(res.data.data)
      applyConfig(parsed)
      return
    }
  } catch { /* 后端不可用时降级到 localStorage */ }

  // 降级：从 localStorage 加载
  const saved = localStorage.getItem(getUserKey())
  if (saved) {
    try {
      const parsed = JSON.parse(saved)
      applyConfig(parsed)
      return
    } catch { /* ignore */ }
  }

  // 新用户默认：2×2，仅天气预报
  gridCols.value = 2
  gridRows.value = 2
  cards.value = [
    { type: 'weather', title: '天气预报', component: loadComponent('weather'), col: 1, row: 1, colSpan: 1, rowSpan: 1, enabled: true },
  ]
}

function applyConfig(parsed) {
  gridCols.value = parsed.gridCols || 3
  gridRows.value = parsed.gridRows || 2
  const enabled = (parsed.cards || []).filter(c => c.enabled)
  if (enabled.length > 0) {
    cards.value = enabled.map(c => ({
      ...c,
      title: c.name,
      component: loadComponent(c.type),
    }))
  }
}

const gridStyle = computed(() => ({
  gridTemplateColumns: `repeat(${gridCols.value}, 1fr)`,
  gridTemplateRows: `repeat(${gridRows.value}, minmax(200px, auto))`,
}))

function getCardStyle(card) {
  return {
    gridColumn: `${card.col} / span ${card.colSpan}`,
    gridRow: `${card.row} / span ${card.rowSpan}`,
  }
}

onMounted(loadConfig)
</script>

<style scoped>
.home-page {
  height: 100%;
  display: flex;
  flex-direction: column;
}
.grid-container {
  flex: 1;
  display: grid;
  gap: 6px;
  padding: 8px;
  background-color: #f5f7fa;
  background-image: radial-gradient(circle, #dcdfe6 1px, transparent 1px);
  background-size: 10px 10px;
  border-radius: 8px;
  border: 2px solid #dcdfe6;
  overflow-y: auto;
  align-content: start;
}
.grid-card {
  background: #fff;
  border-radius: 8px;
  overflow: hidden;
  border: 1px solid #e4e7ed;
  transition: box-shadow 0.2s;
  height: 100%;
  min-height: 180px;
}
.grid-card:hover {
  box-shadow: 0 4px 16px rgba(0,0,0,0.08);
}
.card-header {
  display: flex;
  align-items: center;
  padding: 8px 12px;
  font-size: 13px;
  font-weight: 600;
  color: #606266;
  border-bottom: 1px solid #ebeef5;
}
.card-body {
  padding: 0;
  height: calc(100% - 33px);
}
</style>
