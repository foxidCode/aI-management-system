import { createApp } from 'vue'
import { createPinia } from 'pinia'
import ElementPlus from 'element-plus'
import 'element-plus/dist/index.css'
import * as ElementPlusIconsVue from '@element-plus/icons-vue'
import './style.css'
import App from './App.vue'
import router from './router'

// VForm3 低代码表单设计器
import VForm3 from '@/lib/vform/designer.umd.js'
import '@/lib/vform/designer.style.css'

const app = createApp(App)
const pinia = createPinia()

app.use(pinia)
app.use(ElementPlus)
app.use(router)
app.use(VForm3)  // 全局注册 v-form-designer、v-form-render 等组件

// v-focus 指令（nodeWarp 组件中节点名称编辑用）
app.directive("focus", {
  mounted(el) { el.focus() }
})

// 注册所有 Element Plus 图标
for (const [key, component] of Object.entries(ElementPlusIconsVue)) {
  app.component(key, component)
}

app.mount('#app')
