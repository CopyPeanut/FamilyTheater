using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

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
}

