using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mitrayana.Api.Models
{
    public class ServiceRequest
    {
        [Key]
        public int RequestId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public string Title { get; set; } = string.Empty; // e.g., "Health Assistance"

        public string? SubService { get; set; } // e.g., "Doctor Visit"

        // Detailed description provided by the senior
        public string? Description { get; set; }

        // Contact number for the request
        public string? Contact { get; set; }

        // Preferred date/time for service
        public DateTime? PreferredAt { get; set; }

        // Price and duration at time of booking (string to keep it simple)
        public string? Price { get; set; }
        public string? Duration { get; set; }

        // Open, Accepted, In Progress, Completed, Cancelled
        public string Status { get; set; } = "Open";

        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        // Assigned volunteer/provider details
        public int? AssignedVolunteerId { get; set; }
        public string? AssignedVolunteerName { get; set; }

        // Pin code for location-based matching
        public string? PinCode { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }
    }
}
