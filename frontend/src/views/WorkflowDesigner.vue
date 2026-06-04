<template>
  <div class="designer-container">
    <!-- 工具栏 -->
    <div class="toolbar">
      <div class="toolbar-left">
        <span class="tool-label">拖入节点：</span>
        <div class="node-drag-items">
          <div class="drag-item approval" draggable="true" @dragstart="onDragStart($event, 'approval')">
            <span class="drag-icon">&#10003;</span>审批节点
          </div>
          <div class="drag-item cc" draggable="true" @dragstart="onDragStart($event, 'cc')">
            <span class="drag-icon">&#9993;</span>抄送节点
          </div>
          <div class="drag-item condition" draggable="true" @dragstart="onDragStart($event, 'condition')">
            <span class="drag-icon">&#8691;</span>条件分支
          </div>
        </div>
      </div>
      <div class="toolbar-center">
        <el-input v-model="defName" placeholder="流程名称" style="width:200px" size="small" />
        <el-select v-model="defCategory" style="width:140px;margin-left:8px" size="small">
          <el-option label="入库单审批" value="InboundOrder" />
          <el-option label="账号申请" value="AccountRequest" />
        </el-select>
      </div>
      <div class="toolbar-right">
        <el-button size="small" @click="zoomOut"><el-icon><ZoomOut /></el-icon></el-button>
        <span style="font-size:12px;color:#909399;min-width:40px;text-align:center">{{ Math.round(zoomPercent) }}%</span>
        <el-button size="small" @click="zoomIn"><el-icon><ZoomIn /></el-icon></el-button>
        <el-button size="small" @click="fitView">适应</el-button>
        <el-button size="small" type="primary" @click="save" :loading="saving">保存</el-button>
        <el-button size="small" type="success" @click="publish" :loading="publishing">发布</el-button>
      </div>
    </div>

    <!-- 主体 -->
    <div class="main-area">
      <div class="canvas-wrap" ref="canvasWrap">
        <div ref="canvasRef" class="lf-canvas"></div>
      </div>

      <!-- 节点属性面板 -->
      <div class="prop-panel" v-if="selectedNode">
        <h4 class="panel-title">
          <el-tag :type="nodeTypeTag(selectedNode.type)" size="small">{{ nodeTypeLabel(selectedNode.type) }}</el-tag>
          {{ selectedNode.properties?.label || selectedNode.type }}
        </h4>
        <el-form label-width="90px" size="small">
          <el-form-item label="节点名称">
            <el-input v-model="selectedNode.properties.label" @input="syncNodeProp('label', $event)" />
          </el-form-item>

          <template v-if="selectedNode.type === 'approval'">
            <el-divider content-position="left">审批配置</el-divider>
            <el-form-item label="审批人类型">
              <el-select v-model="nodeConfig.approverType" style="width:100%" @change="syncConfig">
                <el-option label="按角色" value="role" />
                <el-option label="指定用户" value="user" />
              </el-select>
            </el-form-item>
            <el-form-item label="审批人">
              <el-input v-if="nodeConfig.approverType==='role'" v-model="nodeConfig.approverValue" placeholder="角色ID（1=超级管理员,2=普通用户）" @input="syncConfig" />
              <el-input v-else v-model="nodeConfig.approverValue" placeholder="用户ID，如 1" @input="syncConfig" />
            </el-form-item>
            <el-form-item label="审批策略">
              <el-select v-model="nodeConfig.strategy" style="width:100%" @change="syncConfig">
                <el-option label="任签（任一通过）" value="any" />
                <el-option label="会签（全部通过）" value="all" />
              </el-select>
            </el-form-item>
            <el-form-item label="超时(小时)">
              <el-input-number v-model="nodeConfig.timeoutHours" :min="0" :max="720" style="width:100%" @change="syncConfig" />
            </el-form-item>
            <el-form-item label="超时策略">
              <el-select v-model="nodeConfig.timeoutPolicy" style="width:100%" @change="syncConfig">
                <el-option label="自动驳回" value="auto_reject" />
                <el-option label="自动通过" value="auto_pass" />
              </el-select>
            </el-form-item>
            <el-divider content-position="left">出线配置</el-divider>
            <el-form-item label="通过标签">
              <el-input v-model="edgeLabels.approved" size="small" placeholder="通过" />
            </el-form-item>
            <el-form-item label="驳回标签">
              <el-input v-model="edgeLabels.rejected" size="small" placeholder="驳回" />
            </el-form-item>
          </template>

          <template v-if="selectedNode.type === 'cc'">
            <el-divider content-position="left">抄送配置</el-divider>
            <el-form-item label="抄送人">
              <el-input v-model="nodeConfig.ccUserIds" placeholder="用户ID，逗号分隔" @input="syncConfig" />
            </el-form-item>
          </template>

          <template v-if="selectedNode.type === 'condition'">
            <el-divider content-position="left">条件配置</el-divider>
            <el-form-item label="匹配分支">
              <el-input v-model="nodeConfig.expression" placeholder="如 TotalTaxIncludedAmount > 10000" @input="syncConfig" />
            </el-form-item>
            <el-form-item label="匹配标签">
              <el-input v-model="edgeLabels.matched" size="small" placeholder="满足条件" />
            </el-form-item>
            <el-form-item label="不匹配标签">
              <el-input v-model="edgeLabels.unmatched" size="small" placeholder="不满足" />
            </el-form-item>
          </template>

          <el-divider />
          <el-button size="small" type="danger" @click="deleteSelected" v-if="selectedNode.type!=='start' && selectedNode.type!=='end'">
            <el-icon><Delete /></el-icon> 删除节点
          </el-button>
        </el-form>
      </div>
    </div>

    <!-- 小地图 -->
    <div class="minimap-wrap">
      <div ref="minimapRef"></div>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted, onUnmounted, watch, nextTick } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import LogicFlow from '@logicflow/core'
