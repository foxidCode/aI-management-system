<template>
  <div class="schedule-mgmt">
    <!-- 工具栏 -->
    <div class="toolbar">
      <el-button type="primary" @click="openDialog(null)"><el-icon><Plus /></el-icon> 新增计划任务</el-button>
      <el-tooltip content="后台服务每 10 秒检查一次到期任务并自动执行">
        <el-tag type="info" effect="plain"><el-icon><InfoFilled /></el-icon> 10s 轮询</el-tag>
      </el-tooltip>
    </div>

    <!-- 表格 -->
    <el-table :data="tasks" border size="small" v-loading="loading" empty-text="暂无计划任务">
      <el-table-column prop="id" label="ID" width="55" align="center" />
      <el-table-column label="启用" width="60" align="center">
        <template #default="{ row }">
          <el-switch v-model="row.isEnabled" size="small" @change="toggleEnabled(row)" />
        </template>
      </el-table-column>
      <el-table-column prop="name" label="名称" min-width="140" show-overflow-tooltip />
      <el-table-column label="关联任务" min-width="140" show-overflow-tooltip>
        <template #default="{ row }">
          <span style="font-size:13px">{{ row.integrationTaskName || '—' }}</span>
        </template>
      </el-table-column>
      <el-table-column label="执行规则" width="180">
        <template #default="{ row }">
          <div class="cron-cell">
            <template v-if="row.runOnceAt">
              <el-tag type="warning" size="small" effect="plain">单次</el-tag>
              <span class="cron-desc" style="color:#e6a23c">{{ formatDate(row.runOnceAt) }}</span>
            </template>
            <template v-else>
              <code class="cron-code">{{ row.cronExpression || '—' }}</code>
              <span class="cron-desc">{{ row.cronDescription || row.cronExpression }}</span>
            </template>
          </div>
        </template>
      </el-table-column>
      <el-table-column label="上次执行" width="180" show-overflow-tooltip>
        <template #default="{ row }">
          <template v-if="row.lastRunAt">
            <el-tag :type="row.lastRunStatus === 'success' ? 'success' : 'danger'" size="small" effect="dark" style="margin-right:4px">{{ row.lastRunStatus === 'success' ? '✓' : '✗' }}</el-tag>
            <span style="font-size:12px;color:#606266">{{ formatDate(row.lastRunAt) }}</span>
            <span v-if="row.lastRunDurationMs != null" style="font-size:11px;color:#909399;margin-left:4px">{{ row.lastRunDurationMs }}ms</span>
          </template>
          <span v-else style="color:#c0c4cc;font-size:12px">—</span>
        </template>
      </el-table-column>
      <el-table-column label="下次执行" width="155">
        <template #default="{ row }">
          <span v-if="row.nextRunAt && row.isEnabled" style="font-size:12px;color:#409eff">{{ formatDate(row.nextRunAt) }}</span>
          <span v-else style="color:#c0c4cc;font-size:12px">—</span>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="250" fixed="right">
        <template #default="{ row }">
          <div class="action-btns">
            <el-button size="small" type="success" plain @click="triggerNow(row)" :loading="triggeringId === row.id">
              <el-icon><CaretRight /></el-icon>执行
            </el-button>
            <el-button size="small" type="primary" plain @click="openDialog(row)">
              <el-icon><Edit /></el-icon>编辑
            </el-button>
            <el-button size="small" type="danger" plain @click="confirmDel(row)">
              <el-icon><Delete /></el-icon>删除
            </el-button>
          </div>
        </template>
      </el-table-column>
    </el-table>

    <!-- 弹窗 -->
    <el-dialog v-model="dialogVisible" :title="editingId ? '编辑计划任务' : '新增计划任务'" width="680px" @closed="resetForm" destroy-on-close>
      <el-tabs v-model="dialogTab" type="card">
        <el-tab-pane label="基本配置" name="basic">
          <el-form :model="form" label-width="90px" size="small">
            <el-form-item label="任务名称" required>
              <el-input v-model="form.name" placeholder="如：定时同步物料数据" maxlength="100" show-word-limit />
            </el-form-item>
            <el-form-item label="描述">
              <el-input v-model="form.description" placeholder="可选说明" maxlength="500" />
            </el-form-item>
            <el-row :gutter="16">
              <el-col :span="14">
                <el-form-item label="执行方式" required>
                  <el-radio-group v-model="form.runType" size="small" @change="onRunTypeChange">
                    <el-radio-button value="task">关联集成任务</el-radio-button>
                    <el-radio-button value="handler">执行器类</el-radio-button>
                    <el-radio-button value="code">自定义代码</el-radio-button>
                  </el-radio-group>
                </el-form-item>
              </el-col>
              <el-col :span="10">
                <el-form-item label="启用">
                  <el-switch v-model="form.isEnabled" />
                </el-form-item>
              </el-col>
            </el-row>

            <!-- 关联集成任务 -->
            <template v-if="form.runType === 'task'">
              <el-form-item label="关联任务" required>
                <el-select v-model="form.integrationTaskId" style="width:100%" placeholder="选择集成任务">
                  <el-option v-for="t in integrationTasks" :key="t.id" :label="t.name" :value="t.id" />
                </el-select>
              </el-form-item>
            </template>

            <!-- 执行器类 -->
            <template v-if="form.runType === 'handler'">
              <el-form-item label="执行器类" required>
                <el-select v-model="form.handlerClass" style="width:100%" placeholder="选择或输入实现 IScheduledTaskHandler 的类全路径" clearable filterable allow-create @change="onHandlerClassChange">
                  <el-option v-for="h in discoveredHandlers" :key="h.fullName" :label="`${h.className} (${h.namespace})`" :value="h.fullName" />
                </el-select>
              </el-form-item>
              <el-form-item label="执行参数">
                <key-value-editor v-model="handlerParameters" add-label="添加参数" />
                <div v-if="currentHandlerParams.length > 0" class="param-hints">
                  <div v-for="p in currentHandlerParams" :key="p.key" class="param-hint-item">
                    <el-icon :size="14"><InfoFilled /></el-icon>
                    <div class="param-hint-text">
                      <code class="param-key">{{ p.key }}</code>
                      <span v-if="p.label" class="param-label"> — {{ p.label }}</span>
                      <span v-if="p.description" class="param-desc">{{ p.description }}</span>
                      <span v-if="p.defaultValue" class="param-default">默认: {{ p.defaultValue }}</span>
                    </div>
                  </div>
                </div>
                <div v-else style="font-size:11px;color:#909399;margin-top:2px">以 Key-Value 传入 handler 的 parameters 字典</div>
              </el-form-item>
            </template>

            <!-- 自定义代码 -->
            <template v-if="form.runType === 'code'">
              <el-form-item label="自定义代码" required>
                <el-input v-model="form.codeHandler" type="textarea" :rows="10" placeholder="// 可直接使用 db (AppDbContext)&#10;// using System.IO;&#10;var count = await db.Users.CountAsync();" style="font-family:Consolas,monospace;font-size:13px" />
              </el-form-item>
            </template>
          </el-form>
        </el-tab-pane>

        <el-tab-pane label="执行规则" name="cron">
          <el-form :model="form" label-width="90px" size="small">
            <el-form-item label="执行模式">
              <el-radio-group v-model="form.runMode" size="small" @change="onRunModeChange">
                <el-radio-button value="cron">定时重复</el-radio-button>
                <el-radio-button value="once">仅执行一次</el-radio-button>
              </el-radio-group>
            </el-form-item>
            <template v-if="form.runMode === 'once'">
              <el-form-item label="执行时间" required>
                <el-date-picker v-model="form.runOnceAt" type="datetime" placeholder="选择执行时间" format="YYYY-MM-DD HH:mm" value-format="YYYY-MM-DDTHH:mm" style="width:260px" :disabled-date="disabledDate" />
                <span style="margin-left:10px;font-size:12px;color:#909399">到达后自动执行并禁用</span>
              </el-form-item>
            </template>
            <template v-if="form.runMode === 'cron'">
              <el-form-item label="快捷预设">
                <div class="preset-group">
                  <el-radio-group v-model="form.cronExpression" @change="onPresetChange" size="small">
                    <el-radio-button v-for="p in cronPresets" :key="p.value" :value="p.value">{{ p.label }}</el-radio-button>
                  </el-radio-group>
                </div>
              </el-form-item>
              <el-form-item label="自定义">
                <div class="custom-cron">
                  <div class="cron-fields">
                    <div class="cron-field"><span class="field-label">分钟</span><el-input v-model="cronMinute" size="small" class="field-input" @input="syncCron" /></div>
                    <div class="cron-field"><span class="field-label">小时</span><el-input v-model="cronHour" size="small" class="field-input" @input="syncCron" /></div>
                    <div class="cron-field"><span class="field-label">日</span><el-input v-model="cronDay" size="small" class="field-input" @input="syncCron" /></div>
                    <div class="cron-field"><span class="field-label">月</span><el-input v-model="cronMonth" size="small" class="field-input" @input="syncCron" /></div>
                    <div class="cron-field"><span class="field-label">周</span><el-input v-model="cronWeek" size="small" class="field-input" @input="syncCron" /></div>
                  </div>
                  <div class="cron-result">
                    <span class="result-label">完整表达式</span>
                    <code class="result-code">{{ form.cronExpression }}</code>
                    <span v-if="cronPreview" class="result-desc">{{ cronPreview }}</span>
                  </div>
                </div>
              </el-form-item>
            </template>
          </el-form>
        </el-tab-pane>

      </el-tabs>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="save" :loading="saving">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, computed, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import KeyValueEditor from '../components/KeyValueEditor.vue'
