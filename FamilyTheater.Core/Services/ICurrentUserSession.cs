namespace FamilyTheater.Core.Services;

public interface ICurrentUserSession
{
    int? UserId { get; }
    string? Username { get; }
    string Role { get; }
    bool IsAuthenticated { get; }
    bool IsAdmin { get; }

    void SetCurrentUser(int userId, string username, string role);
    void Clear();
}
