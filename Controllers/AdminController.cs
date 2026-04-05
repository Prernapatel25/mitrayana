using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mitrayana.Api.Data;
using Mitrayana.Api.Models;
using Mitrayana.Api.Services;
using System;
using System.IO;

namespace Mitrayana.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IEmailService _emailService;
        private readonly IWebHostEnvironment _env;

        public AdminController(ApplicationDbContext db, IEmailService emailService, IWebHostEnvironment env)
        {
            _db = db;
            _emailService = emailService;
            _env = env;
        }

        private string? GetLogoDataUri()
        {
            try
            {
                var logoPath = Path.Combine(_env.WebRootPath ?? string.Empty, "images", "whitelogo.png");
                if (!System.IO.File.Exists(logoPath))
                    return null;

                var bytes = System.IO.File.ReadAllBytes(logoPath);
                var base64 = Convert.ToBase64String(bytes);
                return $"data:image/png;base64,{base64}";
            }
            catch
            {
                return null;
            }
        }

        [HttpGet("users")]
        [AllowAnonymous]
        public async Task<IActionResult> GetUsers()
        {
              var users = await _db.Users.Select(u => new { u.UserId, u.Name, u.Email, u.Role, ContactNumber = u.ContactNumber, u.Address, u.IsActive, u.CreatedAt }).ToListAsync();
            return Ok(users);
        }

        [HttpDelete("user/{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();
            _db.Users.Remove(user);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("reports")]
        [AllowAnonymous]
        public async Task<IActionResult> Reports()
        {
            var totalUsers = await _db.Users.CountAsync();
            var totalRequests = await _db.ServiceRequests.CountAsync();
            var openRequests = await _db.ServiceRequests.CountAsync(r => r.Status == "Open");
            var acceptedRequests = await _db.ServiceRequests.CountAsync(r => r.Status == "Accepted");
            var completedRequests = await _db.ServiceRequests.CountAsync(r => r.Status == "Completed");
            var inProgressRequests = await _db.ServiceRequests.CountAsync(r => r.Status == "In Progress");
            var totalFeedbacks = await _db.Feedbacks.CountAsync();
            var averageRating = await _db.Feedbacks.AverageAsync(f => (double?)f.Rating) ?? 0;

            return Ok(new {
                totalUsers,
                totalRequests,
                openRequests,
                acceptedRequests,
                completedRequests,
                inProgressRequests,
                totalFeedbacks,
                averageRating = Math.Round(averageRating, 1)
            });
        }

        [HttpGet("pending-providers")]
        public async Task<IActionResult> GetPendingProviders()
        {
            var providers = await _db.Users
                .Where(u => u.Role == "ServiceProvider" && u.IsActive && !u.IsVerified)
                .Select(u => new
                {
                    u.UserId,
                    u.Name,
                    u.Email,
                    u.ContactNumber,
                    u.Address,
                    u.PinCode,
                    u.Skills,
                    u.Experience,
                    u.Availability,
                    u.Location,
                    u.DocumentPath,
                    u.CreatedAt,
                    u.IsVerified
                })
                .ToListAsync();

            Console.WriteLine($"Found {providers.Count} pending service providers");
            foreach (var p in providers)
            {
                Console.WriteLine($"Provider: {p.Name} (ID: {p.UserId}), IsVerified: {p.IsVerified}");
            }

            return Ok(providers);
        }

        [HttpGet("providers")]
        public async Task<IActionResult> GetAllProviders([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            // Normalize date range: start inclusive (00:00), end inclusive (end of day)
            DateTime? start = null;
            DateTime? end = null;
            if (startDate.HasValue) start = startDate.Value.Date;
            if (endDate.HasValue) end = endDate.Value.Date.AddDays(1); // exclusive upper bound

            var query = _db.Users
                .Where(u => u.Role == "ServiceProvider" && u.IsActive);

            if (start.HasValue)
                query = query.Where(u => u.CreatedAt >= start.Value);
            if (end.HasValue)
                query = query.Where(u => u.CreatedAt < end.Value);

            var providers = await query
                .Select(u => new
                {
                    u.UserId,
                    u.Name,
                    u.Email,
                    u.ContactNumber,
                    u.Address,
                    u.PinCode,
                    u.Skills,
                    u.Experience,
                    u.Availability,
                    u.Location,
                    u.DocumentPath,
                    u.CreatedAt,
                    u.IsVerified
                })
                .ToListAsync();

            Console.WriteLine($"Found {providers.Count} service providers");
            foreach (var p in providers)
            {
                Console.WriteLine($"Provider: {p.Name} (ID: {p.UserId}), IsVerified: {p.IsVerified}");
            }

            return Ok(providers);
        }

        [HttpPost("approve-provider/{id}")]
        public async Task<IActionResult> ApproveProvider(int id)
        {
            var provider = await _db.Users.FindAsync(id);
            if (provider == null || provider.Role != "ServiceProvider")
                return NotFound(new { message = "Provider not found" });

            if (provider.IsVerified)
                return BadRequest(new { message = "Provider is already verified" });

            Console.WriteLine($"Approving provider {provider.Name} (ID: {id}), current IsVerified: {provider.IsVerified}");

            provider.IsVerified = true;
            provider.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // Verify the change was saved
            var updatedProvider = await _db.Users.FindAsync(id);
            Console.WriteLine($"After approval, provider {updatedProvider?.Name ?? "Unknown"} IsVerified: {updatedProvider?.IsVerified ?? false}");

            // Send approval email
            try
            {
                var brandName = "Mitrayana";
                var brandColor = "#006f6f";
                var brandFont = "Inter";
                // Embed logo as base64 data URI so email clients can render it without external fetch.
                var logoUrl = GetLogoDataUri() ?? "https://raw.githubusercontent.com/prernap036/Mitrayana-Logo/main/whitelogo.png";

                var html = $@"
<!DOCTYPE html>
<html lang='en'>
<head>
  <meta charset='UTF-8'>
  <meta name='viewport' content='width=device-width, initial-scale=1.0'>
  <title>Account Approved - {brandName}</title>
  <link href='https://fonts.googleapis.com/css2?family={brandFont.Replace(" ", "+")}:wght@300;400;500;600;700&display=swap' rel='stylesheet'>
  <style>
    body, table, td, p, a, li, blockquote {{
      -webkit-text-size-adjust: 100%;
      -ms-text-size-adjust: 100%;
    }}
    table, td {{
      mso-table-lspace: 0pt;
      mso-table-rspace: 0pt;
    }}
    img {{
      -ms-interpolation-mode: bicubic;
    }}
    .email-container {{
      max-width: 600px;
      margin: 0 auto;
      font-family: {brandFont}, 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
      background: #f8f9fa;
      border-radius: 12px;
      overflow: hidden;
      box-shadow: 0 10px 30px rgba(0,0,0,0.1);
    }}
    .header {{
      background: linear-gradient(135deg, {brandColor} 0%, #005a5a 100%);
      padding: 40px 30px;
      text-align: center;
      position: relative;
    }}
    .logo {{
      width: 80px;
      height: auto;
      margin-bottom: 15px;
      filter: drop-shadow(0 2px 4px rgba(0,0,0,0.2));
    }}
    .brand-tagline {{
      color: rgba(255,255,255,0.9);
      font-size: 14px;
      font-weight: 500;
      letter-spacing: 1px;
      margin: 10px 0 0 0;
      text-transform: uppercase;
    }}
    .content {{
      background: #ffffff;
      padding: 50px 40px;
      color: #333333;
    }}
    .greeting {{
      font-size: 24px;
      font-weight: 600;
      color: {brandColor};
      margin: 0 0 20px 0;
      text-align: center;
    }}
    .main-message {{
      font-size: 16px;
      line-height: 1.6;
      margin: 0 0 30px 0;
      color: #555555;
    }}
    .success-box {{
      background: linear-gradient(135deg, #d4edda 0%, #c3e6cb 100%);
      border-left: 4px solid #28a745;
      border-radius: 4px;
      padding: 20px;
      margin: 30px 0;
    }}
    .success-icon {{
      font-size: 20px;
      color: #28a745;
      margin-right: 10px;
    }}
    .success-text {{
      color: #155724;
      font-size: 14px;
      margin: 0;
    }}
    .cta-button {{
      display: inline-block;
      background: linear-gradient(135deg, {brandColor} 0%, #005a5a 100%);
      color: #ffffff !important;
      text-decoration: none;
      padding: 18px 40px;
      border-radius: 8px;
      font-weight: 600;
      font-size: 16px;
      text-align: center;
      box-shadow: 0 4px 15px rgba(0, 111, 111, 0.3);
      margin: 30px 0;
      border: none;
      transition: all 0.3s ease;
    }}
    .cta-button:hover {{
      box-shadow: 0 6px 20px rgba(0, 111, 111, 0.4);
    }}
    .footer {{
      background: #f8f9fa;
      padding: 30px 40px;
      border-top: 1px solid #e9ecef;
      text-align: center;
    }}
    .footer-text {{
      color: #6c757d;
      font-size: 14px;
      line-height: 1.5;
      margin: 0 0 15px 0;
    }}
    .footer-link {{
      color: {brandColor} !important;
      text-decoration: none;
      font-weight: 500;
    }}
    .footer-divider {{
      border-top: 1px solid #dee2e6;
      margin: 20px 0;
    }}
    .footer-note {{
      font-size: 12px;
      color: #adb5bd;
      margin-top: 20px;
    }}
    @media only screen and (max-width: 600px) {{
      .email-container {{
        margin: 10px;
        border-radius: 8px;
      }}
      .header {{
        padding: 30px 20px;
      }}
      .content {{
        padding: 30px 20px;
      }}
      .cta-button {{
        padding: 15px 30px;
        font-size: 14px;
      }}
    }}
  </style>
</head>
<body style='margin:0;padding:0;background:#f8f9fa;'>
  <table width='100%' cellpadding='0' cellspacing='0' style='background:#f8f9fa;'>
    <tr>
      <td align='center' style='padding:40px 20px;'>
        <table class='email-container' cellpadding='0' cellspacing='0'>
          <tr>
            <td class='header'>
              <img src='{logoUrl}' alt='{brandName} Logo' class='logo' />
              <h1 style='color:#ffffff;font-size:28px;font-weight:700;margin:15px 0 5px 0;text-shadow:0 2px 4px rgba(0,0,0,0.3);'>{brandName}</h1>
              <p class='brand-tagline'>Professional Senior Care Services</p>
            </td>
          </tr>
          
          <tr>
            <td class='content'>
              <h2 class='greeting'>Account Approved!</h2>
              
              <p class='main-message'>
                Dear <strong style='color:{brandColor};'>{provider.Name}</strong>,
              </p>
              
              <p class='main-message'>
                Congratulations! Your service provider account has been successfully verified and approved. 
                You can now access available service requests and begin providing care services through our platform.
              </p>
              
              <div class='success-box'>
                <p class='success-text'>
                  <strong>What's Next:</strong> Log in to your dashboard to view available service requests, 
                  update your profile, and start accepting assignments.
                </p>
              </div>
              
              <div style='text-align:center;'>
                <a href='http://localhost:5500/login.html' class='cta-button'>Access Your Dashboard</a>
              </div>
              
              <p style='text-align:center;color:#777;font-size:14px;margin:20px 0;'>
                If you have any questions about getting started, our support team is here to help.
              </p>
            </td>
          </tr>
          
          <tr>
            <td class='footer'>
              <div class='footer-divider'></div>
              <p class='footer-text'>
                For assistance, please contact our support team at 
                <a href='mailto:miitrayana@gamil.com' class='footer-link'>miitrayana@gamil.com</a> or call (555) 123-4567.
              </p>
              
              <p class='footer-text'>
                Welcome to the {brandName} family! We're excited to have you on board.
              </p>
              
              <p class='footer-note'>
                This is an automated message. Please do not reply to this email.<br>
                © 2026 {brandName}. All rights reserved.
              </p>
            </td>
          </tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>";

                await _emailService.SendAsync(provider.Email, $"Account Approved - {brandName}", html);
            }
            catch (Exception ex)
            {
                // Log error but don't fail the approval
                Console.WriteLine($"Failed to send approval email: {ex.Message}");
            }

            return Ok(new { message = "Provider approved successfully" });
        }

        [HttpPost("reject-provider/{id}")]
        public async Task<IActionResult> RejectProvider(int id)
        {
            var provider = await _db.Users.FindAsync(id);
            if (provider == null || provider.Role != "ServiceProvider")
                return NotFound(new { message = "Provider not found" });

            if (provider.IsVerified)
                return BadRequest(new { message = "Cannot reject a verified provider" });

            var email = provider.Email;
            var name = provider.Name;

            // Delete the provider account
            _db.Users.Remove(provider);
            await _db.SaveChangesAsync();

            // Send rejection email
            try
            {
                var brandName = "Mitrayana";
                var brandColor = "#dc3545";
                var brandFont = "Inter";
                // Embed logo as base64 data URI so email clients can render it without external fetch.
                var logoUrl = GetLogoDataUri() ?? "https://raw.githubusercontent.com/prernap036/Mitrayana-Logo/main/whitelogo.png";

                var html = $@"
<!DOCTYPE html>
<html lang='en'>
<head>
  <meta charset='UTF-8'>
  <meta name='viewport' content='width=device-width, initial-scale=1.0'>
  <title>Account Registration Update - {brandName}</title>
  <link href='https://fonts.googleapis.com/css2?family={brandFont.Replace(" ", "+")}:wght@300;400;500;600;700&display=swap' rel='stylesheet'>
  <style>
    body, table, td, p, a, li, blockquote {{
      -webkit-text-size-adjust: 100%;
      -ms-text-size-adjust: 100%;
    }}
    table, td {{
      mso-table-lspace: 0pt;
      mso-table-rspace: 0pt;
    }}
    img {{
      -ms-interpolation-mode: bicubic;
    }}
    .email-container {{
      max-width: 600px;
      margin: 0 auto;
      font-family: {brandFont}, 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
      background: #f8f9fa;
      border-radius: 12px;
      overflow: hidden;
      box-shadow: 0 10px 30px rgba(0,0,0,0.1);
    }}
    .header {{
      background: linear-gradient(135deg, {brandColor} 0%, #b02a37 100%);
      padding: 40px 30px;
      text-align: center;
      position: relative;
    }}
    .logo {{
      width: 80px;
      height: auto;
      margin-bottom: 15px;
      filter: drop-shadow(0 2px 4px rgba(0,0,0,0.2));
    }}
    .brand-tagline {{
      color: rgba(255,255,255,0.9);
      font-size: 14px;
      font-weight: 500;
      letter-spacing: 1px;
      margin: 10px 0 0 0;
      text-transform: uppercase;
    }}
    .content {{
      background: #ffffff;
      padding: 50px 40px;
      color: #333333;
    }}
    .greeting {{
      font-size: 24px;
      font-weight: 600;
      color: {brandColor};
      margin: 0 0 20px 0;
      text-align: center;
    }}
    .main-message {{
      font-size: 16px;
      line-height: 1.6;
      margin: 0 0 30px 0;
      color: #555555;
    }}
    .info-box {{
      background: #f8f9fa;
      border-left: 4px solid {brandColor};
      border-radius: 4px;
      padding: 20px;
      margin: 30px 0;
    }}
    .info-text {{
      color: #666;
      font-size: 14px;
      margin: 0;
    }}
    .cta-button {{
      display: inline-block;
      background: linear-gradient(135deg, {brandColor} 0%, #b02a37 100%);
      color: #ffffff !important;
      text-decoration: none;
      padding: 18px 40px;
      border-radius: 8px;
      font-weight: 600;
      font-size: 16px;
      text-align: center;
      box-shadow: 0 4px 15px rgba(220, 53, 69, 0.3);
      margin: 30px 0;
      border: none;
      transition: all 0.3s ease;
    }}
    .cta-button:hover {{
      box-shadow: 0 6px 20px rgba(220, 53, 69, 0.4);
    }}
    .footer {{
      background: #f8f9fa;
      padding: 30px 40px;
      border-top: 1px solid #e9ecef;
      text-align: center;
    }}
    .footer-text {{
      color: #6c757d;
      font-size: 14px;
      line-height: 1.5;
      margin: 0 0 15px 0;
    }}
    .footer-link {{
      color: {brandColor} !important;
      text-decoration: none;
      font-weight: 500;
    }}
    .footer-divider {{
      border-top: 1px solid #dee2e6;
      margin: 20px 0;
    }}
    .footer-note {{
      font-size: 12px;
      color: #adb5bd;
      margin-top: 20px;
    }}
    @media only screen and (max-width: 600px) {{
      .email-container {{
        margin: 10px;
        border-radius: 8px;
      }}
      .header {{
        padding: 30px 20px;
      }}
      .content {{
        padding: 30px 20px;
      }}
      .cta-button {{
        padding: 15px 30px;
        font-size: 14px;
      }}
    }}
  </style>
</head>
<body style='margin:0;padding:0;background:#f8f9fa;'>
  <table width='100%' cellpadding='0' cellspacing='0' style='background:#f8f9fa;'>
    <tr>
      <td align='center' style='padding:40px 20px;'>
        <table class='email-container' cellpadding='0' cellspacing='0'>
          <tr>
            <td class='header'>
              <img src='{logoUrl}' alt='{brandName} Logo' class='logo' />
              <h1 style='color:#ffffff;font-size:28px;font-weight:700;margin:15px 0 5px 0;text-shadow:0 2px 4px rgba(0,0,0,0.3);'>{brandName}</h1>
              <p class='brand-tagline'>Professional Senior Care Services</p>
            </td>
          </tr>
          
          <tr>
            <td class='content'>
              <h2 class='greeting'>Account Registration Update</h2>
              
              <p class='main-message'>
                Dear <strong style='color:{brandColor};'>{name}</strong>,
              </p>
              
              <p class='main-message'>
                We have reviewed your service provider account registration application. 
                After careful consideration, we regret to inform you that your application has not been approved at this time.
              </p>
              
              <div class='info-box'>
                <p class='info-text'>
                  <strong>Next Steps:</strong> Your account has been removed from our system as per our registration policy. 
                  If you believe this decision was made in error or would like to reapply in the future, 
                  please contact our support team for assistance.
                </p>
              </div>
              
              <div style='text-align:center;'>
                <a href='mailto:support@mitrayana.com' class='cta-button'>Contact Support</a>
              </div>
              
              <p style='text-align:center;color:#777;font-size:14px;margin:20px 0;'>
                We appreciate your interest in providing care services through {brandName}.
              </p>
            </td>
          </tr>
          
          <tr>
            <td class='footer'>
              <div class='footer-divider'></div>
              <p class='footer-text'>
                For questions about this decision, please contact our support team at 
                <a href='mailto:support@mitrayana.com' class='footer-link'>support@mitrayana.com</a> or call (555) 123-4567.
              </p>
              
              <p class='footer-text'>
                Thank you for your understanding.
              </p>
              
              <p class='footer-note'>
                This is an automated message. Please do not reply to this email.<br>
                © 2026 {brandName}. All rights reserved.
              </p>
            </td>
          </tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>";

                await _emailService.SendAsync(email, $"Account Registration Update - {brandName}", html);
            }
            catch (Exception ex)
            {
                // Log error but don't fail the rejection
                Console.WriteLine($"Failed to send rejection email: {ex.Message}");
            }

            return Ok(new { message = "Provider rejected and account deleted" });
        }

        // [HttpGet("document/{userId}")]
        // public async Task<IActionResult> GetDocument(int userId)
        // {
        //     var user = await _db.Users.FindAsync(userId);
        //     if (user == null || string.IsNullOrEmpty(user.DocumentPath))
        //     {
        //         return NotFound(new { message = "Document not found" });
        //     }

        //     var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", user.DocumentPath.TrimStart('/'));
        //     if (!System.IO.File.Exists(filePath))
        //     {
        //         return NotFound(new { message = "Document file not found" });
        //     }

        //     var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
        //     var contentType = GetContentType(filePath);
        //     return File(fileBytes, contentType, Path.GetFileName(filePath));
        // }

        // private string GetContentType(string filePath)
        // {
        //     var extension = Path.GetExtension(filePath).ToLower();
        //     return extension switch
        //     {
        //         ".pdf" => "application/pdf",
        //         ".jpg" => "image/jpeg",
        //         ".jpeg" => "image/jpeg",
        //         ".png" => "image/png",
        //         ".doc" => "application/msword",
        //         ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        //         _ => "application/octet-stream"
        //     };
        // }

        [HttpGet("chart-data")]
        [AllowAnonymous]
        public async Task<IActionResult> GetChartData()
        {
            Console.WriteLine("GetChartData called");
            var today = DateTime.Today;
            var last7Days = Enumerable.Range(0, 7).Select(i => today.AddDays(-i)).Reverse().ToList();

            var data = new List<object>();
            foreach (var date in last7Days)
            {
                var totalRequests = await _db.ServiceRequests.CountAsync(r => r.DateCreated.Date == date);
                var completedRequests = await _db.ServiceRequests.CountAsync(r => r.DateCreated.Date == date && r.Status == "Completed");

                data.Add(new
                {
                    date = date.ToString("yyyy-MM-dd"),
                    total = totalRequests,
                    completed = completedRequests
                });
            }

            Console.WriteLine($"Returning chart data: {data.Count} entries");
            return Ok(data);
        }

        [HttpGet("status-distribution")]
        [AllowAnonymous]
        public async Task<IActionResult> GetStatusDistribution()
        {
            Console.WriteLine("GetStatusDistribution called");
            var completed = await _db.ServiceRequests.CountAsync(r => r.Status == "Completed");
            var pending = await _db.ServiceRequests.CountAsync(r => r.Status == "Open" || r.Status == "Accepted");
            var rejected = await _db.ServiceRequests.CountAsync(r => r.Status == "Rejected");

            var result = new
            {
                completed,
                pending,
                rejected
            };

            Console.WriteLine($"Returning status distribution: completed={completed}, pending={pending}, rejected={rejected}");
            return Ok(result);
        }
    }
}
