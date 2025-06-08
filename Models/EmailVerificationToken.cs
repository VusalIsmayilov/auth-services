using System.ComponentModel.DataAnnotations;

namespace AuthService.Models;

public class EmailVerificationToken
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public int UserId { get; set; }
    
    [Required]
    [StringLength(100)]
    public string Token { get; set; } = string.Empty;
    
    [Required]
    [EmailAddress]
    [StringLength(255)]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    public DateTime ExpiresAt { get; set; }
    
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public bool IsUsed { get; set; } = false;
    
    public DateTime? UsedAt { get; set; }
    
    public User User { get; set; } = null!;
    
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    
    public bool IsValid => !IsExpired && !IsUsed;
}