using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using AuthService.DTOs;
using AuthService.Models.Enums;
using AuthService.Services.Interfaces;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;

namespace AuthService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "PlatformAdmin")]
public class AdminController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IRoleService _roleService;
    private readonly IPasswordService _passwordService;
    private readonly IKeycloakService _keycloakService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IUserService userService,
        IRoleService roleService,
        IPasswordService passwordService,
        IKeycloakService keycloakService,
        ILogger<AdminController> logger)
    {
        _userService = userService;
        _roleService = roleService;
        _passwordService = passwordService;
        _keycloakService = keycloakService;
        _logger = logger;
    }

    /// <summary>
    /// Bootstrap: Create initial admin user (only works if no admin exists)
    /// </summary>
    [HttpPost("bootstrap")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse>> BootstrapAdmin([FromBody] RegisterEmailRequest request)
    {
        try
        {
            // Check if any platform admin user already exists
            var existingAdmins = await _roleService.GetUsersByRoleAsync(UserRole.PlatformAdmin);
            if (existingAdmins.Any())
            {
                return BadRequest(new ApiResponse
                {
                    Success = false,
                    Message = "Platform admin user already exists. Bootstrap not allowed."
                });
            }

            // Check if email is already in use
            if (await _userService.IsEmailInUseAsync(request.Email))
            {
                return BadRequest(new ApiResponse
                {
                    Success = false,
                    Message = "Email already in use"
                });
            }

            // Create the user
            var user = await _userService.RegisterWithEmailAsync(request.Email, request.Password);
            if (user == null)
            {
                return BadRequest(new ApiResponse
                {
                    Success = false,
                    Message = "Failed to create admin user"
                });
            }

            // Assign platform admin role (use user ID 1 as bootstrap admin)
            var roleAssigned = await _roleService.AssignRoleAsync(
                user.Id, 
                UserRole.PlatformAdmin, 
                user.Id, // Self-assigned during bootstrap
                "Bootstrap platform admin user");

            if (!roleAssigned)
            {
                return BadRequest(new ApiResponse
                {
                    Success = false,
                    Message = "Failed to assign admin role"
                });
            }

            _logger.LogWarning("Bootstrap admin user created: {Email} with ID: {UserId}", request.Email, user.Id);

            return Ok(new ApiResponse
            {
                Success = true,
                Message = $"Bootstrap admin user created successfully. User ID: {user.Id}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during admin bootstrap");
            return StatusCode(500, new ApiResponse
            {
                Success = false,
                Message = "An error occurred during admin bootstrap"
            });
        }
    }

    /// <summary>
    /// Get system status and configuration info
    /// </summary>
    [HttpGet("status")]
    public async Task<ActionResult<object>> GetSystemStatus()
    {
        try
        {
            var totalUsers = await _userService.GetActiveUserCountAsync();
            var roleStats = await _roleService.GetRoleStatisticsAsync();
            var platformAdminCount = roleStats.GetValueOrDefault(UserRole.PlatformAdmin, 0);

            return Ok(new
            {
                TotalUsers = totalUsers,
                PlatformAdminUsers = platformAdminCount,
                HomeownerCount = roleStats.GetValueOrDefault(UserRole.Homeowner, 0),
                ContractorCount = roleStats.GetValueOrDefault(UserRole.Contractor, 0),
                ProjectManagerCount = roleStats.GetValueOrDefault(UserRole.ProjectManager, 0),
                ServiceClientCount = roleStats.GetValueOrDefault(UserRole.ServiceClient, 0),
                UsersWithoutRole = totalUsers - roleStats.Values.Sum(),
                IsBootstrapRequired = platformAdminCount == 0,
                AvailableRoles = Enum.GetValues<UserRole>().Select(r => new
                {
                    Value = r,
                    Name = r.ToString(),
                    DisplayName = r.GetDisplayName(),
                    Description = r.GetDescription()
                }).ToList(),
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system status");
            return StatusCode(500, new ApiResponse
            {
                Success = false,
                Message = "An error occurred while retrieving system status"
            });
        }
    }

    /// <summary>
    /// Quick role assignment for development/testing (no authentication required until first admin exists)
    /// </summary>
    [HttpPost("quick-assign-role")]
    public async Task<ActionResult<ApiResponse>> QuickAssignRole([FromBody] QuickRoleAssignRequest request)
    {
        try
        {
            // Check if any platform admin exists
            var existingAdmins = await _roleService.GetUsersByRoleAsync(UserRole.PlatformAdmin);
            if (existingAdmins.Any())
            {
                return BadRequest(new ApiResponse
                {
                    Success = false,
                    Message = "Quick role assignment disabled. Admin users exist. Use proper role management endpoints."
                });
            }

            // Find user by email
            var user = await _userService.GetUserByEmailAsync(request.Email);
            if (user == null)
            {
                return BadRequest(new ApiResponse
                {
                    Success = false,
                    Message = "User not found"
                });
            }

            // Check if user already has a role
            var currentRole = await _roleService.GetUserCurrentRoleAsync(user.Id);
            if (currentRole.HasValue)
            {
                return BadRequest(new ApiResponse
                {
                    Success = false,
                    Message = $"User already has role: {currentRole.Value.GetDisplayName()}"
                });
            }

            // Assign role (use user ID 1 as system admin)
            var roleAssigned = await _roleService.AssignRoleAsync(
                user.Id, 
                request.Role, 
                user.Id, // Self-assigned during quick setup
                "Quick role assignment for development");

            if (roleAssigned)
            {
                _logger.LogWarning("Quick role assignment: {Role} assigned to {Email}", request.Role, request.Email);
                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = $"Role {request.Role.GetDisplayName()} assigned to {request.Email}"
                });
            }

            return BadRequest(new ApiResponse
            {
                Success = false,
                Message = "Failed to assign role"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during quick role assignment");
            return StatusCode(500, new ApiResponse
            {
                Success = false,
                Message = "An error occurred during role assignment"
            });
        }
    }

    /// <summary>
    /// Sync user with Keycloak (admin operation)
    /// </summary>
    [HttpPost("sync-user-keycloak/{userId}")]
    public async Task<ActionResult<ApiResponse>> SyncUserWithKeycloak(int userId)
    {
        try
        {
            var user = await _userService.GetUserByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new ApiResponse
                {
                    Success = false,
                    Message = "User not found"
                });
            }

            var success = await _keycloakService.SyncUserAsync(user);
            if (success)
            {
                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = $"User {user.Email} synchronized with Keycloak successfully"
                });
            }
            else
            {
                return BadRequest(new ApiResponse
                {
                    Success = false,
                    Message = "Failed to synchronize user with Keycloak"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing user {UserId} with Keycloak", userId);
            return StatusCode(500, new ApiResponse
            {
                Success = false,
                Message = "An error occurred during Keycloak synchronization"
            });
        }
    }

    /// <summary>
    /// Get Keycloak user information
    /// </summary>
    [HttpGet("keycloak-user/{userId}")]
    public async Task<ActionResult<object>> GetKeycloakUser(int userId)
    {
        try
        {
            var user = await _userService.GetUserByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new ApiResponse
                {
                    Success = false,
                    Message = "User not found"
                });
            }

            if (string.IsNullOrEmpty(user.KeycloakId))
            {
                return BadRequest(new ApiResponse
                {
                    Success = false,
                    Message = "User is not linked to Keycloak"
                });
            }

            var keycloakUser = await _keycloakService.GetUserAsync(user.KeycloakId);
            if (keycloakUser == null)
            {
                return NotFound(new ApiResponse
                {
                    Success = false,
                    Message = "User not found in Keycloak"
                });
            }

            return Ok(new
            {
                Success = true,
                Data = new
                {
                    LocalUser = new
                    {
                        user.Id,
                        user.Email,
                        user.FirstName,
                        user.LastName,
                        user.KeycloakId,
                        user.IsActive,
                        CurrentRole = user.GetCurrentRole()
                    },
                    KeycloakUser = keycloakUser
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Keycloak user for {UserId}", userId);
            return StatusCode(500, new ApiResponse
            {
                Success = false,
                Message = "An error occurred while retrieving Keycloak user information"
            });
        }
    }
}

public class QuickRoleAssignRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public UserRole Role { get; set; }
}