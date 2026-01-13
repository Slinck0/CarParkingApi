using Microsoft.EntityFrameworkCore;
using V2.Data;
using V2.Models;

public static class OrganizationHandlers
{
    public static async Task<IResult> CreateOrganization(
        AppDbContext db,
        OrganizationCreateRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return Results.BadRequest("Organization name is required.");

        var exists = await db.Organizations.AnyAsync(o => o.Name.ToLower() == req.Name.ToLower());
        if (exists)
            return Results.Conflict("An organization with this name already exists.");

        var org = new OrganizationModel
        {
            Name = req.Name.Trim(),
            Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim(),
            Phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim(),
            Address = string.IsNullOrWhiteSpace(req.Address) ? null : req.Address.Trim(),
            City = string.IsNullOrWhiteSpace(req.City) ? null : req.City.Trim(),
            Country = string.IsNullOrWhiteSpace(req.Country) ? null : req.Country.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Organizations.Add(org);
        await db.SaveChangesAsync();

        return Results.Created($"/admin/organizations/{org.Id}", new
        {
            message = "Organization created successfully.",
            organizationId = org.Id,
            org.Name,
            org.Email,
            org.Phone,
            org.Address,
            org.City,
            org.Country,
            org.CreatedAt
        });
    }
}
