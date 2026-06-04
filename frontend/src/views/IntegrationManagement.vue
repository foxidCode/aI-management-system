<template>
  <div class="integration-mgmt">
    <!-- 左侧面板 -->
    <div class="left-panel">
      <div class="panel-header"><span><el-icon><Connection /></el-icon> 连接配置</span><el-button size="small" type="primary" @click="openConnDialog(null)"><el-icon><Plus /></el-icon></el-button></div>
      <div class="conn-list">
        <div v-for="c in connections" :key="c.id" class="list-item" :class="{ active: editingConnId === c.id }" @click="editingConnId = c.id; editingTaskId = null">
          <el-icon><Link /></el-icon><span>{{ c.name }}</span><span class="tag">{{ c.authType }}</span>
          <div class="item-actions">
            <el-button size="small" text @click.stop="openConnDialog(c)"><el-icon><Edit /></el-icon></el-button>
            <el-popconfirm title="确定删除？" @confirm="delConn(c.id)" @click.stop><template #reference><el-button size="small" text type="danger"><el-icon><Delete /></el-icon></el-button></template></el-popconfirm>
          </div>
        </div>
      </div>

      <div class="panel-header" style="border-top:1px solid #ebeef5"><span><el-icon><Operation /></el-icon> 集成任务</span><el-button size="small" type="primary" @click="editingTaskId = -1; editingConnId = null"><el-icon><Plus /></el-icon></el-button></div>
      <div class="conn-list">
        <div v-for="t in tasks" :key="t.id" class="list-item" :class="{ active: editingTaskId === t.id }" @click="selectTask(t)">
          <el-icon :color="t.isActive ? '#67c23a' : '#909399'"><component :is="t.isActive ? 'VideoPlay' : 'VideoPause'" /></el-icon>
          <span>{{ t.name }}</span>
          <div class="item-actions">
            <el-button size="small" text @click.stop="executeTask(t)"><el-icon :loading="executingId === t.id"><CaretRight /></el-icon></el-button>
            <el-popconfirm title="确定删除？" @confirm="delTask(t.id)" @click.stop><template #reference><el-button size="small" text type="danger"><el-icon><Delete /></el-icon></el-button></template></el-popconfirm>
          </div>
        </div>
      </div>
    </div>

    <!-- 右侧面板 -->
    <div class="right-panel">
      <!-- 连接编辑 -->
      <template v-if="editingConnId">
        <div class="section-title">{{ editingConnId > 0 ? '编辑连接' : '新增连接' }}</div>
        <el-tabs v-model="connTab" type="card">
          <el-tab-pane label="基本配置" name="basic">
            <el-form :model="connForm" label-width="90px" size="small">
              <el-row :gutter="16">
                <el-col :span="12"><el-form-item label="名称"><el-input v-model="connForm.name" /></el-form-item></el-col>
                <el-col :span="12"><el-form-item label="认证类型"><el-select v-model="connForm.authType" style="width:100%"><el-option v-for="a in authTypes" :key="a" :label="a" :value="a" /></el-select></el-form-item></el-col>
              </el-row>
              <el-form-item label="Base URL"><el-input v-model="connForm.baseUrl" placeholder="https://api.example.com" /></el-form-item>
              <el-form-item label="描述"><el-input v-model="connForm.description" /></el-form-item>
            </el-form>
          </el-tab-pane>
          <el-tab-pane label="认证" name="auth" v-if="connForm.authType !== 'None'">
            <el-form label-width="90px" size="small">
              <el-form-item label="认证配置"><el-input v-model="connForm.authConfig" type="textarea" :rows="6" :placeholder="authPlaceholder" /></el-form-item>
            </el-form>
          </el-tab-pane>
          <el-tab-pane label="请求头" name="headers">
            <key-value-editor v-model="connHeaders" />
          </el-tab-pane>
        </el-tabs>
        <div class="task-actions">
          <el-button @click="testConn" :loading="testLoading">测试连接</el-button>
          <el-button type="primary" @click="saveConn" :loading="saveLoading">保存</el-button>
          <el-button @click="editingConnId = null">取消</el-button>
        </div>
      </template>

      <!-- 任务编辑 -->
      <template v-if="editingTaskId">
        <div class="section-title">{{ editingTaskId > 0 ? '编辑任务' : '新增任务' }}</div>
        <el-tabs v-model="taskTab" type="card">
          <!-- ===== 基本配置 ===== -->
          <el-tab-pane label="基本配置" name="basic">
            <el-form :model="taskForm" label-width="110px" size="small">
              <el-row :gutter="16">
                <el-col :span="16"><el-form-item label="任务名称"><el-input v-model="taskForm.name" placeholder="如：同步物料主数据" /></el-form-item></el-col>
                <el-col :span="8"><el-form-item label="启用"><el-switch v-model="taskForm.isActive" /></el-form-item></el-col>
              </el-row>
              <el-form-item label="描述"><el-input v-model="taskForm.description" placeholder="任务说明（可选）" /></el-form-item>

              <el-divider content-position="left">📥 数据来源（拉取）</el-divider>
              <el-row :gutter="16">
                <el-col :span="16"><el-form-item label="连接"><el-select v-model="taskForm.sourceConnectionId" clearable style="width:100%" placeholder="选择API连接"><el-option v-for="c in connections" :key="c.id" :label="c.name + ' (' + c.baseUrl + ')'" :value="c.id" /></el-select></el-form-item></el-col>
                <el-col :span="8"><el-form-item label="请求方法"><el-select v-model="taskForm.sourceMethod"><el-option value="GET" /><el-option value="POST" /></el-select></el-form-item></el-col>
              </el-row>
              <el-form-item label="API路径"><el-input v-model="taskForm.sourcePath" placeholder="/ierp/kapi/v2/ctgp/basedata/queryMaterials?id=xxx" /></el-form-item>
              <el-row :gutter="16">
                <el-col :span="12"><el-form-item label="Content-Type"><el-select v-model="taskForm.sourceContentType" style="width:100%"><el-option v-for="ct in contentTypes" :key="ct" :label="ct" :value="ct" /></el-select></el-form-item></el-col>
                <el-col :span="12"><el-form-item label="返回数据路径"><el-input v-model="taskForm.responseDataPath" placeholder="data.rows（JSON路径，留空自动识别）" /></el-form-item></el-col>
              </el-row>
              <el-form-item v-if="taskForm.sourceMethod==='POST'" label="请求体"><el-input v-model="taskForm.sourceBody" type="textarea" :rows="4" :placeholder="bodyPlaceholder" /></el-form-item>

              <el-divider content-position="left">📤 数据目标</el-divider>
              <el-form-item label="目标类型"><el-radio-group v-model="taskForm.targetType"><el-radio value="Api">推送到外部API</el-radio><el-radio value="Database">存入本系统数据库</el-radio></el-radio-group></el-form-item>
              <template v-if="taskForm.targetType === 'Api'">
                <el-row :gutter="16">
                  <el-col :span="16"><el-form-item label="连接"><el-select v-model="taskForm.targetConnectionId" clearable style="width:100%" placeholder="选择目标连接（可选）"><el-option v-for="c in connections" :key="c.id" :label="c.name + ' (' + c.baseUrl + ')'" :value="c.id" /></el-select></el-form-item></el-col>
                  <el-col :span="8"><el-form-item label="请求方法"><el-select v-model="taskForm.targetMethod"><el-option value="POST" /><el-option value="PUT" /></el-select></el-form-item></el-col>
                </el-row>
                <el-form-item label="API路径"><el-input v-model="taskForm.targetPath" placeholder="/api/sync" /></el-form-item>
                <el-form-item label="Content-Type"><el-select v-model="taskForm.targetContentType" style="width:200px"><el-option v-for="ct in contentTypes" :key="ct" :label="ct" :value="ct" /></el-select></el-form-item>
              </template>
              <template v-if="taskForm.targetType === 'Database'">
                <el-row :gutter="16">
                  <el-col :span="12"><el-form-item label="目标表名"><el-input v-model="taskForm.dbTableName" placeholder="如：MaterialDictionaries" /></el-form-item></el-col>
                  <el-col :span="12"><el-form-item label="子表配置">
                    <el-input v-model="taskForm.dbChildConfig" type="textarea" :rows="2" placeholder='[{"tableName":"xxx","foreignKey":"ParentId","sourceField":"items","mappings":"[...]"}]' />
                  </el-form-item></el-col>
                </el-row>
              </template>
            </el-form>
          </el-tab-pane>

          <!-- ===== 请求头 ===== -->
          <el-tab-pane label="请求头" name="taskHeaders">
            <el-alert title="此处配置的请求头将覆盖连接中的默认请求头" type="info" :closable="false" style="margin-bottom:10px" />
            <key-value-editor v-model="taskHeaders" />
          </el-tab-pane>

          <!-- ===== 字段映射 ===== -->
          <el-tab-pane label="字段映射" name="mapping">
            <el-alert title="配置来源字段到目标字段的映射关系。不配置则使用原始字段名。" type="info" :closable="false" style="margin-bottom:10px" />
            <div class="mapping-toolbar">
              <el-button size="small" @click="addMapping"><el-icon><Plus /></el-icon> 添加映射</el-button>
            </div>
            <el-table :data="fieldMappings" border size="small">
              <el-table-column label="来源字段" width="180"><template #default="{ row }"><el-input v-model="row.sourceField" size="small" placeholder="如：number" /></template></el-table-column>
              <el-table-column label="目标字段" width="180"><template #default="{ row }"><el-input v-model="row.targetField" size="small" placeholder="如：Code" /></template></el-table-column>
              <el-table-column label="转换" width="140"><template #default="{ row }"><el-select v-model="row.transform" size="small"><el-option v-for="t in transforms" :key="t" :label="t" :value="t" /></el-select></template></el-table-column>
              <el-table-column label="默认值" width="140"><template #default="{ row }"><el-input v-model="row.defaultValue" size="small" placeholder="可选" /></template></el-table-column>
              <el-table-column label="操作" width="70"><template #default="{ $index }"><el-button size="small" type="danger" @click="fieldMappings.splice($index, 1)"><el-icon><Delete /></el-icon></el-button></template></el-table-column>
            </el-table>
          </el-tab-pane>

          <!-- ===== 代码处理 ===== -->
          <el-tab-pane label="代码处理" name="code">
            <el-alert title="仅在字段映射无法满足需求时使用。支持 C# 脚本对数据进行复杂转换。" type="info" :closable="false" style="margin-bottom:10px" />
            <el-input v-model="taskForm.codeHandler" type="textarea" :rows="12" placeholder="// 复杂数据处理（可选）&#10;// var list = new List&lt;Dictionary&lt;string,object&gt;&gt;();&#10;// foreach (var item in data.EnumerateArray()) { ... }" style="font-family:Consolas,monospace;font-size:13px" />
          </el-tab-pane>

          <!-- ===== 执行日志 ===== -->
          <el-tab-pane label="执行日志" name="logs">
            <el-table :data="logs" border size="small" max-height="400">
              <el-table-column prop="id" label="ID" width="60" />
              <el-table-column label="状态" width="80"><template #default="{ row }"><el-tag :type="row.status==='Success'?'success':'danger'" size="small">{{ row.status }}</el-tag></template></el-table-column>
              <el-table-column prop="direction" label="方向" width="80" />
              <el-table-column prop="requestUrl" label="请求URL" min-width="200" show-overflow-tooltip />
              <el-table-column prop="errorMessage" label="错误" min-width="150" show-overflow-tooltip />
              <el-table-column label="耗时" width="80"><template #default="{ row }">{{ row.durationMs }}ms</template></el-table-column>
              <el-table-column label="时间" width="160"><template #default="{ row }">{{ formatDate(row.executedAt) }}</template></el-table-column>
              <el-table-column label="操作" width="60"><template #default="{ row }"><el-button size="small" @click="showLogDetail(row)">详情</el-button></template></el-table-column>
            </el-table>
          </el-tab-pane>
        </el-tabs>
        <div class="task-actions">
          <el-button type="success" @click="executeTask({ id: editingTaskId })" :loading="executingId === editingTaskId"><el-icon><CaretRight /></el-icon> 执行任务</el-button>
          <el-button type="primary" @click="saveTask" :loading="saveTaskLoading">保存任务</el-button>
          <el-button @click="editingTaskId = null">关闭</el-button>
        </div>
      </template>

      <el-empty v-if="!editingConnId && !editingTaskId" description="选择左侧连接或任务开始编辑" :image-size="100" />
    </div>

    <!-- 连接对话框 -->
    <el-dialog v-model="connDialogVisible" :title="editingConnForDialog ? '编辑连接' : '新增连接'" width="650px">
      <el-tabs v-model="connDialogTab" type="card">
        <el-tab-pane label="基本" name="basic">
          <el-form :model="connDialogForm" label-width="80px">
            <el-form-item label="名称"><el-input v-model="connDialogForm.name" /></el-form-item>
            <el-form-item label="Base URL"><el-input v-model="connDialogForm.baseUrl" placeholder="https://api.example.com" /></el-form-item>
            <el-form-item label="认证类型"><el-select v-model="connDialogForm.authType" style="width:100%"><el-option v-for="a in authTypes" :key="a" :label="a" :value="a" /></el-select></el-form-item>
            <el-form-item v-if="connDialogForm.authType !== 'None'" label="认证配置"><el-input v-model="connDialogForm.authConfig" type="textarea" :rows="5" :placeholder="authPlaceholder2" /></el-form-item>
            <el-form-item label="描述"><el-input v-model="connDialogForm.description" /></el-form-item>
          </el-form>
        </el-tab-pane>
        <el-tab-pane label="请求头" name="headers">
          <key-value-editor v-model="connDialogHeaders" />
        </el-tab-pane>
      </el-tabs>
      <template #footer>
        <el-button @click="connDialogVisible = false">取消</el-button>
        <el-button @click="testConnDialog" :loading="testLoading">测试连接</el-button>
        <el-button type="primary" @click="saveConnDialog" :loading="saveLoading">保存</el-button>
      </template>
    </el-dialog>

    <!-- 日志详情弹窗 -->
    <el-dialog v-model="logDetailVisible" title="日志详情" width="750px">
      <el-descriptions :column="1" border size="small">
        <el-descriptions-item label="请求URL">{{ logDetail.requestUrl }}</el-descriptions-item>
        <el-descriptions-item label="状态">{{ logDetail.status }}</el-descriptions-item>
        <el-descriptions-item label="耗时">{{ logDetail.durationMs }}ms</el-descriptions-item>
        <el-descriptions-item label="错误">{{ logDetail.errorMessage || '无' }}</el-descriptions-item>
        <el-descriptions-item label="请求头">
          <pre v-if="logDetail.requestHeaders" class="log-code">{{ logDetail.requestHeaders }}</pre>
          <span v-else style="color:#909399">无</span>
        </el-descriptions-item>
        <el-descriptions-item label="请求体">
          <pre v-if="logDetail.requestBody" class="log-code">{{ formatJson(logDetail.requestBody) }}</pre>
          <span v-else style="color:#909399">无</span>
        </el-descriptions-item>
        <el-descriptions-item label="响应体">
          <pre v-if="logDetail.responseData" class="log-code">{{ formatJson(logDetail.responseData) }}</pre>
          <span v-else style="color:#909399">无</span>
        </el-descriptions-item>
      </el-descriptions>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, computed, onMounted, watch } from 'vue'
