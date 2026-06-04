<template>
  <div class="db-management">
    <!-- 左侧面板 -->
    <div class="left-panel">
      <div class="panel-header">
        <span><el-icon><Coin /></el-icon> 数据库连接</span>
        <el-button size="small" type="primary" @click="openConnDialog(null)"><el-icon><Plus /></el-icon></el-button>
      </div>

      <!-- 连接列表 -->
      <div class="conn-list">
        <div
          v-for="conn in connections"
          :key="conn.id"
          class="conn-item"
          :class="{ active: activeConn?.id === conn.id }"
          @click="selectConnection(conn)"
        >
          <div class="conn-info">
            <el-icon color="#409EFF"><component :is="dbIcon(conn.dbType)" /></el-icon>
            <span class="conn-name">{{ conn.name }}</span>
            <span class="conn-type">[{{ conn.dbType }}]</span>
          </div>
          <div class="conn-actions">
            <el-button size="small" text @click.stop="openConnDialog(conn)"><el-icon><Edit /></el-icon></el-button>
            <el-popconfirm title="确定删除此连接？" @confirm="handleDeleteConn(conn.id)" @click.stop>
              <template #reference>
                <el-button size="small" text type="danger"><el-icon><Delete /></el-icon></el-button>
              </template>
            </el-popconfirm>
          </div>
        </div>
        <el-empty v-if="connections.length === 0" description="暂无连接" :image-size="60" />
      </div>

      <!-- 表树 -->
      <div v-if="activeConn" class="table-tree-section">
        <div class="panel-header" style="border-top:1px solid #ebeef5">
          <span><el-icon><Grid /></el-icon> 数据表 ({{ filteredTables.length }})</span>
          <el-button size="small" text @click="loadTables" :loading="tableLoading"><el-icon><RefreshRight /></el-icon></el-button>
        </div>
        <div class="table-search" v-if="tables.length > 0">
          <el-input v-model="tableSearch" placeholder="搜索表名..." size="small" clearable>
            <template #prefix><el-icon><Search /></el-icon></template>
          </el-input>
        </div>
        <div class="table-list" v-loading="tableLoading">
          <div
            v-for="t in filteredTables"
            :key="t.tableName"
            class="table-item"
            :class="{ active: activeTable === t.tableName }"
            @click="selectTable(t)"
          >
            <el-icon><Document /></el-icon>
            <span>{{ t.tableName }}</span>
            <span v-if="t.tableComment" class="table-comment">{{ t.tableComment }}</span>
          </div>
          <el-empty v-if="!tableLoading && tables.length === 0" description="暂无表" :image-size="40" />
        </div>
      </div>
    </div>

    <!-- 右侧面板 -->
    <div class="right-panel">
      <el-tabs v-model="activeTab" type="card">
        <el-tab-pane label="表结构" name="schema" :disabled="!activeTable">
          <div v-if="activeTable" class="schema-view">
            <div class="section-title">{{ activeTable }}
              <span v-if="activeTableComment" style="color:#909399;font-size:13px;margin-left:8px">— {{ activeTableComment }}</span>
            </div>
            <el-table :data="columns" stripe border size="small" style="width:100%">
              <el-table-column prop="columnName" label="字段名" width="180" />
              <el-table-column prop="dataType" label="数据类型" width="130" />
              <el-table-column label="允许空" width="80">
                <template #default="{ row }">{{ row.isNullable ? '是' : '否' }}</template>
              </el-table-column>
              <el-table-column label="主键" width="80">
                <template #default="{ row }">
                  <el-tag v-if="row.isPrimaryKey" size="small" type="warning">PK</el-tag>
                </template>
              </el-table-column>
              <el-table-column prop="defaultValue" label="默认值" width="150" show-overflow-tooltip />
              <el-table-column prop="maxLength" label="最大长度" width="90" />
              <el-table-column prop="comment" label="注释" min-width="180" show-overflow-tooltip />
            </el-table>
          </div>
          <el-empty v-else description="请先选择一个表" :image-size="80" />
        </el-tab-pane>

        <el-tab-pane label="SQL 查询" name="sql" :disabled="!activeConn">
          <div class="sql-view">
            <div class="sql-toolbar">
              <el-button type="primary" @click="executeSql" :loading="sqlRunning">
                <el-icon><CaretRight /></el-icon> 执行
              </el-button>
              <el-button @click="sqlText = ''">清空</el-button>
              <span v-if="sqlResult" class="sql-info">
                {{ sqlResult.rowCount }} 行，耗时 {{ sqlResult.elapsedMs }} ms
              </span>
            </div>
            <el-input
              v-model="sqlText"
              type="textarea"
              :rows="6"
              placeholder="SELECT * FROM table_name LIMIT 10"
              class="sql-editor"
            />
            <div v-if="sqlError" class="sql-error">
              <el-alert :title="sqlError" type="error" :closable="false" />
            </div>
            <div v-if="sqlResult" class="sql-results">
              <el-table :data="sqlResult.rows" stripe border size="small" max-height="400" style="width:100%">
                <el-table-column
                  v-for="col in sqlResult.columns"
                  :key="col"
                  :prop="col"
                  :label="col"
                  min-width="140"
                  show-overflow-tooltip
                />
              </el-table>
            </div>
          </div>
        </el-tab-pane>
      </el-tabs>
    </div>

    <!-- 连接配置对话框 -->
    <el-dialog
      v-model="connDialogVisible"
      :title="editingConn ? '编辑连接' : '新增连接'"
      width="560px"
      :close-on-click-modal="false"
    >
      <el-form ref="connFormRef" :model="connForm" :rules="connRules" label-width="90px">
        <el-form-item label="连接名称" prop="name">
          <el-input v-model="connForm.name" placeholder="如：生产数据库" />
        </el-form-item>
        <el-form-item label="数据库类型" prop="dbType">
          <el-select v-model="connForm.dbType" placeholder="选择数据库类型" style="width:100%" @change="onDbTypeChange">
            <el-option label="MySQL" value="MySQL" />
            <el-option label="SQL Server" value="SqlServer" />
            <el-option label="SQLite" value="SQLite" />
            <el-option label="Oracle" value="Oracle" />
            <el-option label="PostgreSQL" value="PostgreSQL" />
          </el-select>
        </el-form-item>
        <template v-if="connForm.dbType !== 'SQLite'">
          <el-form-item label="主机地址" prop="host">
            <el-input v-model="connForm.host" placeholder="localhost" />
          </el-form-item>
          <el-form-item label="端口" prop="port">
            <el-input-number v-model="connForm.port" :min="1" :max="65535" style="width:100%" />
          </el-form-item>
          <el-form-item label="用户名">
            <el-input v-model="connForm.username" placeholder="用户名" />
          </el-form-item>
          <el-form-item label="密码">
            <el-input v-model="connForm.password" type="password" placeholder="密码（留空不修改）" show-password />
          </el-form-item>
        </template>
        <el-form-item v-if="connForm.dbType === 'SQLite'" label="文件路径" prop="databaseName">
          <el-input v-model="connForm.databaseName" placeholder="如：C:\data\mydb.db" />
        </el-form-item>
        <el-form-item v-else label="数据库名" prop="databaseName">
          <el-input v-model="connForm.databaseName" placeholder="数据库名" />
        </el-form-item>
        <el-form-item label="额外参数">
          <el-input v-model="connForm.extraParams" placeholder="额外的连接字符串参数（可选）" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="connDialogVisible = false">取消</el-button>
        <el-button @click="testConnection" :loading="testLoading">测试连接</el-button>
        <el-button type="primary" @click="saveConnection" :loading="saveLoading">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, computed, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import {
  getDbConnections, createDbConnection, updateDbConnection, deleteDbConnection,
  testDbConnection, getDbTables, getDbTableSchema, executeDbSql,
} from '../api/auth'