import {
  getIntegrationTasks,
  getScheduledTasks, createScheduledTask, updateScheduledTask,
  deleteScheduledTask, triggerScheduledTask, discoverScheduledTaskHandlers,
} from '../api/auth'

const tasks = ref([])
const integrationTasks = ref([])
const loading = ref(false)
const dialogVisible = ref(false)
const dialogTab = ref('basic')
const editingId = ref(null)
const saving = ref(false)
const triggeringId = ref(null)
const cronPreview = ref('')

const form = reactive({
  name: '', description: '', integrationTaskId: null,
  runMode: 'cron', cronExpression: '0 0 * * *', runOnceAt: null,
  codeHandler: '', handlerClass: '', handlerParameters: '', isEnabled: true, runType: 'task',
})

const discoveredHandlers = ref([])
const handlerParameters = ref([]) // Key-Value 编辑器绑定

// 当前选中的执行器类的参数元数据
const currentHandlerParams = computed(() => {
  const h = discoveredHandlers.value.find(d => d.fullName === form.handlerClass)
  return h?.parameters || []
})

function onHandlerClassChange(val) {
  if (!val) { handlerParameters.value = []; return }
  const h = discoveredHandlers.value.find(d => d.fullName === val)
  if (h && h.parameters && h.parameters.length > 0) {
    handlerParameters.value = h.parameters.map(p => ({ key: p.key, value: p.defaultValue || '' }))
  } else {
    handlerParameters.value = []
  }
}

