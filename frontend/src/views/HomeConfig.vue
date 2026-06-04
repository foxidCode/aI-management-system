<template>
  <div class="home-config">
    <el-card>
      <template #header>
        <span><el-icon><Menu /></el-icon> 主页卡片配置</span>
        <el-button style="float: right" size="small" @click="resetDefaults">恢复默认</el-button>
      </template>

      <el-alert
        title="拖拽卡片到目标位置即可调整布局，修改后点击「保存配置」生效。"
        type="info"
        :closable="false"
        show-icon
        style="margin-bottom: 20px"
      />

      <!-- 网格控制 -->
      <div class="layout-controls">
        <span>网格：</span>
        <el-input-number v-model="gridCols" :min="1" :max="10" size="small" style="width:80px" />
        <span>×</span>
        <el-input-number v-model="gridRows" :min="1" :max="12" size="small" style="width:80px" />
        <el-divider direction="vertical" />
        <el-button type="primary" @click="saveConfig" :loading="saving">
          <el-icon><Check /></el-icon> 保存配置
        </el-button>
      </div>

      <el-divider />

      <!-- 卡片预览网格 -->
      <div class="grid-preview" :style="gridStyle" @dragover.prevent @drop="handleDrop">
        <div
          v-for="(card, idx) in enabledCards"
          :key="card.type"
          class="grid-card"
          :class="{ dragging: dragIdx === idx, disabled: !card.enabled }"
          :style="getCardStyle(card)"
          draggable="true"
          @dragstart="handleDragStart($event, idx)"
          @dragend="dragIdx = null"
        >
          <div class="card-preview-header">
            <span>{{ card.name }}</span>
            <div class="card-actions">
              <el-button
                :icon="card.enabled ? 'Close' : 'Check'"
                :type="card.enabled ? 'danger' : 'success'"
                size="small"
                circle
                @click="card.enabled = !card.enabled"
                :title="card.enabled ? '禁用' : '启用'"
              />
            </div>
          </div>
          <div class="card-preview-body">
            <el-icon :size="24"><PictureFilled /></el-icon>
            <span>{{ card.description }}</span>
          </div>
          <div class="card-span-controls">
            <div class="span-row">
              <span class="span-label">列</span>
              <el-button size="small" :disabled="card.colSpan <= 1" @click="card.colSpan--">−</el-button>
              <span class="span-val">{{ card.colSpan }}</span>
              <el-button size="small" :disabled="card.col + card.colSpan > gridCols" @click="card.colSpan++">+</el-button>
            </div>
            <div class="span-row">
              <span class="span-label">行</span>
              <el-button size="small" :disabled="card.rowSpan <= 1" @click="card.rowSpan--">−</el-button>
              <span class="span-val">{{ card.rowSpan }}</span>
              <el-button size="small" :disabled="card.row + card.rowSpan > gridRows" @click="card.rowSpan++">+</el-button>
            </div>
          </div>
        </div>
      </div>

      <!-- 已禁用的卡片列表 -->
      <div v-if="disabledCards.length > 0" class="disabled-section">
        <div class="disabled-title">已禁用的卡片</div>
        <div class="disabled-list">
          <div
            v-for="card in disabledCards"
            :key="card.type"
            class="disabled-card"
          >
            <span class="disabled-name">{{ card.name }}</span>
            <span class="disabled-desc">{{ card.description }}</span>
            <el-button size="small" type="success" @click="card.enabled = true">
              <el-icon><Check /></el-icon> 启用
            </el-button>
          </div>
        </div>
      </div>
    </el-card>
  </div>
</template>

