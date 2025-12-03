namespace V2.Models;

public class UserModel
{
    public int Id { get; set; }
    public string Username { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public string Role { get; set; } = "USER";
    public DateOnly CreatedAt { get; set; }
    public int? BirthYear { get; set; }
    public bool Active { get; set; }
}