const cronMinute = ref('0')
const cronHour = ref('0')
const cronDay = ref('*')
const cronMonth = ref('*')
const cronWeek = ref('*')

function disabledDate(time) { return time.getTime() < Date.now() - 60 * 1000 } // 不能选过去的时间

const cronPresets = [
  { label: '每分钟', value: '* * * * *' },
  { label: '每5分钟', value: '*/5 * * * *' },
  { label: '每15分钟', value: '*/15 * * * *' },
  { label: '每30分钟', value: '*/30 * * * *' },
  { label: '每小时', value: '0 * * * *' },
  { label: '每2小时', value: '0 */2 * * *' },
  { label: '每天0点', value: '0 0 * * *' },
  { label: '每天12点', value: '0 12 * * *' },
  { label: '周一8点', value: '0 8 * * 1' },
  { label: '每月1号', value: '0 0 1 * *' },
  { label: '工作日9点', value: '0 9 * * 1-5' },
]

async function loadAll() {
  loading.value = true
  try {
    const [s, t, h] = await Promise.all([getScheduledTasks(), getIntegrationTasks(), discoverScheduledTaskHandlers()])
    if (s.data.success) tasks.value = s.data.data
    if (t.data.success) integrationTasks.value = t.data.data
    if (h.data.success) discoveredHandlers.value = h.data.data
  } catch { } finally { loading.value = false }
}

