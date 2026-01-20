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

    public static async Task<IResult> GetOrganizationById(int id, AppDbContext db)
    {
        var org = await db.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id);

        if (org is null)
            return Results.NotFound("Organization not found.");

        return Results.Ok(new
        {
            org.Id,
            org.Name,
            org.Email,
            org.Phone,
            org.Address,
            org.City,
            org.Country,
            org.CreatedAt
        });
    }


    public static async Task<IResult> UpdateOrganization(
    int id,
    AppDbContext db,
    OrganizationUpdateRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return Results.BadRequest("Organization name is required.");

        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == id);
        if (org is null)
            return Results.NotFound("Organization not found.");

        var newName = req.Name.Trim();

        // Only check uniqueness if the name changes
        if (!string.Equals(org.Name, newName, StringComparison.OrdinalIgnoreCase))
        {
            var exists = await db.Organizations.AnyAsync(o =>
                o.Id != id && o.Name.ToLower() == newName.ToLower());

            if (exists)
                return Results.Conflict("An organization with this name already exists.");
        }

        org.Name = newName;
        org.Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim();
        org.Phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim();
        org.Address = string.IsNullOrWhiteSpace(req.Address) ? null : req.Address.Trim();
        org.City = string.IsNullOrWhiteSpace(req.City) ? null : req.City.Trim();
        org.Country = string.IsNullOrWhiteSpace(req.Country) ? null : req.Country.Trim();

        await db.SaveChangesAsync();

        return Results.Ok(new
        {
            message = "Organization updated successfully.",
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

    public static async Task<IResult> DeleteOrganization(
        int id,
        AppDbContext db)
    {
        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == id);
        if (org is null)
            return Results.NotFound("Organization not found.");

        db.Organizations.Remove(org);
        await db.SaveChangesAsync();

        return Results.NoContent();
    }

}
