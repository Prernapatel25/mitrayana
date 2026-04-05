using System;
using System.ComponentModel.DataAnnotations;

namespace Mitrayana.Api.Models
{
    public class User
    {
        [Key]
        public int UserId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        public DateTime DateOfBirth { get; set; }

        [Required]
        [Phone]
        [StringLength(15)]
        public string ContactNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(10)]
        public string Gender { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string Address { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Role { get; set; } = "Senior"; // Senior, Provider, Admin

        // Pin code / postal code (optional in DB to avoid breaking existing seed)
        [StringLength(20)]
        public string? PinCode { get; set; }

        // Senior-specific fields
        public int? Age { get; set; }
        [StringLength(500)]
        public string? HealthCondition { get; set; }
        [Phone]
        [StringLength(15)]
        public string? EmergencyContact { get; set; }

        // Provider-specific fields
        [StringLength(500)]
        public string? Skills { get; set; }
        [StringLength(100)]
        public string? Experience { get; set; }
        [StringLength(20)]
        public string? Availability { get; set; } // Full-time, Part-time, Volunteer
        [StringLength(200)]
        public string? Location { get; set; }
        [StringLength(200)]
        public string? DocumentPath { get; set; } // Store the path to uploaded document

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsVerified { get; set; } = false; // For email verification or document verification

        // Password reset token and expiry
        [StringLength(100)]
        public string? ResetToken { get; set; }

        public DateTime? ResetTokenExpiry { get; set; }
    }
} 