function openDialog(row) {
  dialogTab.value = 'basic'
  if (row) {
    editingId.value = row.id
    const mode = row.runMode || (row.runOnceAt ? 'once' : 'cron')
    const onceAt = row.runOnceAt ? new Date(row.runOnceAt).toISOString().slice(0, 16) : null
    // 推断 runType
    let rt = 'task'
    if (row.handlerClass) rt = 'handler'
    else if (row.codeHandler && !row.integrationTaskId) rt = 'code'
    else if (row.integrationTaskId > 0) rt = 'task'

    Object.assign(form, {
      name: row.name, description: row.description || '',
      integrationTaskId: row.integrationTaskId > 0 ? row.integrationTaskId : null,
      runMode: mode, cronExpression: row.cronExpression || '0 0 * * *',
      runOnceAt: onceAt, codeHandler: row.codeHandler || '', handlerClass: row.handlerClass || '',
      handlerParameters: row.handlerParameters || '', isEnabled: row.isEnabled, runType: rt,
    })
    try {
      const obj = JSON.parse(row.handlerParameters || '{}')
      handlerParameters.value = typeof obj === 'object' && !Array.isArray(obj)
        ? Object.entries(obj).map(([key, value]) => ({ key, value: value ?? '' }))
        : []
    } catch { handlerParameters.value = [] }
  } else {
    editingId.value = null
    Object.assign(form, { name: '', description: '', integrationTaskId: null, runMode: 'cron', cronExpression: '0 0 * * *', runOnceAt: null, codeHandler: '', handlerClass: '', handlerParameters: '', isEnabled: true, runType: 'task' })
    handlerParameters.value = []
  }
  syncCronFields()
  updatePreview()
  dialogVisible.value = true
}

function onRunModeChange() {
  if (form.runMode === 'once') form.cronExpression = ''; else if (!form.cronExpression) form.cronExpression = '0 0 * * *'
}

function onRunTypeChange() {
  // 切换执行方式时清空其他方式的值
  if (form.runType === 'task') { form.handlerClass = ''; form.codeHandler = ''; handlerParameters.value = [] }
  else if (form.runType === 'handler') { form.integrationTaskId = null; form.codeHandler = '' }
  else if (form.runType === 'code') { form.integrationTaskId = null; form.handlerClass = ''; handlerParameters.value = [] }
}

function resetForm() { editingId.value = null }

function syncCron() {
  form.cronExpression = [cronMinute.value || '*', cronHour.value || '*', cronDay.value || '*', cronMonth.value || '*', cronWeek.value || '*'].join(' ')
  updatePreview()
}
function syncCronFields() {
  const parts = (form.cronExpression || '').split(/\s+/)
  cronMinute.value = parts[0] || '*'
  cronHour.value = parts[1] || '*'
  cronDay.value = parts[2] || '*'
  cronMonth.value = parts[3] || '*'
  cronWeek.value = parts[4] || '*'
  updatePreview()
}
function onPresetChange() { syncCronFields(); updatePreview() }
function updatePreview() {
  const preset = cronPresets.find(p => p.value === form.cronExpression)
  cronPreview.value = preset ? preset.label : ''
}

async function save() {
  if (!form.name) { ElMessage.warning('请输入任务名称'); return }
  if (form.runType === 'task' && !form.integrationTaskId) { ElMessage.warning('请选择关联任务'); return }
  if (form.runType === 'handler' && !form.handlerClass) { ElMessage.warning('请选择执行器类'); return }
  if (form.runType === 'code' && !form.codeHandler) { ElMessage.warning('请输入自定义代码'); return }
  saving.value = true
  // 根据 runType 清理无关字段
  const paramsJson = handlerParameters.value.filter(h => h.key).length > 0
    ? JSON.stringify(Object.fromEntries(handlerParameters.value.filter(h => h.key).map(h => [h.key, h.value || ''])))
    : ''
  const data = {
    ...form,
    handlerParameters: paramsJson,
    integrationTaskId: form.runType === 'task' ? (form.integrationTaskId || 0) : 0,
    handlerClass: form.runType === 'handler' ? (form.handlerClass || '') : '',
    codeHandler: form.runType === 'code' ? (form.codeHandler || '') : '',
  }
  try {
    if (editingId.value) {
      await updateScheduledTask(editingId.value, data)
      ElMessage.success('已更新')
    } else {
      await createScheduledTask(data)
      ElMessage.success('已创建')
    }
    dialogVisible.value = false
    await loadAll()
  } catch { ElMessage.error('保存失败') } finally { saving.value = false }
}

