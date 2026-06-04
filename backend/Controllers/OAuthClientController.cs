using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.DTOs;
using backend.Models;

namespace backend.Controllers;

[ApiController]
[Route("api/oauth/clients")]
[Authorize]
public class OAuthClientController : ControllerBase
{
    private readonly AppDbContext _db;

    public OAuthClientController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var total = await _db.OAuthClients.CountAsync();
        var clients = await _db.OAuthClients
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => ToResponse(c))
            .ToListAsync();

        return Ok(new { success = true, data = new { list = clients, total, page, pageSize } });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var client = await _db.OAuthClients.FindAsync(id);
        if (client == null)
            return NotFound(new { success = false, message = "客户端不存在" });

        return Ok(new { success = true, data = ToResponse(client) });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOAuthClientRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, message = GetModelError() });

        if (await _db.OAuthClients.AnyAsync(c => c.ClientId == request.ClientId))
            return BadRequest(new { success = false, message = "ClientId 已存在" });

        var client = new OAuthClient
        {
            ClientId = request.ClientId,
            ClientSecret = request.ClientSecret != null
                ? BCrypt.Net.BCrypt.HashPassword(request.ClientSecret)
                : null,
            ClientName = request.ClientName,
            RedirectUris = JsonSerializer.Serialize(request.RedirectUris),
            AllowedScopes = string.Join(" ", request.AllowedScopes),
            AllowedGrantTypes = string.Join(" ", request.AllowedGrantTypes),
            IsFirstParty = request.IsFirstParty,
            RequirePkce = request.RequirePkce,
            IsActive = true,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
        };

        _db.OAuthClients.Add(client);
        await _db.SaveChangesAsync();

        var response = ToResponse(client);
        response.ClientSecretPlain = request.ClientSecret; // 仅在创建时返回

        return Ok(new { success = true, data = response });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateOAuthClientRequest request)
    {
        var client = await _db.OAuthClients.FindAsync(id);
        if (client == null)
            return NotFound(new { success = false, message = "客户端不存在" });

        if (request.ClientName != null) client.ClientName = request.ClientName;
        if (request.RedirectUris != null) client.RedirectUris = JsonSerializer.Serialize(request.RedirectUris);
        if (request.AllowedScopes != null) client.AllowedScopes = string.Join(" ", request.AllowedScopes);
        if (request.AllowedGrantTypes != null) client.AllowedGrantTypes = string.Join(" ", request.AllowedGrantTypes);
        if (request.IsFirstParty.HasValue) client.IsFirstParty = request.IsFirstParty.Value;
        if (request.RequirePkce.HasValue) client.RequirePkce = request.RequirePkce.Value;
        if (request.IsActive.HasValue) client.IsActive = request.IsActive.Value;
        if (request.ClientSecret != null)
            client.ClientSecret = BCrypt.Net.BCrypt.HashPassword(request.ClientSecret);

        client.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();

        return Ok(new { success = true, data = ToResponse(client) });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var client = await _db.OAuthClients.FindAsync(id);
        if (client == null)
            return NotFound(new { success = false, message = "客户端不存在" });

        // 软删除
        client.IsActive = false;
        client.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();

        return Ok(new { success = true, message = "客户端已禁用" });
    }

    private static OAuthClientResponse ToResponse(OAuthClient c)
    {
        return new OAuthClientResponse
        {
            Id = c.Id,
            ClientId = c.ClientId,
            ClientSecret = c.ClientSecret != null ? "****" : null,
            ClientName = c.ClientName,
            RedirectUris = JsonSerializer.Deserialize<List<string>>(c.RedirectUris) ?? new(),
            AllowedScopes = c.AllowedScopes.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList(),
            AllowedGrantTypes = c.AllowedGrantTypes.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList(),
            IsFirstParty = c.IsFirstParty,
            RequirePkce = c.RequirePkce,
            IsActive = c.IsActive,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt,
        };
    }

    private string GetModelError()
    {
        return string.Join("; ", ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage));
    }
}
