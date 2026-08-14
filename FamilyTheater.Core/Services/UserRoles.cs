namespace FamilyTheater.Core.Services;

public static class UserRoles
{
    public const string Admin = "admin";
    public const string User = "user";
    public const string Manager = "manager";

    public static string Normalize(string? role)
    {
        return (role ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            Admin => Admin,
            Manager => Manager,
            _ => User
        };
    }

    public static bool IsEditableRole(string? role)
    {
        var normalized = Normalize(role);
        return normalized is User or Manager;
    }
}
