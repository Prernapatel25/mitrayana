using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mitrayana.Api.Data;
using Mitrayana.Api.DTOs;
using Mitrayana.Api.Models;
using System.Security.Claims;

namespace Mitrayana.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ServiceRequestsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<ServiceRequestsController> _logger;

        public ServiceRequestsController(ApplicationDbContext db, ILogger<ServiceRequestsController> logger)
        {
            _db = db;
            _logger = logger;
        }

        // Create a new service request (authenticated)
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create([FromBody] CreateServiceRequest req)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { message = "Invalid input", errors = ModelState });

            // Get user id from token
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                return Unauthorized(new { message = "Invalid user token" });

            var user = await _db.Users.FindAsync(userId);

            var sr = new ServiceRequest
            {
                UserId = userId,
                Title = req.Title,
                SubService = req.SubService,
                Description = req.Description,
                Contact = req.Contact,
                PreferredAt = req.PreferredAt,
                Price = req.Price,
                Duration = req.Duration,
                Status = "Open",
                DateCreated = DateTime.UtcNow,
                PinCode = user?.PinCode
            };

            _db.ServiceRequests.Add(sr);
            await _db.SaveChangesAsync();

            var response = new ServiceRequestResponse
            {
                RequestId = sr.RequestId,
                Title = sr.Title,
                SubService = sr.SubService,
                Description = sr.Description,
                Contact = sr.Contact,
                PreferredAt = sr.PreferredAt,
                Price = sr.Price,
                Duration = sr.Duration,
                Status = sr.Status,
                DateCreated = sr.DateCreated,
                SeniorName = user?.Name
            };

            return CreatedAtAction(nameof(GetById), new { id = sr.RequestId }, response);
        }

        [HttpGet("mine")]
        [Authorize]
        public async Task<IActionResult> Mine()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                return Unauthorized(new { message = "Invalid user token" });

            var list = await _db.ServiceRequests
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.DateCreated)
                .Select(s => new ServiceRequestResponse
                {
                    RequestId = s.RequestId,
                    Title = s.Title,
                    SubService = s.SubService,
                    Description = s.Description,
                    Contact = s.Contact,
                    PreferredAt = s.PreferredAt,
                    Price = s.Price,
                    Duration = s.Duration,
                    Status = s.Status,
                    DateCreated = s.DateCreated,
                    AssignedVolunteerId = s.AssignedVolunteerId,
                    AssignedVolunteerName = s.AssignedVolunteerName,
                    AssignedVolunteerContact = _db.Users.Where(u => u.UserId == s.AssignedVolunteerId).Select(u => u.ContactNumber).FirstOrDefault(),
                    SeniorName = s.User != null ? s.User.Name : null
                })
                .ToListAsync();

            return Ok(list);
        }

        // Available requests for service providers filtered by provider's PinCode
        [HttpGet("available")]
        [Authorize(Roles = "ServiceProvider")]
        public async Task<IActionResult> Available()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                return Unauthorized(new { message = "Invalid user token" });

            var provider = await _db.Users.FindAsync(userId);
            if (provider == null) return Unauthorized(new { message = "Provider not found" });
            if (!provider.IsVerified) return Ok(new List<ServiceRequestResponse>()); // Only verified providers can see requests

            var pin = provider.PinCode;
            var skills = provider.Skills;
            if (string.IsNullOrWhiteSpace(pin)) return Ok(new List<ServiceRequestResponse>());

            var query = _db.ServiceRequests
                .Where(s => s.Status == "Open" && s.AssignedVolunteerId == null && s.PinCode == pin);

            // Filter by provider's skills -> only show requests matching the provider's chosen service category
            if (!string.IsNullOrWhiteSpace(skills))
            {
                var normalizedSkills = skills.Trim();

                // Known mappings for skill values used in registration to service request titles
                if (normalizedSkills.Equals("Household Help", StringComparison.OrdinalIgnoreCase))
                {
                    // Show requests whose Title contains "Household" (e.g., "Household Support")
                    query = query.Where(s => s.Title != null && s.Title.ToLower().Contains("household"));
                }
                else if (normalizedSkills.Equals("Medical Assistance", StringComparison.OrdinalIgnoreCase))
                {
                    // Match either Medical or Health themed requests
                    query = query.Where(s => (s.Title != null && (s.Title.ToLower().Contains("medical") || s.Title.ToLower().Contains("health"))) ||
                                              (s.SubService != null && s.SubService.ToLower().Contains("medical")));
                }
                else if (normalizedSkills.Equals("Wellness", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(s => s.Title != null && s.Title.ToLower().Contains("wellness"));
                }
                else
                {
                    // Fallback: loose contains match to cover variants and future values
                    var lowSkill = normalizedSkills.ToLower();
                    query = query.Where(s => (s.Title != null && s.Title.ToLower().Contains(lowSkill)) ||
                                              (s.SubService != null && s.SubService.ToLower().Contains(lowSkill)));
                }
            }

            var list = await query
                .Include(s => s.User)
                .OrderByDescending(s => s.DateCreated)
                .Select(s => new ServiceRequestResponse
                {
                    RequestId = s.RequestId,
                    Title = s.Title,
                    SubService = s.SubService,
                    Description = s.Description,
                    Contact = s.Contact,
                    PreferredAt = s.PreferredAt,
                    Price = s.Price,
                    Duration = s.Duration,
                    Status = s.Status,
                    DateCreated = s.DateCreated,
                    AssignedVolunteerId = s.AssignedVolunteerId,
                    AssignedVolunteerName = s.AssignedVolunteerName,
                    PinCode = s.PinCode,
                    SeniorName = s.User != null ? s.User.Name : null
                })
                .ToListAsync();

            return Ok(list);
        }

        // Provider can claim (assign) a request to themselves
        [HttpPost("{id}/assign")]
        [Authorize(Roles = "ServiceProvider")]
        public async Task<IActionResult> AssignToSelf(int id)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                return Unauthorized(new { message = "Invalid user token" });

            var provider = await _db.Users.FindAsync(userId);
            if (provider == null) return Unauthorized(new { message = "Provider not found" });

            var sr = await _db.ServiceRequests.FindAsync(id);
            if (sr == null) return NotFound();
            if (sr.AssignedVolunteerId != null) return BadRequest(new { message = "Request already assigned" });
            if (sr.PinCode != provider.PinCode) return Forbid(); // only same pin providers can claim

            // Ensure provider offers this service category (prevent assigning unrelated requests)
            var pskill = provider.Skills?.Trim();
            if (!string.IsNullOrWhiteSpace(pskill))
            {
                var matches = false;
                var skillLower = pskill.ToLower();
                var title = sr.Title ?? string.Empty;
                var sub = sr.SubService ?? string.Empty;

                if (pskill.Equals("Household Help", StringComparison.OrdinalIgnoreCase))
                    matches = title.ToLower().Contains("household");
                else if (pskill.Equals("Medical Assistance", StringComparison.OrdinalIgnoreCase))
                    matches = title.ToLower().Contains("medical") || title.ToLower().Contains("health") || sub.ToLower().Contains("medical") || sub.ToLower().Contains("health");
                else if (pskill.Equals("Wellness", StringComparison.OrdinalIgnoreCase))
                    matches = title.ToLower().Contains("wellness");
                else
                    matches = title.ToLower().Contains(skillLower) || sub.ToLower().Contains(skillLower);

                if (!matches) return Forbid();
            }

            sr.AssignedVolunteerId = provider.UserId;
            sr.AssignedVolunteerName = provider.Name;
            sr.Status = "In Progress";
            await _db.SaveChangesAsync();

            return Ok(new { message = "Assigned", requestId = sr.RequestId });
        }

        // Assigned requests for currently authenticated provider (both In Progress and Completed)
        [HttpGet("assigned")]
        [Authorize(Roles = "ServiceProvider")]
        public async Task<IActionResult> Assigned()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                return Unauthorized(new { message = "Invalid user token" });

            var list = await _db.ServiceRequests
                .Where(s => s.AssignedVolunteerId == userId)
                .Include(s => s.User)
                .OrderByDescending(s => s.DateCreated)
                .Select(s => new ServiceRequestResponse
                {
                    RequestId = s.RequestId,
                    Title = s.Title,
                    SubService = s.SubService,
                    Description = s.Description,
                    Contact = s.Contact,
                    PreferredAt = s.PreferredAt,
                    Price = s.Price,
                    Duration = s.Duration,
                    Status = s.Status,
                    DateCreated = s.DateCreated,
                    AssignedVolunteerId = s.AssignedVolunteerId,
                    AssignedVolunteerName = s.AssignedVolunteerName,
                    PinCode = s.PinCode,
                    SeniorName = s.User != null ? s.User.Name : null
                })
                .ToListAsync();

            return Ok(list);
        }

        // Provider can mark an assigned request as completed
        [HttpPost("{id}/complete")]
        [Authorize(Roles = "ServiceProvider")]
        public async Task<IActionResult> Complete(int id)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                return Unauthorized(new { message = "Invalid user token" });

            var provider = await _db.Users.FindAsync(userId);
            if (provider == null) return Unauthorized(new { message = "Provider not found" });

            var sr = await _db.ServiceRequests.FindAsync(id);
            if (sr == null) return NotFound();
            if (sr.AssignedVolunteerId != provider.UserId) return Forbid();

            sr.Status = "Completed";
            await _db.SaveChangesAsync();

            return Ok(new { message = "Completed", requestId = sr.RequestId });
        }

        // Senior can cancel their own request if it's still Open
        [HttpPost("{id}/cancel")]
        [Authorize]
        public async Task<IActionResult> Cancel(int id)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                return Unauthorized(new { message = "Invalid user token" });

            var sr = await _db.ServiceRequests.FindAsync(id);
            if (sr == null) return NotFound();
            
            // Only the owner can cancel
            if (sr.UserId != userId) return Forbid();
            
            // Can only cancel if not assigned and status is Open
            if (sr.Status != "Open" || sr.AssignedVolunteerId != null)
                return BadRequest(new { message = "Cannot cancel request that is already assigned or in progress" });

            sr.Status = "Cancelled";
            await _db.SaveChangesAsync();

            return Ok(new { message = "Request cancelled", requestId = sr.RequestId });
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetById(int id)
        {
            var sr = await _db.ServiceRequests
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.RequestId == id);
            if (sr == null) return NotFound();
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                return Unauthorized(new { message = "Invalid user token" });

            // allow access if owner or assigned volunteer
            if (sr.UserId != userId && sr.AssignedVolunteerId != userId)
                return Forbid();

            var assignedContact = await _db.Users
                .Where(u => u.UserId == sr.AssignedVolunteerId)
                .Select(u => u.ContactNumber)
                .FirstOrDefaultAsync();

            var resp = new ServiceRequestResponse
            {
                RequestId = sr.RequestId,
                Title = sr.Title,
                SubService = sr.SubService,
                Description = sr.Description,
                Contact = sr.Contact,
                PreferredAt = sr.PreferredAt,
                Price = sr.Price,
                Duration = sr.Duration,
                Status = sr.Status,
                DateCreated = sr.DateCreated,
                AssignedVolunteerId = sr.AssignedVolunteerId,
                AssignedVolunteerName = sr.AssignedVolunteerName,
                AssignedVolunteerContact = assignedContact,
                PinCode = sr.PinCode,
                SeniorName = sr.User != null ? sr.User.Name : null
            };

            return Ok(resp);
        }

        // Admin: Get all service requests
        [HttpGet("admin/all")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAllForAdmin()
        {
            var requests = await _db.ServiceRequests
                .Include(sr => sr.User)
                .OrderByDescending(sr => sr.DateCreated)
                .Select(sr => new
                {
                    sr.RequestId,
                    UserId = sr.UserId,
                    SeniorName = sr.User != null ? sr.User.Name : "Unknown",
                    Service = sr.Title,
                    SubService = sr.SubService,
                    RequestedDate = sr.DateCreated,
                    Status = sr.Status,
                    CareProvider = sr.AssignedVolunteerName,
                    Notes = sr.Description,
                    Price = sr.Price
                })
                .ToListAsync();

            return Ok(requests);
        }
    }
}