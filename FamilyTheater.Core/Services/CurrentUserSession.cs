namespace FamilyTheater.Core.Services;

public class CurrentUserSession : ICurrentUserSession
{
    public int? UserId { get; private set; }
    public string? Username { get; private set; }
    public string Role { get; private set; } = UserRoles.User;
    public bool IsAuthenticated => UserId.HasValue;
    public bool IsAdmin => Role == UserRoles.Admin;

    public void SetCurrentUser(int userId, string username, string role)
    {
        UserId = userId;
        Username = username;
        Role = UserRoles.Normalize(role);
    }

    public void Clear()
    {
        UserId = null;
        Username = null;
        Role = UserRoles.User;
    }
}
