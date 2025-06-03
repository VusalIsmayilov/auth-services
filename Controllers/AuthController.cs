using AuthService.DTOs;
using AuthService.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IOtpService _otpService;
        private readonly ITokenService _tokenService;

        public AuthController(
            IUserService userService,
            IOtpService otpService,
            ITokenService tokenService)
        {
            _userService = userService;
            _otpService = otpService;
            _tokenService = tokenService;
        }

        [HttpPost("register/email")]
        public async Task<ActionResult<AuthResponse>> RegisterWithEmail([FromBody] RegisterEmailRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new AuthResponse
                {
                    Success = false,
                    Message = "Invalid request data"
                });
            }

            var result = await _userService.RegisterWithEmailAsync(request);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        [HttpPost("register/phone")]
        public async Task<ActionResult<AuthResponse>> RegisterWithPhone([FromBody] RegisterPhoneRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new AuthResponse
                {
                    Success = false,
                    Message = "Invalid request data"
                });
            }

            var result = await _userService.RegisterWithPhoneAsync(request);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        [HttpPost("login/email")]
        public async Task<ActionResult<AuthResponse>> LoginWithEmail([FromBody] LoginEmailRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new AuthResponse
                {
                    Success = false,
                    Message = "Invalid request data"
                });
            }

            var result = await _userService.LoginWithEmailAsync(request);

            if (!result.Success)
            {
                return Unauthorized(result);
            }

            return Ok(result);
        }

        [HttpPost("login/phone")]
        public async Task<ActionResult<AuthResponse>> LoginWithPhone([FromBody] LoginPhoneRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new AuthResponse
                {
                    Success = false,
                    Message = "Invalid request data"
                });
            }

            var result = await _userService.LoginWithPhoneAsync(request);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        [HttpPost("verify-otp")]
        public async Task<ActionResult<AuthResponse>> VerifyOtp([FromBody] VerifyOtpRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new AuthResponse
                {
                    Success = false,
                    Message = "Invalid request data"
                });
            }

            var result = await _userService.VerifyOtpAndLoginAsync(request);

            if (!result.Success)
            {
                return Unauthorized(result);
            }

            return Ok(result);
        }

        [HttpPost("send-otp")]
        public async Task<ActionResult<OtpResponse>> SendOtp([FromBody] LoginPhoneRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new OtpResponse
                {
                    Success = false,
                    Message = "Invalid request data"
                });
            }

            var result = await _otpService.GenerateOtpAsync(request.PhoneNumber);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<UserInfo>> GetCurrentUser()
        {
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (authHeader == null || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized();
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();
            var userId = _tokenService.GetUserIdFromToken(token);

            if (userId == null)
            {
                return Unauthorized();
            }

            var user = await _userService.GetUserByIdAsync(userId.Value);

            if (user == null)
            {
                return NotFound();
            }

            return Ok(user);
        }
    }
}