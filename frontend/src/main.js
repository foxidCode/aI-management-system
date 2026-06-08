import { createApp } from 'vue'
import { createPinia } from 'pinia'
import ElementPlus from 'element-plus'
import 'element-plus/dist/index.css'
import * as ElementPlusIconsVue from '@element-plus/icons-vue'
import './style.css'
import App from './App.vue'
import router from './router'

// FormCreate 低代码表单
import formCreate from '@form-create/element-ui'
import FcDesigner from '@form-create/designer'

const app = createApp(App)
const pinia = createPinia()

app.use(pinia)
app.use(ElementPlus)
app.use(router)
app.use(formCreate)   // 全局注册 <form-create> 渲染器
app.use(FcDesigner)   // 全局注册 <fc-designer> 设计器

// v-focus 指令（nodeWarp 组件中节点名称编辑用）
app.directive("focus", {
  mounted(el) { el.focus() }
})

// 注册所有 Element Plus 图标
for (const [key, component] of Object.entries(ElementPlusIconsVue)) {
  app.component(key, component)
}

app.mount('#app')