async function toggleEnabled(row) {
  try {
    await updateScheduledTask(row.id, {
      name: row.name, description: row.description || '', integrationTaskId: row.integrationTaskId,
      cronExpression: row.cronExpression, isEnabled: row.isEnabled,
    })
  } catch { row.isEnabled = !row.isEnabled; ElMessage.error('操作失败') }
}

async function triggerNow(row) {
  triggeringId.value = row.id
  try {
    const r = await triggerScheduledTask(row.id)
    ElMessage[r.data.success ? 'success' : 'error'](r.data.success ? '执行成功' : (r.data.data?.message || '执行失败'))
    await loadAll()
  } catch { ElMessage.error('执行失败') } finally { triggeringId.value = null }
}

async function confirmDel(row) {
  try { await ElMessageBox.confirm(`确定删除计划任务「${row.name}」？`, '确认删除', { type: 'warning', confirmButtonText: '删除', cancelButtonText: '取消' }) }
  catch { return }
  await deleteScheduledTask(row.id); ElMessage.success('已删除'); await loadAll()
}
function formatDate(s) { if (!s) return ''; const d = new Date(s); if (isNaN(d.getTime())) return s; return d.toLocaleString() }
onMounted(loadAll)
</script>

<style scoped>
.schedule-mgmt { background: #fff; border-radius: 6px; padding: 20px; min-height: calc(100vh - 160px); }
.toolbar { display: flex; align-items: center; gap: 12px; margin-bottom: 18px; }
.action-btns { display: flex; gap: 6px; flex-wrap: nowrap; }
.cron-cell { display: flex; flex-direction: column; gap: 2px; }
.cron-code { background: #f0f2f5; padding: 1px 6px; border-radius: 3px; font-size: 11px; font-family: Consolas, monospace; display: inline-block; max-width: fit-content; }
.cron-desc { font-size: 11px; color: #409eff; white-space: nowrap; }

/* Cron 预设 */
.preset-group { width: 100%; }
.preset-group :deep(.el-radio-group) { display: flex; flex-wrap: wrap; gap: 8px; }

/* Cron 自定义 */
.custom-cron { background: #fafafa; border: 1px solid #ebeef5; border-radius: 6px; padding: 16px; }
.cron-fields { display: flex; gap: 12px; margin-bottom: 12px; }
.cron-field { flex: 1; text-align: center; }
.field-label { display: block; font-size: 12px; color: #909399; margin-bottom: 4px; font-weight: 500; }
.field-input :deep(input) { text-align: center; font-family: Consolas, monospace; }
.cron-result { display: flex; align-items: center; gap: 10px; padding-top: 10px; border-top: 1px dashed #d9d9d9; }
.result-label { font-size: 12px; color: #909399; flex-shrink: 0; }
.result-code { font-family: Consolas, monospace; font-size: 14px; color: #303133; background: #e6f7ff; padding: 2px 10px; border-radius: 4px; }
.result-desc { font-size: 12px; color: #67c23a; font-weight: 500; }

/* 参数说明提示 */
.param-hints { margin-top: 8px; padding: 10px 12px; background: #f5f7fa; border-radius: 6px; border: 1px solid #e4e7ed; }
.param-hint-item { display: flex; gap: 8px; margin-bottom: 6px; color: #606266; }
.param-hint-item:last-child { margin-bottom: 0; }
.param-hint-text { font-size: 12px; line-height: 1.6; }
.param-key { font-family: Consolas, monospace; font-size: 12px; background: #e6f7ff; padding: 0 4px; border-radius: 3px; color: #1890ff; }
.param-label { font-weight: 500; color: #303133; }
.param-desc { color: #909399; display: block; }
.param-default { color: #67c23a; font-family: Consolas, monospace; font-size: 11px; }
</style>
