using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mitrayana.Api.Data;
using Mitrayana.Api.DTOs;
using Mitrayana.Api.Models;
using Mitrayana.Api.Services;

namespace Mitrayana.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly JwtService _jwt;
        private readonly ILogger<AuthController> _logger;
        private readonly IEmailService _email;
        private readonly IConfiguration _config;
        private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _env;

        public AuthController(ApplicationDbContext db, JwtService jwt, ILogger<AuthController> logger, IEmailService email, IConfiguration config, Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
        {
            _db = db;
            _jwt = jwt;
            _logger = logger;
            _email = email;
            _config = config;
            _env = env;
        }

        // Forgot password - sends email with reset link
        [HttpPost("forgot")]
        public async Task<IActionResult> Forgot([FromBody] ForgotRequest req)
        {
            Console.WriteLine("Forgot password called with email: " + req.Email);
            try
            {
                Console.WriteLine("Looking for user");
                var user = await _db.Users.SingleOrDefaultAsync(u => u.Email == req.Email);
                Console.WriteLine("User found: " + (user != null));
                if (user == null)
                {
                    // Explicitly inform user when email is not registered
                    _logger.LogInformation("Password reset requested for non-existent email {Email}", req.Email);
                    return BadRequest(new { message = "Email is not registered" });
                }

                Console.WriteLine("Updating user token");
                user.ResetToken = Guid.NewGuid().ToString("N");
                user.ResetTokenExpiry = DateTime.UtcNow.AddHours(2);
                await _db.SaveChangesAsync();
                Console.WriteLine("Saved changes");

                // Build frontend base URL:
                // Prefer the configured FrontendUrl when it is set (deterministic for dev/testing). Otherwise fall back to the request host.
                var configuredFrontend = _config["FrontendUrl"]?.Trim();
                string? frontend = null;

                if (!string.IsNullOrWhiteSpace(configuredFrontend))
                {
                    frontend = configuredFrontend;
                    Console.WriteLine("Using configured FrontendUrl: " + configuredFrontend);
                }
                else if (Request?.Scheme != null && Request?.Host.HasValue == true)
                {
                    var requestBase = $"{Request.Scheme}://{Request.Host.Value}";
                    frontend = requestBase;
                    Console.WriteLine("Using request host as frontend: " + requestBase);
                }

                if (string.IsNullOrWhiteSpace(frontend))
                {
                    frontend = _config["Urls"] ?? "http://localhost:5500";
                    Console.WriteLine("Using fallback frontend: " + frontend);
                }

                var resetUrl = frontend!.TrimEnd('/') + $"/reset.html?email={Uri.EscapeDataString(user.Email)}&token={user.ResetToken}";
                Console.WriteLine("Reset URL: " + resetUrl);

                var html = BuildResetEmail(user, resetUrl);

                try
                {
                    Console.WriteLine("Sending email");
                    await _email.SendAsync(user.Email, "Mitrayana - Reset your password", html);
                    _logger.LogInformation("Password reset email queued for {Email}", user.Email);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Email send failed: " + ex.Message);
                    _logger.LogError(ex, "Failed to send reset email to {Email}", user.Email);
                }

                return Ok(new { message = "If the email is registered, a reset link has been sent." });
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unexpected error: " + ex.Message);
                _logger.LogError(ex, "Unexpected error in forgot password");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // Reset password using token
        [HttpPost("reset")]
        public async Task<IActionResult> Reset([FromBody] ResetRequest req)
        {
            var user = await _db.Users.SingleOrDefaultAsync(u => u.Email == req.Email && u.ResetToken == req.Token);
            if (user == null || user.ResetTokenExpiry == null || user.ResetTokenExpiry < DateTime.UtcNow)
            {
                return BadRequest(new { message = "Invalid or expired token" });
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
            user.ResetToken = null;
            user.ResetTokenExpiry = null;
            await _db.SaveChangesAsync();

            return Ok(new { message = "Password updated successfully" });
        } 

        // ✅ HEALTH CHECK (Only One)
        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new { status = "OK", timestamp = DateTime.UtcNow });
        }

        // ✅ REGISTER
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromForm] RegisterRequest req)
        {
            try
            {
                var reqJson = JsonSerializer.Serialize(req);
                _logger.LogInformation("Register request: {Req}", reqJson);
            }
            catch
            {
                _logger.LogInformation("Register request received (could not serialize)");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(new { message = "Invalid input", errors = ModelState });
            }

            // Enforce document upload for service providers
            if (req.Role == "ServiceProvider" && (req.Document == null || req.Document.Length == 0))
            {
                return BadRequest(new { message = "Identity proof document is required for service providers." });
            }

            if (await _db.Users.AnyAsync(u => u.Email == req.Email))
                return BadRequest(new { message = "Email already registered" });

            var user = new User
            {
                Name = req.Name,
                Email = req.Email,
                Role = string.IsNullOrWhiteSpace(req.Role) ? "Senior" : req.Role,
                DateOfBirth = req.DateOfBirth,
                ContactNumber = req.ContactNumber,
                Gender = req.Gender,
                Address = req.Address,
                Age = req.Age,
                HealthCondition = req.HealthCondition,
                EmergencyContact = req.EmergencyContact,
                Skills = req.Skills,
                Availability = req.Availability,
                Location = req.Location,
                PinCode = req.PinCode,
                IsVerified = (string.IsNullOrWhiteSpace(req.Role) ? "Senior" : req.Role) != "ServiceProvider", // Service providers need verification
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password)
            };

            _db.Users.Add(user);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Log detailed exception for debugging (safe in dev)
                _logger.LogError(ex, "Failed to save new user during registration");

                // Include base exception message in the response to make debugging easier in dev
                var baseMessage = ex.GetBaseException().Message;
                return StatusCode(500, new { message = "Server error while saving user", detail = baseMessage });
            }

            // Handle document upload for service providers
            if (req.Document != null && req.Document.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "documents");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var fileName = $"{user.UserId}_{Path.GetFileName(req.Document.FileName)}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await req.Document.CopyToAsync(stream);
                }

                user.DocumentPath = $"/uploads/documents/{fileName}";
                await _db.SaveChangesAsync();
            }

            string token;
            try
            {
                token = _jwt.GenerateToken(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate JWT token for user {UserId}: {Message}", user.UserId, ex.Message);
                token = ""; // Allow registration to succeed even if token generation fails
            }

            return Ok(new AuthResponse
            {
                Token = token,
                Role = user.Role
            });
        }

        // ✅ LOGIN
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            var user = await _db.Users.SingleOrDefaultAsync(u => u.Email == req.Email);

            if (user == null)
                return Unauthorized(new { message = "Invalid credentials" });

            if (!BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
                return Unauthorized(new { message = "Invalid credentials" });

            var token = _jwt.GenerateToken(user);

            return Ok(new AuthResponse
            {
                Token = token,
                Role = user.Role
            });
        }

        // Helper: build rich HTML reset email using Brand config and user info
        private string BuildResetEmail(Mitrayana.Api.Models.User user, string resetUrl)
        {
            var brandName = _config["Brand:Name"] ?? "Mitrayana";
            var brandColor = _config["Brand:Color"] ?? "#00bfa5";
            var brandFont = _config["Brand:Font"] ?? "Inter";
            var logoUrl = _config["Brand:LogoUrl"] ?? string.Empty;

            var html = $@"<!DOCTYPE html>
<html>
<head>
  <meta charset='UTF-8'>
  <meta name='viewport' content='width=device-width, initial-scale=1.0'>
  <link href='https://fonts.googleapis.com/css2?family={brandFont.Replace(" ", "+")}:wght@400;500;600;700&display=swap' rel='stylesheet'>
</head>

<body style='margin:0;padding:0;background-color:#f4f6f8;font-family:{brandFont},Arial,sans-serif;'>

  <table width='100%' cellpadding='0' cellspacing='0'>
    <tr>
      <td align='center'>

        <table width='600' cellpadding='0' cellspacing='0' style='background:#ffffff;border-radius:10px;box-shadow:0 6px 18px rgba(0,0,0,0.08);margin:40px 0;overflow:hidden;'>

          <tr>
            <td style='background:{brandColor};padding:22px;text-align:center;'>

              <img src='{logoUrl}' alt='{brandName} Logo' style='height:50px;margin-bottom:8px;' />

              <p style='margin:0;color:#ffffff;font-size:14px;letter-spacing:0.5px;'>Caring Beyond Age</p>
            </td>
          </tr>

          <tr>
            <td style='padding:32px;color:#333;'>

              <h2 style='margin-top:0;color:{brandColor};'>Reset Your Password</h2>

              <p>Hello <strong>{user.Name}</strong>,</p>

              <p>We received a request to reset the password for your <strong>{brandName}</strong> account.</p>

              <div style='text-align:center;margin:32px 0;'>
                <a href='{resetUrl}' style='background:{brandColor};color:#ffffff;padding:14px 32px;text-decoration:none;border-radius:6px;font-size:16px;font-weight:600;display:inline-block;'>Reset Password</a>
              </div>

              <p style='font-size:14px;color:#555;'>⏱ This link is valid for <strong>2 hours</strong>.</p>

              <hr style='border:none;border-top:1px solid #e0e0e0;margin:30px 0;'>

              <p style='font-size:13px;color:#777;'>Need help? Contact us at <a href='mailto:support@mitrayana.com' style='color:{brandColor};text-decoration:none;'>support@mitrayana.com</a></p>

              <p style='font-size:13px;color:#777;'>— Team {brandName}</p>

            </td>
          </tr>

        </table>

      </td>
    </tr>
  </table>

</body>
</html>";

            return html;
        }
    }
}