import '@logicflow/core/dist/index.css'
import { MiniMap, Control } from '@logicflow/extension'
import '@logicflow/extension/lib/style/index.css'
import { getWorkflowDefinition, createWorkflowDefinition, updateWorkflowDefinition, publishWorkflow } from '../api/auth'

const route = useRoute()
const router = useRouter()

const defName = ref('未命名流程')
const defCategory = ref('InboundOrder')
const saving = ref(false)
const publishing = ref(false)
const zoomPercent = ref(100)

const canvasWrap = ref(null)
const canvasRef = ref(null)
const minimapRef = ref(null)
let lf = null

const selectedNode = ref(null)
const nodeConfig = reactive({ approverType: 'role', approverValue: '1', strategy: 'any', timeoutHours: 24, timeoutPolicy: 'auto_reject', ccUserIds: '', expression: '' })
const edgeLabels = reactive({ approved: '通过', rejected: '驳回', matched: '符合条件', unmatched: '不符合' })

// ---- 节点标签 ----
function nodeTypeTag(t) { return { start:'success', end:'danger', approval:'', cc:'info', condition:'warning' }[t] || '' }
function nodeTypeLabel(t) { return { start:'开始', end:'结束', approval:'审批', cc:'抄送', condition:'条件' }[t] || t }

// ---- 同步节点属性到 LogicFlow ----
function syncNodeProp(key, val) {
  if (!selectedNode.value || !lf) return
  lf.setProperties(selectedNode.value.id, { [key]: val })
}

function syncConfig() {
  if (!selectedNode.value || !lf) return
  lf.setProperties(selectedNode.value.id, { config: { ...nodeConfig } })
}

// ---- 拖入节点 ----
function onDragStart(e, type) {
  e.dataTransfer.setData('nodeType', type)
}