import { ElMessage } from 'element-plus'
import {
  getIntegrationConnections, createIntegrationConnection, updateIntegrationConnection,
  deleteIntegrationConnection, testIntegrationConnection,
  getIntegrationTasks, createIntegrationTask, updateIntegrationTask,
  deleteIntegrationTask, executeIntegrationTask, getIntegrationLogs,
} from '../api/auth'
import KeyValueEditor from '../components/KeyValueEditor.vue'

const authTypes = ['None', 'Basic', 'Bearer', 'ApiKey', 'Chain']
const contentTypes = ['application/json', 'application/xml', 'text/xml', 'text/plain', 'application/x-www-form-urlencoded']
const transforms = ['none', 'toString', 'toNumber', 'toDate', 'toBoolean']

const connections = ref([])
const tasks = ref([])
const logs = ref([])

const editingConnId = ref(null)
const editingTaskId = ref(null)
const taskTab = ref('basic')
const connTab = ref('basic')
const testLoading = ref(false)
const saveLoading = ref(false)
const saveTaskLoading = ref(false)
const executingId = ref(null)

const connForm = reactive({ name: '', baseUrl: '', authType: 'None', authConfig: '', defaultHeaders: '', description: '' })
const connHeaders = ref([])
const taskForm = reactive({ name: '', description: '', sourceConnectionId: null, sourcePath: '', sourceMethod: 'GET', sourceContentType: 'application/json', sourceBody: '', responseDataPath: '', targetType: 'Api', targetConnectionId: null, targetPath: '', targetContentType: 'application/json', targetMethod: 'POST', dbTableName: '', dbChildConfig: '', fieldMappings: '[]', codeHandler: '', beforeExecute: '', afterExecute: '', isActive: true })
const taskHeaders = ref([])
const fieldMappings = ref([])

