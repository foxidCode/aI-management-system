import { defineConfig } from 'vitepress'

export default defineConfig({
  title: '权限管理系统',
  description: '基于 ASP.NET Core + Vue 3 的全栈权限管理系统',
  lang: 'zh-CN',
  lastUpdated: true,

  themeConfig: {
    logo: '',
    nav: [
      { text: '首页', link: '/' },
      { text: '指南', link: '/guide/getting-started' },
      { text: '功能', link: '/features/auth' },
    ],

    sidebar: {
      '/guide/': [
        {
          text: '快速开始',
          items: [
            { text: '项目简介', link: '/guide/getting-started' },
            { text: '环境要求', link: '/guide/prerequisites' },
            { text: '部署运行', link: '/guide/deployment' },
          ]
        },
        {
          text: '技术架构',
          items: [
            { text: '技术栈', link: '/guide/tech-stack' },
            { text: '项目结构', link: '/guide/structure' },
            { text: '数据库设计', link: '/guide/database' },
            { text: '并发性能', link: '/guide/performance' },
          ]
        }
      ],
      '/features/': [
        {
          text: '核心功能',
          items: [
            { text: '用户认证', link: '/features/auth' },
            { text: '权限管理 (RBAC)', link: '/features/rbac' },
            { text: '动态菜单', link: '/features/menus' },
            { text: '入库单管理', link: '/features/inbound' },
            { text: '流程审批', link: '/features/workflow' },
            { text: '计划任务', link: '/features/scheduler' },
            { text: '集成平台', link: '/features/integration' },
            { text: '数据库管理', link: '/features/database' },
            { text: 'SSO / OAuth', link: '/features/sso-oauth' },
          ]
        }
      ]
    },

    socialLinks: [],

    search: {
      provider: 'local',
      options: {
        translations: {
          button: { buttonText: '搜索文档' },
          modal: { noResultsText: '无结果', resetButtonTitle: '清除', footer: { selectText: '选择', navigateText: '切换' } }
        }
      }
    },

    footer: {
      message: '基于 ASP.NET Core 10 + Vue 3 + Element Plus 构建',
    },

    outline: { level: [2, 3] },
    docFooter: { prev: '上一页', next: '下一页' },
    lastUpdatedText: '最后更新',
    darkModeSwitchLabel: '主题',
    sidebarMenuLabel: '菜单',
    returnToTopLabel: '回到顶部',
  }
})
