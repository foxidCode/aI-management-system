<template>
		<div class="workflow-shell">
			<header class="top-nav">
				<div class="nav-left">
					<el-button text class="back-btn" @click="handleBack">
						<el-icon><ArrowLeft /></el-icon>
					</el-button>
					<span class="process-title">{{ pageTitle }}</span>
				</div>

				<div class="nav-steps" :class="`step-${currentStep}`" role="list" aria-label="流程步骤">
					<div class="step-active-bg" aria-hidden="true"></div>
					<div v-for="(step, index) in steps" :key="step.value" class="step-item"
						:class="{ active: currentStep === index }" @click="currentStep = index">
						<span class="step-index">{{ index + 1 }}</span>
						<span class="step-text">{{ step.label }}</span>
					</div>
				</div>

				<div class="nav-right">
					<el-button class="publish-btn" @click="handlePublish">发布</el-button>
				</div>
			</header>

			<section class="content-panel">
				<div v-show="currentStep === 0" class="step-page">
					<BasicSetting ref="basicSettingRef" v-if="basicSettingDataConf" :data="basicSettingDataConf" />
				</div>
				<div v-show="currentStep === 1" class="step-page">
					<DynamicForm ref="dynamicFormRef" v-if="dynamicFormDataConf" :lfFormData="dynamicFormDataConf" />
				</div>
				<div v-show="currentStep === 2" class="step-page">
					<FlowDesign ref="flowDesignRef" v-if="nodesDataConf" :data="nodesDataConf" />
				</div>
			</section>

			<ErrorDialog v-model:visible="publishErrorVisible" :items="publishErrors" @edit="handlePublishEdit" />
		</div>
	</template>

	<script setup>
	import { ref, computed, onMounted } from 'vue'
	import { ElMessage } from 'element-plus'
	import { useRouter } from 'vue-router'
	import { ArrowLeft } from '@element-plus/icons-vue'
	import BasicSetting from "./basicSetting/index.vue";
	import DynamicForm from "./dynamicForm/index.vue";
	import FlowDesign from "./flowDesign/index.vue";
	import ErrorDialog from "./flowDesign/drawer/dialog/errorDialog.vue";
	import { FormatDisplayUtils } from '@/workflow/utils/workflow/formatDisplayData';
	import { NodeUtils } from "@/workflow/utils/workflow/nodeUtils";
	import { getWorkFlowData, saveDefinition } from "@/workflow/api/workflow.js";

	const props = defineProps({ definitionId: { type: Number, default: null } })
	const router = useRouter()

	const pageTitle = computed(() => props.definitionId ? '编辑流程' : '新建流程')

	const steps = [
		{ value: 'basic', label: '基础设置' },
		{ value: 'form', label: '表单设计' },
		{ value: 'flow', label: '流程设计' }
	]

	const currentStep = ref(0)
	const basicSettingRef = ref(null)
	const dynamicFormRef = ref(null)
	const flowDesignRef = ref(null)
	const basicSettingDataConf = ref(null)
	const dynamicFormDataConf = ref(null)
	const nodesDataConf = ref(null)

	onMounted(async () => {
		await init();
	});

	const init = async () => {
		if (props.definitionId) {
			try {
				const res = await getWorkFlowData(props.definitionId)
				const defData = res.data.data
				let nodes = defData.nodes
				if (typeof nodes === 'string') { try { nodes = JSON.parse(nodes) } catch(e) {} }
				const workflowResult = FormatDisplayUtils.getToTree({ ...defData, nodes: nodes || [] })
				const { nodeConfig, frmValue, ...restData } = workflowResult
				nodesDataConf.value = nodeConfig
				dynamicFormDataConf.value = frmValue
				basicSettingDataConf.value = restData
			} catch(e) {
				ElMessage.error('加载流程定义失败，使用空白模板')
				const nodeDemo = NodeUtils.initNode()
				const fallback = FormatDisplayUtils.getToTree(nodeDemo)
				const { nodeConfig: nc, frmValue: fv, ...rd } = fallback
				nodesDataConf.value = nc
				dynamicFormDataConf.value = fv
				basicSettingDataConf.value = rd
			}
			return
		}
		const nodeDemo = NodeUtils.initNode()
		const workflowResult = FormatDisplayUtils.getToTree(nodeDemo)
		const { nodeConfig, frmValue, ...restData } = workflowResult
		nodesDataConf.value = nodeConfig
		dynamicFormDataConf.value = frmValue
		basicSettingDataConf.value = restData
	};

	const publish = () => {
		const step1 = basicSettingRef.value.getData();
		const step2 = dynamicFormRef.value.getData();
		const step3 = flowDesignRef.value.getData();
		Promise.all([step1, step2, step3])
			.then((res) => {
				const basicData = res[0].formData;
				const dynamicFormData = res[1].formData;
				const flowDesignData = res[2].formData;
				Object.assign(basicData, {
					frmType: 1,
					frmValue: JSON.stringify(dynamicFormData),
					id: props.definitionId,
					nodes: JSON.stringify(flowDesignData.map(item => {
						return { ...item, definitionKey: basicData.key || '' }
					}))
				});
				return saveDefinition(basicData);
			})
			.then(() => {
				ElMessage.success('流程发布成功');
				router.push('/dashboard/workflow/definitions');
			})
			.catch((err) => {
				const msg = err?.response?.data?.message || err?.message || '至少配置一个有效审批人节点';
				ElMessage.error(msg);
			});
	};

	const publishErrorVisible = ref(false)
	const publishErrors = ref([])
	const handlePublish = () => {
		const errors = flowDesignRef.value?.validatePublish() || []
		if (errors.length > 0) {
			publishErrors.value = errors
			publishErrorVisible.value = true
			return
		}
		publish();
	}

	const handlePublishEdit = () => {
		currentStep.value = 2
	}

	const handleBack = () => {
		router.push('/dashboard/workflow/definitions')
	}
	</script>

	<style scoped>
	.workflow-shell {
		min-height: 100vh;
		background: linear-gradient(180deg, #eef4ff 0%, #f9fbff 56%, #ffffff 100%);
	}

	.top-nav {
		position: sticky;
		top: 0;
		z-index: 30;
		height: 65px;
		display: grid;
		grid-template-columns: minmax(160px, 260px) minmax(420px, 1fr) auto;
		align-items: center;
		gap: 16px;
		padding: 0 18px;
		background: linear-gradient(90deg, #2d82da 0%, #3f97eb 100%);
		box-shadow: 0 6px 16px rgba(34, 101, 189, 0.24);
		overflow-x: auto;
		overflow-y: hidden;
	}

	.nav-left {
		display: flex;
		align-items: center;
		gap: 6px;
		color: #fff;
		white-space: nowrap;
	}

	.back-btn { color: #d6e9ff; padding: 6px; }

	.process-title {
		font-size: 18px;
		font-weight: 600;
		letter-spacing: 0.5px;
		max-width: 200px;
		overflow: hidden;
		text-overflow: ellipsis;
		white-space: nowrap;
	}

	.nav-steps {
		position: relative;
		width: min(480px, 100%);
		min-width: 420px;
		height: 65px;
		margin: 0 auto;
		display: flex;
		justify-content: space-between;
		align-items: stretch;
		overflow: hidden;
	}

	.step-active-bg {
		position: absolute;
		left: 0; top: 0;
		width: calc(100% / 3);
		height: 100%;
		background: rgba(76, 118, 230, 0.36);
		transition: transform 0.28s cubic-bezier(0.2, 0.8, 0.2, 1);
	}

	.nav-steps.step-1 .step-active-bg { transform: translateX(100%); }
	.nav-steps.step-2 .step-active-bg { transform: translateX(200%); }

	.step-active-bg::after {
		content: '';
		position: absolute;
		left: 50%; bottom: -1px;
		transform: translateX(-50%);
		width: 0; height: 0;
		border-left: 7px solid transparent;
		border-right: 7px solid transparent;
		border-bottom: 8px solid #dfe7f6;
	}

	.step-item {
		position: relative; z-index: 1;
		flex: 1;
		display: flex; flex-direction: row;
		align-items: center; justify-content: center;
		gap: 6px;
		cursor: pointer;
		transition: color 0.22s;
	}

	.step-index {
		width: 24px; height: 24px;
		border-radius: 50%;
		border: 1px solid #ffffff;
		color: #fff;
		display: inline-flex;
		align-items: center; justify-content: center;
		font-size: 14px; line-height: 1;
		background: rgba(255, 255, 255, 0.08);
		transition: background-color 0.22s;
	}

	.step-text {
		color: #fff;
		font-size: 25px;
		transform: scale(0.56);
		transform-origin: left center;
		font-weight: 500;
		line-height: 1;
		white-space: nowrap;
		transition: opacity 0.22s;
	}

	.step-item.active .step-index { background: rgba(255, 255, 255, 0.24); }

	.nav-right {
		display: flex;
		align-items: center;
		justify-content: flex-end;
		gap: 10px;
		white-space: nowrap;
	}

	.publish-btn {
		min-width: 82px;
		color: #2f87df;
		background: #f4f8ff;
		border: none;
		border-radius: 6px;
		font-weight: 600;
	}

	.content-panel { position: relative; }
	.step-page { width: 100%; }

	@media (max-width: 1100px) {
		.top-nav { height: 56px; grid-template-columns: minmax(120px, 200px) minmax(360px, 1fr) auto; gap: 10px; padding: 0 12px; }
		.nav-left { min-width: 0; }
		.nav-right { justify-content: flex-end; gap: 8px; }
		.process-title { font-size: 16px; max-width: 160px; }
		.nav-steps { width: min(520px, 100%); min-width: 360px; height: 56px; border-radius: 4px; overflow: hidden; }
		.step-active-bg::after { bottom: -1px; }
		.step-text { font-size: 25px; transform: scale(0.52); }
	}

	@media (max-width: 860px) {
		.top-nav { height: 56px; row-gap: 6px; overflow: hidden; }
		.brand-btn { display: none; }
		.publish-btn { min-width: 72px; height: 30px; padding: 0 14px; }
		.nav-steps { height: 56px; }
		.step-item { gap: 4px; }
		.step-index { width: 20px; height: 20px; font-size: 12px; }
		.step-text { font-size: 25px; transform: scale(0.5); }
	}

	@media (max-width: 560px) {
		.top-nav { overflow: hidden; }
		.back-btn { padding: 4px; }
		.nav-steps { height: 56px; }
		.step-text { font-size: 25px; }
	}
	</style>
