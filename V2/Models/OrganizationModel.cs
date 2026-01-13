using System;

namespace V2.Models;

public class OrganizationModel
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;
    public string? Email { get; set; }
    public string? Phone { get; set; }

    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public record OrganizationCreateRequest(
    string Name,
    string? Email,
    string? Phone,
    string? Address,
    string? City,
    string? Country
);