// ---- LogicFlow 初始化 ----
function initLF() {
  if (!canvasRef.value) return

  lf = new LogicFlow({
    container: canvasRef.value,
    grid: { size: 20, visible: true, type: 'dot' },
    keyboard: { enabled: true },
    style: { origin: { fill: '#e1f3d8', stroke: '#67c23a' } },
    edgeType: 'polyline',
    isSilentMode: false,
    adjustEdge: true,
    allowResize: true,
    nodeTextDraggable: true,
    plugins: [MiniMap, Control],
  })

  // 注册自定义节点
  registerNodes()

  // 渲染
  lf.render()
  fitView()

  // 事件
  lf.on('node:click', ({ data }) => selectNode(data))
  lf.on('blank:click', () => { selectedNode.value = null })
  lf.on('node:dnd-add', ({ data }) => onNodeDrop(data))
  lf.on('node:delete', () => { selectedNode.value = null })

  // 渲染小地图
  if (minimapRef.value) {
    setTimeout(() => {
      const mini = new MiniMap({ container: minimapRef.value, width: 180, height: 130 })
      mini.render(lf)
    }, 500)
  }

  // 缩放监听
  lf.on('transform:change', ({ transform }) => { zoomPercent.value = transform.SCALE_X * 100 })

  // 拖入
  canvasWrap.value?.addEventListener('dragover', e => e.preventDefault())
  canvasWrap.value?.addEventListener('drop', e => {
    e.preventDefault()
    const type = e.dataTransfer.getData('nodeType')
    if (!type || !lf) return
    const pos = lf.getPointByClient(e.clientX, e.clientY)
    onNodeDrop({ type, x: pos.x, y: pos.y })
  })

  // 加载已有流程
  loadDefinition()
}

function registerNodes() {
  // 开始
  lf.register('start', ({ RectNode, RectNodeModel, h }) => {
    class Model extends RectNodeModel {
      initNodeData(data) { super.initNodeData(data); this.width = 80; this.height = 36; this.radius = 18 }
      getNodeStyle() { return { fill: '#e1f3d8', stroke: '#67c23a', strokeWidth: 2 } }
    }
    class View extends RectNode {
      getShape() {
        const { model } = this.props
        const { x, y, width, height } = model
        const attrs = model.getNodeStyle()
        return h('g', {}, [
          h('rect', { ...attrs, x: x - width/2, y: y - height/2, width, height, rx: 18, ry: 18 }),
          h('text', { x, y: y + 5, textAnchor: 'middle', fontSize: 13, fill: '#67c23a' }, '开始')
        ])
      }
    }
    return { model: Model, view: View }
  })

  // 结束
  lf.register('end', ({ RectNode, RectNodeModel, h }) => {
    class Model extends RectNodeModel {
      initNodeData(data) { super.initNodeData(data); this.width = 80; this.height = 36; this.radius = 18 }
      getNodeStyle() { return { fill: '#fef0f0', stroke: '#f56c6c', strokeWidth: 2 } }
    }
    class View extends RectNode {
      getShape() {
        const { model } = this.props
        const { x, y, width, height } = model
        const attrs = model.getNodeStyle()
        return h('g', {}, [
          h('rect', { ...attrs, x: x - width/2, y: y - height/2, width, height, rx: 18, ry: 18 }),
          h('text', { x, y: y + 5, textAnchor: 'middle', fontSize: 13, fill: '#f56c6c' }, '结束')
        ])
      }
    }
    return { model: Model, view: View }
  })

  // 审批
  lf.register('approval', ({ RectNode, RectNodeModel, h }) => {
    class Model extends RectNodeModel {
      initNodeData(data) { super.initNodeData(data); this.width = 140; this.height = 64 }
      getNodeStyle() { return { fill: '#ecf5ff', stroke: '#409EFF', strokeWidth: 2 } }
    }
    class View extends RectNode {
      getShape() {
        const { model } = this.props
        const { x, y, width, height, properties } = model
        const attrs = model.getNodeStyle()
        const label = properties?.label || '审批节点'
        return h('g', {}, [
          h('rect', { ...attrs, x: x - width/2, y: y - height/2, width, height, rx: 6, ry: 6 }),
          h('text', { x, y: y - 6, textAnchor: 'middle', fontSize: 13, fontWeight: 500, fill: '#303133' }, label.length > 10 ? label.slice(0,10)+'...' : label),
          h('text', { x, y: y + 14, textAnchor: 'middle', fontSize: 11, fill: '#909399' }, '审批')
        ])
      }
    }
    return { model: Model, view: View }
  })

  // 抄送
  lf.register('cc', ({ RectNode, RectNodeModel, h }) => {
    class Model extends RectNodeModel {
      initNodeData(data) { super.initNodeData(data); this.width = 120; this.height = 58 }
      getNodeStyle() { return { fill: '#fdf6ec', stroke: '#e6a23c', strokeWidth: 1.5, strokeDasharray: '4 2' } }
    }
    class View extends RectNode {
      getShape() {
        const { model } = this.props
        const { x, y, width, height, properties } = model
        const attrs = model.getNodeStyle()
        const label = properties?.label || '抄送节点'
        return h('g', {}, [
          h('rect', { ...attrs, x: x - width/2, y: y - height/2, width, height, rx: 6, ry: 6 }),
          h('text', { x, y: y - 4, textAnchor: 'middle', fontSize: 12, fill: '#e6a23c' }, '📨 ' + (label.length > 8 ? label.slice(0,8)+'...' : label)),
          h('text', { x, y: y + 14, textAnchor: 'middle', fontSize: 11, fill: '#c0c4cc' }, '抄送')
        ])
      }
    }
    return { model: Model, view: View }
  })

  // 条件
  lf.register('condition', ({ RectNode, RectNodeModel, h }) => {
    class Model extends RectNodeModel {
      initNodeData(data) { super.initNodeData(data); this.width = 100; this.height = 50 }
      getNodeStyle() { return { fill: '#f4f4f5', stroke: '#909399', strokeWidth: 2 } }
    }
    class View extends RectNode {
      getShape() {
        const { model } = this.props
        const { x, y, width, height, properties } = model
        const label = properties?.label || '条件分支'
        const expr = properties?.config?.expression || ''
        const points = `${x},${y - height/2} ${x + width/2},${y} ${x},${y + height/2} ${x - width/2},${y}`
        const attrs = model.getNodeStyle()
        return h('g', {}, [
          h('polygon', { ...attrs, points }),
          h('text', { x, y: y - 2, textAnchor: 'middle', fontSize: 12, fontWeight: 500, fill: '#606266' }, label.length > 6 ? label.slice(0,6)+'...' : label),
          expr ? h('text', { x, y: y + 14, textAnchor: 'middle', fontSize: 10, fill: '#c0c4cc' }, expr.length > 10 ? expr.slice(0,10)+'...' : expr) : null,
        ])
      }
    }
    return { model: Model, view: View }
  })

  // 连接校验：审批节点允许两条出线（通过/驳回）
  lf.setDefaultEdgeType('polyline')
}

