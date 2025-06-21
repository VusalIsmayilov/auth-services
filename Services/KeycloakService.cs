using System.Text;
using System.Text.Json;
using AuthService.Models;
using AuthService.Models.Enums;
using AuthService.Services.Interfaces;

namespace AuthService.Services
{
    public class KeycloakService : IKeycloakService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<KeycloakService> _logger;
        
        private readonly string _baseUrl;
        private readonly string _platformRealmUrl;
        private readonly string _servicesRealmUrl;
        private readonly string _platformClientId;
        private readonly string _platformClientSecret;
        private readonly string _servicesClientId;
        private readonly string _servicesClientSecret;

        public KeycloakService(
            HttpClient httpClient, 
            IConfiguration configuration, 
            ILogger<KeycloakService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            
            _baseUrl = _configuration["Keycloak:BaseUrl"] ?? "http://localhost:8082";
            _platformRealmUrl = $"{_baseUrl}/admin/realms/platform";
            _servicesRealmUrl = $"{_baseUrl}/admin/realms/services";
            
            _platformClientId = _configuration["Keycloak:PlatformRealm:ClientId"] ?? "authservice-platform";
            _platformClientSecret = _configuration["Keycloak:PlatformRealm:ClientSecret"] ?? "";
            _servicesClientId = _configuration["Keycloak:ServiceRealm:ClientId"] ?? "authservice-backend";
            _servicesClientSecret = _configuration["Keycloak:ServiceRealm:ClientSecret"] ?? "";
        }

        public async Task<string?> CreateUserAsync(string email, string firstName, string lastName, string password)
        {
            try
            {
                _logger.LogInformation("Starting user creation in Keycloak for: {Email}", email);
                var token = await GetAdminTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogError("Failed to get admin token for user creation");
                    return null;
                }
                
                _logger.LogDebug("Successfully obtained admin token for user creation");

                var userRequest = new
                {
                    email = email,
                    firstName = firstName,
                    lastName = lastName,
                    username = email,
                    enabled = true,
                    emailVerified = false,
                    credentials = new[]
                    {
                        new
                        {
                            type = "password",
                            value = "TemporaryPassword123!@#$%^&*",
                            temporary = true
                        }
                    }
                };

                var json = JsonSerializer.Serialize(userRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                using var request = new HttpRequestMessage(HttpMethod.Post, $"{_platformRealmUrl}/users");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                request.Content = content;

                var response = await _httpClient.SendAsync(request);
                
                if (response.IsSuccessStatusCode)
                {
                    var location = response.Headers.Location?.ToString();
                    if (!string.IsNullOrEmpty(location))
                    {
                        var userId = location.Split('/').Last();
                        _logger.LogInformation("Created user in Keycloak platform realm: {UserId}", userId);
                        return userId;
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to create user in Keycloak: {StatusCode} - {Error}", 
                        response.StatusCode, errorContent);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user in Keycloak");
                return null;
            }
        }

        public async Task<bool> UpdateUserAsync(string keycloakId, string email, string firstName, string lastName)
        {
            try
            {
                var token = await GetAdminTokenAsync();
                if (string.IsNullOrEmpty(token))
                    return false;

                var userUpdate = new
                {
                    email = email,
                    firstName = firstName,
                    lastName = lastName,
                    username = email
                };

                var json = JsonSerializer.Serialize(userUpdate);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                using var request = new HttpRequestMessage(HttpMethod.Put, $"{_platformRealmUrl}/users/{keycloakId}");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                request.Content = content;

                var response = await _httpClient.SendAsync(request);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Updated user in Keycloak: {UserId}", keycloakId);
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to update user in Keycloak: {StatusCode} - {Error}", 
                        response.StatusCode, errorContent);
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user in Keycloak: {UserId}", keycloakId);
                return false;
            }
        }

