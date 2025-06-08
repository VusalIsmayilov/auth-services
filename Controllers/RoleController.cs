using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using AuthService.DTOs;
using AuthService.Models.Enums;
using AuthService.Services.Interfaces;
using AuthService.Authorization;

namespace AuthService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RoleController : ControllerBase
{
    private readonly IRoleService _roleService;
    private readonly IUserService _userService;
    private readonly ILogger<RoleController> _logger;

    public RoleController(
        IRoleService roleService,
        IUserService userService,
        ILogger<RoleController> logger)
    {
        _roleService = roleService;
        _userService = userService;
        _logger = logger;
    }

    /// <summary>
    /// Assign a role to a user (Admin only)
    /// </summary>
    [HttpPost("assign")]
    [AdminOnly]
    public async Task<ActionResult<ApiResponse>> AssignRole([FromBody] AssignRoleRequest request)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                return Unauthorized(new ApiResponse
                {
                    Success = false,
                    Message = "Invalid token"
                });
            }

            // Check if current user can assign this role
            var canAssign = await _roleService.CanUserAssignRoleAsync(currentUserId.Value, request.Role);
            if (!canAssign)
            {
                return Forbid();
            }

            var result = await _roleService.AssignRoleAsync(
                request.UserId, 
                request.Role, 
                currentUserId.Value, 
                request.Notes);

            if (result)
            {
                _logger.LogInformation("Role {Role} assigned to user {UserId} by {AssignedBy}", 
                    request.Role, request.UserId, currentUserId.Value);

                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = $"Role {request.Role.GetDisplayName()} assigned successfully"
                });
            }

            return BadRequest(new ApiResponse
            {
                Success = false,
                Message = "Failed to assign role. User may already have an active role."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning role {Role} to user {UserId}", request.Role, request.UserId);
            return StatusCode(500, new ApiResponse
            {
                Success = false,
                Message = "An error occurred while assigning the role"
            });
        }
    }

    /// <summary>
    /// Revoke a role from a user (Admin only)
    /// </summary>
    [HttpPost("revoke")]
    [AdminOnly]
    public async Task<ActionResult<ApiResponse>> RevokeRole([FromBody] RevokeRoleRequest request)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                return Unauthorized(new ApiResponse
                {
                    Success = false,
                    Message = "Invalid token"
                });
            }

            var result = await _roleService.RevokeRoleAsync(
                request.UserId, 
                request.Role, 
                currentUserId.Value, 
                request.Notes);

            if (result)
            {
                _logger.LogInformation("Role {Role} revoked from user {UserId} by {RevokedBy}", 
                    request.Role, request.UserId, currentUserId.Value);

                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = $"Role {request.Role.GetDisplayName()} revoked successfully"
                });
            }

            return BadRequest(new ApiResponse
            {
                Success = false,
                Message = "Failed to revoke role. User may not have this active role."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking role {Role} from user {UserId}", request.Role, request.UserId);
            return StatusCode(500, new ApiResponse
            {
                Success = false,
                Message = "An error occurred while revoking the role"
            });
        }
    }

    /// <summary>
    /// Get all users with their current roles (Admin only)
    /// </summary>
    [HttpGet("users")]
    [AdminOnly]
    public async Task<ActionResult<List<UserWithRoleResponse>>> GetUsersWithRoles()
    {
        try
        {
            var users = await _userService.GetAllUsersAsync();
            var usersWithRoles = new List<UserWithRoleResponse>();

            foreach (var user in users)
            {
                var currentRole = await _roleService.GetUserCurrentRoleAsync(user.Id);
                
                usersWithRoles.Add(new UserWithRoleResponse
                {
                    Id = user.Id,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    IsEmailVerified = user.IsEmailVerified,
                    IsPhoneVerified = user.IsPhoneVerified,
                    CurrentRole = currentRole,
                    CurrentRoleDisplayName = currentRole?.GetDisplayName(),
                    CreatedAt = user.CreatedAt,
                    LastLoginAt = user.LastLoginAt,
                    IsActive = user.IsActive
                });
            }

            return Ok(usersWithRoles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users with roles");
            return StatusCode(500, new List<UserWithRoleResponse>());
        }
    }

    /// <summary>
    /// Get users by specific role (Admin only)
    /// </summary>
    [HttpGet("users/{role}")]
    [AdminOnly]
    public async Task<ActionResult<List<UserWithRoleResponse>>> GetUsersByRole(UserRole role)
    {
        try
        {
            var users = await _roleService.GetUsersByRoleAsync(role);
            var usersWithRoles = new List<UserWithRoleResponse>();

            foreach (var user in users)
            {
                usersWithRoles.Add(new UserWithRoleResponse
                {
                    Id = user.Id,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    IsEmailVerified = user.IsEmailVerified,
                    IsPhoneVerified = user.IsPhoneVerified,
                    CurrentRole = role,
                    CurrentRoleDisplayName = role.GetDisplayName(),
                    CreatedAt = user.CreatedAt,
                    LastLoginAt = user.LastLoginAt,
                    IsActive = user.IsActive
                });
            }

            return Ok(usersWithRoles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users by role {Role}", role);
            return StatusCode(500, new List<UserWithRoleResponse>());
        }
    }

    /// <summary>
    /// Get role assignment history for a user (Admin only)
    /// </summary>
    [HttpGet("history/{userId}")]
    [AdminOnly]
    public async Task<ActionResult<List<RoleAssignmentResponse>>> GetUserRoleHistory(int userId)
    {
        try
        {
            var roleHistory = await _roleService.GetUserRoleHistoryAsync(userId);
            var response = roleHistory.Select(r => new RoleAssignmentResponse
            {
                Id = r.Id,
                UserId = r.UserId,
                Role = r.Role,
                RoleDisplayName = r.Role.GetDisplayName(),
                AssignedAt = r.AssignedAt,
                AssignedByUserId = r.AssignedByUserId,
                AssignedByEmail = r.AssignedByUser?.Email,
                RevokedAt = r.RevokedAt,
                RevokedByUserId = r.RevokedByUserId,
                RevokedByEmail = r.RevokedByUser?.Email,
                IsActive = r.IsActive,
                Notes = r.Notes
            }).ToList();

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting role history for user {UserId}", userId);
            return StatusCode(500, new List<RoleAssignmentResponse>());
        }
    }

    /// <summary>
    /// Get role statistics (Admin only)
    /// </summary>
    [HttpGet("statistics")]
    [AdminOnly]
    public async Task<ActionResult<RoleStatisticsResponse>> GetRoleStatistics()
    {
        try
        {
            var roleStats = await _roleService.GetRoleStatisticsAsync();
            var totalUsers = await _userService.GetActiveUserCountAsync();
            var usersWithRoles = roleStats.Values.Sum();

            var response = new RoleStatisticsResponse
            {
                RoleCounts = roleStats.ToDictionary(
                    kvp => kvp.Key.GetDisplayName(), 
                    kvp => kvp.Value),
                TotalActiveUsers = totalUsers,
                UsersWithoutRole = totalUsers - usersWithRoles
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting role statistics");
            return StatusCode(500, new RoleStatisticsResponse());
        }
    }

    /// <summary>
    /// Get current user's role and permissions
    /// </summary>
    [HttpGet("my-role")]
    [ValidUser]
    public async Task<ActionResult<object>> GetMyRole()
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                return Unauthorized();
            }

            var currentRole = await _roleService.GetUserCurrentRoleAsync(currentUserId.Value);
            if (!currentRole.HasValue)
            {
                return Ok(new
                {
                    HasRole = false,
                    Message = "No role assigned"
                });
            }

            return Ok(new
            {
                HasRole = true,
                Role = currentRole.Value.ToString(),
                RoleDisplayName = currentRole.Value.GetDisplayName(),
                Description = currentRole.Value.GetDescription(),
                Permissions = currentRole.Value.GetPermissions(),
                HierarchyLevel = currentRole.Value.GetHierarchyLevel()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current user role");
            return StatusCode(500, new ApiResponse
            {
                Success = false,
                Message = "An error occurred while retrieving role information"
            });
        }
    }

    /// <summary>
    /// Check if current user has specific permission
    /// </summary>
    [HttpGet("check-permission/{permission}")]
    [ValidUser]
    public async Task<ActionResult<object>> CheckPermission(string permission)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                return Unauthorized();
            }

            var hasPermission = await _roleService.UserHasPermissionAsync(currentUserId.Value, permission);

            return Ok(new
            {
                Permission = permission,
                HasPermission = hasPermission
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking permission {Permission}", permission);
            return StatusCode(500, new
            {
                Permission = permission,
                HasPermission = false,
                Error = "An error occurred while checking permission"
            });
        }
    }

    /// <summary>
    /// Get available roles and their descriptions (Admin only)
    /// </summary>
    [HttpGet("available")]
    [AdminOnly]
    public ActionResult<object> GetAvailableRoles()
    {
        try
        {
            var roles = Enum.GetValues<UserRole>().Select(role => new
            {
                Value = role,
                Name = role.ToString(),
                DisplayName = role.GetDisplayName(),
                Description = role.GetDescription(),
                Permissions = role.GetPermissions(),
                HierarchyLevel = role.GetHierarchyLevel()
            }).ToList();

            return Ok(roles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available roles");
            return StatusCode(500, new ApiResponse
            {
                Success = false,
                Message = "An error occurred while retrieving available roles"
            });
        }
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }
        return null;
    }
}