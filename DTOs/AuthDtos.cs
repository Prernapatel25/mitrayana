namespace Mitrayana.Api.DTOs
{
    using Microsoft.AspNetCore.Http;
    public class RegisterRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = "Senior";
        public DateTime DateOfBirth { get; set; }
        public string ContactNumber { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string PinCode { get; set; } = string.Empty;

        // Senior-specific fields
        public int? Age { get; set; }
        public string? HealthCondition { get; set; }
        public string? EmergencyContact { get; set; }

        // Provider-specific fields
        public string? Skills { get; set; }
        public string? Availability { get; set; }
        public string? Location { get; set; }
        public IFormFile? Document { get; set; }
    }

    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class AuthResponse
    {
        public string Token { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    public class ForgotRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    public class ResetRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
} 