        public async Task<bool> AssignRoleAsync(string keycloakId, UserRole role)
        {
            try
            {
                var token = await GetAdminTokenAsync();
                if (string.IsNullOrEmpty(token))
                    return false;

                var roleName = GetKeycloakRoleName(role);
                var realmUrl = role.IsPlatformRole() ? _platformRealmUrl : _servicesRealmUrl;

                // First, get the role information
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var roleResponse = await _httpClient.GetAsync($"{realmUrl}/roles/{roleName}");
                if (!roleResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("Role {RoleName} not found in Keycloak", roleName);
                    return false;
                }

                var roleJson = await roleResponse.Content.ReadAsStringAsync();
                var roleData = JsonSerializer.Deserialize<JsonElement>(roleJson);

                var roleAssignment = new[]
                {
                    new
                    {
                        id = roleData.GetProperty("id").GetString(),
                        name = roleData.GetProperty("name").GetString()
                    }
                };

                var json = JsonSerializer.Serialize(roleAssignment);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{realmUrl}/users/{keycloakId}/role-mappings/realm", content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Assigned role {RoleName} to user {UserId} in Keycloak", roleName, keycloakId);
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to assign role in Keycloak: {StatusCode} - {Error}", 
                        response.StatusCode, errorContent);
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning role in Keycloak: {UserId}, {Role}", keycloakId, role);
                return false;
            }
        }

        public async Task<bool> RemoveRoleAsync(string keycloakId, UserRole role)
        {
            try
            {
                var token = await GetAdminTokenAsync();
                if (string.IsNullOrEmpty(token))
                    return false;

                var roleName = GetKeycloakRoleName(role);
                var realmUrl = role.IsPlatformRole() ? _platformRealmUrl : _servicesRealmUrl;

                _httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                // Get role information
                var roleResponse = await _httpClient.GetAsync($"{realmUrl}/roles/{roleName}");
                if (!roleResponse.IsSuccessStatusCode)
                    return false;

                var roleJson = await roleResponse.Content.ReadAsStringAsync();
                var roleData = JsonSerializer.Deserialize<JsonElement>(roleJson);

                var roleAssignment = new[]
                {
                    new
                    {
                        id = roleData.GetProperty("id").GetString(),
                        name = roleData.GetProperty("name").GetString()
                    }
                };

                var json = JsonSerializer.Serialize(roleAssignment);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Delete, 
                    $"{realmUrl}/users/{keycloakId}/role-mappings/realm")
                {
                    Content = content
                };

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Removed role {RoleName} from user {UserId} in Keycloak", roleName, keycloakId);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing role in Keycloak: {UserId}, {Role}", keycloakId, role);
                return false;
            }
        }

        public async Task<bool> SetUserEnabledAsync(string keycloakId, bool enabled)
        {
            try
            {
                var token = await GetAdminTokenAsync();
                if (string.IsNullOrEmpty(token))
                    return false;

                var userUpdate = new { enabled = enabled };

                _httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var json = JsonSerializer.Serialize(userUpdate);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync($"{_platformRealmUrl}/users/{keycloakId}", content);
                
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting user enabled status in Keycloak: {UserId}", keycloakId);
                return false;
            }
        }

        public async Task<bool> DeleteUserAsync(string keycloakId)
        {
            try
            {
                var token = await GetAdminTokenAsync();
                if (string.IsNullOrEmpty(token))
                    return false;

                _httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.DeleteAsync($"{_platformRealmUrl}/users/{keycloakId}");
                
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user in Keycloak: {UserId}", keycloakId);
                return false;
            }
        }

        public async Task<KeycloakUser?> GetUserAsync(string keycloakId)
        {
            try
            {
                var token = await GetAdminTokenAsync();
                if (string.IsNullOrEmpty(token))
                    return null;

                _httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.GetAsync($"{_platformRealmUrl}/users/{keycloakId}");
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return ParseKeycloakUser(json);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user from Keycloak: {UserId}", keycloakId);
                return null;
            }
        }

        public async Task<KeycloakUser?> GetUserByEmailAsync(string email)
        {
            try
            {
                var token = await GetAdminTokenAsync();
                if (string.IsNullOrEmpty(token))
                    return null;

                _httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.GetAsync($"{_platformRealmUrl}/users?email={Uri.EscapeDataString(email)}");
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var users = JsonSerializer.Deserialize<JsonElement[]>(json);
                    
                    if (users?.Length > 0)
                    {
                        return ParseKeycloakUser(users[0].GetRawText());
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by email from Keycloak: {Email}", email);
                return null;
            }
        }

        public async Task<string?> AuthenticateUserAsync(string email, string password)
        {
            try
            {
                var tokenRequest = new Dictionary<string, string>
                {
                    ["grant_type"] = "password",
                    ["client_id"] = _platformClientId,
                    ["client_secret"] = _platformClientSecret,
                    ["username"] = email,
                    ["password"] = password
                };

                var content = new FormUrlEncodedContent(tokenRequest);
                var response = await _httpClient.PostAsync($"{_baseUrl}/realms/platform/protocol/openid-connect/token", content);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var tokenData = JsonSerializer.Deserialize<JsonElement>(json);
                    return tokenData.GetProperty("access_token").GetString();
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error authenticating user in Keycloak: {Email}", email);
                return null;
            }
        }

        public async Task<string?> GetAdminTokenAsync()
        {
            try
            {
                var tokenRequest = new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = _platformClientId,
                    ["client_secret"] = _platformClientSecret
                };

                var content = new FormUrlEncodedContent(tokenRequest);
                var response = await _httpClient.PostAsync($"{_baseUrl}/realms/platform/protocol/openid-connect/token", content);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var tokenData = JsonSerializer.Deserialize<JsonElement>(json);
                    var token = tokenData.GetProperty("access_token").GetString();
                    _logger.LogDebug("Successfully obtained admin token from Keycloak");
                    return token;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to get admin token from Keycloak: {StatusCode} - {Error}", 
                        response.StatusCode, errorContent);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting admin token from Keycloak");
                return null;
            }
        }

        public async Task<bool> SyncUserAsync(User user)
        {
            try
            {
                if (string.IsNullOrEmpty(user.KeycloakId))
                {
                    // User doesn't exist in Keycloak, skip sync
                    return true;
                }

                var keycloakUser = await GetUserAsync(user.KeycloakId);
                if (keycloakUser == null)
                {
                    _logger.LogWarning("User not found in Keycloak: {KeycloakId}", user.KeycloakId);
                    return false;
                }

                // Update user info if different
                if (keycloakUser.Email != user.Email ||
                    keycloakUser.FirstName != user.FirstName ||
                    keycloakUser.LastName != user.LastName)
                {
                    await UpdateUserAsync(user.KeycloakId, user.Email ?? "", user.FirstName ?? "", user.LastName ?? "");
                }

                // Sync enabled status
                await SetUserEnabledAsync(user.KeycloakId, user.IsActive);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing user with Keycloak: {UserId}", user.Id);
                return false;
            }
        }

        private string GetKeycloakRoleName(UserRole role)
        {
            return role switch
            {
                UserRole.PlatformAdmin => "platform-admin",
                UserRole.Homeowner => "homeowner",
                UserRole.Contractor => "contractor",
                UserRole.ProjectManager => "project-manager",
                UserRole.ServiceClient => "service-client",
                _ => role.ToString().ToLowerInvariant()
            };
        }

        private KeycloakUser? ParseKeycloakUser(string json)
        {
            try
            {
                var userData = JsonSerializer.Deserialize<JsonElement>(json);
                
                return new KeycloakUser
                {
                    Id = userData.GetProperty("id").GetString() ?? "",
                    Email = userData.TryGetProperty("email", out var email) ? email.GetString() ?? "" : "",
                    FirstName = userData.TryGetProperty("firstName", out var firstName) ? firstName.GetString() ?? "" : "",
                    LastName = userData.TryGetProperty("lastName", out var lastName) ? lastName.GetString() ?? "" : "",
                    Enabled = userData.TryGetProperty("enabled", out var enabled) && enabled.GetBoolean(),
                    CreatedAt = userData.TryGetProperty("createdTimestamp", out var created) 
                        ? DateTimeOffset.FromUnixTimeMilliseconds(created.GetInt64()).DateTime
                        : DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing Keycloak user data");
                return null;
            }
        }
    }
}