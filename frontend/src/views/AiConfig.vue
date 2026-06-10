<template>
  <div class="ai-config-page">
    <el-card>
      <template #header>
        <div class="page-header">
          <span class="page-title">AI 模型配置</span>
          <el-button type="primary" :icon="Plus" @click="openDialog()">添加模型</el-button>
        </div>
      </template>

      <el-table :data="configs" stripe v-loading="loading">
        <el-table-column prop="name" label="名称" min-width="140" />
        <el-table-column prop="provider" label="提供商" width="100">
          <template #default="{ row }">
            <el-tag size="small" :type="row.provider === 'anthropic' ? 'warning' : 'primary'">
              {{ row.provider === 'anthropic' ? 'Anthropic' : 'OpenAI兼容' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="modelName" label="模型" min-width="160">
          <template #default="{ row }">
            <code>{{ row.modelName }}</code>
          </template>
        </el-table-column>
        <el-table-column prop="apiEndpoint" label="API 地址" min-width="200" show-overflow-tooltip />
        <el-table-column prop="apiKey" label="API Key" width="140">
          <template #default="{ row }">
            <span class="key-text">{{ row.apiKey }}</span>
          </template>
        </el-table-column>
        <el-table-column prop="isActive" label="状态" width="80" align="center">
          <template #default="{ row }">
            <el-tag size="small" :type="row.isActive ? 'success' : 'info'">
              {{ row.isActive ? '启用' : '停用' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="200" fixed="right">
          <template #default="{ row }">
            <el-button link type="primary" size="small" @click="openDialog(row)">编辑</el-button>
            <el-button link type="primary" size="small" @click="handleTest(row)">测试</el-button>
            <el-popconfirm title="确定删除？" @confirm="handleDelete(row.id)">
              <template #reference>
                <el-button link type="danger" size="small">删除</el-button>
              </template>
            </el-popconfirm>
          </template>
        </el-table-column>
      </el-table>
      <el-empty v-if="!loading && configs.length === 0" description="暂无模型配置" />
    </el-card>

    <!-- 编辑弹窗 -->
    <el-dialog
      v-model="dialogVisible"
      :title="isEdit ? '编辑模型' : '添加模型'"
      width="560px"
      :close-on-click-modal="false"
    >
      <el-form :model="form" label-width="100px" label-position="left">
        <el-form-item label="名称" required>
          <el-input v-model="form.name" placeholder="如：DeepSeek V3、ChatGPT 4o" />
        </el-form-item>
        <el-form-item label="提供商" required>
          <el-select v-model="form.provider" style="width: 100%">
            <el-option label="OpenAI 兼容 (DeepSeek/ChatGPT等)" value="openai" />
            <el-option label="Anthropic (Claude)" value="anthropic" />
          </el-select>
        </el-form-item>
        <el-form-item label="API 地址" required>
          <el-input v-model="form.apiEndpoint" placeholder="https://api.deepseek.com" />
          <div class="form-hint">如 https://api.deepseek.com 或 https://api.openai.com</div>
        </el-form-item>
        <el-form-item label="API Key" required>
          <el-input
            v-model="form.apiKey"
            :type="isEdit ? 'text' : 'password'"
            :show-password="!isEdit"
            :disabled="isEdit"
            :placeholder="isEdit ? '' : 'sk-...'"
          />
          <div class="form-hint">
            {{ isEdit ? 'API Key 仅支持添加时输入，不支持修改查看' : '输入后不支持修改和查看，请妥善保管' }}
          </div>
        </el-form-item>
        <el-form-item label="模型名称" required>
          <el-input v-model="form.modelName" placeholder="deepseek-chat / gpt-4o / claude-sonnet-4-6" />
        </el-form-item>
        <el-form-item>
          <template #label>
            <el-tooltip content="模型单次响应的最大 Token 数，也代表上下文窗口大小。默认 128K（131072），值越大可处理的内容越多，但成本越高" placement="top" raw-content>
              <span>最大Token <el-icon :size="14" style="vertical-align: middle; color: #909399"><QuestionFilled /></el-icon></span>
            </el-tooltip>
          </template>
          <el-input-number v-model="form.maxTokens" :min="256" :max="131072" :step="1024" />
        </el-form-item>
        <el-form-item>
          <template #label>
            <el-tooltip content="控制输出随机性。0=确定性强、输出固定；1=较高的随机性；2=最大随机性。设为 0.7 是推荐的平衡值" placement="top" raw-content>
              <span>温度 <el-icon :size="14" style="vertical-align: middle; color: #909399"><QuestionFilled /></el-icon></span>
            </el-tooltip>
          </template>
          <el-slider v-model="form.temperature" :min="0" :max="2" :step="0.1" show-input style="width: 280px" />
        </el-form-item>
        <el-form-item label="启用">
          <el-switch v-model="form.isActive" />
          <span class="switch-hint">同时只能启用一个模型</span>
        </el-form-item>
        <el-form-item label="提示词">
          <el-input v-model="form.systemPrompt" type="textarea" :rows="6"
            placeholder="定义 AI 助手的角色、行为和知识范围。例如：&#10;&#10;你是本管理系统的 AI 助手，专门帮助用户熟悉和使用系统功能。你应该：&#10;1. 用中文回答问题，保持专业、清晰、友好&#10;2. 优先参考知识库中的系统文档回答&#10;3. 如不确定答案，诚实告知并建议联系管理员&#10;4. 回答时尽量给出具体的操作步骤和菜单位置" />
          <div class="form-hint">系统提示词（System Prompt），决定 AI 的角色定位、回答风格和知识边界</div>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="handleSave" :loading="saving">保存</el-button>
      </template>
    </el-dialog>

    <!-- 测试结果弹窗 -->
    <el-dialog v-model="testVisible" title="测试连接" width="420px">
      <div v-if="testing" style="text-align: center; padding: 20px">
        <el-icon :size="32" class="is-loading"><Loading /></el-icon>
        <p style="margin-top: 12px">正在测试连接...</p>
      </div>
      <el-alert v-else :title="testResult.message" :type="testResult.success ? 'success' : 'error'" :closable="false" />
      <template #footer>
        <el-button @click="testVisible = false">关闭</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { Plus, Loading, QuestionFilled } from '@element-plus/icons-vue'
import {
  getAiConfigs, getAiConfigById, createAiConfig, updateAiConfig,
  deleteAiConfig, testAiConfig
} from '../api/ai'

const configs = ref([])
const loading = ref(false)

// Dialog
const dialogVisible = ref(false)
const isEdit = ref(false)
const editingId = ref(null)
const saving = ref(false)
const form = ref(getDefaultForm())

function getDefaultForm() {
  return {
    name: '',
    provider: 'openai',
    apiEndpoint: '',
    apiKey: '',
    modelName: '',
    maxTokens: 131072,
    temperature: 0.7,
    isActive: false,
    systemPrompt: ''
  }
}

// Test
const testVisible = ref(false)
const testing = ref(false)
const testResult = ref({ success: false, message: '' })

// ========== Data ==========
async function fetchData() {
  loading.value = true
  try {
    const res = await getAiConfigs()
    if (res.data.success) configs.value = res.data.data
  } catch { /* ignore */ }
  loading.value = false
}

// ========== Dialog ==========
async function openDialog(row) {
  if (row) {
    isEdit.value = true
    editingId.value = row.id
    try {
      const res = await getAiConfigById(row.id)
      if (res.data.success) {
        const d = res.data.data
        form.value = {
          name: d.name,
          provider: d.provider,
          apiEndpoint: d.apiEndpoint,
          apiKey: d.apiKey,
          modelName: d.modelName,
          maxTokens: d.maxTokens,
          temperature: d.temperature,
          isActive: d.isActive,
          systemPrompt: d.systemPrompt || ''
        }
      }
    } catch { ElMessage.error('获取详情失败') }
  } else {
    isEdit.value = false
    editingId.value = null
    form.value = getDefaultForm()
  }
  dialogVisible.value = true
}

async function handleSave() {
  if (!form.value.name || !form.value.apiEndpoint || !form.value.apiKey || !form.value.modelName) {
    ElMessage.warning('请填写名称、API地址、Key和模型名称')
    return
  }
  saving.value = true
  try {
    if (isEdit.value) {
      await updateAiConfig(editingId.value, form.value)
      ElMessage.success('更新成功')
    } else {
      await createAiConfig(form.value)
      ElMessage.success('添加成功')
    }
    dialogVisible.value = false
    await fetchData()
  } catch (err) {
    ElMessage.error(err.response?.data?.message || '操作失败')
  }
  saving.value = false
}

async function handleDelete(id) {
  try {
    await deleteAiConfig(id)
    ElMessage.success('已删除')
    await fetchData()
  } catch {
    ElMessage.error('删除失败')
  }
}

async function handleTest(row) {
  testVisible.value = true
  testing.value = true
  try {
    const res = await testAiConfig({
      configId: row.id,
      provider: row.provider,
      apiEndpoint: row.apiEndpoint,
      apiKey: row.apiKey,
      modelName: row.modelName
    })
    testResult.value = res.data
  } catch {
    testResult.value = { success: false, message: '测试请求失败' }
  }
  testing.value = false
}

onMounted(fetchData)
</script>

<style scoped>
.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.page-title {
  font-size: 16px;
  font-weight: 600;
}
.key-text {
  font-family: monospace;
  font-size: 12px;
  color: #909399;
}
.form-hint {
  font-size: 12px;
  color: #909399;
  margin-top: 4px;
}
.switch-hint {
  font-size: 12px;
  color: #909399;
  margin-left: 8px;
}
</style>
