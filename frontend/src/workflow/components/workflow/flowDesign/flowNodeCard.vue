<template>
  <div class="fnode-warp">
    <!-- 条件网关 / 并行审批 分支容器 -->
    <template v-if="isBranch">
      <div class="fnode-branch-container">
        <div class="fnode-branch-label">{{ branchLabel }}</div>
        <div class="fnode-branches-wrapper">
          <div
            class="fnode-branch-col"
            v-for="(b, i) in branches"
            :key="b?.nodeId || i"
          >
            <!-- 分支节点卡片 -->
            <div class="fnode-card-wrap">
              <div :class="['fnode-card', statusClass(b?.nodeId)]">
                <span class="fnode-priority" v-if="b">优先级{{ b.nodeWeight || i + 1 }}</span>
                <div :class="['fnode-header', headerClass(b?.nodeType)]">
                  <span class="fnode-title">{{ b?.nodeName || '' }}</span>
                </div>
                <div class="fnode-content">
                  <span :class="b?.error ? 'fnode-placeholder' : 'fnode-text'">
                    {{ b?.error ? '请选择审核人' : (b?.nodeDisplayName || b?.nodeName || '') }}
                  </span>
                  <span class="fnode-dot"></span>
                </div>
              </div>
              <div v-if="b" :class="statusIndicator(b.nodeId)"></div>
            </div>
            <!-- 分支连接线 -->
            <div class="fnode-line" v-if="b">
              <div class="fnode-line-top"></div>
              <div class="fnode-line-bottom"></div>
              <div class="fnode-line-arrow"></div>
            </div>
            <div class="fnode-branch-space" v-if="b"></div>
            <!-- 分支子节点递归 -->
            <FlowNodeCard
              v-if="b?.childNode"
              :node="b.childNode"
              :activeNodeId="activeNodeId"
              :completedNodeIds="completedNodeIds"
            />
            <div v-if="i === 0" class="fnode-left-cover top-left"></div>
            <div v-if="i === branches.length - 1" class="fnode-right-cover top-right"></div>
            <div v-if="i === 0" class="fnode-left-cover bottom-left"></div>
            <div v-if="i === branches.length - 1" class="fnode-right-cover bottom-right"></div>
          </div>
        </div>
      </div>
      <!-- 分支汇合线 -->
      <div class="fnode-line">
        <div class="fnode-line-top"></div>
        <div class="fnode-line-bottom"></div>
        <div class="fnode-line-arrow"></div>
      </div>
      <!-- 分支后的节点 -->
      <FlowNodeCard
        v-if="node.childNode"
        :node="node.childNode"
        :activeNodeId="activeNodeId"
        :completedNodeIds="completedNodeIds"
      />
    </template>

    <!-- 普通节点 -->
    <template v-else>
      <div class="fnode-card-wrap">
        <div :class="['fnode-card', statusClass(node.nodeId)]">
          <div :class="['fnode-header', headerClass(node.nodeType)]">
            <span class="fnode-title">{{ node.nodeName || '' }}</span>
          </div>
          <div class="fnode-content">
            <span :class="node.error ? 'fnode-placeholder' : 'fnode-text'">
              {{ node.error ? '请选择审核人' : (node.nodeDisplayName || node.nodeName || '') }}
            </span>
            <span class="fnode-dot"></span>
          </div>
        </div>
        <div :class="statusIndicator(node.nodeId)"></div>
      </div>
      <!-- 连接线 -->
      <div class="fnode-line">
        <div class="fnode-line-top"></div>
        <div class="fnode-line-bottom"></div>
        <div class="fnode-line-arrow"></div>
      </div>
      <!-- 递归子节点 -->
      <FlowNodeCard
        v-if="node.childNode"
        :node="node.childNode"
        :activeNodeId="activeNodeId"
        :completedNodeIds="completedNodeIds"
      />
    </template>
  </div>
</template>

<script>
import { NodeType } from '@/workflow/utils/workflow/constant.js'

export default {
  name: 'FlowNodeCard',
  props: {
    node: { type: Object, default: null },
    activeNodeId: { type: String, default: '' },
    completedNodeIds: { type: Object, default: () => new Set() },
  },
  computed: {
    isBranch() {
      return this.node?.nodeType === NodeType.GatewayNode
          || this.node?.nodeType === NodeType.ParallelApproveWayNode
    },
    branchLabel() {
      return this.node?.nodeType === NodeType.GatewayNode ? '条件分支' : '并行审批'
    },
    branches() {
      if (this.node?.nodeType === NodeType.GatewayNode)
        return this.node?.nodeProperty?.conditionNodes || []
      if (this.node?.nodeType === NodeType.ParallelApproveWayNode)
        return this.node?.nodeProperty?.parallelNodes || []
      return []
    },
  },
  methods: {
    statusClass(nid) {
      if (nid === this.activeNodeId) return 'is-active'
      if (this.completedNodeIds?.has(nid)) return 'is-completed'
      return ''
    },
    headerClass(nt) {
      return 'hd-' + (nt || 0)
    },
    statusIndicator(nid) {
      if (nid === this.activeNodeId) return 'fnode-active-label'
      if (this.completedNodeIds?.has(nid)) return 'fnode-done-label'
      return ''
    },
  },
}
</script>

