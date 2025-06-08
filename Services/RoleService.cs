using AuthService.Data;
using AuthService.Models;
using AuthService.Models.Enums;
using AuthService.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Services;

public class RoleService : IRoleService
{
    private readonly AuthDbContext _context;
    private readonly ILogger<RoleService> _logger;

    public RoleService(AuthDbContext context, ILogger<RoleService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> AssignRoleAsync(int userId, UserRole role, int assignedByUserId, string? notes = null)
    {
        try
        {
            // Check if user exists
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("Attempted to assign role to non-existent user: {UserId}", userId);
                return false;
            }

            // Check if user already has an active role
            var existingRole = await GetUserCurrentRoleAsync(userId);
            if (existingRole.HasValue)
            {
                _logger.LogWarning("User {UserId} already has active role: {Role}", userId, existingRole.Value);
                return false;
            }

            // Create new role assignment
            var roleAssignment = new UserRoleAssignment
            {
                UserId = userId,
                Role = role,
                AssignedByUserId = assignedByUserId,
                AssignedAt = DateTime.UtcNow,
                Notes = notes
            };

            _context.UserRoleAssignments.Add(roleAssignment);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Role {Role} assigned to user {UserId} by user {AssignedBy}", 
                role, userId, assignedByUserId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning role {Role} to user {UserId}", role, userId);
            return false;
        }
    }

    public async Task<bool> RevokeRoleAsync(int userId, UserRole role, int revokedByUserId, string? notes = null)
    {
        try
        {
            var activeRoleAssignment = await _context.UserRoleAssignments
                .FirstOrDefaultAsync(r => r.UserId == userId && r.Role == role && r.RevokedAt == null);

            if (activeRoleAssignment == null)
            {
                _logger.LogWarning("No active role {Role} found for user {UserId}", role, userId);
                return false;
            }

            activeRoleAssignment.RevokedAt = DateTime.UtcNow;
            activeRoleAssignment.RevokedByUserId = revokedByUserId;
            if (!string.IsNullOrEmpty(notes))
            {
                activeRoleAssignment.Notes = $"{activeRoleAssignment.Notes} | Revoked: {notes}";
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Role {Role} revoked from user {UserId} by user {RevokedBy}", 
                role, userId, revokedByUserId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking role {Role} from user {UserId}", role, userId);
            return false;
        }
    }

    public async Task<bool> RevokeAllUserRolesAsync(int userId, int revokedByUserId, string? notes = null)
    {
        try
        {
            var activeRoleAssignments = await _context.UserRoleAssignments
                .Where(r => r.UserId == userId && r.RevokedAt == null)
                .ToListAsync();

            if (!activeRoleAssignments.Any())
            {
                _logger.LogWarning("No active roles found for user {UserId}", userId);
                return false;
            }

            foreach (var roleAssignment in activeRoleAssignments)
            {
                roleAssignment.RevokedAt = DateTime.UtcNow;
                roleAssignment.RevokedByUserId = revokedByUserId;
                if (!string.IsNullOrEmpty(notes))
                {
                    roleAssignment.Notes = $"{roleAssignment.Notes} | Revoked: {notes}";
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("All roles revoked from user {UserId} by user {RevokedBy}", 
                userId, revokedByUserId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking all roles from user {UserId}", userId);
            return false;
        }
    }

    public async Task<UserRole?> GetUserCurrentRoleAsync(int userId)
    {
        try
        {
            var currentRole = await _context.UserRoleAssignments
                .Where(r => r.UserId == userId && r.RevokedAt == null)
                .OrderByDescending(r => r.AssignedAt)
                .Select(r => r.Role)
                .FirstOrDefaultAsync();

            return currentRole == 0 ? null : currentRole;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current role for user {UserId}", userId);
            return null;
        }
    }

    public async Task<List<UserRoleAssignment>> GetUserRoleHistoryAsync(int userId)
    {
        try
        {
            return await _context.UserRoleAssignments
                .Include(r => r.AssignedByUser)
                .Include(r => r.RevokedByUser)
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.AssignedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting role history for user {UserId}", userId);
            return new List<UserRoleAssignment>();
        }
    }

    public async Task<List<User>> GetUsersByRoleAsync(UserRole role)
    {
        try
        {
            var userIds = await _context.UserRoleAssignments
                .Where(r => r.Role == role && r.RevokedAt == null)
                .Select(r => r.UserId)
                .Distinct()
                .ToListAsync();

            return await _context.Users
                .Where(u => userIds.Contains(u.Id))
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users by role {Role}", role);
            return new List<User>();
        }
    }

    public async Task<bool> UserHasRoleAsync(int userId, UserRole role)
    {
        try
        {
            return await _context.UserRoleAssignments
                .AnyAsync(r => r.UserId == userId && r.Role == role && r.RevokedAt == null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if user {UserId} has role {Role}", userId, role);
            return false;
        }
    }

    public async Task<bool> UserHasPermissionAsync(int userId, string permission)
    {
        try
        {
            var userRole = await GetUserCurrentRoleAsync(userId);
            return userRole?.HasPermission(permission) ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking permission {Permission} for user {UserId}", permission, userId);
            return false;
        }
    }

    public async Task<Dictionary<UserRole, int>> GetRoleStatisticsAsync()
    {
        try
        {
            var roleStats = await _context.UserRoleAssignments
                .Where(r => r.RevokedAt == null)
                .GroupBy(r => r.Role)
                .Select(g => new { Role = g.Key, Count = g.Count() })
                .ToListAsync();

            var result = new Dictionary<UserRole, int>();
            foreach (var stat in roleStats)
            {
                result[stat.Role] = stat.Count;
            }

            // Ensure all roles are represented
            foreach (UserRole role in Enum.GetValues<UserRole>())
            {
                if (!result.ContainsKey(role))
                {
                    result[role] = 0;
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting role statistics");
            return new Dictionary<UserRole, int>();
        }
    }

    public async Task<bool> CanUserAssignRoleAsync(int currentUserId, UserRole targetRole)
    {
        try
        {
            var currentUserRole = await GetUserCurrentRoleAsync(currentUserId);
            if (!currentUserRole.HasValue)
            {
                return false;
            }

            return currentUserRole.Value.CanAccessRole(targetRole);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if user {UserId} can assign role {Role}", currentUserId, targetRole);
            return false;
        }
    }
}