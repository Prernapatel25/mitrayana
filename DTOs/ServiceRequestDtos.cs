using System.ComponentModel.DataAnnotations;

namespace Mitrayana.Api.DTOs
{
    public class CreateServiceRequest
    {
        [Required]
        public string Title { get; set; } = string.Empty;

        public string? SubService { get; set; }
        public string? Description { get; set; }
        public string? Contact { get; set; }
        public DateTime? PreferredAt { get; set; }
        public string? Price { get; set; }
        public string? Duration { get; set; }
    }

    public class ServiceRequestResponse
    {
        public int RequestId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? SubService { get; set; }
        public string? Description { get; set; }
        public string? Contact { get; set; }
        public DateTime? PreferredAt { get; set; }
        public string? Price { get; set; }
        public string? Duration { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime DateCreated { get; set; }
        public int? AssignedVolunteerId { get; set; }
        public string? AssignedVolunteerName { get; set; }
        public string? AssignedVolunteerContact { get; set; }
        public string? PinCode { get; set; }

        // The senior who requested the service
        public string? SeniorName { get; set; }
    }
}