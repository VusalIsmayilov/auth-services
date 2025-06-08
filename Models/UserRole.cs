using System.ComponentModel.DataAnnotations;
using AuthService.Models.Enums;

namespace AuthService.Models;

public class UserRoleAssignment
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public int UserId { get; set; }
    
    [Required]
    public UserRole Role { get; set; }
    
    [Required]
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    
    public int? AssignedByUserId { get; set; }
    
    public DateTime? RevokedAt { get; set; }
    
    public int? RevokedByUserId { get; set; }
    
    public bool IsActive => !RevokedAt.HasValue;
    
    [StringLength(500)]
    public string? Notes { get; set; }
    
    // Navigation properties
    public User User { get; set; } = null!;
    public User? AssignedByUser { get; set; }
    public User? RevokedByUser { get; set; }
}