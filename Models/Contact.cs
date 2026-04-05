using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mitrayana.Api.Models
{
    [Table("contact_us")]
    public class Contact
    {
        [Key]
        public int ContactId { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Message { get; set; } = string.Empty;

        public string? Role { get; set; } // Senior, ServiceProvider, etc.

        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        public bool IsResolved { get; set; } = false;
    }
}