<style>
/* 非 scoped 样式确保递归子组件也能应用 */
.fnode-warp {
  display: flex;
  flex-direction: column;
  align-items: center;
  width: 100%;
}
.fnode-card-wrap { position: relative; width: 260px; }
.fnode-card {
  position: relative; width: 100%; background: #fff;
  border: 2px solid #e0e0e0; border-radius: 8px;
  box-shadow: 0 2px 8px rgba(0,0,0,0.06); overflow: hidden; transition: all 0.3s;
}
.fnode-card.is-active {
  border-color: #409eff;
  box-shadow: 0 0 0 4px rgba(64, 158, 255, 0.25);
  transform: scale(1.03);
}
.fnode-card.is-completed { border-color: #67c23a; opacity: 0.85; }

.fnode-priority {
  position: absolute; top: 6px; right: 10px; padding: 0 6px;
  border-radius: 10px; background: #eee; font-size: 11px;
  line-height: 18px; color: #555;
}
.fnode-header {
  padding: 6px 12px; display: flex; align-items: center;
  color: #fff; font-size: 14px;
}
.fnode-header.hd-1 { background: #5b698d; }
.fnode-header.hd-3 { background: #27ae60; }
.fnode-header.hd-4, .fnode-header.hd-7 { background: #ff943e; }
.fnode-header.hd-6 { background: #3296fa; }

.fnode-content {
  padding: 4px 10px 10px; font-size: 13px; color: #333;
  min-height: 36px; display: flex; align-items: center; justify-content: space-between;
}
.fnode-text { flex: 1; }
.fnode-placeholder { color: #999; }

.fnode-dot { width: 8px; height: 8px; border-radius: 50%; margin-left: 6px; flex-shrink: 0; background: #c0c4cc; }
.is-active .fnode-dot { background: #409eff; animation: fnode-pulse 1.5s infinite; }
.is-completed .fnode-dot { background: #67c23a; }
@keyframes fnode-pulse { 0%, 100% { opacity: 1; } 50% { opacity: 0.3; } }

.fnode-active-label { text-align: center; font-size: 11px; color: #409eff; font-weight: 600; margin-top: 2px; }
.fnode-active-label::before { content: '● 当前'; }
.fnode-done-label { text-align: center; font-size: 11px; color: #67c23a; margin-top: 2px; }
.fnode-done-label::before { content: '✓ 已完成'; }

/* 连接线 */
.fnode-line { display: flex; flex-direction: column; align-items: center; }
.fnode-line-top { width: 1px; height: 20px; background: #cacaca; }
.fnode-line-bottom { width: 1px; height: 20px; background: #cacaca; }
.fnode-line-arrow {
  width: 0; height: 0; border-left: 5px solid transparent;
  border-right: 5px solid transparent; border-top: 6px solid #cacaca;
}

/* 分支容器 */
.fnode-branch-container {
  position: relative; display: flex; flex-direction: column;
  align-items: center; margin: 12px 0 0;
}
.fnode-branch-label {
  position: absolute; top: -10px; z-index: 2; background: #fff;
  padding: 2px 10px; border-radius: 20px; font-size: 12px;
  color: #3296fa; box-shadow: 0 2px 6px rgba(0,0,0,0.1);
}
.fnode-branches-wrapper {
  display: flex; justify-content: center; position: relative;
  width: fit-content; padding-top: 20px; padding-bottom: 20px; margin: 0 20px;
}
.fnode-branches-wrapper::before {
  content: ""; position: absolute; top: 0; left: 50%; transform: translateX(-50%);
  width: calc(100% - 260px); height: 1px; background: #cacaca;
}
.fnode-branches-wrapper::after {
  content: ""; position: absolute; bottom: 0; left: 50%; transform: translateX(-50%);
  width: calc(100% - 260px); height: 1px; background: #cacaca;
}
.fnode-branch-col {
  flex: 0 0 260px; display: flex; flex-direction: column;
  align-items: center; margin: 0 20px; position: relative;
}
.fnode-branch-col::before {
  content: ""; position: absolute; top: -20px; left: 50%;
  transform: translateX(-50%); width: 1px; height: 20px; background: #cacaca;
}
.fnode-branch-col::after {
  content: ""; position: absolute; bottom: -20px; left: 50%;
  transform: translateX(-50%); width: 1px; height: 20px; background: #cacaca;
}
.fnode-branch-space { flex: 1; width: 1px; background: #cacaca; }

.fnode-left-cover, .fnode-right-cover {
  position: absolute; height: 8px; width: 50%; background: #f5f5f7;
}
.fnode-left-cover { left: -1px; }
.fnode-right-cover { right: -1px; }
.top-left, .top-right { top: -22px; }
.bottom-left, .bottom-right { bottom: -22px; z-index: 1; }
</style>
