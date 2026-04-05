using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mitrayana.Api.Models
{
    [Table("subservices")]
    public class SubService
    {
        [Key]
        [Column("subserviceid")]
        public int SubServiceId { get; set; }

        [Required]
        [Column("serviceid")]
        public int ServiceId { get; set; }

        // Actual DB column is 'SubServiceName'
        [Required]
        [StringLength(200)]
        [Column("SubServiceName")]
        public string SubServiceName { get; set; } = string.Empty;

        // Compatibility property used by existing code
        [NotMapped]
        public string Name { get => SubServiceName; set => SubServiceName = value; }

        [Column("price")]
        public decimal? Price { get; set; }

        [StringLength(1000)]
        [Column("description")]
        public string? Description { get; set; }

        [Column("Hours")]
        public int? Hours { get; set; }

        [Column("IsActive")]
        public bool IsActive { get; set; } = true;

        [Column("CreatedAt")]
        public DateTime? CreatedAt { get; set; }

        // Navigation property
        [ForeignKey("ServiceId")]
        public Service? Service { get; set; }
    }
}