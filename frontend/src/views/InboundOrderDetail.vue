<template>
  <div class="inbound-detail">
    <el-page-header @back="$router.push('/dashboard/inbound')" title="返回列表" style="margin-bottom:20px">
      <template #content>
        <span class="page-title">{{ hasId ? (editing ? '编辑入库单' : '查看入库单') : '新增入库单' }}</span>
      </template>
    </el-page-header>

    <!-- 入库单基本信息 -->
    <el-card shadow="never" style="margin-bottom:20px">
      <template #header>
        <div class="card-header">
          <span class="card-title">基本信息</span>
          <div class="header-actions">
            <el-button v-if="hasId && !editing" type="primary" :disabled="!canManage" @click="startEdit">编辑</el-button>
            <el-button v-if="editing" type="primary" :loading="saveLoading" @click="handleSave">保存入库单</el-button>
            <el-button v-if="hasId && editing" @click="cancelEdit">取消</el-button>
          </div>
        </div>
      </template>
      <el-form ref="formRef" :model="form" :rules="formRules" label-width="100px">
        <el-row :gutter="20">
          <el-col :span="8">
            <el-form-item label="单据编码">
              <el-input :model-value="hasId ? orderCode : '自动生成'" disabled />
            </el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="创建人">
              <el-input :model-value="createdByName" disabled />
            </el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="创建日期">
              <el-input :model-value="formatDate(createdAt)" disabled />
            </el-form-item>
          </el-col>
        </el-row>
        <el-row :gutter="20">
          <el-col :span="8">
            <el-form-item label="库房名称">
              <el-input v-model="form.warehouseName" placeholder="库房名称" :disabled="!editing" />
            </el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="供应商">
              <el-input v-model="form.supplier" placeholder="供应商" :disabled="!editing" />
            </el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="合同">
              <el-input v-model="form.contract" placeholder="合同编号" :disabled="!editing" />
            </el-form-item>
          </el-col>
        </el-row>
        <el-row :gutter="20">
          <el-col :span="8">
            <el-form-item label="税率(%)">
              <el-input-number v-model="form.taxRate" :min="0" :max="100" :precision="2" :controls="false" :disabled="!editing" style="width:100%" />
            </el-form-item>
          </el-col>
        </el-row>
        <!-- 金额汇总（始终显示） -->
        <el-row :gutter="20">
          <el-col :span="8">
            <el-form-item label="含税金额">
              <el-input :model-value="fmt(totalTax)" disabled />
            </el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="计成本金额">
              <el-input :model-value="fmt(totalCost)" disabled />
            </el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="税额">
              <el-input :model-value="fmt(totalTaxAmount)" disabled />
            </el-form-item>
          </el-col>
        </el-row>
        <!-- 备注（末尾，占满行） -->
        <el-row>
          <el-col :span="24">
            <el-form-item label="备注">
              <el-input v-model="form.remark" type="textarea" :rows="3" :disabled="!editing" />
            </el-form-item>
          </el-col>
        </el-row>
      </el-form>
    </el-card>

    <!-- 材料明细（始终显示） -->
    <el-card shadow="never">
      <template #header>
        <div class="card-header">
          <span class="card-title">材料明细</span>
          <div class="header-actions">
            <el-button type="primary" size="small" :disabled="!editing" @click="openAddDialog">新增材料</el-button>
            <el-button type="danger" size="small" :disabled="!editing || selectedDetailIds.length === 0" @click="handleBatchDelete">批量删除</el-button>
            <el-button size="small" @click="exportDetails">导出</el-button>
            <el-button size="small" :disabled="!editing" @click="triggerImport">导入</el-button>
            <input ref="importFileRef" type="file" accept=".csv,.xls,.xlsx" style="display:none" @change="handleImportFile" />
          </div>
        </div>
      </template>

      <el-table ref="detailTableRef" :data="displayedDetails" stripe border v-loading="detailLoading" max-height="500"
        style="width:100%" show-summary :summary-method="getSummaries"
        @selection-change="handleDetailSelectionChange">
        <el-table-column type="selection" width="50" v-if="editing" />
        <el-table-column type="index" width="70" label="序号" />
        <el-table-column prop="materialCode" label="材料编码" width="120" />
        <el-table-column prop="materialName" label="材料名称" width="150" show-overflow-tooltip />
        <el-table-column prop="specification" label="规格" width="90" />
        <el-table-column prop="model" label="型号" width="90" />
        <el-table-column prop="unit" label="单位" width="65" />
        <el-table-column label="入库数量" width="120">
          <template #default="{ row }">
            <el-input-number v-if="editing" v-model="row.quantity" :min="0" :precision="4" :controls="false" size="small" style="width:100%" @change="onDetailChange(row)" />
            <span v-else>{{ row.quantity }}</span>
          </template>
        </el-table-column>
        <el-table-column label="单价" width="110">
          <template #default="{ row }">
            <el-input-number v-if="editing" v-model="row.unitPrice" :min="0" :precision="4" :controls="false" size="small" style="width:100%" @change="onDetailChange(row)" />
            <span v-else>{{ row.unitPrice.toFixed(4) }}</span>
          </template>
        </el-table-column>
        <el-table-column label="含税金额" width="120">
          <template #default="{ row }">{{ row.taxIncludedAmount.toFixed(2) }}</template>
        </el-table-column>
        <el-table-column label="税额" width="100">
          <template #default="{ row }">{{ row.taxAmount.toFixed(2) }}</template>
        </el-table-column>
        <el-table-column label="计成本金额" width="120">
          <template #default="{ row }">{{ row.costAmount.toFixed(2) }}</template>
        </el-table-column>
        <el-table-column label="税率(%)" width="85">
          <template #default="{ row }">
            <el-input-number v-if="editing" v-model="row.taxRate" :min="0" :max="100" :precision="2" :controls="false" size="small" style="width:100%" @change="onDetailChange(row)" />
            <span v-else>{{ row.taxRate }}</span>
          </template>
        </el-table-column>
        <el-table-column prop="remark" label="备注" min-width="100" show-overflow-tooltip />
        <el-table-column label="操作" width="80" fixed="right" v-if="editing">
          <template #default="{ row }">
            <el-button size="small" type="danger" @click="handleDeleteDetail(row)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>

      <div class="pagination-wrapper" v-if="displayedDetailsTotal > detailPageSize">
        <span class="total-info">共 {{ displayedDetailsTotal }} 条明细</span>
        <el-pagination v-model:current-page="detailPage" v-model:page-size="detailPageSize"
          :page-sizes="[10, 20, 50, 100]" :total="displayedDetailsTotal"
          layout="sizes,total,prev,pager,next" background small />
      </div>
    </el-card>

    <!-- 附件明细 -->
    <el-card shadow="never" style="margin-top:20px" v-if="hasId">
      <template #header>
        <div class="card-header">
          <span class="card-title">附件明细</span>
          <el-upload
            multiple
            :show-file-list="false"
            :http-request="handleUpload"
            :disabled="!editing"
            accept="*"
          >
            <el-button type="primary" size="small" :disabled="!editing">上传附件</el-button>
          </el-upload>
        </div>
      </template>

      <el-table :data="attachments" stripe border v-loading="attLoading" max-height="300" style="width:100%">
        <el-table-column type="index" width="60" label="#" />
        <el-table-column prop="fileName" label="文件名" min-width="200" show-overflow-tooltip />
        <el-table-column label="文件大小" width="100">
          <template #default="{ row }">{{ formatFileSize(row.fileSize) }}</template>
        </el-table-column>
        <el-table-column label="上传时间" width="170">
          <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
        </el-table-column>
        <el-table-column label="操作" width="160" fixed="right">
          <template #default="{ row }">
            <el-button size="small" type="primary" @click="handleDownload(row)">下载</el-button>
            <el-button size="small" type="danger" :disabled="!editing" @click="handleDeleteAtt(row)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>

      <div class="pagination-wrapper" v-if="attTotal > attPageSize">
        <span class="total-info">共 {{ attTotal }} 个附件</span>
        <el-pagination v-model:current-page="attPage" :page-size="attPageSize" :total="attTotal"
          layout="total,prev,pager,next" background small @current-change="fetchAttachments" />
      </div>
    </el-card>

    <!-- 选择材料弹窗（纯多选） -->
    <el-dialog v-model="addDialog" title="选择材料" width="650px" :close-on-click-modal="false">
      <el-input v-model="materialKw" placeholder="搜索材料编码/名称" clearable style="margin-bottom:12px"
        @keyup.enter="searchMaterials" @clear="searchMaterials">
        <template #prefix><el-icon><Search /></el-icon></template>
      </el-input>
      <el-table ref="matTableRef" :data="materials" stripe border max-height="350"
        @selection-change="onMatSelectionChange" v-loading="matLoading">
        <el-table-column type="selection" width="50" />
        <el-table-column prop="code" label="编码" width="120" />
        <el-table-column prop="name" label="名称" width="180" show-overflow-tooltip />
        <el-table-column prop="specification" label="规格" width="100" />
        <el-table-column prop="model" label="型号" width="100" />
        <el-table-column prop="unit" label="单位" width="70" />
      </el-table>
      <el-pagination v-if="matTotal > matPageSize" v-model:current-page="matPage" v-model:page-size="matPageSize"
        :page-sizes="[10, 20, 50,100,300,500]" :total="matTotal"
        layout="sizes,prev,pager,next" background small style="margin-top:10px;justify-content:center"
        @size-change="searchMaterials" @current-change="searchMaterials" />
      <span style="color:#999;font-size:13px">已选 {{ selectedMaterials.length }} 种材料</span>
      <template #footer>
        <el-button @click="addDialog = false">取消</el-button>
        <el-button type="primary" :disabled="selectedMaterials.length === 0" @click="confirmAddMaterials">
          确认添加（{{ selectedMaterials.length }}）
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted, computed, nextTick } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import {
  getInboundOrderById, createInboundOrder, updateInboundOrder,
  getInboundDetails, syncInboundDetails,
  getMaterials,
  getAttachments, uploadAttachments, deleteAttachment,
  api,
} from '../api/auth'

