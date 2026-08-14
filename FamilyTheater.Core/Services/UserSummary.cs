namespace FamilyTheater.Core.Services;

public sealed class UserSummary
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = UserRoles.User;
}
