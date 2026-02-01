// Controllers/AuthController.cs
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Logging;

namespace DoAnTotNghiep.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly IConfiguration _config;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            SignInManager<IdentityUser> signInManager,
            IConfiguration config,
            ILogger<AuthController> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _signInManager = signInManager;
            _config = config;
            _logger = logger;
        }

        // ---------------------------
        // DTOs
        // ---------------------------
        public class LoginModel
        {
            [Required, EmailAddress]
            public string? Email { get; set; }

            [Required]
            public string? Password { get; set; }
        }

        public class RegisterModel
        {
            [Required, EmailAddress]
            public string? Email { get; set; }

            [Required, MinLength(6)]
            public string? Password { get; set; }

            [Required, Compare("Password", ErrorMessage = "Xác nhận mật khẩu không khớp.")]
            public string? ConfirmPassword { get; set; }
        }

        // ---------------------------
        // Register
        // ---------------------------
        [HttpPost("register")]
        [Produces("application/json")]
        public async Task<IActionResult> Register([FromBody] RegisterModel model)
        {
            _logger.LogInformation("Register called for {Email}", model?.Email);

            if (model == null)
            {
                return BadRequest(new { message = "Payload rỗng" });
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(kv => kv.Value.Errors.Count > 0)
                    .ToDictionary(
                        kv => kv.Key,
                        kv => kv.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                    );

                _logger.LogWarning("Register invalid modelstate for {Email}: {@Errors}", model.Email, errors);
                return BadRequest(new { message = "Validation failed", errors });
            }

            var exists = await _userManager.FindByEmailAsync(model.Email!);
            if (exists != null)
            {
                _logger.LogWarning("Register attempt with existing email {Email}", model.Email);
                return BadRequest(new { message = "Email đã được sử dụng." });
            }

            var user = new IdentityUser
            {
                UserName = model.Email,
                Email = model.Email,
                EmailConfirmed = true 
            };

            var createResult = await _userManager.CreateAsync(user, model.Password!);
            if (!createResult.Succeeded)
            {
                var identityErrors = createResult.Errors.Select(e => e.Description).ToArray();
                _logger.LogWarning("Register Identity errors for {Email}: {@Errors}", model.Email, identityErrors);
                return BadRequest(new { message = "Không thể tạo tài khoản", errors = identityErrors });
            }

            var defaultRole = "Customer";
            if (!await _roleManager.RoleExistsAsync(defaultRole))
            {
                try
                {
                    await _roleManager.CreateAsync(new IdentityRole(defaultRole));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create default role {Role}", defaultRole);
                }
            }

            try
            {
                await _userManager.AddToRoleAsync(user, defaultRole);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to add user {Email} to role {Role}", user.Email, defaultRole);
            }

            await _signInManager.SignInAsync(user, isPersistent: false);

            _logger.LogInformation("Register success for {Email}", user.Email);
            return Ok(new { message = "Đăng ký thành công" });
        }

        
        [HttpPost("login")]
        [Produces("application/json")]
        public async Task<IActionResult> Login([FromBody] LoginModel model)
        {
            _logger.LogInformation("Login attempt for {Email}", model?.Email);

            if (model == null || string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Password))
            {
                return BadRequest(new { message = "Vui lòng nhập email và mật khẩu." });
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                _logger.LogWarning("Login failed: email not found {Email}", model.Email);
                return Unauthorized(new { message = "Email không tồn tại." });
            }

            var passwordValid = await _userManager.CheckPasswordAsync(user, model.Password);
            if (!passwordValid)
            {
                _logger.LogWarning("Login failed: invalid password for {Email}", model.Email);
                return Unauthorized(new { message = "Mật khẩu không đúng." });
            }

            var roles = await _userManager.GetRolesAsync(user);

            var authClaims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(ClaimTypes.Name, user.UserName ?? string.Empty),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            foreach (var role in roles)
            {
                authClaims.Add(new Claim(ClaimTypes.Role, role));
            }

            var token = GenerateJwtToken(authClaims);

            return Ok(new
            {
                token = new JwtSecurityTokenHandler().WriteToken(token),
                expiration = token.ValidTo,
                email = user.Email,
                roles
            });
        }

        
        private JwtSecurityToken GenerateJwtToken(IEnumerable<Claim> authClaims)
        {
            var keyString = _config["Jwt:Key"];
            if (string.IsNullOrWhiteSpace(keyString))
                throw new Exception("Thiếu JWT Key trong cấu hình.");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var issuer = _config["Jwt:Issuer"];
            var audience = _config["Jwt:Audience"];
            var expireMinutes = 60.0;
            if (double.TryParse(_config["Jwt:ExpireMinutes"], out var m)) expireMinutes = m;

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                expires: DateTime.UtcNow.AddMinutes(expireMinutes),
                claims: authClaims,
                signingCredentials: credentials
            );

            return token;
        }
    }
}