// 数据库图标映射
const dbIcon = type => ({ MySQL: 'Orange', SqlServer: 'Platform', SQLite: 'Document', Oracle: 'Key', PostgreSQL: 'Stamp' }[type] || 'Coin')

const connections = ref([])
const activeConn = ref(null)
const tables = ref([])
const tableSearch = ref('')
const filteredTables = computed(() => {
  if (!tableSearch.value) return tables.value
  const kw = tableSearch.value.toLowerCase()
  return tables.value.filter(t =>
    t.tableName.toLowerCase().includes(kw) ||
    (t.tableComment && t.tableComment.toLowerCase().includes(kw))
  )
})
const tableLoading = ref(false)
const activeTable = ref('')
const activeTableComment = ref('')
const columns = ref([])
const activeTab = ref('sql')

// SQL 查询
const sqlText = ref('')
const sqlRunning = ref(false)
const sqlResult = ref(null)
const sqlError = ref('')

// 连接对话框
const connDialogVisible = ref(false)
const editingConn = ref(null)
const connFormRef = ref(null)
const saveLoading = ref(false)
const testLoading = ref(false)

const connForm = reactive({
  name: '', dbType: '', host: '', port: 3306, databaseName: '', username: '', password: '', extraParams: '',
})

const connRules = {
  name: [{ required: true, message: '请输入连接名称', trigger: 'blur' }],
  dbType: [{ required: true, message: '请选择数据库类型', trigger: 'change' }],
  host: [{ required: true, message: '请输入主机地址', trigger: 'blur' }],
  port: [{ required: true, message: '请输入端口', trigger: 'blur' }],
  databaseName: [{ required: true, message: '此项必填', trigger: 'blur' }],
}

