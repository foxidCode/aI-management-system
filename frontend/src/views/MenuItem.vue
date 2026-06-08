<template>
  <el-sub-menu v-if="menu.children && menu.children.length > 0" :index="String(menu.id)">
    <template #title>
      <el-icon v-if="menu.icon"><component :is="menu.icon" /></el-icon>
      <span>{{ menu.name }}</span>
    </template>
    <MenuItem v-for="child in menu.children" :key="child.id" :menu="child" />
  </el-sub-menu>
  <el-menu-item v-else-if="menu.path" :index="menu.path">
    <el-icon v-if="menu.icon"><component :is="menu.icon" /></el-icon>
    <span>{{ menu.name }}</span>
  </el-menu-item>
</template>

<script setup>
defineProps({
  menu: { type: Object, required: true }
})
</script>
