using Microsoft.EntityFrameworkCore;
using V2.Data;
using V2.Models;
using V2.Services;

public static class UserHandlers
{
    public static IResult Register(RegisterUserRequest req, AppDbContext db)
    {
        if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password) || string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.PhoneNumber) || req.BirthYear <= 0)
        {
            return Results.BadRequest("Bad request:/nUsername, Password, Name, PhoneNumber, Email and BirthYear are required.");
        }
        if (!req.Email.Contains("@") || !req.Email.Contains("."))
        {
            return Results.BadRequest("Bad request:/nInvalid email format.");
        }

        var exist = db.Users.Any(u => u.Username == req.Username || u.Email == req.Email);
        if (exist)
        {
            return Results.Conflict("Bad request:/nUsername or Email already exists.");
        }
        var user = new UserModel
        {
            Username = req.Username,
            Password = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Email = req.Email,
            Name = req.Name,
            Phone = req.PhoneNumber,
            BirthYear = req.BirthYear,
            CreatedAt = DateOnly.FromDateTime(DateTime.UtcNow),
            Active = true
        };

        db.Users.Add(user);
        db.SaveChanges();
        return Results.Created($"/users/{user.Id}", new { user.Id, user.Username, user.Email });
    }

    public static async Task<IResult> Login(LoginRequest req, AppDbContext db, TokenService token)
    {
        if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
        {
            return Results.BadRequest(new
            {
                error = "missing_fields",
                message = "Bad request:/nUsername and Password are required."
            });
        }
        var user = db.Users.FirstOrDefault(u => u.Username == req.Username);
        if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.Password))
        {
            return Results.Unauthorized();
        }

        var jwt = token.CreateToken(user);
        return Results.Ok(new
        {
            token = jwt,
            user = new UserResponse(user.Id, user.Username, user.Role.ToString())
        });
    }

    public record OrgRoleRequest(string OrganizationRole);

    public static async Task<IResult> AdminAssignUserToOrganization(
        int orgId,
        int userId,
        OrgRoleRequest req,
        AppDbContext db)
    {
        var role = (req.OrganizationRole ?? "").Trim().ToUpperInvariant();
        if (role != "ADMIN" && role != "EMPLOYEE")
            return Results.BadRequest("OrganizationRole must be 'ADMIN' or 'EMPLOYEE'.");

        var orgExists = await db.Organizations.AnyAsync(o => o.Id == orgId);
        if (!orgExists) return Results.NotFound("Organization not found.");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return Results.NotFound("User not found.");

        user.OrganizationId = orgId;
        user.OrganizationRole = role;

        var vehicles = await db.Vehicles.Where(v => v.UserId == userId).ToListAsync();
        foreach (var v in vehicles)
            v.OrganizationId = orgId;

        await db.SaveChangesAsync();

        return Results.Ok(new
        {
            message = "User assigned to organization.",
            userId = user.Id,
            organizationId = orgId,
            organizationRole = user.OrganizationRole,
            vehiclesUpdated = vehicles.Count
        });
    }

    public static async Task<IResult> AdminRemoveUserFromOrganization(
        int orgId,
        int userId,
        AppDbContext db)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return Results.NotFound("User not found.");

        if (user.OrganizationId != orgId)
            return Results.Conflict("User is not assigned to this organization.");

        user.OrganizationId = null;
        user.OrganizationRole = null;

        var vehicles = await db.Vehicles.Where(v => v.UserId == userId).ToListAsync();
        foreach (var v in vehicles)
            v.OrganizationId = null;

        await db.SaveChangesAsync();

        return Results.Ok(new
        {
            message = "User removed from organization.",
            userId = user.Id,
            organizationId = orgId,
            vehiclesUpdated = vehicles.Count
        });
    }

    public static async Task<IResult> AdminUpdateUserOrganizationRole(
        int orgId,
        int userId,
        OrgRoleRequest req,
        AppDbContext db)
    {
        var role = (req.OrganizationRole ?? "").Trim().ToUpperInvariant();
        if (role != "ADMIN" && role != "EMPLOYEE")
            return Results.BadRequest("OrganizationRole must be 'ADMIN' or 'EMPLOYEE'.");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return Results.NotFound("User not found.");

        if (user.OrganizationId != orgId)
            return Results.Conflict("User is not assigned to this organization.");

        user.OrganizationRole = role;
        await db.SaveChangesAsync();

        return Results.Ok(new
        {
            message = "Organization role updated.",
            userId = user.Id,
            organizationId = orgId,
            organizationRole = user.OrganizationRole
        });
    }

    public static async Task<IResult> AdminGetOrganizationUsers(
    int orgId,
    AppDbContext db)
    {
        var orgExists = await db.Organizations.AnyAsync(o => o.Id == orgId);
        if (!orgExists) return Results.NotFound("Organization not found.");

        var users = await db.Users
            .AsNoTracking()
            .Where(u => u.OrganizationId == orgId)
            .Select(u => new
            {
                u.Id,
                u.Username,
                u.Name,
                u.Email,
                u.Phone,
                u.Role,               // your global role
                u.OrganizationId,
                u.OrganizationRole,   // "ADMIN" | "EMPLOYEE"
                u.CreatedAt,
                u.BirthYear,
                u.Active
            })
            .ToListAsync();

        return Results.Ok(new
        {
            organizationId = orgId,
            count = users.Count,
            users
        });
    }

}