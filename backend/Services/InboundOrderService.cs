using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.DTOs;
using backend.Models;

namespace backend.Services;

public class InboundOrderService
{
    private readonly AppDbContext _db;
    private readonly AttachmentService _attachmentService;
    private readonly WorkflowEngine? _workflowEngine;

    public InboundOrderService(AppDbContext db, AttachmentService attachmentService, WorkflowEngine? workflowEngine = null)
    {
        _db = db;
        _attachmentService = attachmentService;
        _workflowEngine = workflowEngine;
    }

    // ========== 材料字典 ==========

    public async Task<MaterialListResponse> GetMaterialsAsync(string? keyword, int page = 1, int pageSize = 0)
    {
        var q = _db.MaterialDictionaries.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            q = q.Where(m => m.Code.ToLower().Contains(kw) || m.Name.ToLower().Contains(kw));
        }
        var total = await q.CountAsync();
        var query = q.OrderBy(m => m.Id);
        if (pageSize > 0) query = (IOrderedQueryable<MaterialDictionary>)query.Skip((page - 1) * pageSize).Take(pageSize);
        var list = await query.Select(m => new MaterialResponse
        {
            Id = m.Id, Code = m.Code, Name = m.Name,
            Specification = m.Specification, Model = m.Model,
            Unit = m.Unit, Remark = m.Remark,
            CreatedAt = m.CreatedAt, UpdatedAt = m.UpdatedAt
        }).ToListAsync();
        return new MaterialListResponse { List = list, Total = total };
    }

    public async Task<MaterialResponse?> GetMaterialByIdAsync(int id)
    {
        return await _db.MaterialDictionaries.Where(m => m.Id == id).Select(m => new MaterialResponse
        {
            Id = m.Id, Code = m.Code, Name = m.Name,
            Specification = m.Specification, Model = m.Model,
            Unit = m.Unit, Remark = m.Remark,
            CreatedAt = m.CreatedAt, UpdatedAt = m.UpdatedAt
        }).FirstOrDefaultAsync();
    }

    public async Task<MaterialResponse> CreateMaterialAsync(CreateMaterialRequest req)
    {
        if (await _db.MaterialDictionaries.AnyAsync(m => m.Code == req.Code))
            throw new InvalidOperationException("材料编码已存在");
        var m = new MaterialDictionary
        {
            Code = req.Code, Name = req.Name,
            Specification = req.Specification, Model = req.Model,
            Unit = req.Unit, Remark = req.Remark,
            CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now
        };
        _db.MaterialDictionaries.Add(m);
        await _db.SaveChangesAsync();
        return (await GetMaterialByIdAsync(m.Id))!;
    }

    public async Task<MaterialResponse?> UpdateMaterialAsync(int id, UpdateMaterialRequest req)
    {
        var m = await _db.MaterialDictionaries.FindAsync(id);
        if (m == null) return null;
        m.Name = req.Name;
        m.Specification = req.Specification;
        m.Model = req.Model;
        m.Unit = req.Unit;
        m.Remark = req.Remark;
        m.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return await GetMaterialByIdAsync(id);
    }

    public async Task<bool> DeleteMaterialAsync(int id)
    {
        var m = await _db.MaterialDictionaries.FindAsync(id);
        if (m == null) return false;
        _db.MaterialDictionaries.Remove(m);
        await _db.SaveChangesAsync();
        return true;
    }

    // ========== 入库单 ==========

    public async Task<InboundOrderListResponse> GetOrdersAsync(
        string? keyword, int page = 1, int pageSize = 10,
        string? sortField = null, string? sortOrder = null)
    {
        var q = _db.InboundOrders.Where(o => !o.IsDeleted);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            q = q.Where(o => o.OrderCode.ToLower().Contains(kw)
                || (o.Supplier != null && o.Supplier.ToLower().Contains(kw))
                || (o.WarehouseName != null && o.WarehouseName.ToLower().Contains(kw))
                || (o.Contract != null && o.Contract.ToLower().Contains(kw)));
        }

        // 排序
        IOrderedQueryable<InboundOrder> ordered;
        var asc = sortOrder != "descending";
        ordered = sortField switch
        {
            "orderCode" => asc ? q.OrderBy(o => o.OrderCode) : q.OrderByDescending(o => o.OrderCode),
            "createdAt" => asc ? q.OrderBy(o => o.CreatedAt) : q.OrderByDescending(o => o.CreatedAt),
            "updatedAt" => asc ? q.OrderBy(o => o.UpdatedAt) : q.OrderByDescending(o => o.UpdatedAt),
            "totalTaxIncludedAmount" => asc ? q.OrderBy(o => o.TotalTaxIncludedAmount) : q.OrderByDescending(o => o.TotalTaxIncludedAmount),
            _ => q.OrderByDescending(o => o.CreatedAt)
        };

        var total = await ordered.CountAsync();
        var list = await ordered.Skip((page - 1) * pageSize).Take(pageSize).Select(o => new InboundOrderResponse
        {
            Id = o.Id, OrderCode = o.OrderCode,
            WarehouseName = o.WarehouseName, Supplier = o.Supplier, Contract = o.Contract,
            TotalTaxIncludedAmount = o.TotalTaxIncludedAmount,
            TotalCostAmount = o.TotalCostAmount,
            TotalTaxAmount = o.TotalTaxAmount,
            TaxRate = o.TaxRate, Remark = o.Remark,
            CreatedBy = o.CreatedBy, CreatedAt = o.CreatedAt, UpdatedAt = o.UpdatedAt
        }).ToListAsync();

        // 填充创建人姓名
        var userIds = list.Select(o => o.CreatedBy).Distinct().ToList();
        var users = await _db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username);
        foreach (var o in list)
            o.CreatedByName = users.GetValueOrDefault(o.CreatedBy);

        return new InboundOrderListResponse { List = list, Total = total };
    }

    public async Task<InboundOrderResponse?> GetOrderByIdAsync(int id)
    {
        var o = await _db.InboundOrders.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (o == null) return null;
        var username = await _db.Users.Where(u => u.Id == o.CreatedBy).Select(u => u.Username).FirstOrDefaultAsync();
        return MapOrder(o, username);
    }

    public async Task<InboundOrderResponse> CreateOrderAsync(CreateInboundOrderRequest req, int userId)
    {
        var order = new InboundOrder
        {
            OrderCode = await GenerateOrderCodeAsync(),
            WarehouseName = req.WarehouseName,
            Supplier = req.Supplier,
            Contract = req.Contract,
            TaxRate = req.TaxRate,
            Remark = req.Remark,
            CreatedBy = userId,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        // 材料明细（逐条计算金额）
        foreach (var d in req.Details)
        {
            var taxIncluded = d.Quantity * d.UnitPrice;
            var rate = d.TaxRate > 0 ? d.TaxRate / 100m : 0;
            var taxAmount = rate > 0 ? Math.Round(taxIncluded * rate / (1 + rate), 2) : 0;
            var costAmount = taxIncluded - taxAmount;

            order.Details.Add(new InboundOrderDetail
            {
                MaterialCode = d.MaterialCode, MaterialName = d.MaterialName,
                Specification = d.Specification, Model = d.Model, Unit = d.Unit,
                Quantity = d.Quantity, UnitPrice = d.UnitPrice,
                TaxRate = d.TaxRate, Remark = d.Remark,
                TaxIncludedAmount = taxIncluded,
                TaxAmount = taxAmount,
                CostAmount = costAmount
            });
        }

        // 汇总
        order.TotalTaxIncludedAmount = order.Details.Sum(d => d.TaxIncludedAmount);
        order.TotalTaxAmount = order.Details.Sum(d => d.TaxAmount);
        order.TotalCostAmount = order.Details.Sum(d => d.CostAmount);

        _db.InboundOrders.Add(order);
        await _db.SaveChangesAsync();
        return (await GetOrderByIdAsync(order.Id))!;
    }

    public async Task<InboundOrderResponse?> UpdateOrderAsync(int id, UpdateInboundOrderRequest req)
    {
        var o = await _db.InboundOrders.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (o == null) return null;
        o.WarehouseName = req.WarehouseName;
        o.Supplier = req.Supplier;
        o.Contract = req.Contract;
        o.TaxRate = req.TaxRate;
        o.Remark = req.Remark;
        o.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return await GetOrderByIdAsync(id);
    }

    public async Task<bool> DeleteOrderAsync(int id)
    {
        var o = await _db.InboundOrders.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (o == null) return false;
        o.IsDeleted = true;
        o.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return true;
    }

    // ========== 明细管理（服务端分页，支持大数据量） ==========

    public async Task<InboundDetailListResponse> GetDetailsAsync(int orderId, int page = 1, int pageSize = 20)
    {
        var q = _db.InboundOrderDetails.Where(d => d.InboundOrderId == orderId);
        var total = await q.CountAsync();
        IQueryable<Models.InboundOrderDetail> pagedQuery = q.OrderBy(d => d.Id);
        if (pageSize > 0)
            pagedQuery = pagedQuery.Skip((page - 1) * pageSize).Take(pageSize);
        var list = await pagedQuery.Select(d => new InboundOrderDetailResponse
            {
                Id = d.Id, MaterialCode = d.MaterialCode, MaterialName = d.MaterialName,
                Specification = d.Specification, Model = d.Model, Unit = d.Unit,
                Quantity = d.Quantity, UnitPrice = d.UnitPrice,
                TaxIncludedAmount = d.TaxIncludedAmount, TaxAmount = d.TaxAmount,
                CostAmount = d.CostAmount, TaxRate = d.TaxRate, Remark = d.Remark
            }).ToListAsync();
        return new InboundDetailListResponse { List = list, Total = total };
    }

    public async Task<InboundOrderDetailResponse> AddDetailAsync(int orderId, CreateInboundDetailRequest req)
    {
        var o = await _db.InboundOrders.FirstOrDefaultAsync(x => x.Id == orderId && !x.IsDeleted);
        if (o == null) throw new InvalidOperationException("入库单不存在");

        var taxIncluded = req.Quantity * req.UnitPrice;
        var rate = req.TaxRate > 0 ? req.TaxRate / 100m : 0;
        var taxAmount = rate > 0 ? Math.Round(taxIncluded * rate / (1 + rate), 2) : 0;
        var costAmount = taxIncluded - taxAmount;

        var detail = new InboundOrderDetail
        {
            InboundOrderId = orderId,
            MaterialCode = req.MaterialCode, MaterialName = req.MaterialName,
            Specification = req.Specification, Model = req.Model, Unit = req.Unit,
            Quantity = req.Quantity, UnitPrice = req.UnitPrice,
            TaxRate = req.TaxRate, Remark = req.Remark,
            TaxIncludedAmount = taxIncluded, TaxAmount = taxAmount, CostAmount = costAmount
        };
        _db.InboundOrderDetails.Add(detail);

        // 更新汇总
        o.TotalTaxIncludedAmount += taxIncluded;
        o.TotalTaxAmount += taxAmount;
        o.TotalCostAmount += costAmount;
        o.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();

        return new InboundOrderDetailResponse
        {
            Id = detail.Id, MaterialCode = detail.MaterialCode, MaterialName = detail.MaterialName,
            Specification = detail.Specification, Model = detail.Model, Unit = detail.Unit,
            Quantity = detail.Quantity, UnitPrice = detail.UnitPrice,
            TaxIncludedAmount = detail.TaxIncludedAmount, TaxAmount = detail.TaxAmount,
            CostAmount = detail.CostAmount, TaxRate = detail.TaxRate, Remark = detail.Remark
        };
    }

    public async Task<InboundOrderDetailResponse?> UpdateDetailAsync(long detailId, UpdateInboundDetailRequest req)
    {
        var d = await _db.InboundOrderDetails.Include(x => x.InboundOrder).FirstOrDefaultAsync(x => x.Id == detailId);
        if (d == null) return null;

        var oldTaxIncluded = d.TaxIncludedAmount;
        var oldTaxAmount = d.TaxAmount;
        var oldCostAmount = d.CostAmount;

        d.Quantity = req.Quantity;
        d.UnitPrice = req.UnitPrice;
        d.TaxRate = req.TaxRate;
        d.Remark = req.Remark;

        var taxIncluded = req.Quantity * req.UnitPrice;
        var rate = req.TaxRate > 0 ? req.TaxRate / 100m : 0;
        var taxAmount = rate > 0 ? Math.Round(taxIncluded * rate / (1 + rate), 2) : 0;
        var costAmount = taxIncluded - taxAmount;

        d.TaxIncludedAmount = taxIncluded;
        d.TaxAmount = taxAmount;
        d.CostAmount = costAmount;

        // 更新汇总
        var o = d.InboundOrder;
        o.TotalTaxIncludedAmount += (taxIncluded - oldTaxIncluded);
        o.TotalTaxAmount += (taxAmount - oldTaxAmount);
        o.TotalCostAmount += (costAmount - oldCostAmount);
        o.UpdatedAt = DateTime.Now;

        await _db.SaveChangesAsync();

        return new InboundOrderDetailResponse
        {
            Id = d.Id, MaterialCode = d.MaterialCode, MaterialName = d.MaterialName,
            Specification = d.Specification, Model = d.Model, Unit = d.Unit,
            Quantity = d.Quantity, UnitPrice = d.UnitPrice,
            TaxIncludedAmount = d.TaxIncludedAmount, TaxAmount = d.TaxAmount,
            CostAmount = d.CostAmount, TaxRate = d.TaxRate, Remark = d.Remark
        };
    }

    public async Task<bool> DeleteDetailAsync(long detailId)
    {
        var d = await _db.InboundOrderDetails.Include(x => x.InboundOrder).FirstOrDefaultAsync(x => x.Id == detailId);
        if (d == null) return false;
        var o = d.InboundOrder;
        o.TotalTaxIncludedAmount -= d.TaxIncludedAmount;
        o.TotalTaxAmount -= d.TaxAmount;
        o.TotalCostAmount -= d.CostAmount;
        o.UpdatedAt = DateTime.Now;
        _db.InboundOrderDetails.Remove(d);
        await _db.SaveChangesAsync();
        return true;
    }

    // ========== 明细批量同步 ==========

    public async Task SyncDetailsAsync(int orderId, List<CreateInboundDetailRequest> details)
    {
        var o = await _db.InboundOrders.Include(x => x.Details).FirstOrDefaultAsync(x => x.Id == orderId && !x.IsDeleted);
        if (o == null) throw new InvalidOperationException("入库单不存在");

        // 清空旧明细
        _db.InboundOrderDetails.RemoveRange(o.Details);

        // 重新插入
        decimal totalTaxIncluded = 0, totalTax = 0, totalCost = 0;
        foreach (var d in details)
        {
            var taxIncluded = d.Quantity * d.UnitPrice;
            var rate = d.TaxRate > 0 ? d.TaxRate / 100m : 0;
            var taxAmount = rate > 0 ? Math.Round(taxIncluded * rate / (1 + rate), 2) : 0;
            var costAmount = taxIncluded - taxAmount;

            _db.InboundOrderDetails.Add(new Models.InboundOrderDetail
            {
                InboundOrderId = orderId,
                MaterialCode = d.MaterialCode, MaterialName = d.MaterialName,
                Specification = d.Specification, Model = d.Model, Unit = d.Unit,
                Quantity = d.Quantity, UnitPrice = d.UnitPrice,
                TaxRate = d.TaxRate, Remark = d.Remark,
                TaxIncludedAmount = taxIncluded, TaxAmount = taxAmount, CostAmount = costAmount
            });

            totalTaxIncluded += taxIncluded;
            totalTax += taxAmount;
            totalCost += costAmount;
        }

        o.TotalTaxIncludedAmount = totalTaxIncluded;
        o.TotalTaxAmount = totalTax;
        o.TotalCostAmount = totalCost;
        o.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
    }

    // ========== 附件管理（委托给统一 AttachmentService） ==========

    public async Task<AttachmentListResponse> GetAttachmentsAsync(int orderId, int page = 1, int pageSize = 20)
    {
        return await _attachmentService.GetByRecordAsync("InboundOrder", orderId.ToString(), page, pageSize);
    }

    public async Task<AttachmentResponse> AddAttachmentAsync(int orderId, string fileName, string objectKey, long fileSize, string? contentType, int uploadedBy)
    {
        // 获取入库单编码作为 RelatedName
        var orderCode = await _db.InboundOrders
            .Where(o => o.Id == orderId)
            .Select(o => o.OrderCode)
            .FirstOrDefaultAsync();

        var unified = await _attachmentService.AddAsync(
            "InboundOrder", orderId.ToString(), orderCode,
            fileName, objectKey, fileSize, contentType, uploadedBy);

        return new AttachmentResponse
        {
            Id = unified.Id,
            FileName = unified.FileName,
            ObjectKey = unified.ObjectKey,
            FileSize = unified.FileSize,
            ContentType = unified.ContentType,
            CreatedAt = unified.CreatedAt,
        };
    }

    public async Task<InboundOrderAttachment?> GetAttachmentAsync(long attachmentId)
    {
        // 兼容旧表查询
        var old = await _db.InboundOrderAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId);
        if (old != null) return old;

        // 查新表
        var a = await _attachmentService.GetByIdAsync(attachmentId);
        if (a == null) return null;
        return new InboundOrderAttachment
        {
            Id = a.Id,
            InboundOrderId = int.TryParse(a.RelatedId, out var rid) ? rid : 0,
            FileName = a.FileName,
            ObjectKey = a.ObjectKey,
            FileSize = a.FileSize,
            ContentType = a.ContentType,
            CreatedAt = a.CreatedAt,
        };
    }

    public async Task<bool> DeleteAttachmentAsync(long attachmentId)
    {
        // 先尝试删旧表（兼容历史数据）
        var old = await _db.InboundOrderAttachments.FindAsync(attachmentId);
        if (old != null)
        {
            _db.InboundOrderAttachments.Remove(old);
            await _db.SaveChangesAsync();
            return true;
        }

        // 删新表
        return await _attachmentService.DeleteAsync(attachmentId);
    }

    // ========== 导出 ==========

    public async Task<List<InboundOrderResponse>> GetAllOrdersForExportAsync(string? keyword, int page = 0, int pageSize = 0)
    {
        var q = _db.InboundOrders.Where(o => !o.IsDeleted);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            q = q.Where(o => o.OrderCode.ToLower().Contains(kw)
                || (o.Supplier != null && o.Supplier.ToLower().Contains(kw))
                || (o.WarehouseName != null && o.WarehouseName.ToLower().Contains(kw))
                || (o.Contract != null && o.Contract.ToLower().Contains(kw)));
        }
        var ordered = q.OrderByDescending(o => o.CreatedAt);
        if (page > 0 && pageSize > 0)
            ordered = (IOrderedQueryable<InboundOrder>)ordered.Skip((page - 1) * pageSize).Take(pageSize);
        var list = await ordered.Select(o => new InboundOrderResponse
        {
            Id = o.Id, OrderCode = o.OrderCode,
            WarehouseName = o.WarehouseName, Supplier = o.Supplier, Contract = o.Contract,
            TotalTaxIncludedAmount = o.TotalTaxIncludedAmount,
            TotalCostAmount = o.TotalCostAmount,
            TotalTaxAmount = o.TotalTaxAmount,
            TaxRate = o.TaxRate, Remark = o.Remark,
            CreatedBy = o.CreatedBy, CreatedAt = o.CreatedAt, UpdatedAt = o.UpdatedAt
        }).ToListAsync();
        var userIds = list.Select(o => o.CreatedBy).Distinct().ToList();
        var users = await _db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username);
        foreach (var o in list) o.CreatedByName = users.GetValueOrDefault(o.CreatedBy);
        return list;
    }

    // ========== 统计 ==========

    public async Task<List<DailyAmountResponse>> GetDailyAmountsAsync(int days = 7)
    {
        var since = DateTime.Now.Date.AddDays(-days + 1);
        var orders = await _db.InboundOrders
            .Where(o => !o.IsDeleted && o.CreatedAt >= since)
            .ToListAsync();

        var dailyData = orders
            .GroupBy(o => o.CreatedAt.Date)
            .Select(g => new DailyAmountResponse
            {
                Date = g.Key.ToString("MM-dd"),
                Amount = g.Sum(o => o.TotalTaxIncludedAmount),
                Count = g.Count()
            })
            .OrderBy(d => d.Date)
            .ToList();

        // 补全没有数据的日期
        var allDays = new List<DailyAmountResponse>();
        for (int i = 0; i < days; i++)
        {
            var date = since.AddDays(i);
            var key = date.ToString("MM-dd");
            var exists = dailyData.FirstOrDefault(d => d.Date == key);
            allDays.Add(exists ?? new DailyAmountResponse { Date = key, Amount = 0, Count = 0 });
        }

        return allDays;
    }

    // ========== 辅助 ==========

    // ========== 审批流程 ==========

    public async Task<string> SubmitForApprovalAsync(int orderId, int definitionId, int userId)
    {
        var order = await _db.InboundOrders.FindAsync(orderId)
            ?? throw new InvalidOperationException("入库单不存在");

        if (order.Status != "draft")
            throw new InvalidOperationException("只有草稿状态的入库单可以提交审批");

        if (_workflowEngine == null)
            throw new InvalidOperationException("工作流引擎未配置");

        var instance = await _workflowEngine.StartWorkflowAsync(definitionId, "InboundOrder", orderId.ToString(), userId);

        order.Status = "pending_approval";
        order.WorkflowInstanceId = instance.Id;
        order.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();

        return "已提交审批";
    }

    private async Task<string> GenerateOrderCodeAsync()
    {
        var date = DateTime.Now.ToString("yyyyMMdd");
        var todayCount = await _db.InboundOrders.CountAsync(o => o.CreatedAt.Date == DateTime.Now.Date);
        return $"RK-{date}-{(todayCount + 1):D4}";
    }

    private static InboundOrderResponse MapOrder(InboundOrder o, string? createdByName) => new()
    {
        Id = o.Id, OrderCode = o.OrderCode,
        WarehouseName = o.WarehouseName, Supplier = o.Supplier, Contract = o.Contract,
        TotalTaxIncludedAmount = o.TotalTaxIncludedAmount,
        TotalCostAmount = o.TotalCostAmount,
        TotalTaxAmount = o.TotalTaxAmount,
        TaxRate = o.TaxRate, Remark = o.Remark,
        CreatedBy = o.CreatedBy, CreatedByName = createdByName,
        CreatedAt = o.CreatedAt, UpdatedAt = o.UpdatedAt
    };
}