function onNodeDrop({ type, x, y }) {
  if (!lf) return
  const id = `${type}_${Date.now()}`
  const label = { approval: '审批节点', cc: '抄送节点', condition: '条件分支' }[type] || type
  const config = type === 'approval' ? { approverType: 'role', approverValue: '1', strategy: 'any', timeoutHours: 24, timeoutPolicy: 'auto_reject' }
    : type === 'cc' ? { ccUserIds: '' }
    : type === 'condition' ? { expression: '' } : {}
  lf.addNode({ id, type, x, y, properties: { label, config } })
}

function selectNode(data) {
  selectedNode.value = data
  const cfg = data.properties?.config || {}
  Object.assign(nodeConfig, {
    approverType: cfg.approverType || 'role',
    approverValue: cfg.approverValue || '1',
    strategy: cfg.strategy || 'any',
    timeoutHours: cfg.timeoutHours ?? 24,
    timeoutPolicy: cfg.timeoutPolicy || 'auto_reject',
    ccUserIds: cfg.ccUserIds || '',
    expression: cfg.expression || '',
  })
}

function deleteSelected() {
  if (!selectedNode.value || !lf) return
  lf.deleteNode(selectedNode.value.id)
  selectedNode.value = null
}

function zoomIn() { lf?.zoom(true) }
function zoomOut() { lf?.zoom(false) }
function fitView() { lf?.fitView() }

// ---- 数据转换 ----

function getGraphData() {
  if (!lf) return '{}'
  const data = lf.getGraphData()
  const nodes = (data.nodes || []).map(n => ({
    id: n.id, type: n.type, label: n.properties?.label || n.type,
    x: Math.round(n.x), y: Math.round(n.y),
    config: n.properties?.config || {},
  }))
  const edges = (data.edges || []).map(e => ({
    source: e.sourceNodeId, target: e.targetNodeId,
    condition: e.properties?.condition || 'always',
  }))
  return JSON.stringify({ nodes, edges })
}

