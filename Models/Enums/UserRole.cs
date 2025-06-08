namespace AuthService.Models.Enums;

public enum UserRole
{
    Admin = 1,
    User1 = 2,
    User2 = 3
}

public static class UserRoleExtensions
{
    public static string GetDisplayName(this UserRole role)
    {
        return role switch
        {
            UserRole.Admin => "Administrator",
            UserRole.User1 => "User Type 1",
            UserRole.User2 => "User Type 2",
            _ => role.ToString()
        };
    }

    public static string GetDescription(this UserRole role)
    {
        return role switch
        {
            UserRole.Admin => "Full system access with administrative capabilities",
            UserRole.User1 => "User Type 1 with specific access permissions",
            UserRole.User2 => "User Type 2 with different access permissions",
            _ => "Unknown role"
        };
    }

    public static List<string> GetPermissions(this UserRole role)
    {
        return role switch
        {
            UserRole.Admin => new List<string>
            {
                "admin:full_access", "user:create", "user:read", "user:update", "user:delete",
                "role:assign", "role:revoke", "system:manage", "auth:manage", 
                "email:manage", "token:manage", "reports:admin"
            },
            UserRole.User1 => new List<string>
            {
                "user1:access", "profile:read", "profile:update", "data:read", 
                "reports:view", "auth:basic"
            },
            UserRole.User2 => new List<string>
            {
                "user2:access", "profile:read", "profile:update", "data:write", 
                "data:read", "auth:basic"
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
            UserRole.Admin => true, // Admins can access everything
            UserRole.User1 => targetRole == UserRole.User1,
            UserRole.User2 => targetRole == UserRole.User2,
            _ => false
        };
    }

    public static int GetHierarchyLevel(this UserRole role)
    {
        return role switch
        {
            UserRole.Admin => 1,
            UserRole.User1 => 2,
            UserRole.User2 => 2,
            _ => 999
        };
    }
}