<script setup>
import { ref, reactive, computed, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { Menu, Check, PictureFilled } from '@element-plus/icons-vue'
import { getHomeConfig, saveHomeConfig } from '../api/auth'

// ========== 默认卡片配置 ==========
const DEFAULT_CARDS = [
  {
    type: 'weather',
    name: '天气预报',
    description: '显示当前位置天气信息',
    enabled: true,
    col: 1, row: 1, colSpan: 1, rowSpan: 1,
  },
  {
    type: 'inboundChart',
    name: '入库统计（柱状图）',
    description: '近7天每日入库金额柱状图',
    enabled: false,
    col: 2, row: 1, colSpan: 2, rowSpan: 1,
  },
  {
    type: 'stats',
    name: '系统统计',
    description: '当前在线用户列表',
    enabled: false,
    col: 1, row: 2, colSpan: 1, rowSpan: 1,
  },
  {
    type: 'clock',
    name: '时钟',
    description: '实时时钟，显示时间日期',
    enabled: false,
    col: 2, row: 2, colSpan: 1, rowSpan: 1,
  },
]

// ========== 状态 ==========
const saving = ref(false)
const gridCols = ref(3)
const gridRows = ref(2)
const dragIdx = ref(null)
const cards = ref([])

const enabledCards = computed(() =>
  cards.value.filter(c => c.enabled).sort((a, b) => {
    if (a.row !== b.row) return a.row - b.row
    return a.col - b.col
  })
)
const disabledCards = computed(() => cards.value.filter(c => !c.enabled))

const gridStyle = computed(() => ({
  gridTemplateColumns: `repeat(${gridCols.value}, 1fr)`,
  gridTemplateRows: `repeat(${gridRows.value}, minmax(160px, auto))`,
}))

// ========== 方法 ==========
function getCardStyle(card) {
  return {
    gridColumn: `${card.col} / span ${card.colSpan}`,
    gridRow: `${card.row} / span ${card.rowSpan}`,
  }
}

function getUserKey() {
  const user = JSON.parse(localStorage.getItem('user') || '{}')
  return `home_card_config_${user.username || 'default'}`
}

async function loadConfig() {
  const defaults = JSON.parse(JSON.stringify(DEFAULT_CARDS))

  // 优先从后端加载
  try {
    const res = await getHomeConfig()
    if (res.data.success && res.data.data) {
      const parsed = JSON.parse(res.data.data)
      const savedCards = parsed.cards || []
      const merged = defaults.map(d => {
        const existing = savedCards.find(s => s.type === d.type)
        return existing ? { ...d, ...existing, name: d.name, description: d.description } : d
      })
      savedCards.forEach(s => {
        if (!merged.find(m => m.type === s.type)) merged.push(s)
      })
      cards.value = merged
      gridCols.value = parsed.gridCols || 3
      gridRows.value = parsed.gridRows || 2
      return
    }
  } catch { /* 后端不可用时降级到 localStorage */ }

  // 降级：从 localStorage 加载
  const saved = localStorage.getItem(getUserKey())
  if (saved) {
    try {
      const parsed = JSON.parse(saved)
      const savedCards = parsed.cards || []
      const merged = defaults.map(d => {
        const existing = savedCards.find(s => s.type === d.type)
        return existing ? { ...d, ...existing, name: d.name, description: d.description } : d
      })
      savedCards.forEach(s => {
        if (!merged.find(m => m.type === s.type)) merged.push(s)
      })
      cards.value = merged
      gridCols.value = parsed.gridCols || 3
      gridRows.value = parsed.gridRows || 2
      return
    } catch { /* ignore */ }
  }

  cards.value = defaults
  gridCols.value = 3
  gridRows.value = 2
}

async function saveConfig() {
  saving.value = true
  const config = JSON.stringify({
    cards: cards.value,
    gridCols: gridCols.value,
    gridRows: gridRows.value,
  })

  try {
    await saveHomeConfig(config)
    // 同步保存到 localStorage 作为离线备份
    localStorage.setItem(getUserKey(), config)
    saving.value = false
    ElMessage.success('配置已保存，刷新主页即可生效（已同步到服务器）')
  } catch {
    // 降级：仅保存到 localStorage
    localStorage.setItem(getUserKey(), config)
    saving.value = false
    ElMessage.warning('服务器保存失败，已保存到本地。换个浏览器可能看不到此配置。')
  }
}

function resetDefaults() {
  cards.value = JSON.parse(JSON.stringify(DEFAULT_CARDS))
  gridCols.value = 3
  gridRows.value = 2
  localStorage.removeItem(getUserKey())
  ElMessage.success('已恢复默认配置，点击「保存配置」生效')
}

// 拖拽
function handleDragStart(e, idx) {
  dragIdx.value = idx
  e.dataTransfer.effectAllowed = 'move'
}

function handleDrop(e) {
  if (dragIdx.value === null) return
  const grid = e.currentTarget
  const rect = grid.getBoundingClientRect()
  const colWidth = rect.width / gridCols.value
  const rowHeight = rect.height / gridRows.value

  const col = Math.floor((e.clientX - rect.left) / colWidth) + 1
  const row = Math.floor((e.clientY - rect.top) / rowHeight) + 1

  if (col >= 1 && col <= gridCols.value && row >= 1 && row <= gridRows.value) {
    const movedCard = enabledCards.value[dragIdx.value]
    const targetCol = Math.min(col, gridCols.value - movedCard.colSpan + 1)
    const targetRow = Math.min(row, gridRows.value - movedCard.rowSpan + 1)

    // 检查目标位置是否已被其他卡片占用
    const occupied = enabledCards.value.some((c, i) =>
      i !== dragIdx.value &&
      c.col < targetCol + movedCard.colSpan &&
      c.col + c.colSpan > targetCol &&
      c.row < targetRow + movedCard.rowSpan &&
      c.row + c.rowSpan > targetRow
    )

    if (!occupied) {
      movedCard.col = targetCol
      movedCard.row = targetRow
    }
  }
  dragIdx.value = null
}


onMounted(loadConfig)
</script>

<style scoped>
.home-config {
  padding: 4px;
  height: 100%;
  display: flex;
  flex-direction: column;
}
.home-config > .el-card {
  flex: 1;
  display: flex;
  flex-direction: column;
}
.home-config > .el-card > .el-card__body {
  flex: 1;
  display: flex;
  flex-direction: column;
}

.layout-controls {
  display: flex;
  align-items: center;
  gap: 12px;
  font-size: 14px;
  color: #606266;
}

.grid-preview {
  display: grid;
  gap: 6px;
  padding: 16px;
  background-color: #f5f7fa;
  background-image: radial-gradient(circle, #c0c4cc 1px, transparent 1px);
  background-size: 10px 10px;
  border-radius: 8px;
  border: 2px solid #dcdfe6;
  min-height: 60vh;
  flex: 1;
  overflow-y: auto;
  align-content: start;
}

.grid-card {
  background: #fff;
  border-radius: 8px;
  overflow: hidden;
  cursor: grab;
  border: 2px solid #e4e7ed;
  position: relative;
  z-index: 1;
  transition: box-shadow 0.2s, border-color 0.2s;
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 140px;
}

.grid-card:hover {
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.1);
  border-color: #409eff;
}

.grid-card.dragging {
  opacity: 0.5;
  border-color: #409eff;
}

.grid-card.disabled {
  opacity: 0.6;
  border-style: dashed;
}

.card-preview-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 12px;
  font-size: 13px;
  font-weight: 600;
  color: #606266;
  border-bottom: 1px solid #ebeef5;
}

