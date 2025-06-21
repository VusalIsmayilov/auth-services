using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthService.Migrations
{
    /// <inheritdoc />
    public partial class MigrateToNewRoleSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Migration script to convert old roles to new role system
            // This migration handles the transition from legacy roles (Admin, User1, User2) 
            // to the new platform/service role system
            
            migrationBuilder.Sql(@"
                -- Update existing role assignments to use new role values
                -- Map legacy roles to new platform roles
                
                -- Admin (1) -> PlatformAdmin (10)
                UPDATE ""UserRoleAssignments"" 
                SET ""Role"" = 10 
                WHERE ""Role"" = 1 AND ""IsActive"" = true;
                
                -- User1 (2) -> Homeowner (11) - assuming User1 represents homeowners
                UPDATE ""UserRoleAssignments"" 
                SET ""Role"" = 11 
                WHERE ""Role"" = 2 AND ""IsActive"" = true;
                
                -- User2 (3) -> Contractor (12) - assuming User2 represents contractors
                UPDATE ""UserRoleAssignments"" 
                SET ""Role"" = 12 
                WHERE ""Role"" = 3 AND ""IsActive"" = true;
                
                -- Add audit trail for the role migration
                INSERT INTO ""UserRoleAssignments"" (""UserId"", ""Role"", ""AssignedAt"", ""AssignedByUserId"", ""IsActive"", ""Notes"")
                SELECT 
                    ur.""UserId"",
                    CASE 
                        WHEN ur.""Role"" = 10 THEN 10  -- PlatformAdmin
                        WHEN ur.""Role"" = 11 THEN 11  -- Homeowner  
                        WHEN ur.""Role"" = 12 THEN 12  -- Contractor
                    END,
                    NOW(),
                    ur.""AssignedByUserId"",
                    false, -- Mark as inactive audit record
                    'Legacy role migration: ' || 
                    CASE 
                        WHEN ur.""Role"" = 10 THEN 'Admin -> PlatformAdmin'
                        WHEN ur.""Role"" = 11 THEN 'User1 -> Homeowner'
                        WHEN ur.""Role"" = 12 THEN 'User2 -> Contractor'
                    END
                FROM ""UserRoleAssignments"" ur
                WHERE ur.""Role"" IN (10, 11, 12) 
                AND ur.""IsActive"" = true
                AND NOT EXISTS (
                    SELECT 1 FROM ""UserRoleAssignments"" ur2 
                    WHERE ur2.""UserId"" = ur.""UserId"" 
                    AND ur2.""Role"" = ur.""Role"" 
                    AND ur2.""IsActive"" = false 
                    AND ur2.""Notes"" LIKE 'Legacy role migration:%'
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rollback script to restore legacy roles if needed
            migrationBuilder.Sql(@"
                -- Rollback: Convert new roles back to legacy roles
                -- This should rarely be used, but provides a safety net
                
                -- Remove migration audit records
                DELETE FROM ""UserRoleAssignments"" 
                WHERE ""Notes"" LIKE 'Legacy role migration:%' AND ""IsActive"" = false;
                
                -- PlatformAdmin (10) -> Admin (1)
                UPDATE ""UserRoleAssignments"" 
                SET ""Role"" = 1 
                WHERE ""Role"" = 10 AND ""IsActive"" = true;
                
                -- Homeowner (11) -> User1 (2)
                UPDATE ""UserRoleAssignments"" 
                SET ""Role"" = 2 
                WHERE ""Role"" = 11 AND ""IsActive"" = true;
                
                -- Contractor (12) -> User2 (3)
                UPDATE ""UserRoleAssignments"" 
                SET ""Role"" = 3 
                WHERE ""Role"" = 12 AND ""IsActive"" = true;
            ");
        }
    }
}