// 对话框
const connDialogVisible = ref(false)
const connDialogTab = ref('basic')
const editingConnForDialog = ref(null)
const connDialogForm = reactive({ name: '', baseUrl: '', authType: 'None', authConfig: '', defaultHeaders: '', description: '' })
const connDialogHeaders = ref([])

const authPlaceholder = computed(() => ({
  Basic: '{"username":"admin","password":"password"}',
  Bearer: '{"token":"xxx"} 或\n{"loginUrl":"/api/auth/login","loginBody":"{\\"username\\":\\"admin\\",\\"password\\":\\"123\\"}","tokenField":"data.token"}',
  ApiKey: '{"key":"X-API-Key","value":"xxx","in":"Header"}',
  Chain: '{"steps":[{"url":"/api/step1","method":"POST","body":"{...}","extractField":"data.token","saveAs":"myToken"}],"headerName":"X-Token","headerTemplate":"{{myToken}}"}'
}[connForm.authType] || '{}'))
const authPlaceholder2 = computed(() => ({
  Basic: '{"username":"admin","password":"password"}',
  Bearer: '{"token":"xxx"} 或\n{"loginUrl":"/api/auth/login","loginBody":"{\\"username\\":\\"admin\\",\\"password\\":\\"123\\"}","tokenField":"data.token"}',
  ApiKey: '{"key":"X-API-Key","value":"xxx","in":"Header"}',
  Chain: '{"steps":[{"url":"/api/step1","method":"POST","body":"{...}","extractField":"data.token","saveAs":"myToken"}],"headerName":"X-Token","headerTemplate":"{{myToken}}"}'
}[connDialogForm.authType] || '{}'))
const bodyPlaceholder = computed(() => taskForm.sourceContentType?.includes('xml') ? '<root>\n  <key>value</key>\n</root>' : '{"key":"value"}')

