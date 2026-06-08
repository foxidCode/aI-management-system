using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.DTOs;
using backend.Models;

namespace backend.Services;

public class WorkflowDefinitionService
{
    private readonly AppDbContext _db;

    public WorkflowDefinitionService(AppDbContext db)
    {
        _db = db;
    }

    // ========== 流程定义 CRUD ==========

    public async Task<WorkflowDefinitionListResponse> GetAllDefinitionsAsync(string? keyword, int page = 1, int pageSize = 0)
    {
        var q = _db.WorkflowDefinitions.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            q = q.Where(d => d.Name.ToLower().Contains(kw) || d.Key.ToLower().Contains(kw) || (d.FlowCode != null && d.FlowCode.ToLower().Contains(kw)));
        }
        var total = await q.CountAsync();
        var query = q.OrderByDescending(d => d.UpdatedAt);
        if (pageSize > 0) query = (IOrderedQueryable<WorkflowDefinition>)query.Skip((page - 1) * pageSize).Take(pageSize);
        var list = await query.Select(d => new WorkflowDefinitionResponse
        {
            Id = d.Id, Name = d.Name, Key = d.Key,
            GroupId = d.GroupId, FlowCode = d.FlowCode,
            FrmType = d.FrmType, FrmValue = d.FrmValue,
            FrmUrl = d.FrmUrl, DistinctType = d.DistinctType,
            IsActive = d.IsActive, Version = d.Version,
            Remark = d.Remark, Nodes = d.Nodes,
            CreatedAt = d.CreatedAt, UpdatedAt = d.UpdatedAt
        }).ToListAsync();
        return new WorkflowDefinitionListResponse { List = list, Total = total };
    }

    public async Task<WorkflowDefinitionResponse?> GetDefinitionByIdAsync(int id)
    {
        return await _db.WorkflowDefinitions.Where(d => d.Id == id).Select(d => new WorkflowDefinitionResponse
        {
            Id = d.Id, Name = d.Name, Key = d.Key,
            GroupId = d.GroupId, FlowCode = d.FlowCode,
            FrmType = d.FrmType, FrmValue = d.FrmValue,
            FrmUrl = d.FrmUrl, DistinctType = d.DistinctType,
            IsActive = d.IsActive, Version = d.Version,
            Remark = d.Remark, Nodes = d.Nodes,
            CreatedAt = d.CreatedAt, UpdatedAt = d.UpdatedAt
        }).FirstOrDefaultAsync();
    }

    public async Task<WorkflowDefinitionResponse> CreateDefinitionAsync(CreateWorkflowDefinitionRequest req)
    {
        if (await _db.WorkflowDefinitions.AnyAsync(d => d.Key == req.Key))
            throw new InvalidOperationException("流程 Key 已存在");

        var d = new WorkflowDefinition
        {
            Name = req.Name, Key = req.Key,
            GroupId = req.GroupId, FlowCode = req.FlowCode,
            FrmType = req.FrmType, FrmValue = req.FrmValue,
            FrmUrl = req.FrmUrl, DistinctType = req.DistinctType,
            IsActive = req.IsActive, Version = req.Version ?? "1.0",
            Remark = req.Remark, Nodes = req.Nodes,
            CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now
        };
        _db.WorkflowDefinitions.Add(d);
        await _db.SaveChangesAsync();
        return (await GetDefinitionByIdAsync(d.Id))!;
    }

    public async Task<WorkflowDefinitionResponse?> UpdateDefinitionAsync(int id, UpdateWorkflowDefinitionRequest req)
    {
        var d = await _db.WorkflowDefinitions.FindAsync(id);
        if (d == null) return null;

        d.Name = req.Name;
        d.GroupId = req.GroupId;
        d.FrmType = req.FrmType;
        d.FrmValue = req.FrmValue;
        d.FrmUrl = req.FrmUrl;
        d.DistinctType = req.DistinctType;
        d.IsActive = req.IsActive;
        d.Remark = req.Remark;
        d.Nodes = req.Nodes;
        d.UpdatedAt = DateTime.Now;

        await _db.SaveChangesAsync();
        return await GetDefinitionByIdAsync(id);
    }

    public async Task<bool> DeleteDefinitionAsync(int id)
    {
        var d = await _db.WorkflowDefinitions.FindAsync(id);
        if (d == null) return false;
        d.IsActive = false;
        d.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return true;
    }

    // ========== 流程实例 ==========

    public async Task<WorkflowInstanceListResponse> GetInstancesAsync(int? userId, string? status, int page = 1, int pageSize = 10)
    {
        var q = _db.WorkflowInstances
            .Include(i => i.Applicant)
            .Include(i => i.Tasks)
            .Include(i => i.Histories)
            .AsQueryable();

        if (userId.HasValue)
            q = q.Where(i => i.ApplicantId == userId.Value);

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(i => i.Status == status);

        var total = await q.CountAsync();
        var query = q.OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize);

        var list = await query.Select(i => new WorkflowInstanceResponse
        {
            Id = i.Id,
            DefinitionId = i.DefinitionId,
            DefinitionName = i.DefinitionName,
            Version = i.Version,
            ApplicantId = i.ApplicantId,
            ApplicantName = i.Applicant != null ? i.Applicant.Username : null,
            FormData = i.FormData,
            Status = i.Status,
            CurrentNodeId = i.CurrentNodeId,
            CreatedAt = i.CreatedAt,
            UpdatedAt = i.UpdatedAt,
            CompletedAt = i.CompletedAt,
            Tasks = i.Tasks.Select(t => new WorkflowTaskResponse
            {
                Id = t.Id, InstanceId = t.InstanceId,
                NodeId = t.NodeId, NodeName = t.NodeName,
                NodeType = t.NodeType,
                AssigneeId = t.AssigneeId,
                AssigneeName = t.Assignee != null ? t.Assignee.Username : null,
                AssigneeType = t.AssigneeType,
                ActionType = t.ActionType, Status = t.Status,
                FormData = t.FormData, Comment = t.Comment,
                CreatedAt = t.CreatedAt, CompletedAt = t.CompletedAt,
                ParentTaskId = t.ParentTaskId
            }).ToList(),
            Histories = i.Histories.OrderByDescending(h => h.CreatedAt).Select(h => new WorkflowHistoryResponse
            {
                Id = h.Id, InstanceId = h.InstanceId,
                TaskId = h.TaskId, ActionType = h.ActionType,
                ActorId = h.ActorId, ActorName = h.ActorName,
                Comment = h.Comment,
                FormDataSnapshot = h.FormDataSnapshot,
                FromNodeId = h.FromNodeId, ToNodeId = h.ToNodeId,
                CreatedAt = h.CreatedAt
            }).ToList()
        }).ToListAsync();

        return new WorkflowInstanceListResponse { List = list, Total = total };
    }

    public async Task<WorkflowInstanceResponse?> GetInstanceByIdAsync(int id)
    {
        return await _db.WorkflowInstances
            .Include(i => i.Applicant)
            .Include(i => i.Tasks).ThenInclude(t => t.Assignee)
            .Include(i => i.Histories).ThenInclude(h => h.Actor)
            .Where(i => i.Id == id)
            .Select(i => new WorkflowInstanceResponse
            {
                Id = i.Id,
                DefinitionId = i.DefinitionId,
                DefinitionName = i.DefinitionName,
                Version = i.Version,
                ApplicantId = i.ApplicantId,
                ApplicantName = i.Applicant != null ? i.Applicant.Username : null,
                FormData = i.FormData,
                Status = i.Status,
                CurrentNodeId = i.CurrentNodeId,
                CreatedAt = i.CreatedAt,
                UpdatedAt = i.UpdatedAt,
                CompletedAt = i.CompletedAt,
                Tasks = i.Tasks.Select(t => new WorkflowTaskResponse
                {
                    Id = t.Id, InstanceId = t.InstanceId,
                    NodeId = t.NodeId, NodeName = t.NodeName,
                    NodeType = t.NodeType,
                    AssigneeId = t.AssigneeId,
                    AssigneeName = t.Assignee != null ? t.Assignee.Username : null,
                    AssigneeType = t.AssigneeType,
                    ActionType = t.ActionType, Status = t.Status,
                    FormData = t.FormData, Comment = t.Comment,
                    CreatedAt = t.CreatedAt, CompletedAt = t.CompletedAt,
                    ParentTaskId = t.ParentTaskId
                }).ToList(),
                Histories = i.Histories.OrderByDescending(h => h.CreatedAt).Select(h => new WorkflowHistoryResponse
                {
                    Id = h.Id, InstanceId = h.InstanceId,
                    TaskId = h.TaskId, ActionType = h.ActionType,
                    ActorId = h.ActorId, ActorName = h.ActorName,
                    Comment = h.Comment,
                    FormDataSnapshot = h.FormDataSnapshot,
                    FromNodeId = h.FromNodeId, ToNodeId = h.ToNodeId,
                    CreatedAt = h.CreatedAt
                }).ToList()
            }).FirstOrDefaultAsync();
    }

    // ========== 任务 ==========

    public async Task<WorkflowTaskListResponse> GetPendingTasksAsync(int assigneeId, int page = 1, int pageSize = 10)
    {
        var q = _db.WorkflowTasks
            .Include(t => t.Instance).ThenInclude(i => i.Applicant)
            .Include(t => t.Assignee)
            .Where(t => t.Status == "Pending" && t.AssigneeId == assigneeId);

        var total = await q.CountAsync();
        var list = await q.OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(t => new WorkflowTaskResponse
            {
                Id = t.Id, InstanceId = t.InstanceId,
                NodeId = t.NodeId, NodeName = t.NodeName,
                NodeType = t.NodeType,
                AssigneeId = t.AssigneeId,
                AssigneeName = t.Assignee != null ? t.Assignee.Username : null,
                AssigneeType = t.AssigneeType,
                ActionType = t.ActionType, Status = t.Status,
                FormData = t.FormData, Comment = t.Comment,
                CreatedAt = t.CreatedAt, CompletedAt = t.CompletedAt,
                ParentTaskId = t.ParentTaskId,
                Instance = t.Instance != null ? new TaskInstanceBrief
                {
                    Id = t.Instance.Id,
                    DefinitionId = t.Instance.DefinitionId,
                    DefinitionName = t.Instance.DefinitionName,
                    ApplicantName = t.Instance.Applicant != null ? t.Instance.Applicant.Username : null,
                    Status = t.Instance.Status
                } : null
            }).ToListAsync();

        return new WorkflowTaskListResponse { List = list, Total = total };
    }

    public async Task<WorkflowTaskListResponse> GetTaskHistoryAsync(int assigneeId, int page = 1, int pageSize = 10)
    {
        var q = _db.WorkflowTasks
            .Include(t => t.Instance)
            .Include(t => t.Assignee)
            .Where(t => t.Status != "Pending" && t.AssigneeId == assigneeId);

        var total = await q.CountAsync();
        var list = await q.OrderByDescending(t => t.CompletedAt ?? t.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(t => new WorkflowTaskResponse
            {
                Id = t.Id, InstanceId = t.InstanceId,
                NodeId = t.NodeId, NodeName = t.NodeName,
                NodeType = t.NodeType,
                AssigneeId = t.AssigneeId,
                AssigneeName = t.Assignee != null ? t.Assignee.Username : null,
                AssigneeType = t.AssigneeType,
                ActionType = t.ActionType, Status = t.Status,
                FormData = t.FormData, Comment = t.Comment,
                CreatedAt = t.CreatedAt, CompletedAt = t.CompletedAt,
                ParentTaskId = t.ParentTaskId,
                Instance = t.Instance != null ? new TaskInstanceBrief
                {
                    Id = t.Instance.Id,
                    DefinitionId = t.Instance.DefinitionId,
                    DefinitionName = t.Instance.DefinitionName,
                    ApplicantName = t.Instance.Applicant != null ? t.Instance.Applicant.Username : null,
                    Status = t.Instance.Status
                } : null
            }).ToListAsync();

        return new WorkflowTaskListResponse { List = list, Total = total };
    }

    public async Task<WorkflowTask?> GetTaskByIdAsync(int id)
    {
        return await _db.WorkflowTasks
            .Include(t => t.Instance)
            .Include(t => t.Assignee)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    // ========== 历史 ==========

    public async Task<List<WorkflowHistoryResponse>> GetHistoryByInstanceIdAsync(int instanceId)
    {
        return await _db.WorkflowHistories
            .Where(h => h.InstanceId == instanceId)
            .OrderByDescending(h => h.CreatedAt)
            .Select(h => new WorkflowHistoryResponse
            {
                Id = h.Id, InstanceId = h.InstanceId,
                TaskId = h.TaskId, ActionType = h.ActionType,
                ActorId = h.ActorId, ActorName = h.ActorName,
                Comment = h.Comment,
                FormDataSnapshot = h.FormDataSnapshot,
                FromNodeId = h.FromNodeId, ToNodeId = h.ToNodeId,
                CreatedAt = h.CreatedAt
            }).ToListAsync();
    }
}
