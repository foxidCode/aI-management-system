# 入库单管理

## 功能

完整的采购入库管理流程，支持材料明细、附件上传、金额自动计算、CSV 导出、审批提交。

## 数据模型

```
InboundOrder (主表)
  ├── OrderCode: RK-YYYYMMDD-NNNN (自动生成)
  ├── WarehouseName, Supplier, Contract
  ├── TotalTaxIncludedAmount (含税合计)
  ├── TotalCostAmount (计成本合计)
  ├── TotalTaxAmount (税额合计)
  ├── TaxRate (税率，小数形式)
  ├── Status: draft / pending_approval / approved / rejected
  ├── WorkflowInstanceId (关联流程实例)
  ├── IsDeleted (软删除)
  └── Details[] (明细列表)

InboundOrderDetail (明细)
  ├── MaterialCode, MaterialName, Specification, Model, Unit
  ├── Quantity × UnitPrice = TaxIncludedAmount
  ├── TaxAmount = TaxIncluded × TaxRate / (1 + TaxRate)
  └── CostAmount = TaxIncluded - TaxAmount
```

## 金额计算

明细行填写数量和含税单价后，系统自动计算：

- **含税金额** = 数量 × 含税单价
- **税额** = 含税金额 × 税率 ÷ (1 + 税率)
- **计成本金额** = 含税金额 − 税额

主表金额为所有明细行金额的合计。

## 订单编码

格式：`RK-YYYYMMDD-NNNN`

基于当天数据库记录数自动递增，确保同一天内编码唯一。

## 材料字典集成

明细行可从材料字典快速选择，自动填充材料编码、名称、规格、型号、单位，减少手动输入。

## API 端点

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/inboundorder` | 列表（搜索、排序、分页） |
| GET | `/api/inboundorder/{id}` | 详情（含明细） |
| POST | `/api/inboundorder` | 创建（含明细） |
| PUT | `/api/inboundorder/{id}` | 更新 |
| DELETE | `/api/inboundorder/{id}` | 软删除 |
| POST | `/api/inboundorder/{id}/submit` | 提交审批 |
| PUT | `/api/inboundorder/{id}/details/sync` | 同步明细（批量替换） |
| GET | `/api/inboundorder/{id}/attachments` | 获取附件列表 |
| POST | `/api/inboundorder/{id}/upload` | 上传附件 |
| DELETE | `/api/inboundorder/{id}/attachments/{attId}` | 删除附件 |
| GET | `/api/inboundorder/export` | CSV 导出 |
| GET | `/api/inboundorder/daily-stats` | 日金额统计 |
| GET | `/api/material` | 材料字典列表 |
| POST | `/api/material` | 创建材料 |
| PUT | `/api/material/{id}` | 更新材料 |
| DELETE | `/api/material/{id}` | 删除材料 |
| POST | `/api/material/batch-delete` | 批量删除材料 |

## 附件管理

支持上传附件到 MinIO 对象存储，与入库单关联：

- 多文件上传
- 文件信息存入 `InboundOrderAttachments` 表
- MinIO ObjectKey 用于下载和删除
- 支持在详情页预览和下载

## 审批集成

1. 入库单编辑完成后点击"提交审批"
2. 状态变为 `pending_approval`
3. 自动创建流程实例，关联 `WorkflowInstanceId`
4. 审批人可在"待办任务"中查看并审批
5. 通过 → `approved`，驳回 → `rejected`（可重新编辑提交）

## CSV 导出

- 导出当前筛选/搜索结果
- 包含主表所有字段和明细汇总
- 使用 xlsx 库生成，支持 Excel 打开

## 前端页面

| 路径 | 页面 | 功能 |
|------|------|------|
| `/dashboard/inbound` | 入库单列表 | 搜索/排序/分页，CRUD，提交审批 |
| `/dashboard/inbound/detail/:id?` | 入库单详情 | 查看明细，附件管理 |
| `/dashboard/materials` | 材料字典 | 材料 CRUD，批量操作 |