// --- 数据加载 ---
async function loadAll() {
  const [c, t] = await Promise.all([getIntegrationConnections(), getIntegrationTasks()])
  if (c.data.success) connections.value = c.data.data
  if (t.data.success) tasks.value = t.data.data
}
async function loadLogs(taskId) {
  const r = await getIntegrationLogs({ taskId, pageSize: 50 })
  if (r.data.success) logs.value = r.data.data
}

// --- 连接操作 ---
function openConnDialog(conn) {
  editingConnForDialog.value = conn
  if (conn) {
    Object.assign(connDialogForm, { name: conn.name, baseUrl: conn.baseUrl, authType: conn.authType, authConfig: '', defaultHeaders: conn.defaultHeaders || '', description: conn.description || '' })
    try { connDialogHeaders.value = JSON.parse(conn.defaultHeaders || '{}') } catch { connDialogHeaders.value = kvToArray(conn.defaultHeaders) }
  } else {
    Object.assign(connDialogForm, { name: '', baseUrl: '', authType: 'None', authConfig: '', defaultHeaders: '', description: '' })
    connDialogHeaders.value = []
  }
  connDialogVisible.value = true
  connDialogTab.value = 'basic'
}
async function testConnDialog() { testLoading.value = true; try { const r = await testIntegrationConnection({ ...connDialogForm, defaultHeaders: headersToJson(connDialogHeaders.value) }); ElMessage[r.data.success ? 'success' : 'error'](r.data.message) } catch { ElMessage.error('测试失败') } finally { testLoading.value = false } }
async function saveConnDialog() { saveLoading.value = true; try { const data = { ...connDialogForm, defaultHeaders: headersToJson(connDialogHeaders.value) }; if (editingConnForDialog.value) { await updateIntegrationConnection(editingConnForDialog.value.id, data); ElMessage.success('已更新') } else { await createIntegrationConnection(data); ElMessage.success('已创建') }; connDialogVisible.value = false; await loadAll() } catch { ElMessage.error('保存失败') } finally { saveLoading.value = false } }
async function delConn(id) { await deleteIntegrationConnection(id); ElMessage.success('已删除'); await loadAll() }
async function testConn() {
  testLoading.value = true
  try { const r = await testIntegrationConnection({ ...connForm, defaultHeaders: headersToJson(connHeaders.value) }); ElMessage[r.data.success ? 'success' : 'error'](r.data.message) } catch { ElMessage.error('测试失败') } finally { testLoading.value = false }
}
async function saveConn() {
  saveLoading.value = true
  try {
    const data = { ...connForm, defaultHeaders: headersToJson(connHeaders.value) }
    if (editingConnId.value > 0) { await updateIntegrationConnection(editingConnId.value, data); ElMessage.success('已更新') }
    else { await createIntegrationConnection(data); ElMessage.success('已创建') }
    editingConnId.value = null; await loadAll()
  } catch { ElMessage.error('保存失败') } finally { saveLoading.value = false }
}

