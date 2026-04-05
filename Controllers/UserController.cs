using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mitrayana.Api.Data;
using Mitrayana.Api.Models;

namespace Mitrayana.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public UserController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetMyProfile()
        {
            var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(idClaim, out var id)) return Unauthorized();

            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();

            return Ok(new
            {
                user.UserId,
                user.Name,
                user.Email,
                user.Role,
                user.ContactNumber,
                user.Address,
                user.DateOfBirth,
                user.Gender,
                user.Age,
                user.HealthCondition,
                user.EmergencyContact,
                user.Skills,
                user.Experience,
                user.Availability,
                user.Location,
                user.DocumentPath,
                user.CreatedAt,
                user.UpdatedAt,
                user.IsActive,
                user.IsVerified
            });
        }

        [HttpPut("me")]
        public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateProfileDto profile)
        {
            var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(idClaim, out var id)) return Unauthorized();

            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();

            // Update allowed fields if provided
            user.Name = profile.Name ?? user.Name;
            user.ContactNumber = profile.ContactNumber ?? user.ContactNumber;
            user.Address = profile.Address ?? user.Address;
            user.HealthCondition = profile.HealthCondition ?? user.HealthCondition;
            user.EmergencyContact = profile.EmergencyContact ?? user.EmergencyContact;

            // Optional fields
            if (profile.DateOfBirth.HasValue) user.DateOfBirth = profile.DateOfBirth.Value;
            if (!string.IsNullOrWhiteSpace(profile.Gender)) user.Gender = profile.Gender;
            if (profile.Age.HasValue) user.Age = profile.Age.Value;

            // provider fields (if present)
            if (!string.IsNullOrWhiteSpace(profile.Skills)) user.Skills = profile.Skills;
            if (!string.IsNullOrWhiteSpace(profile.Experience)) user.Experience = profile.Experience;
            if (!string.IsNullOrWhiteSpace(profile.Availability)) user.Availability = profile.Availability;
            if (!string.IsNullOrWhiteSpace(profile.Location)) user.Location = profile.Location;

            user.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            // Return the updated profile for the frontend to apply immediately
            return Ok(new
            {
                user.UserId,
                user.Name,
                user.Email,
                user.Role,
                user.ContactNumber,
                user.Address,
                user.DateOfBirth,
                user.Gender,
                user.Age,
                user.HealthCondition,
                user.EmergencyContact,
                user.Skills,
                user.Experience,
                user.Availability,
                user.Location,
                user.DocumentPath,
                user.CreatedAt,
                user.UpdatedAt,
                user.IsActive,
                user.IsVerified
            });
        }
    }

    public class UpdateProfileDto
    {
        public string? Name { get; set; }
        public string? ContactNumber { get; set; }
        public string? Address { get; set; }
        public string? HealthCondition { get; set; }
        public string? EmergencyContact { get; set; }

        // Optional fields
        public DateTime? DateOfBirth { get; set; }
        public string? Gender { get; set; }
        public int? Age { get; set; }

        // Provider-related (may be null for seniors)
        public string? Skills { get; set; }
        public string? Experience { get; set; }
        public string? Availability { get; set; }
        public string? Location { get; set; }
    }
}
