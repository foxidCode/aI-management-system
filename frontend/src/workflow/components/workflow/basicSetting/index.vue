<template>
		<section class="basic-setting-page">
			<div class="basic-setting-wrap">
				<el-form ref="formRef" :model="basicForm" :rules="rules" label-position="right" label-width="90px"
					class="setting-form">
					<el-form-item label="流程编号" prop="flowCode">
						<el-input v-model="basicForm.flowCode" placeholder="留空则自动生成" maxlength="30" />
						<span class="form-tip">留空则发布时自动生成</span>
					</el-form-item>

					<el-form-item label="流程名称" prop="name" required>
						<el-input v-model="basicForm.name" placeholder="请输入流程名称" maxlength="30" />
					</el-form-item>

					<el-form-item label="流程说明" prop="remark">
						<el-input v-model="basicForm.remark" type="textarea" :rows="4" maxlength="100" show-word-limit
							placeholder="请输入流程说明" />
					</el-form-item>
				</el-form>
			</div>
		</section>
	</template>

	<script setup>
	import { reactive, ref, watch } from 'vue'
	import { getRandomUniqueCode } from '@/workflow/utils/workflow/commonUtils'
	let props = defineProps({
		data: {
			type: Object,
			default: () => ({})
		}
	});
	const formRef = ref(null)
	const basicForm = reactive({
		name: '',
		key: '',
		flowCode: '',
		remark: '',
	})
	watch(() => props.data, (value) => {
		// 只取需要的字段，忽略 initNode 中的历史包袱
		Object.assign(basicForm, {
			name: value.name || '',
			key: `BIZ_${getRandomUniqueCode()}`,
			flowCode: value.flowCode || '',
			remark: value.remark || value.Remark || '',
		})
	}, { immediate: true, deep: true })
	const rules = {
		name: [{ required: true, message: '请输入流程名称', trigger: 'blur' }]
	}

	const getData = () => {
		return new Promise((resolve, reject) => {
			formRef.value?.validate((valid) => {
				if (!valid) return reject(new Error('请完善基础设置'))
				// 流程编号为空时自动生成
				if (!basicForm.flowCode) {
					basicForm.flowCode = `WF-${new Date().toISOString().slice(0, 10).replace(/-/g, '')}-${getRandomUniqueCode().slice(0, 6)}`
				}
				resolve({
					formData: {
						name: basicForm.name,
						key: basicForm.key,
						flowCode: basicForm.flowCode,
						remark: basicForm.remark,
					}
				})
			})
		})
	}

	defineExpose({ getData })
	</script>

	<style scoped>
	.basic-setting-page {
		min-height: 100%;
		padding: 0 64px;
	}

	.basic-setting-wrap {
		min-height: calc(100vh - 74px);
		max-width: 760px;
		min-width: 320px;
		margin: 0 auto;
		padding: 26px 64px;
		background: #fff;
		box-sizing: border-box;
	}

	.setting-form {
		max-width: 520px;
		margin: 0 auto;
	}

	.form-tip {
		font-size: 12px;
		color: #909399;
		margin-top: 4px;
		display: block;
	}

	:deep(.el-form-item) {
		margin-bottom: 16px;
	}

	:deep(.el-form-item__label) {
		color: #4f5b6f;
		font-size: 15px;
	}

	:deep(.el-input__wrapper),
	:deep(.el-textarea__inner),
	:deep(.el-select__wrapper) {
		background: #ffffff;
		box-shadow: 0 0 0 1px #d9dde5 inset;
		border-radius: 2px;
	}

	:deep(.el-textarea .el-input__count) {
		background: transparent;
		right: 8px;
		color: #7d8797;
	}

	@media (max-width: 900px) {
		.basic-setting-page {
			padding: 0 10px;
		}

		.basic-setting-wrap {
			padding: 16px 12px;
		}

		.setting-form {
			max-width: 100%;
		}
	}
	</style>
