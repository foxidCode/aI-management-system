using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.DTOs;
using backend.Models;

namespace backend.Services;

/// <summary>
/// AI 知识库服务：CRUD + 关键词检索
/// </summary>
public class AiKnowledgeService
{
    private readonly AppDbContext _db;

    public AiKnowledgeService(AppDbContext db)
    {
        _db = db;
    }

    // ========== CRUD ==========

    public async Task<List<KnowledgeEntryResponse>> GetAllAsync(string? category = null, int page = 1, int pageSize = 20)
    {
        var query = _db.KnowledgeEntries.AsQueryable();
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(k => k.Category == category);

        return await query
            .OrderByDescending(k => k.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(k => ToResponse(k))
            .ToListAsync();
    }

    public async Task<int> GetTotalCountAsync(string? category = null)
    {
        var query = _db.KnowledgeEntries.AsQueryable();
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(k => k.Category == category);
        return await query.CountAsync();
    }

    public async Task<KnowledgeEntryResponse?> GetByIdAsync(int id)
    {
        var entry = await _db.KnowledgeEntries.FindAsync(id);
        return entry == null ? null : ToResponse(entry);
    }

    public async Task<KnowledgeEntryResponse> CreateAsync(KnowledgeEntryRequest req)
    {
        var entry = new KnowledgeEntry
        {
            Title = req.Title,
            Content = req.Content,
            Category = req.Category,
            Source = "manual",
            IsActive = req.IsActive,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
        _db.KnowledgeEntries.Add(entry);
        await _db.SaveChangesAsync();
        return ToResponse(entry);
    }

    public async Task<KnowledgeEntryResponse?> UpdateAsync(int id, KnowledgeEntryRequest req)
    {
        var entry = await _db.KnowledgeEntries.FindAsync(id);
        if (entry == null) return null;

        entry.Title = req.Title;
        entry.Content = req.Content;
        entry.Category = req.Category;
        entry.IsActive = req.IsActive;
        entry.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return ToResponse(entry);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entry = await _db.KnowledgeEntries.FindAsync(id);
        if (entry == null) return false;
        _db.KnowledgeEntries.Remove(entry);
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// 从审批通过的总结创建知识库条目
    /// </summary>
    public async Task<KnowledgeEntryResponse> CreateFromSummaryAsync(int summaryId, string title, string content)
    {
        var entry = new KnowledgeEntry
        {
            Title = title,
            Content = content,
            Category = "ai-summary",
            Source = $"summary:{summaryId}",
            IsActive = true,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
        _db.KnowledgeEntries.Add(entry);
        await _db.SaveChangesAsync();
        return ToResponse(entry);
    }

    /// <summary>
    /// 关键词检索：根据用户问题在知识库中搜索相关内容
    /// </summary>
    public async Task<List<KnowledgeEntryResponse>> SearchAsync(string keyword, int topK = 5)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return new List<KnowledgeEntryResponse>();

        var entries = await _db.KnowledgeEntries
            .Where(k => k.IsActive)
            .ToListAsync();

        // 简单的关键词匹配评分
        var kwLower = keyword.ToLower();
        var scored = entries
            .Select(e => new
            {
                Entry = e,
                Score = GetRelevanceScore(e, kwLower)
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => ToResponse(x.Entry))
            .ToList();

        return scored;
    }

    /// <summary>
    /// 获取所有分类
    /// </summary>
    public async Task<List<string>> GetCategoriesAsync()
    {
        return await _db.KnowledgeEntries
            .Where(k => k.IsActive)
            .Select(k => k.Category)
            .Distinct()
            .ToListAsync();
    }

    // ========== Helpers ==========

    private static int GetRelevanceScore(KnowledgeEntry entry, string keyword)
    {
        var score = 0;
        var titleLower = entry.Title.ToLower();
        var contentLower = entry.Content.ToLower();
        var kwLower = keyword.ToLower();

        // 1. 全关键词精确匹配
        if (titleLower.Contains(kwLower)) score += 20;
        if (contentLower.Contains(kwLower)) score += 10;

        // 2. 按中文标点和空格切分为语义片段
        var segments = SplitChineseSegments(kwLower);
        foreach (var seg in segments)
        {
            if (seg.Length < 2) continue;
            if (titleLower.Contains(seg)) score += 8;
            if (contentLower.Contains(seg)) score += 4;
        }

        // 3. 中文 2-gram 滑动窗口匹配（核心：解决中文无空格分词问题）
        var bigrams = GetChineseBigrams(kwLower);
        foreach (var bg in bigrams)
        {
            if (titleLower.Contains(bg)) score += 3;
            if (contentLower.Contains(bg)) score += 1;
        }

        return score;
    }

    /// <summary>按中文标点 + 空格切分语义片段</summary>
    private static string[] SplitChineseSegments(string text)
    {
        return System.Text.RegularExpressions.Regex.Split(text, @"[\s,，。.!！?？:：;；、""""''「」『』【】\[\]\(\)（《》\-—\+=\\/@#\$%^&\*]+")
            .Where(s => s.Length >= 2)
            .ToArray();
    }

    /// <summary>生成中文 2 字符滑动窗口 bigram</summary>
    private static List<string> GetChineseBigrams(string text)
    {
        var seen = new HashSet<string>();
        var bigrams = new List<string>();
        // 仅处理中文字符、字母和数字
        var chars = text.Where(c => (c >= 0x4E00 && c <= 0x9FFF) || char.IsLetterOrDigit(c)).ToArray();
        for (int i = 0; i < chars.Length - 1; i++)
        {
            var bg = new string(new[] { chars[i], chars[i + 1] });
            if (seen.Add(bg))
                bigrams.Add(bg);
        }
        return bigrams;
    }

    private static KnowledgeEntryResponse ToResponse(KnowledgeEntry e) => new()
    {
        Id = e.Id,
        Title = e.Title,
        Content = e.Content,
        Category = e.Category,
        Source = e.Source,
        IsActive = e.IsActive,
        CreatedAt = e.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
        UpdatedAt = e.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss")
    };
}
