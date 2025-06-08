using AuthService.Models;
using AuthService.Models.Enums;

namespace AuthService.Services.Interfaces;

public interface IRoleService
{
    Task<bool> AssignRoleAsync(int userId, UserRole role, int assignedByUserId, string? notes = null);
    Task<bool> RevokeRoleAsync(int userId, UserRole role, int revokedByUserId, string? notes = null);
    Task<bool> RevokeAllUserRolesAsync(int userId, int revokedByUserId, string? notes = null);
    Task<UserRole?> GetUserCurrentRoleAsync(int userId);
    Task<List<UserRoleAssignment>> GetUserRoleHistoryAsync(int userId);
    Task<List<User>> GetUsersByRoleAsync(UserRole role);
    Task<bool> UserHasRoleAsync(int userId, UserRole role);
    Task<bool> UserHasPermissionAsync(int userId, string permission);
    Task<Dictionary<UserRole, int>> GetRoleStatisticsAsync();
    Task<bool> CanUserAssignRoleAsync(int currentUserId, UserRole targetRole);
}