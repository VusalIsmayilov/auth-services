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

public class PlatformAdminOnlyAttribute : RequireRoleAttribute
{
    public PlatformAdminOnlyAttribute() : base(UserRole.PlatformAdmin) { }
}

public class HomeownerAccessAttribute : RequireRoleAttribute
{
    public HomeownerAccessAttribute() : base(UserRole.Homeowner) { }
}

public class ContractorAccessAttribute : RequireRoleAttribute
{
    public ContractorAccessAttribute() : base(UserRole.Contractor) { }
}

public class ProjectManagerAccessAttribute : RequireRoleAttribute
{
    public ProjectManagerAccessAttribute() : base(UserRole.ProjectManager) { }
}

public class ServiceClientAccessAttribute : RequireRoleAttribute
{
    public ServiceClientAccessAttribute() : base(UserRole.ServiceClient) { }
}

public class PlatformUserAttribute : AuthorizeAttribute
{
    public PlatformUserAttribute()
    {
        Policy = "PlatformUser";
    }
}

public class AnyRealmAttribute : AuthorizeAttribute
{
    public AnyRealmAttribute()
    {
        Policy = "AnyRealm";
    }
}

public class PlatformRealmAttribute : AuthorizeAttribute
{
    public PlatformRealmAttribute()
    {
        Policy = "PlatformRealm";
    }
}

public class ServiceRealmAttribute : AuthorizeAttribute
{
    public ServiceRealmAttribute()
    {
        Policy = "ServiceRealm";
    }
}

public class ValidUserAttribute : AuthorizeAttribute
{
    public ValidUserAttribute()
    {
        Policy = "PlatformUser";
    }
}

public class AuthenticatedUserAttribute : AuthorizeAttribute
{
    public AuthenticatedUserAttribute()
    {
        Policy = "AuthenticatedUser";
    }
}

// Legacy attribute for backward compatibility (maps to PlatformAdmin)
[Obsolete("Use PlatformAdminOnlyAttribute instead")]
public class AdminOnlyAttribute : RequireRoleAttribute
{
    public AdminOnlyAttribute() : base(UserRole.PlatformAdmin) { }
}