function onDbTypeChange(type) {
  const defaults = { MySQL: 3306, SqlServer: 1433, SQLite: 0, Oracle: 1521, PostgreSQL: 5432 }
  connForm.port = defaults[type] || 3306
}

async function loadConnections() {
  try {
    const res = await getDbConnections()
    if (res.data.success) connections.value = res.data.data
  } catch { /* ignore */ }
}

async function selectConnection(conn) {
  activeConn.value = conn
  activeTable.value = ''
  columns.value = []
  sqlResult.value = null
  sqlError.value = ''
  await loadTables()
}

async function loadTables() {
  if (!activeConn.value) return
  tableLoading.value = true
  try {
    const res = await getDbTables(activeConn.value.id)
    if (res.data.success) tables.value = res.data.data
    else ElMessage.error(res.data.message)
  } catch { ElMessage.error('获取表列表失败') }
  finally { tableLoading.value = false }
}

async function selectTable(t) {
  activeTable.value = t.tableName
  activeTableComment.value = t.tableComment || ''
  activeTab.value = 'schema'
  try {
    const res = await getDbTableSchema(activeConn.value.id, t.tableName)
    if (res.data.success) columns.value = res.data.data
    else ElMessage.error(res.data.message)
  } catch { ElMessage.error('获取表结构失败') }
}

async function executeSql() {
  if (!sqlText.value.trim()) { ElMessage.warning('请输入SQL语句'); return }
  sqlRunning.value = true
  sqlError.value = ''
  sqlResult.value = null
  try {
    const res = await executeDbSql(activeConn.value.id, sqlText.value)
    if (res.data.success) { sqlResult.value = res.data.data; sqlError.value = '' }
    else sqlError.value = res.data.message
  } catch (err) { sqlError.value = err.response?.data?.message || '执行失败' }
  finally { sqlRunning.value = false }
}

function openConnDialog(conn) {
  editingConn.value = conn
  if (conn) {
    connForm.name = conn.name; connForm.dbType = conn.dbType
    connForm.host = conn.host || ''; connForm.port = conn.port || 0
    connForm.databaseName = conn.databaseName || ''; connForm.username = conn.username || ''
    connForm.password = ''; connForm.extraParams = conn.extraParams || ''
  } else {
    connForm.name = ''; connForm.dbType = ''; connForm.host = ''; connForm.port = 3306
    connForm.databaseName = ''; connForm.username = ''; connForm.password = ''; connForm.extraParams = ''
  }
  connFormRef.value?.clearValidate()
  connDialogVisible.value = true
}

