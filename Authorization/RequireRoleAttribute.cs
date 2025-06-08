using AuthService.Models.Enums;
using Microsoft.AspNetCore.Authorization;

namespace AuthService.Authorization;

public class RequireRoleAttribute : AuthorizeAttribute
{
    public RequireRoleAttribute(UserRole role)
    {
        Roles = role.ToString();
    }

    public RequireRoleAttribute(params UserRole[] roles)
    {
        Roles = string.Join(",", roles.Select(r => r.ToString()));
    }
}

public class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permission)
    {
        Policy = $"Permission_{permission}";
    }
}

public class AdminOnlyAttribute : RequireRoleAttribute
{
    public AdminOnlyAttribute() : base(UserRole.Admin) { }
}

public class User1AccessAttribute : AuthorizeAttribute
{
    public User1AccessAttribute()
    {
        Policy = "User1Access";
    }
}

public class User2AccessAttribute : AuthorizeAttribute
{
    public User2AccessAttribute()
    {
        Policy = "User2Access";
    }
}

public class ValidUserAttribute : AuthorizeAttribute
{
    public ValidUserAttribute()
    {
        Policy = "ValidUser";
    }
}