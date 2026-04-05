using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mitrayana.Api.Data;
using Mitrayana.Api.Models;

namespace Mitrayana.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ServiceController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public ServiceController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpPost]
        [Authorize(Roles = "Senior")]
        public async Task<IActionResult> Create([FromBody] ServiceRequest req)
        {
            var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(idClaim, out var userId)) return Unauthorized();

            req.UserId = userId;
            req.DateCreated = DateTime.UtcNow;
            req.Status = "Open";

            _db.ServiceRequests.Add(req);
            await _db.SaveChangesAsync();
            return Ok(req);
        }

        [HttpGet("my")]
        [Authorize]
        public async Task<IActionResult> MyRequests()
        {
            var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(idClaim, out var userId)) return Unauthorized();

            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            if (role == "Senior")
            {
                var my = await _db.ServiceRequests.Where(r => r.UserId == userId).ToListAsync();
                return Ok(my);
            }

            if (role == "ServiceProvider")
            {
                var provider = await _db.Users.FindAsync(userId);
                if (provider == null) return Unauthorized();

                var query = _db.ServiceRequests
                    .Where(r => r.Status == "Open");

                // Filter by provider's skills
                if (!string.IsNullOrWhiteSpace(provider.Skills))
                {
                    var normalizedSkills = provider.Skills.Trim();
                    if (normalizedSkills.Equals("Medical Assistance", StringComparison.OrdinalIgnoreCase))
                    {
                        query = query.Where(r => r.Title == "Household Support");
                    }
                    else if (normalizedSkills.Equals("Household Help", StringComparison.OrdinalIgnoreCase))
                    {
                        query = query.Where(r => r.Title == "Health Assistance" || r.Title == "Wellness");
                    }
                    else if (normalizedSkills.Equals("Wellness", StringComparison.OrdinalIgnoreCase))
                    {
                        query = query.Where(r => r.Title == "Wellness");
                    }
                }

                var list = await query
                    .Include(r => r.User)
                    .Select(r => new {
                        id = r.RequestId,
                        title = r.Title,
                        description = r.Description,
                        status = r.Status,
                        dateCreated = r.DateCreated,
                        userId = r.UserId,
                        userName = r.User != null ? r.User.Name : null,
                        userContact = r.User != null ? r.User.ContactNumber : null,
                        userAddress = r.User != null ? r.User.Address : null
                    })
                    .ToListAsync();
                return Ok(list);
            }

            // Admin sees all
            var all = await _db.ServiceRequests.ToListAsync();
            return Ok(all);
        }

        [HttpPost("accept/{id}")]
        [Authorize(Roles = "ServiceProvider")]
        public async Task<IActionResult> Accept(int id)
        {
            var req = await _db.ServiceRequests.FindAsync(id);
            if (req == null) return NotFound();
            if (req.Status != "Open") return BadRequest(new { message = "Request not open" });

            var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(idClaim, out var serviceProviderId)) return Unauthorized();

            req.Status = "Accepted";
            req.AssignedVolunteerId = serviceProviderId;
            await _db.SaveChangesAsync();
            return Ok(req);
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> Update(int id, [FromBody] ServiceRequest update)
        {
            var req = await _db.ServiceRequests.FindAsync(id);
            if (req == null) return NotFound();

            // Only owner, service provider assigned, or admin can update status/description
            var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(idClaim, out var userId)) return Unauthorized();

            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            if (role != "Admin" && req.UserId != userId && req.AssignedVolunteerId != userId)
                return Forbid();

            req.Title = update.Title ?? req.Title;
            req.Description = update.Description ?? req.Description;
            req.Status = update.Status ?? req.Status;
            await _db.SaveChangesAsync();
            return Ok(req);
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            var req = await _db.ServiceRequests.FindAsync(id);
            if (req == null) return NotFound();

            var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(idClaim, out var userId)) return Unauthorized();

            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            if (role != "Admin" && req.UserId != userId)
                return Forbid();

            _db.ServiceRequests.Remove(req);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
