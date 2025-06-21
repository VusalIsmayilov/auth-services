using Microsoft.AspNetCore.Mvc;
using AuthService.Services.Interfaces;

namespace AuthService.Controllers
{
    [ApiController]
    [Route(".well-known")]
    public class JwksController : ControllerBase
    {
        private readonly ITokenService _tokenService;
        private readonly ILogger<JwksController> _logger;

        public JwksController(ITokenService tokenService, ILogger<JwksController> logger)
        {
            _tokenService = tokenService;
            _logger = logger;
        }

        /// <summary>
        /// Get JSON Web Key Set (JWKS) for JWT verification
        /// </summary>
        [HttpGet("jwks.json")]
        [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
        public ActionResult<object> GetJwks()
        {
            try
            {
                var jwks = _tokenService.GetJwks();
                
                // Add cache headers for external caching
                Response.Headers["Cache-Control"] = "public, max-age=3600";
                Response.Headers["ETag"] = $"\"{DateTime.UtcNow:yyyyMMddHH}\"";
                
                _logger.LogDebug("JWKS endpoint accessed");
                return Ok(jwks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving JWKS");
                return StatusCode(500, new { error = "Unable to retrieve JWKS" });
            }
        }

        /// <summary>
        /// Get OpenID Connect discovery document
        /// </summary>
        [HttpGet("openid_configuration")]
        [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
        public ActionResult<object> GetOpenIdConfiguration()
        {
            try
            {
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                
                var config = new
                {
                    issuer = baseUrl,
                    jwks_uri = $"{baseUrl}/.well-known/jwks.json",
                    authorization_endpoint = $"{baseUrl}/api/auth/login/email",
                    token_endpoint = $"{baseUrl}/api/auth/refresh-token",
                    userinfo_endpoint = $"{baseUrl}/api/auth/me",
                    registration_endpoint = $"{baseUrl}/api/auth/register/email",
                    response_types_supported = new[] { "code", "token" },
                    subject_types_supported = new[] { "public" },
                    id_token_signing_alg_values_supported = new[] { "HS256" },
                    scopes_supported = new[] { "openid", "profile", "email" },
                    token_endpoint_auth_methods_supported = new[] { "client_secret_basic", "client_secret_post" },
                    claims_supported = new[] { "sub", "iss", "aud", "exp", "iat", "email", "email_verified", "phone", "phone_verified" }
                };

                Response.Headers["Cache-Control"] = "public, max-age=3600";
                Response.Headers["ETag"] = $"\"{DateTime.UtcNow:yyyyMMddHH}\"";

                _logger.LogDebug("OpenID Connect configuration accessed");
                return Ok(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving OpenID Connect configuration");
                return StatusCode(500, new { error = "Unable to retrieve OpenID Connect configuration" });
            }
        }
    }
}