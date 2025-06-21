namespace AuthService.Models.Enums;

public enum UserRole
{
    // Platform roles (for end users)
    PlatformAdmin = 10,
    Homeowner = 11,
    Contractor = 12,
    ProjectManager = 13,
    
    // Service roles (for microservice authentication)
    ServiceClient = 20
}

public static class UserRoleExtensions
{
    public static string GetDisplayName(this UserRole role)
    {
        return role switch
        {
            // Platform roles
            UserRole.PlatformAdmin => "Platform Administrator",
            UserRole.Homeowner => "Homeowner",
            UserRole.Contractor => "Contractor",
            UserRole.ProjectManager => "Project Manager",
            
            // Service roles
            UserRole.ServiceClient => "Service Client",
            
            _ => role.ToString()
        };
    }

    public static string GetDescription(this UserRole role)
    {
        return role switch
        {
            // Platform roles
            UserRole.PlatformAdmin => "Platform administrator with access to all platform features",
            UserRole.Homeowner => "Property owner who can create renovation projects",
            UserRole.Contractor => "Professional contractor who can bid on and complete projects",
            UserRole.ProjectManager => "Project manager who oversees renovation projects",
            
            // Service roles
            UserRole.ServiceClient => "Service-to-service authentication role for microservices",
            
            _ => "Unknown role"
        };
    }

    public static List<string> GetPermissions(this UserRole role)
    {
        return role switch
        {
            // Platform roles
            UserRole.PlatformAdmin => new List<string>
            {
                "platform:admin", "user:manage", "project:manage", "contractor:manage",
                "reports:admin", "settings:manage", "billing:manage", "support:admin"
            },
            UserRole.Homeowner => new List<string>
            {
                "profile:read", "profile:update", "project:create", "project:view",
                "project:update", "contractor:search", "contractor:hire", "billing:view",
                "messages:send", "messages:receive"
            },
            UserRole.Contractor => new List<string>
            {
                "profile:read", "profile:update", "project:view", "project:bid",
                "project:accept", "project:complete", "portfolio:manage", "calendar:manage",
                "messages:send", "messages:receive", "billing:receive"
            },
            UserRole.ProjectManager => new List<string>
            {
                "profile:read", "profile:update", "project:view", "project:manage",
                "project:assign", "contractor:manage", "timeline:manage", "budget:manage",
                "reports:project", "messages:send", "messages:receive"
            },
            
            // Service roles
            UserRole.ServiceClient => new List<string>
            {
                "service:authenticate", "service:api_access", "service:token_exchange"
            },
            
            _ => new List<string>()
        };
    }

    public static bool HasPermission(this UserRole role, string permission)
    {
        return role.GetPermissions().Contains(permission);
    }

    public static bool CanAccessRole(this UserRole currentRole, UserRole targetRole)
    {
        return currentRole switch
        {
            // Platform admin can access all platform roles
            UserRole.PlatformAdmin => targetRole is UserRole.Homeowner or UserRole.Contractor or UserRole.ProjectManager or UserRole.PlatformAdmin,
            
            // Platform users can only access their own role
            UserRole.Homeowner => targetRole == UserRole.Homeowner,
            UserRole.Contractor => targetRole == UserRole.Contractor,
            UserRole.ProjectManager => targetRole is UserRole.Homeowner or UserRole.Contractor or UserRole.ProjectManager,
            
            // Service clients have no user access
            UserRole.ServiceClient => false,
            
            _ => false
        };
    }

    public static int GetHierarchyLevel(this UserRole role)
    {
        return role switch
        {
            // Platform roles
            UserRole.PlatformAdmin => 1,
            UserRole.ProjectManager => 2,
            UserRole.Homeowner => 3,
            UserRole.Contractor => 3,
            
            // Service roles
            UserRole.ServiceClient => 10,
            
            _ => 999
        };
    }

    public static string GetRealm(this UserRole role)
    {
        return role switch
        {
            // Platform roles
            UserRole.PlatformAdmin or UserRole.Homeowner or UserRole.Contractor or UserRole.ProjectManager => "platform",
            
            // Service roles
            UserRole.ServiceClient => "services",
            
            _ => "platform"
        };
    }

    public static bool IsPlatformRole(this UserRole role)
    {
        return role is UserRole.PlatformAdmin or UserRole.Homeowner or UserRole.Contractor or UserRole.ProjectManager;
    }

    public static bool IsServiceRole(this UserRole role)
    {
        return role is UserRole.ServiceClient;
    }
}