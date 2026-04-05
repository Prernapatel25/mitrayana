using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mitrayana.Api.Models
{
    [Table("feedback")]
    public class Feedback
    {
        [Key]
        public int FeedbackId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public string Message { get; set; } = string.Empty;

        public string? Category { get; set; }

        // 1-5 rating
        public int Rating { get; set; } = 5;

        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        [ForeignKey("UserId")]
        public User? User { get; set; }
    }
}
