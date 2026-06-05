/**
 * SignalR 实时通知 composable
 */
import { ref, onUnmounted } from 'vue'
import * as signalR from '@microsoft/signalr'

const notifications = ref([])       // 通知列表
const unreadCount = ref(0)          // 未读数
let connection = null

export function useSignalR() {
  const connect = () => {
    const token = localStorage.getItem('token')
    if (!token || connection) return

    connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/notification', {
        accessTokenFactory: () => token,
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    connection.on('NewTask', (data) => {
      notifications.value.unshift({
        ...data,
        id: Date.now(),
        type: 'NewTask',
        read: false,
      })
      unreadCount.value++
    })

    connection.on('InstanceStatusChanged', (data) => {
      notifications.value.unshift({
        ...data,
        id: Date.now(),
        type: 'InstanceStatusChanged',
        read: false,
      })
      unreadCount.value++
    })

    connection.start().catch(() => {
      // 静默失败，几秒后自动重连
    })
  }

  const disconnect = () => {
    if (connection) {
      connection.stop()
      connection = null
    }
    notifications.value = []
    unreadCount.value = 0
  }

  const markAllRead = () => {
    notifications.value.forEach(n => n.read = true)
    unreadCount.value = 0
  }

  const clearAll = () => {
    notifications.value = []
    unreadCount.value = 0
  }

  return {
    notifications,
    unreadCount,
    connect,
    disconnect,
    markAllRead,
    clearAll,
  }
}
