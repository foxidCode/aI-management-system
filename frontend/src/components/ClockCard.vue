<template>
  <div class="clock-card">
    <div class="clock-time">{{ timeStr }}</div>
    <div class="clock-date">{{ dateStr }}</div>
    <div class="clock-week">{{ weekStr }}</div>
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted } from 'vue'

const timeStr = ref('')
const dateStr = ref('')
const weekStr = ref('')

const weeks = ['星期日', '星期一', '星期二', '星期三', '星期四', '星期五', '星期六']

function tick() {
  const now = new Date()
  timeStr.value = now.toLocaleTimeString('zh-CN', { hour12: false })
  dateStr.value = now.toLocaleDateString('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  })
  weekStr.value = weeks[now.getDay()]
}

let timer = null
onMounted(() => {
  tick()
  timer = setInterval(tick, 1000)
})
onUnmounted(() => clearInterval(timer))
</script>

<style scoped>
.clock-card {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 100%;
  background: linear-gradient(145deg, #1a1a2e 0%, #16213e 100%);
  color: #fff;
  border-radius: 8px;
  user-select: none;
}
.clock-time {
  font-size: 42px;
  font-weight: 300;
  font-family: 'Courier New', monospace;
  letter-spacing: 4px;
  margin-bottom: 8px;
}
.clock-date {
  font-size: 15px;
  font-weight: 400;
  color: rgba(255,255,255,0.8);
  margin-bottom: 4px;
}
.clock-week {
  font-size: 13px;
  color: rgba(255,255,255,0.5);
}
</style>