// --- 任务操作 ---
function selectTask(t) {
  editingConnId.value = null; editingTaskId.value = t.id; taskTab.value = 'basic'
  Object.assign(taskForm, {
    name: t.name, description: t.description || '', sourceConnectionId: t.sourceConnectionId,
    sourcePath: t.sourcePath || '', sourceMethod: t.sourceMethod, sourceContentType: t.sourceContentType || 'application/json',
    sourceBody: t.sourceBody || '', responseDataPath: t.responseDataPath || '',
    targetType: t.targetType || 'Api', targetConnectionId: t.targetConnectionId,
    targetPath: t.targetPath || '', targetContentType: t.targetContentType || 'application/json', targetMethod: t.targetMethod,
    dbTableName: t.dbTableName || '', dbChildConfig: t.dbChildConfig || '',
    codeHandler: t.codeHandler || '', beforeExecute: t.beforeExecute || '', afterExecute: t.afterExecute || '',
    isActive: t.isActive
  })
  try { fieldMappings.value = JSON.parse(t.fieldMappings || '[]') } catch { fieldMappings.value = [] }
  // 从连接默认头复制到任务请求头
  const conn = connections.value.find(c => c.id === t.sourceConnectionId)
  try { taskHeaders.value = JSON.parse(conn?.defaultHeaders || '{}') } catch { taskHeaders.value = kvToArray(conn?.defaultHeaders) }
  loadLogs(t.id)
}
function addMapping() { fieldMappings.value.push({ sourceField: '', targetField: '', transform: 'none', defaultValue: '' }) }
async function saveTask() {
  saveTaskLoading.value = true
  try {
    const data = { ...taskForm, fieldMappings: JSON.stringify(fieldMappings.value) }
    if (editingTaskId.value > 0) { await updateIntegrationTask(editingTaskId.value, data); ElMessage.success('任务已更新') }
    else { await createIntegrationTask(data); ElMessage.success('任务已创建'); editingTaskId.value = null }
    await loadAll()
  } catch { ElMessage.error('保存失败') } finally { saveTaskLoading.value = false }
}
async function executeTask(t) {
  executingId.value = t.id
  try {
    const r = await executeIntegrationTask(t.id)
    if (r.data.success) {
      const d = r.data.data || {}
      ElMessage.success(d.message || '执行成功')
      taskTab.value = 'logs'
    } else {
      ElMessage.error((r.data.data && r.data.data.message) || '执行失败')
    }
    await loadLogs(t.id)
  } catch { ElMessage.error('执行失败') } finally { executingId.value = null }
}
async function delTask(id) { await deleteIntegrationTask(id); ElMessage.success('任务已删除'); editingTaskId.value = null; await loadAll() }

