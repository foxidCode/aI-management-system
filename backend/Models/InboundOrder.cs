using System.ComponentModel.DataAnnotations;

namespace backend.Models;

/// <summary>
/// 入库单
/// </summary>
public class InboundOrder
{
    [Key]
    public int Id { get; set; }

    /// <summary>单据编码（自动生成，如 RK-20260531-001）</summary>
    [Required, MaxLength(30)]
    public string OrderCode { get; set; } = string.Empty;

    /// <summary>库房名称</summary>
    [MaxLength(200)]
    public string? WarehouseName { get; set; }

    /// <summary>供应商</summary>
    [MaxLength(200)]
    public string? Supplier { get; set; }

    /// <summary>合同</summary>
    [MaxLength(200)]
    public string? Contract { get; set; }

    /// <summary>含税金额合计</summary>
    public decimal TotalTaxIncludedAmount { get; set; }

    /// <summary>计成本金额合计</summary>
    public decimal TotalCostAmount { get; set; }

    /// <summary>税额合计</summary>
    public decimal TotalTaxAmount { get; set; }

    /// <summary>税率（如 13 表示 13%）</summary>
    public decimal TaxRate { get; set; }

    [MaxLength(500)]
    public string? Remark { get; set; }

    public int CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    /// <summary>软删除</summary>
    public bool IsDeleted { get; set; } = false;

    /// <summary>流程状态：draft（草稿）/ pending_approval（审批中）/ approved（已通过）/ rejected（已驳回）</summary>
    [MaxLength(20)]
    public string Status { get; set; } = "draft";

    /// <summary>关联的流程实例 ID</summary>
    public int? WorkflowInstanceId { get; set; }

    public ICollection<InboundOrderDetail> Details { get; set; } = new List<InboundOrderDetail>();
}

/// <summary>
/// 入库单材料明细
/// </summary>
public class InboundOrderDetail
{
    [Key]
    public long Id { get; set; }

    public int InboundOrderId { get; set; }
    public InboundOrder InboundOrder { get; set; } = null!;

    /// <summary>材料编码</summary>
    [Required, MaxLength(50)]
    public string MaterialCode { get; set; } = string.Empty;

    /// <summary>材料名称</summary>
    [Required, MaxLength(200)]
    public string MaterialName { get; set; } = string.Empty;

    /// <summary>规格</summary>
    [MaxLength(100)]
    public string? Specification { get; set; }

    /// <summary>型号</summary>
    [MaxLength(100)]
    public string? Model { get; set; }

    /// <summary>单位</summary>
    [MaxLength(20)]
    public string? Unit { get; set; }

    /// <summary>入库数量</summary>
    public decimal Quantity { get; set; }

    /// <summary>含税单价</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>含税金额 = 数量 × 单价</summary>
    public decimal TaxIncludedAmount { get; set; }

    /// <summary>税额 = 含税金额 × 税率 / (1 + 税率)</summary>
    public decimal TaxAmount { get; set; }

    /// <summary>计成本金额 = 含税金额 - 税额</summary>
    public decimal CostAmount { get; set; }

    /// <summary>税率（如 13 表示 13%）</summary>
    public decimal TaxRate { get; set; }

    [MaxLength(200)]
    public string? Remark { get; set; }
}