.card-actions {
  display: flex;
  gap: 4px;
}

.card-preview-body {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 8px;
  color: #909399;
  font-size: 13px;
  padding: 12px;
}

.card-span-controls {
  display: flex;
  gap: 12px;
  padding: 6px 10px 10px;
  border-top: 1px solid #ebeef5;
  justify-content: center;
}
.span-row {
  display: flex;
  align-items: center;
  gap: 4px;
}
.span-label {
  font-size: 11px;
  color: #909399;
  margin-right: 4px;
}
.span-row .el-button {
  width: 22px;
  height: 22px;
  padding: 0;
  font-size: 13px;
}
.span-val {
  display: inline-block;
  width: 20px;
  text-align: center;
  font-size: 13px;
  font-weight: 600;
  color: #303133;
}

.disabled-section {
  margin-top: 20px;
}

.disabled-title {
  font-size: 14px;
  font-weight: 600;
  color: #909399;
  margin-bottom: 10px;
  padding-bottom: 8px;
  border-bottom: 1px solid #ebeef5;
}

.disabled-list {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
}

.disabled-card {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 8px 14px;
  background: #f5f7fa;
  border: 1px solid #e4e7ed;
  border-radius: 8px;
  font-size: 13px;
}

.disabled-name {
  font-weight: 500;
}

.disabled-desc {
  color: #909399;
  font-size: 12px;
}
</style>