// --- 日志 ---
const logDetailVisible = ref(false)
const logDetail = reactive({ requestUrl: '', status: '', durationMs: 0, errorMessage: '', requestBody: '', responseData: '', requestHeaders: '' })
function showLogDetail(row) { Object.assign(logDetail, { requestUrl: row.requestUrl || '', status: row.status, durationMs: row.durationMs, errorMessage: row.errorMessage || '', requestBody: row.requestBody || '', responseData: row.responseData || '', requestHeaders: row.requestHeaders || '' }); logDetailVisible.value = true }
function formatJson(s) { try { return JSON.stringify(JSON.parse(s), null, 2) } catch { return s } }
function formatDate(s) { if (!s) return ''; const d = new Date(s); if (isNaN(d.getTime())) return s; return d.toLocaleString() }

// --- 请求头工具 ---
function headersToJson(arr) {
  if (!arr || !arr.length) return ''
  const obj = {}
  arr.forEach(h => { if (h.key) obj[h.key] = h.value || '' })
  return JSON.stringify(obj)
}
function kvToArray(str) {
  if (!str) return []
  try {
    const obj = JSON.parse(str)
    return Object.keys(obj).map(k => ({ key: k, value: obj[k] || '' }))
  } catch { return [] }
}

// 连接编辑时同步请求头
watch(editingConnId, (v) => {
  if (v && v > 0) {
    const c = connections.value.find(x => x.id === v)
    if (c) {
      Object.assign(connForm, { name: c.name, baseUrl: c.baseUrl, authType: c.authType, authConfig: '', defaultHeaders: c.defaultHeaders || '', description: c.description || '' })
      try { connHeaders.value = JSON.parse(c.defaultHeaders || '{}') } catch { connHeaders.value = kvToArray(c.defaultHeaders) }
    }
  }
})

