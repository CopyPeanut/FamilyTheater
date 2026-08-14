using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using FamilyTheater.Core.Services;

namespace FamilyTheater.Core.Data;

public class User
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Role { get; set; } = UserRoles.User;
}
