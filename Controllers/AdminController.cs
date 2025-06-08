using Microsoft.AspNetCore.Mvc;
using AuthService.DTOs;
using AuthService.Models.Enums;
using AuthService.Services.Interfaces;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;

namespace AuthService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IRoleService _roleService;
    private readonly IPasswordService _passwordService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IUserService userService,
        IRoleService roleService,
        IPasswordService passwordService,
        ILogger<AdminController> logger)
    {
        _userService = userService;
        _roleService = roleService;
        _passwordService = passwordService;
        _logger = logger;
    }

    /// <summary>
    /// Bootstrap: Create initial admin user (only works if no admin exists)
    /// </summary>
    [HttpPost("bootstrap")]
    public async Task<ActionResult<ApiResponse>> BootstrapAdmin([FromBody] RegisterEmailRequest request)
    {
        try
        {
            // Check if any admin user already exists
            var existingAdmins = await _roleService.GetUsersByRoleAsync(UserRole.Admin);
            if (existingAdmins.Any())
            {
                return BadRequest(new ApiResponse
                {
                    Success = false,
                    Message = "Admin user already exists. Bootstrap not allowed."
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

            // Assign admin role (use user ID 1 as bootstrap admin)
            var roleAssigned = await _roleService.AssignRoleAsync(
                user.Id, 
                UserRole.Admin, 
                user.Id, // Self-assigned during bootstrap
                "Bootstrap admin user");

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
            var adminCount = roleStats.GetValueOrDefault(UserRole.Admin, 0);

            return Ok(new
            {
                TotalUsers = totalUsers,
                AdminUsers = adminCount,
                User1Count = roleStats.GetValueOrDefault(UserRole.User1, 0),
                User2Count = roleStats.GetValueOrDefault(UserRole.User2, 0),
                UsersWithoutRole = totalUsers - roleStats.Values.Sum(),
                IsBootstrapRequired = adminCount == 0,
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
            // Check if any admin exists
            var existingAdmins = await _roleService.GetUsersByRoleAsync(UserRole.Admin);
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
}

public class QuickRoleAssignRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public UserRole Role { get; set; }
}