const route = useRoute()
const router = useRouter()

// 权限
const perms = JSON.parse(localStorage.getItem('permissions') || '[]')
const canManage = computed(() => perms.includes('inbound:manage'))

const orderId = ref(null)
const hasId = computed(() => orderId.value != null)
const editing = ref(false)
const orderCode = ref('')
const createdByName = ref('')
const createdAt = ref('')
const saveLoading = ref(false)

const form = reactive({ warehouseName: '', supplier: '', contract: '', taxRate: 0, remark: '' })
const beforeEditForm = {}

function fmt(v) { return (v || 0).toFixed(2) }

function formatDate(dateStr) {
  if (!dateStr) return ''
  const d = new Date(dateStr)
  if (isNaN(d.getTime())) return dateStr
  const pad = n => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`
}

const totalTax = ref(0)
const totalCost = ref(0)
const totalTaxAmount = ref(0)

// ===== 材料明细（纯前端操作，保存时统一提交） =====
const details = ref([])           // 全部明细（本地）
const selectedDetailIds = ref([])
const detailLoading = ref(false)
const detailPage = ref(1)
const detailPageSize = ref(20)
let nextLocalId = -1               // 新增材料的临时负数 ID

// 本地计算单条金额
function recalcRow(row) {
  const taxIncluded = (row.quantity || 0) * (row.unitPrice || 0)
  const rate = (row.taxRate || 0) > 0 ? (row.taxRate || 0) / 100 : 0
  row.taxIncludedAmount = taxIncluded
  row.taxAmount = rate > 0 ? Math.round(taxIncluded * rate / (1 + rate) * 100) / 100 : 0
  row.costAmount = taxIncluded - row.taxAmount
}

// 本地重算汇总
function recalcTotals() {
  totalTax.value = details.value.reduce((s, r) => s + (r.taxIncludedAmount || 0), 0)
  totalTaxAmount.value = details.value.reduce((s, r) => s + (r.taxAmount || 0), 0)
  totalCost.value = details.value.reduce((s, r) => s + (r.costAmount || 0), 0)
}

// 客户端分页
const displayedDetails = computed(() => {
  if (!details.value) return []
  const start = (detailPage.value - 1) * detailPageSize.value
  return details.value.slice(start, start + detailPageSize.value)
})

// 表格数据绑定到 computed
const displayedDetailsTotal = computed(() => details.value.length)

// 合计行
function getSummaries({ columns, data }) {
  const sums = []
  columns.forEach((col, idx) => {
    if (idx === 0) { sums[idx] = ''; return }
    if (idx === 1) { sums[idx] = '合计'; return }
    const label = col.label
    if (label === '入库数量') sums[idx] = data.reduce((s, r) => s + (r.quantity || 0), 0).toFixed(4)
    else if (label === '含税金额') sums[idx] = data.reduce((s, r) => s + (r.taxIncludedAmount || 0), 0).toFixed(2)
    else if (label === '税额') sums[idx] = data.reduce((s, r) => s + (r.taxAmount || 0), 0).toFixed(2)
    else if (label === '计成本金额') sums[idx] = data.reduce((s, r) => s + (r.costAmount || 0), 0).toFixed(2)
    else sums[idx] = ''
  })
  return sums
}

function handleDetailSelectionChange(selection) {
  selectedDetailIds.value = selection.map(r => r.id)
}

// 加载全部明细（仅查看模式从服务端加载）
async function fetchAllDetails() {
  if (!orderId.value) return
  detailLoading.value = true
  try {
    // 一次性加载全部明细
    const res = await getInboundDetails(orderId.value, { pageSize: 0 })
    if (res.data.success) { details.value = res.data.data; recalcTotals() }
  } catch { ElMessage.error('获取明细失败') } finally { detailLoading.value = false }
}

// 内联编辑：修改数量/单价/税率后本地重算
function onDetailChange(row) {
  recalcRow(row)
  recalcTotals()
}

// 删除明细（本地）
function handleDeleteDetail(row) {
  details.value = details.value.filter(r => r.id !== row.id)
  selectedDetailIds.value = selectedDetailIds.value.filter(id => id !== row.id)
  recalcTotals()
}

// 批量删除（本地）
function handleBatchDelete() {
  const count = selectedDetailIds.value.length
  if (count === 0) return
  ElMessageBox.confirm(`确定删除选中的 ${count} 条材料吗？`, '批量删除', { type: 'warning' })
    .then(() => {
      const ids = new Set(selectedDetailIds.value)
      details.value = details.value.filter(r => !ids.has(r.id))
      selectedDetailIds.value = []
      recalcTotals()
      ElMessage.success(`已删除 ${count} 条材料`)
    })
    .catch(() => {})
}

// ===== 加载/保存入库单 =====
async function loadOrder() {
  if (!orderId.value) return
  try {
    const res = await getInboundOrderById(orderId.value)
    if (res.data.success) {
      const o = res.data.data
      orderCode.value = o.orderCode
      createdByName.value = o.createdByName || ''
      createdAt.value = o.createdAt || ''
      totalTax.value = o.totalTaxIncludedAmount
      totalCost.value = o.totalCostAmount
      totalTaxAmount.value = o.totalTaxAmount
      // 仅在非编辑状态（查看模式或首次加载）时覆盖表单字段，避免冲掉用户正在编辑的值
      if (!editing.value) {
        form.warehouseName = o.warehouseName || ''
        form.supplier = o.supplier || ''
        form.contract = o.contract || ''
        form.taxRate = o.taxRate
        form.remark = o.remark || ''
      }
    }
  } catch { ElMessage.error('获取入库单失败') }
}

function startEdit() {
  Object.assign(beforeEditForm, { ...form })
  editing.value = true
}

function cancelEdit() {
  Object.assign(form, beforeEditForm)
  editing.value = false
  // 重新加载明细，丢弃本地修改
  fetchAllDetails()
}

async function handleSave() {
  saveLoading.value = true
  try {
    const data = {
      warehouseName: form.warehouseName || null,
      supplier: form.supplier || null,
      contract: form.contract || null,
      taxRate: form.taxRate || 0,
      remark: form.remark || null,
    }
    if (!hasId.value) {
      ElMessage.error('请等待草稿创建完成')
      return
    }
    // 先更新基本信息
    await updateInboundOrder(orderId.value, data)
    // 再同步全部明细
    const detailPayload = details.value.map(r => ({
      materialCode: r.materialCode, materialName: r.materialName,
      specification: r.specification || '', model: r.model || '', unit: r.unit || '',
      quantity: r.quantity || 0, unitPrice: r.unitPrice || 0,
      taxRate: r.taxRate || 0, remark: r.remark || '',
    }))
    await syncInboundDetails(orderId.value, detailPayload)
    ElMessage.success('保存成功')
    editing.value = false
    await loadOrder()
    // 重新加载明细以获取服务器端 ID
    await fetchAllDetails()
  } catch (err) { ElMessage.error(err.response?.data?.message || '保存失败') }
  finally { saveLoading.value = false }
}

// ===== 新增材料弹窗（纯多选） =====
const addDialog = ref(false)
const materialKw = ref('')
const materials = ref([])
const matTableRef = ref(null)
const selectedMaterials = ref([])
const matLoading = ref(false)
const matPage = ref(1)
const matPageSize = ref(10)
const matTotal = ref(0)

function onMatSelectionChange(selection) {
  selectedMaterials.value = selection
}

async function searchMaterials() {
  matLoading.value = true
  try {
    const res = await getMaterials({ keyword: materialKw.value || undefined, page: matPage.value, pageSize: matPageSize.value })
    if (res.data.success) { materials.value = res.data.data; matTotal.value = res.data.total }
  } catch { ElMessage.error('搜索材料失败') } finally { matLoading.value = false }
}

function openAddDialog() {
  selectedMaterials.value = []
  materialKw.value = ''; matPage.value = 1; matPageSize.value = 10
  searchMaterials()
  addDialog.value = true
}

function confirmAddMaterials() {
  if (selectedMaterials.value.length === 0) return
  for (const m of selectedMaterials.value) {
    details.value.push({
      id: nextLocalId--,
      materialCode: m.code || '', materialName: m.name || '',
      specification: m.specification || '', model: m.model || '', unit: m.unit || '',
      quantity: 0, unitPrice: 0, taxIncludedAmount: 0, taxAmount: 0, costAmount: 0,
      taxRate: form.taxRate || 0, remark: '',
    })
  }
  recalcTotals()
  addDialog.value = false
  ElMessage.success(`已添加 ${selectedMaterials.value.length} 种材料`)
}

// ===== 导出/导入明细 =====
import * as XLSX from 'xlsx'

const importFileRef = ref(null)

function triggerImport() {
  importFileRef.value?.click()
}

function exportDetails() {
  if (details.value.length === 0) { ElMessage.warning('没有可导出的明细'); return }
  const data = details.value.map(r => ({
    '材料编码': r.materialCode, '材料名称': r.materialName,
    '规格': r.specification || '', '型号': r.model || '', '单位': r.unit || '',
    '入库数量': r.quantity, '单价': r.unitPrice,
    '含税金额': r.taxIncludedAmount, '税额': r.taxAmount, '计成本金额': r.costAmount,
    '税率(%)': r.taxRate || 0, '备注': r.remark || '',
  }))
  const ws = XLSX.utils.json_to_sheet(data)
  ws['!cols'] = [
    { wch: 14 }, { wch: 20 }, { wch: 12 }, { wch: 12 }, { wch: 8 },
    { wch: 12 }, { wch: 12 }, { wch: 14 }, { wch: 14 }, { wch: 14 },
    { wch: 10 }, { wch: 16 },
  ]
  const wb = XLSX.utils.book_new()
  XLSX.utils.book_append_sheet(wb, ws, '材料明细')
  XLSX.writeFile(wb, `材料明细_${orderCode.value || 'export'}_${new Date().toISOString().slice(0, 10)}.xlsx`)
  ElMessage.success(`已导出 ${details.value.length} 条明细`)
}

function handleImportFile(e) {
  const file = e.target.files?.[0]
  if (!file) return
  const reader = new FileReader()
  reader.onload = (ev) => {
    try {
      const data = new Uint8Array(ev.target.result)
      const wb = XLSX.read(data, { type: 'array' })
      const ws = wb.Sheets[wb.SheetNames[0]]
      const rows = XLSX.utils.sheet_to_json(ws, { header: 1 })
      if (rows.length < 2) { ElMessage.warning('文件内容为空'); return }
      let added = 0
      for (let i = 1; i < rows.length; i++) {
        const cols = rows[i]
        if (!cols || cols.length < 2) continue
        const qty = parseFloat(cols[5]) || 0
        const price = parseFloat(cols[6]) || 0
        const taxRate = parseFloat(cols[7]) || 0
        const taxIncluded = qty * price
        const rate = taxRate > 0 ? taxRate / 100 : 0
        const taxAmount = rate > 0 ? Math.round(taxIncluded * rate / (1 + rate) * 100) / 100 : 0
        details.value.push({
          id: nextLocalId--,
          materialCode: String(cols[0] || ''), materialName: String(cols[1] || ''),
          specification: String(cols[2] || ''), model: String(cols[3] || ''), unit: String(cols[4] || ''),
          quantity: qty, unitPrice: price,
          taxIncludedAmount: taxIncluded, taxAmount: taxAmount, costAmount: taxIncluded - taxAmount,
          taxRate: taxRate, remark: String(cols[8] || ''),
        })
        added++
      }
      if (added > 0) { recalcTotals(); ElMessage.success(`已导入 ${added} 条明细`) }
      else ElMessage.warning('未识别到有效数据')
    } catch { ElMessage.error('文件解析失败，请检查文件格式') }
  }
  reader.readAsArrayBuffer(file)
  e.target.value = ''
}

// ===== 附件 =====
const attachments = ref([])
const attLoading = ref(false)
const attPage = ref(1)
const attPageSize = ref(10)
const attTotal = ref(0)

function formatFileSize(bytes) {
  if (!bytes) return '0 B'
  const units = ['B', 'KB', 'MB', 'GB']
  let i = 0
  let size = bytes
  while (size >= 1024 && i < units.length - 1) { size /= 1024; i++ }
  return size.toFixed(i > 0 ? 1 : 0) + ' ' + units[i]
}

async function fetchAttachments() {
  if (!orderId.value) return
  attLoading.value = true
  try {
    const res = await getAttachments(orderId.value, { page: attPage.value, pageSize: attPageSize.value })
    if (res.data.success) { attachments.value = res.data.data; attTotal.value = res.data.total }
  } catch { ElMessage.error('获取附件列表失败') } finally { attLoading.value = false }
}

async function handleUpload({ file }) {
  const formData = new FormData()
  formData.append('files', file)
  try {
    await uploadAttachments(orderId.value, formData)
    ElMessage.success(`"${file.name}" 上传成功`)
    attPage.value = 1
    fetchAttachments()
  } catch (err) {
    ElMessage.error(err.response?.data?.message || '上传失败')
  }
}

async function handleDownload(row) {
  try {
    const res = await api.get(`/inboundorder/attachments/${row.id}/download`, { responseType: 'blob' })
    const url = URL.createObjectURL(res.data)
    const a = document.createElement('a')
    a.href = url
    a.download = row.fileName
    a.click()
    URL.revokeObjectURL(url)
  } catch {
    ElMessage.error('下载失败')
  }
}

async function handleDeleteAtt(row) {
  try {
    await ElMessageBox.confirm(`确定删除附件 "${row.fileName}" 吗？`, '删除确认', { type: 'warning' })
    await deleteAttachment(row.id)
    ElMessage.success('删除成功')
    fetchAttachments()
  } catch { /* 取消 */ }
}

onMounted(async () => {
  const idFromRoute = route.params.id
  if (idFromRoute) {
    orderId.value = parseInt(idFromRoute)
    editing.value = false
    await loadOrder()
    fetchAllDetails()
    fetchAttachments()
  } else {
    // 新增模式：自动创建草稿，以便立即添加材料明细
    editing.value = true
    try {
      const res = await createInboundOrder({ warehouseName: null, supplier: null, contract: null, taxRate: 0, remark: null, details: [] })
      orderId.value = res.data.data.id
      orderCode.value = res.data.data.orderCode
      createdAt.value = res.data.data.createdAt || ''
    } catch (err) {
      ElMessage.error('创建草稿失败: ' + (err.response?.data?.message || err.message))
    }
  }
})
</script>

<style scoped>
.inbound-detail { background:#fff;padding:20px;border-radius:4px;min-height:calc(100vh - 120px); }
.page-title { font-size:18px;font-weight:600;color:#303133; }
.card-title { font-weight:600; }
.card-header { display:flex;align-items:center;justify-content:space-between; }
.header-actions { display:flex;gap:8px; }
.pagination-wrapper { display:flex;align-items:center;justify-content:space-between;margin-top:12px; }
.total-info { font-size:13px;color:#666; }
</style>
