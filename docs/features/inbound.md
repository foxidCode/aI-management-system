# 入库单管理

## 功能

完整的采购入库管理流程，支持材料明细、附件上传、金额自动计算、CSV 导出。

## 数据模型

```
InboundOrder (主表)
  ├── OrderCode: RK-YYYYMMDD-NNNN (自动生成)
  ├── WarehouseName, Supplier, Contract
  ├── TotalTaxIncludedAmount (含税合计)
  ├── TotalCostAmount (计成本合计)
  ├── TotalTaxAmount (税额合计)
  ├── Status: draft / pending_approval / approved / rejected
  └── Details[] (明细列表)

InboundOrderDetail (明细)
  ├── MaterialCode, MaterialName
  ├── Quantity × UnitPrice = TaxIncludedAmount
  ├── TaxAmount = TaxIncluded × Rate / (1 + Rate)
  └── CostAmount = TaxIncluded - TaxAmount
```

## API 端点

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/inboundorder` | 列表（搜索、排序、分页） |
| GET | `/api/inboundorder/{id}` | 详情 |
| POST | `/api/inboundorder` | 创建（含明细） |
| PUT | `/api/inboundorder/{id}` | 更新 |
| DELETE | `/api/inboundorder/{id}` | 软删除 |
| POST | `/api/inboundorder/{id}/submit` | 提交审批 |
| PUT | `/api/inboundorder/{id}/details/sync` | 同步明细 |
| GET | `/api/inboundorder/export` | CSV 导出 |
| GET | `/api/inboundorder/daily-stats` | 日统计 |

## 附件管理

支持上传附件到 MinIO 对象存储，与入库单关联。
