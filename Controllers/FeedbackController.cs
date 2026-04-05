using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mitrayana.Api.Data;
using Mitrayana.Api.Models;
using Mitrayana.Api.Services;

namespace Mitrayana.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FeedbackController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<FeedbackController> _logger;
        private readonly IEmailService _emailService;

        public FeedbackController(ApplicationDbContext db, ILogger<FeedbackController> logger, IEmailService emailService)
        {
            _db = db;
            _logger = logger;
            _emailService = emailService;
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Post([FromBody] Feedback fb)
        {
            try
            {
                var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(idClaim, out var uid))
                {
                    _logger.LogWarning("Invalid user ID claim: {Claim}", idClaim);
                    return Unauthorized();
                }

                fb.UserId = uid;
                fb.DateCreated = DateTime.UtcNow;
                _db.Feedbacks.Add(fb);
                await _db.SaveChangesAsync();
                _logger.LogInformation("Feedback submitted by user {UserId}: {Message}", uid, fb.Message);
                return Ok(fb);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting feedback");
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                // Use left-join semantics so missing users don't cause issues
                var all = await _db.Feedbacks
                    .OrderByDescending(f => f.DateCreated)
                    .GroupJoin(_db.Users, f => f.UserId, u => u.UserId, (f, users) => new { f, users })
                    .SelectMany(x => x.users.DefaultIfEmpty(), (x, u) => new
                    {
                        x.f.FeedbackId,
                        x.f.UserId,
                        UserName = u != null ? u.Name : "Unknown",
                        UserEmail = u != null ? u.Email : string.Empty,
                        x.f.Message,
                        // exclude Category here to be tolerant of older schemas
                        Category = (string?)null,
                        x.f.Rating,
                        x.f.DateCreated
                    })
                    .ToListAsync();

                return Ok(all);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "GetAll feedbacks failed");
                // Return a minimal problem detail in development; client will see 500 but server logs will contain stack.
                return Problem(detail: ex.Message, statusCode: 500);
            }
        }

        [HttpPost("{id}/send-automatic-reply")]
        [Authorize]
        public async Task<IActionResult> SendAutomaticReply(int id)
        {
            try
            {
                var feedback = await _db.Feedbacks
                    .Include(f => f.User)
                    .FirstOrDefaultAsync(f => f.FeedbackId == id);

                if (feedback == null)
                {
                    return NotFound(new { message = "Feedback not found" });
                }

                if (feedback.User == null)
                {
                    return BadRequest(new { message = "User not found" });
                }

                // Determine recipients based on user's role
                var recipients = new List<User>();
                if (feedback.User.Role == "Senior")
                {
                    // Send to all service providers
                    recipients = await _db.Users.Where(u => u.Role == "Provider").ToListAsync();
                }
                else if (feedback.User.Role == "Provider")
                {
                    // Send to all seniors
                    recipients = await _db.Users.Where(u => u.Role == "Senior").ToListAsync();
                }
                else
                {
                    return BadRequest(new { message = "Invalid user role" });
                }

                if (!recipients.Any())
                {
                    return BadRequest(new { message = "No recipients found for the role" });
                }

                // Generate appropriate reply based on rating
                string subject;
                string body;

                if (feedback.Rating >= 4)
                {
                    subject = $"Positive Feedback from {feedback.User.Name} ({feedback.User.Role})";
                    body = $@"
                        <div style='text-align: center; margin-bottom: 20px;'>
                            <img src='http://localhost:5500/images/logo.png' alt='Mitrayana Logo' style='max-width: 200px; height: auto;'>
                        </div>
                        <h2>Positive Feedback Received</h2>
                        <p>Dear Team,</p>
                        <p>We have received positive feedback from {feedback.User.Name} (a {feedback.User.Role.ToLower()}).</p>
                        <p>Rating: {feedback.Rating}/5</p>
                        <p>Message: {feedback.Message}</p>
                        <p>Please continue providing excellent service!</p>
                        <p>Best regards,<br>Admin Team</p>
                    ";
                }
                else if (feedback.Rating >= 2)
                {
                    subject = $"Feedback from {feedback.User.Name} ({feedback.User.Role}) - Room for Improvement";
                    body = $@"
                        <div style='text-align: center; margin-bottom: 20px;'>
                            <img src='http://localhost:5500/images/logo.png' alt='Mitrayana Logo' style='max-width: 200px; height: auto;'>
                        </div>
                        <h2>Feedback Received</h2>
                        <p>Dear Team,</p>
                        <p>We have received feedback from {feedback.User.Name} (a {feedback.User.Role.ToLower()}).</p>
                        <p>Rating: {feedback.Rating}/5</p>
                        <p>Message: {feedback.Message}</p>
                        <p>Please review and consider improvements.</p>
                        <p>Best regards,<br>Admin Team</p>
                    ";
                }
                else
                {
                    subject = $"Urgent: Negative Feedback from {feedback.User.Name} ({feedback.User.Role})";
                    body = $@"
                        <div style='text-align: center; margin-bottom: 20px;'>
                            <img src='http://localhost:5500/images/logo.png' alt='Mitrayana Logo' style='max-width: 200px; height: auto;'>
                        </div>
                        <h2>Negative Feedback Received</h2>
                        <p>Dear Team,</p>
                        <p>We have received negative feedback from {feedback.User.Name} (a {feedback.User.Role.ToLower()}).</p>
                        <p>Rating: {feedback.Rating}/5</p>
                        <p>Message: {feedback.Message}</p>
                        <p>Please address this matter urgently.</p>
                        <p>Best regards,<br>Admin Team</p>
                    ";
                }

                // Send email to all recipients
                foreach (var recipient in recipients)
                {
                    if (!string.IsNullOrEmpty(recipient.Email))
                    {
                        await _emailService.SendAsync(recipient.Email, subject, body);
                    }
                }

                _logger.LogInformation("Automatic reply sent for feedback {FeedbackId} to {Count} recipients", id, recipients.Count);

                return Ok(new { message = "Automatic reply sent successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending automatic reply for feedback {FeedbackId}", id);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPost("{id}/send-reply")]
        [Authorize]
        public async Task<IActionResult> SendReply(int id, [FromBody] SendReplyRequest request)
        {
            try
            {
                var feedback = await _db.Feedbacks
                    .Include(f => f.User)
                    .FirstOrDefaultAsync(f => f.FeedbackId == id);

                if (feedback == null)
                {
                    return NotFound(new { message = "Feedback not found" });
                }

                if (string.IsNullOrEmpty(feedback.User?.Email))
                {
                    return BadRequest(new { message = "User email not available" });
                }

                if (string.IsNullOrWhiteSpace(request.Reply))
                {
                    return BadRequest(new { message = "Reply message is required" });
                }

                string subject = $"Reply to your feedback from Mitrayana Admin";
                string body = $@"
                    <div style='text-align: center; margin-bottom: 20px;'>
                        <img src='http://localhost:5500/images/logo.png' alt='Mitrayana Logo' style='max-width: 200px; height: auto;'>
                    </div>
                    <h2>Reply from Mitrayana Admin</h2>
                    <p>Dear {feedback.User.Name},</p>
                    <p>Regarding your feedback:</p>
                    <blockquote>{feedback.Message}</blockquote>
                    <p><strong>Admin Reply:</strong></p>
                    <p>{request.Reply.Replace("\n", "<br>")}</p>
                    <p>Best regards,<br>Mitrayana Admin Team</p>
                ";

                await _emailService.SendAsync(feedback.User.Email, subject, body);

                _logger.LogInformation("Custom reply sent for feedback {FeedbackId} to {Email}", id, feedback.User.Email);

                return Ok(new { message = "Reply sent successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending reply for feedback {FeedbackId}", id);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var feedback = await _db.Feedbacks.FindAsync(id);
                if (feedback == null)
                {
                    return NotFound(new { message = "Feedback not found" });
                }

                _db.Feedbacks.Remove(feedback);
                await _db.SaveChangesAsync();

                _logger.LogInformation("Feedback {FeedbackId} deleted by admin", id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting feedback {FeedbackId}", id);
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }

    public class SendReplyRequest
    {
        public string Reply { get; set; } = string.Empty;
    }
}
