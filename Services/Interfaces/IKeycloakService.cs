using AuthService.Models;
using AuthService.Models.Enums;

namespace AuthService.Services.Interfaces
{
    public interface IKeycloakService
    {
        /// <summary>
        /// Creates a user in the Keycloak platform realm
        /// </summary>
        Task<string?> CreateUserAsync(string email, string firstName, string lastName, string password);

        /// <summary>
        /// Updates user information in Keycloak
        /// </summary>
        Task<bool> UpdateUserAsync(string keycloakId, string email, string firstName, string lastName);

        /// <summary>
        /// Assigns a role to a user in the appropriate Keycloak realm
        /// </summary>
        Task<bool> AssignRoleAsync(string keycloakId, UserRole role);

        /// <summary>
        /// Removes a role from a user in Keycloak
        /// </summary>
        Task<bool> RemoveRoleAsync(string keycloakId, UserRole role);

        /// <summary>
        /// Enables or disables a user account in Keycloak
        /// </summary>
        Task<bool> SetUserEnabledAsync(string keycloakId, bool enabled);

        /// <summary>
        /// Deletes a user from Keycloak
        /// </summary>
        Task<bool> DeleteUserAsync(string keycloakId);

        /// <summary>
        /// Gets user information from Keycloak by ID
        /// </summary>
        Task<KeycloakUser?> GetUserAsync(string keycloakId);

        /// <summary>
        /// Gets user information from Keycloak by email
        /// </summary>
        Task<KeycloakUser?> GetUserByEmailAsync(string email);

        /// <summary>
        /// Validates user credentials against Keycloak
        /// </summary>
        Task<string?> AuthenticateUserAsync(string email, string password);

        /// <summary>
        /// Gets an admin access token for service operations
        /// </summary>
        Task<string?> GetAdminTokenAsync();

        /// <summary>
        /// Synchronizes local user with Keycloak user
        /// </summary>
        Task<bool> SyncUserAsync(User user);
    }

    public class KeycloakUser
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public List<string> Roles { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }
}