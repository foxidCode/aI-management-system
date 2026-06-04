<template>
  <div class="inbound-chart-card">
    <div class="chart-header">
      <span class="chart-title">📊 每日入库金额（近 {{ days }} 天）</span>
      <span class="chart-total">合计: ¥{{ formatMoney(totalAmount) }}</span>
    </div>

    <div v-if="loading" class="chart-status">
      <el-icon class="is-loading" :size="22"><Loading /></el-icon>
      <span>加载中...</span>
    </div>

    <div v-else-if="rawData.length === 0" class="chart-status">
      暂无入库数据
    </div>

    <v-chart
      v-else
      class="chart"
      :option="option"
      :autoresize="true"
    />
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { Loading } from '@element-plus/icons-vue'
import VChart from 'vue-echarts'
import { use } from 'echarts/core'
import { BarChart } from 'echarts/charts'
import { GridComponent, TooltipComponent } from 'echarts/components'
import { CanvasRenderer } from 'echarts/renderers'

use([BarChart, GridComponent, TooltipComponent, CanvasRenderer])

const props = defineProps({
  days: { type: Number, default: 7 },
})

const loading = ref(true)
const rawData = ref([])

const totalAmount = computed(() =>
  rawData.value.reduce((sum, d) => sum + d.amount, 0)
)

const option = computed(() => ({
  grid: {
    top: 20,
    right: 20,
    bottom: 30,
    left: 55,
  },
  tooltip: {
    trigger: 'axis',
    formatter: (params) => {
      const p = params[0]
      return `<b>${p.axisValue}</b><br/>入库金额: <b>¥${p.value.toLocaleString()}</b><br/>单数: ${rawData.value.find(d => d.date === p.axisValue)?.count || 0}`
    },
  },
  xAxis: {
    type: 'category',
    data: rawData.value.map(d => d.date),
    axisLine: { lineStyle: { color: '#dcdfe6' } },
    axisLabel: { color: '#909399', fontSize: 11 },
  },
  yAxis: {
    type: 'value',
    name: '元',
    nameTextStyle: { color: '#909399', fontSize: 11 },
    axisLabel: {
      color: '#909399',
      fontSize: 10,
      formatter: (v) => v >= 10000 ? (v / 10000).toFixed(1) + '万' : v,
    },
    splitLine: { lineStyle: { color: '#ebeef5', type: 'dashed' } },
  },
  series: [{
    type: 'bar',
    data: rawData.value.map(d => d.amount),
    itemStyle: {
      color: '#409eff',
      borderRadius: [4, 4, 0, 0],
    },
    barWidth: '50%',
    label: {
      show: true,
      position: 'top',
      color: '#303133',
      fontSize: 10,
      formatter: (p) => p.value > 0 ? '¥' + (p.value >= 10000 ? (p.value / 10000).toFixed(1) + '万' : p.value) : '',
    },
  }],
}))

function formatMoney(val) {
  return Number(val || 0).toLocaleString('zh-CN', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })
}

async function fetchData() {
  loading.value = true
  try {
    const { api } = await import('../api/auth')
    const res = await api.get('/inboundorder/daily-stats', {
      params: { days: props.days },
    })
    if (res.data?.success) {
      rawData.value = (res.data.data || []).map(d => ({
        date: d.date,
        amount: Number(d.amount) || 0,
        count: d.count || 0,
      }))
    } else {
      rawData.value = []
    }
  } catch {
    rawData.value = []
  } finally {
    loading.value = false
  }
}

onMounted(fetchData)
</script>

<style scoped>
.inbound-chart-card {
  padding: 12px 16px;
  background: #fff;
  border-radius: 8px;
  height: 100%;
  display: flex;
  flex-direction: column;
  box-sizing: border-box;
}
.chart-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 4px;
  flex-shrink: 0;
}
.chart-title {
  font-size: 14px;
  font-weight: 600;
  color: #303133;
}
.chart-total {
  font-size: 12px;
  color: #909399;
}
.chart-status {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 6px;
  color: #c0c4cc;
  font-size: 13px;
}
.chart {
  flex: 1;
  min-height: 0;
}
</style>
