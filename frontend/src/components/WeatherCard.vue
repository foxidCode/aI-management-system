<template>
  <div class="weather-card" v-loading="loading">
    <div class="weather-header">
      <el-icon :size="20"><PartlyCloudy /></el-icon>
      <span class="city-name">{{ displayCity }}</span>
      <el-button
        class="refresh-btn"
        :icon="Refresh"
        circle
        size="small"
        text
        @click="initWeather"
      />
    </div>
    <div v-if="weather" class="weather-body">
      <div class="weather-main">
        <span class="temp">{{ weather.temp }}°C</span>
        <span class="desc">{{ weather.desc }}</span>
      </div>
      <div class="weather-details">
        <div class="detail">
          <el-icon><WindPower /></el-icon>
          <span>{{ weather.wind }} km/h</span>
        </div>
        <div class="detail">
          <el-icon><Sunny /></el-icon>
          <span>{{ weather.humidity }}%</span>
        </div>
        <div class="detail">
          <span>体感 {{ weather.feelsLike }}°C</span>
        </div>
      </div>
    </div>
    <div v-else-if="!loading" class="weather-error">
      {{ errorMsg || '加载失败' }}
      <el-button link type="primary" size="small" @click="initWeather">重试</el-button>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { PartlyCloudy, WindPower, Sunny, Refresh } from '@element-plus/icons-vue'

const loading = ref(true)
const weather = ref(null)
const displayCity = ref('定位中...')
const errorMsg = ref('')
const useIpFallback = ref(localStorage.getItem('geo_failed') === '1')

async function fetchWeather(params) {
  const { api } = await import('../api/auth')
  const res = await api.get('/weather', { params })
  const body = res.data
  displayCity.value = body.resolvedCity || '当前位置'
  const current = body.data.current_condition[0]
  weather.value = {
    temp: current.temp_C,
    desc: current.weatherDesc[0].value,
    wind: current.windspeedKmph,
    humidity: current.humidity,
    feelsLike: current.FeelsLikeC,
  }
  errorMsg.value = ''
}

async function tryBrowserGeo() {
  if (!window.isSecureContext) throw new Error('非安全上下文')
  if (!navigator.geolocation) throw new Error('浏览器不支持定位')

  const permStatus = await navigator.permissions?.query({ name: 'geolocation' })
  if (permStatus?.state === 'denied') throw new Error('定位权限已拒绝')

  const pos = await new Promise((resolve, reject) => {
    navigator.geolocation.getCurrentPosition(resolve, reject, {
      timeout: 5000,
      enableHighAccuracy: false,
      maximumAge: 300000,
    })
  })
  return { lat: pos.coords.latitude, lon: pos.coords.longitude }
}

async function initWeather() {
  loading.value = true
  weather.value = null
  errorMsg.value = ''
  displayCity.value = '定位中...'

  if (!useIpFallback.value) {
    try {
      // 整个浏览器定位过程最多等 5 秒
      const coords = await Promise.race([
        tryBrowserGeo(),
        new Promise((_, reject) => setTimeout(() => reject(new Error('timeout')), 2000)),
      ])
      await fetchWeather(coords)
      loading.value = false
      return
    } catch (err) {
      console.warn('浏览器定位失败，改用 IP 定位:', err?.message || err)
      localStorage.setItem('geo_failed', '1')
      useIpFallback.value = true
    }
  }

  // IP 定位兜底
  displayCity.value = 'IP定位中...'
  try {
    await fetchWeather({})  // 不传参，后端走 IP 定位
  } catch {
    errorMsg.value = '天气数据加载失败'
  }
  loading.value = false
}

onMounted(initWeather)
</script>

<style scoped>
.weather-card {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  border-radius: 12px;
  padding: 20px;
  color: #fff;
  min-height: 160px;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
}
.weather-header {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 16px;
  font-weight: 600;
  margin-bottom: 12px;
}
.city-name { flex: 1; }
.refresh-btn { color: rgba(255,255,255,0.7); }
.refresh-btn:hover { color: #fff; }
.weather-body { flex: 1; }
.weather-main {
  display: flex;
  align-items: baseline;
  gap: 12px;
  margin-bottom: 16px;
}
.temp { font-size: 36px; font-weight: 700; }
.desc { font-size: 16px; opacity: 0.9; }
.weather-details {
  display: flex;
  gap: 20px;
  font-size: 13px;
  opacity: 0.9;
}
.detail { display: flex; align-items: center; gap: 4px; }
.weather-error {
  text-align: center;
  opacity: 0.7;
  padding: 30px 0;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
}
</style>