onMounted(loadAll)
</script>

<style scoped>
.integration-mgmt { display: flex; height: calc(100vh - 120px); background: #fff; border-radius: 4px; overflow: hidden; }
.left-panel { width: 280px; min-width: 260px; border-right: 1px solid #ebeef5; display: flex; flex-direction: column; background: #fafafa; overflow-y: auto; }
.panel-header { display: flex; align-items: center; justify-content: space-between; padding: 10px 12px; font-size: 14px; font-weight: 600; gap: 8px; }
.conn-list { flex: 0 0 auto; }
.list-item { display: flex; align-items: center; gap: 6px; padding: 8px 12px; cursor: pointer; border-bottom: 1px solid #f0f0f0; font-size: 13px; }
.list-item:hover { background: #ecf5ff; }
.list-item.active { background: #d9ecff; }
.list-item .tag { font-size: 11px; color: #909399; margin-left: 4px; }
.item-actions { display: flex; flex-shrink: 0; margin-left: auto; }
.right-panel { flex: 1; overflow-y: auto; padding: 16px; }
.section-title { font-size: 16px; font-weight: 600; margin-bottom: 14px; }
.mapping-toolbar { margin-bottom: 10px; }
.task-actions { display: flex; gap: 10px; margin-top: 16px; padding-top: 12px; border-top: 1px solid #ebeef5; }
.log-code { background: #f5f7fa; padding: 10px; border-radius: 4px; font-size: 12px; font-family: Consolas, monospace; max-height: 300px; overflow: auto; white-space: pre-wrap; word-break: break-all; margin: 0; }
</style>