function setGraphData(json) {
  if (!lf) return
  try {
    const data = typeof json === 'string' ? JSON.parse(json) : json
    const nodes = (data.nodes || []).map(n => ({
      id: n.id, type: n.type, x: n.x, y: n.y,
      properties: { label: n.label || n.id, config: n.config || {} },
    }))
    const edges = (data.edges || []).map((e, i) => ({
      id: e.id || `edge_${i}`, type: 'polyline',
      sourceNodeId: e.source, targetNodeId: e.target,
      properties: { condition: e.condition || 'always' },
    }))
    lf.render({ nodes, edges })
    fitView()
  } catch { /* ignore */ }
}

async function loadDefinition() {
  const editId = route.params.id
  if (!editId) {
    // 新建：添加开始和结束节点
    setGraphData({
      nodes: [
        { id: 'start_1', type: 'start', label: '开始', x: 100, y: 300 },
        { id: 'end_1', type: 'end', label: '结束', x: 700, y: 300 },
      ],
      edges: []
    })
    return
  }
  try {
    const r = await getWorkflowDefinition(parseInt(editId))
    if (r.data.success) {
      const d = r.data.data
      defName.value = d.name
      defCategory.value = d.category
      setGraphData(d.nodeData)
    }
  } catch { ElMessage.error('加载失败') }
}

async function save() {
  if (!defName.value.trim()) { ElMessage.warning('请输入流程名称'); return }
  saving.value = true
  try {
    const data = { name: defName.value, category: defCategory.value, nodeData: getGraphData() }
    const editId = route.params.id
    if (editId) {
      await updateWorkflowDefinition(parseInt(editId), data)
      ElMessage.success('已保存')
    } else {
      const r = await createWorkflowDefinition(data)
      ElMessage.success('已创建')
      router.replace(`/dashboard/workflows/design/${r.data.data.id}`)
    }
  } catch (e) { ElMessage.error(e.response?.data?.message || '保存失败') } finally { saving.value = false }
}

async function publish() {
  const editId = route.params.id
  if (!editId) { await save(); if (!route.params.id) return }
  publishing.value = true
  try {
    await publishWorkflow(parseInt(route.params.id))
    ElMessage.success('已发布')
  } catch (e) { ElMessage.error(e.response?.data?.message || '发布失败') } finally { publishing.value = false }
}

onMounted(() => { nextTick(initLF) })
onUnmounted(() => { lf?.destroy() })
</script>

<style scoped>
.designer-container {
  display: flex; flex-direction: column; height: calc(100vh - 120px);
  background: #fff; border-radius: 6px; overflow: hidden;
}
.toolbar {
  display: flex; align-items: center; justify-content: space-between;
  padding: 8px 16px; border-bottom: 1px solid #ebeef5; gap: 12px; flex-shrink: 0;
}
.toolbar-left { display: flex; align-items: center; gap: 8px; }
.tool-label { font-size: 12px; color: #909399; white-space: nowrap; }
.node-drag-items { display: flex; gap: 6px; }
.drag-item {
  padding: 4px 12px; border-radius: 4px; font-size: 12px; cursor: grab; user-select: none;
  border: 1px solid #dcdfe6; display: flex; align-items: center; gap: 4px;
  transition: all 0.2s;
}
.drag-item:hover { transform: translateY(-1px); box-shadow: 0 2px 8px rgba(0,0,0,0.1); }
.drag-item.approval { background: #ecf5ff; color: #409EFF; border-color: #b3d8ff; }
.drag-item.cc { background: #fdf6ec; color: #e6a23c; border-color: #f5dab1; }
.drag-item.condition { background: #f4f4f5; color: #909399; border-color: #d4d4d8; }
.drag-icon { font-size: 14px; }
.toolbar-center { display: flex; align-items: center; }
.toolbar-right { display: flex; align-items: center; gap: 6px; }

.main-area { display: flex; flex: 1; overflow: hidden; }
.canvas-wrap { flex: 1; position: relative; }
.lf-canvas { width: 100%; height: 100%; }

.prop-panel {
  width: 300px; padding: 16px; border-left: 1px solid #ebeef5;
  overflow-y: auto; flex-shrink: 0; background: #fafafa;
}
.panel-title { margin: 0 0 12px 0; display: flex; align-items: center; gap: 8px; font-size: 14px; }

.minimap-wrap { position: absolute; bottom: 12px; right: 312px; z-index: 10; opacity: 0.8; }

:deep(.lf-mini-map) { box-shadow: 0 0 4px rgba(0,0,0,0.15); border-radius: 4px; }
</style>
