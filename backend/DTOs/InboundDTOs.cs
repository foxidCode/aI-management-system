using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

// ========== 材料字典 DTO ==========

public class CreateMaterialRequest
{
    [Required(ErrorMessage = "请输入材料编码"), MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "请输入材料名称"), MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)] public string? Specification { get; set; }
    [MaxLength(100)] public string? Model { get; set; }
    [MaxLength(20)] public string? Unit { get; set; }
    [MaxLength(200)] public string? Remark { get; set; }
}

public class UpdateMaterialRequest
{
    [Required(ErrorMessage = "请输入材料名称"), MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)] public string? Specification { get; set; }
    [MaxLength(100)] public string? Model { get; set; }
    [MaxLength(20)] public string? Unit { get; set; }
    [MaxLength(200)] public string? Remark { get; set; }
}

public class MaterialResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Specification { get; set; }
    public string? Model { get; set; }
    public string? Unit { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class MaterialListResponse
{
    public List<MaterialResponse> List { get; set; } = new();
    public int Total { get; set; }
}

// ========== 入库单 DTO ==========

public class InboundOrderResponse
{
    public int Id { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string? WarehouseName { get; set; }
    public string? Supplier { get; set; }
    public string? Contract { get; set; }
    public decimal TotalTaxIncludedAmount { get; set; }
    public decimal TotalCostAmount { get; set; }
    public decimal TotalTaxAmount { get; set; }
    public decimal TaxRate { get; set; }
    public string? Remark { get; set; }
    public string? CreatedByName { get; set; }
    public int CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class InboundOrderListResponse
{
    public List<InboundOrderResponse> List { get; set; } = new();
    public int Total { get; set; }
}

public class DailyAmountResponse
{
    public string Date { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Count { get; set; }
}

public class CreateInboundOrderRequest
{
    [MaxLength(200)] public string? WarehouseName { get; set; }
    [MaxLength(200)] public string? Supplier { get; set; }
    [MaxLength(200)] public string? Contract { get; set; }
    public decimal TaxRate { get; set; }
    [MaxLength(500)] public string? Remark { get; set; }
    public List<CreateInboundDetailRequest> Details { get; set; } = new();
}

public class UpdateInboundOrderRequest
{
    [MaxLength(200)] public string? WarehouseName { get; set; }
    [MaxLength(200)] public string? Supplier { get; set; }
    [MaxLength(200)] public string? Contract { get; set; }
    public decimal TaxRate { get; set; }
    [MaxLength(500)] public string? Remark { get; set; }
}

public class CreateInboundDetailRequest
{
    [Required, MaxLength(50)] public string MaterialCode { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string MaterialName { get; set; } = string.Empty;
    [MaxLength(100)] public string? Specification { get; set; }
    [MaxLength(100)] public string? Model { get; set; }
    [MaxLength(20)] public string? Unit { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    [MaxLength(200)] public string? Remark { get; set; }
}

public class InboundOrderDetailResponse
{
    public long Id { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public string? Specification { get; set; }
    public string? Model { get; set; }
    public string? Unit { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxIncludedAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal CostAmount { get; set; }
    public decimal TaxRate { get; set; }
    public string? Remark { get; set; }
}

public class UpdateInboundDetailRequest
{
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    [MaxLength(200)] public string? Remark { get; set; }
}

public class InboundDetailListResponse
{
    public List<InboundOrderDetailResponse> List { get; set; } = new();
    public int Total { get; set; }
}

// ========== 附件 DTO ==========

public class AttachmentResponse
{
    public long Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ObjectKey { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? ContentType { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? DownloadUrl { get; set; }
}

public class AttachmentListResponse
{
    public List<AttachmentResponse> List { get; set; } = new();
    public int Total { get; set; }
}

// ========== 统一附件管理 DTO ==========

public class UnifiedAttachmentResponse
{
    public long Id { get; set; }
    public string ModuleName { get; set; } = string.Empty;
    public string ModuleDisplay { get; set; } = string.Empty;
    public string RelatedId { get; set; } = string.Empty;
    public string? RelatedName { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ObjectKey { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? ContentType { get; set; }
    public string? UploadedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UnifiedAttachmentListResponse
{
    public List<UnifiedAttachmentResponse> List { get; set; } = new();
    public int Total { get; set; }
}

public class CreateUnifiedAttachmentRequest
{
    [Required, MaxLength(100)]
    public string ModuleName { get; set; } = string.Empty;
    [Required, MaxLength(100)]
    public string RelatedId { get; set; } = string.Empty;
    [MaxLength(200)]
    public string? RelatedName { get; set; }
}

public class AttachmentModuleOption
{
    public string ModuleName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