async function testConnection() {
  const valid = await connFormRef.value.validate().catch(() => false)
  if (!valid) return
  testLoading.value = true
  try {
    const res = await testDbConnection(connForm)
    if (res.data.success) ElMessage.success('连接成功！')
    else ElMessage.error(res.data.message)
  } catch { ElMessage.error('测试连接失败') }
  finally { testLoading.value = false }
}

async function saveConnection() {
  const valid = await connFormRef.value.validate().catch(() => false)
  if (!valid) return
  saveLoading.value = true
  try {
    if (editingConn.value) {
      const res = await updateDbConnection(editingConn.value.id, connForm)
      if (res.data.success) { ElMessage.success('连接已更新'); connDialogVisible.value = false; await loadConnections() }
      else ElMessage.error(res.data.message)
    } else {
      const res = await createDbConnection(connForm)
      if (res.data.success) { ElMessage.success('连接已创建'); connDialogVisible.value = false; await loadConnections() }
      else ElMessage.error(res.data.message)
    }
  } catch { ElMessage.error('保存失败') }
  finally { saveLoading.value = false }
}

async function handleDeleteConn(id) {
  try {
    const res = await deleteDbConnection(id)
    if (res.data.success) { ElMessage.success('连接已删除'); if (activeConn.value?.id === id) { activeConn.value = null; tables.value = [] } await loadConnections() }
    else ElMessage.error(res.data.message)
  } catch { ElMessage.error('删除失败') }
}

onMounted(loadConnections)
</script>

<style scoped>
.db-management { display: flex; height: calc(100vh - 120px); gap: 0; background: #fff; border-radius: 4px; overflow: hidden; }
.left-panel { width: 280px; min-width: 260px; border-right: 1px solid #ebeef5; display: flex; flex-direction: column; background: #fafafa; }
.panel-header { display: flex; align-items: center; justify-content: space-between; padding: 10px 12px; font-size: 14px; font-weight: 600; color: #303133; gap: 8px; }
.conn-list { flex: 0 0 auto; max-height: 35%; overflow-y: auto; }
.conn-item { display: flex; align-items: center; justify-content: space-between; padding: 8px 12px; cursor: pointer; border-bottom: 1px solid #f0f0f0; transition: background 0.2s; }
.conn-item:hover { background: #ecf5ff; }
.conn-item.active { background: #d9ecff; }
.conn-info { display: flex; align-items: center; gap: 6px; font-size: 13px; overflow: hidden; }
.conn-name { font-weight: 500; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.conn-type { font-size: 11px; color: #909399; flex-shrink: 0; }
.conn-actions { display: flex; flex-shrink: 0; }
.table-tree-section { flex: 1; display: flex; flex-direction: column; overflow: hidden; }
.table-search { padding: 6px 10px; }
.table-list { flex: 1; overflow-y: auto; padding: 4px 0; }
.table-item { display: flex; align-items: center; gap: 6px; padding: 6px 12px 6px 24px; cursor: pointer; font-size: 13px; transition: background 0.2s; }
.table-item:hover { background: #ecf5ff; }
.table-item.active { background: #d9ecff; color: #409EFF; }
.table-comment { font-size: 11px; color: #909399; margin-left: auto; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; max-width: 100px; }
.right-panel { flex: 1; overflow: hidden; display: flex; flex-direction: column; }
.right-panel :deep(.el-tabs) { height: 100%; display: flex; flex-direction: column; }
.right-panel :deep(.el-tabs__content) { flex: 1; overflow-y: auto; padding: 12px; }
.section-title { font-size: 16px; font-weight: 600; margin-bottom: 12px; color: #303133; }
.schema-view { padding: 4px; }
.sql-view { padding: 4px; }
.sql-toolbar { display: flex; align-items: center; gap: 10px; margin-bottom: 10px; }
.sql-info { font-size: 13px; color: #67c23a; margin-left: 12px; }
.sql-editor { font-family: 'Consolas', 'Courier New', monospace; font-size: 13px; margin-bottom: 12px; }
.sql-error { margin-bottom: 12px; }
.sql-results { margin-top: 12px; }
</style>
