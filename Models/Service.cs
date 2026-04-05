using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mitrayana.Api.Models
{
    [Table("services")]
    public class Service
    {
        [Key]
        [Column("serviceid")]
        public int ServiceId { get; set; }

        [Required]
        [StringLength(200)]
        [Column("ServiceName")]
        public string ServiceName { get; set; } = string.Empty;

        [StringLength(1000)]
        [Column("description")]
        public string? Description { get; set; }

        [Column("MinPrice")]
        public decimal? MinPrice { get; set; }

        [Column("MaxPrice")]
        public decimal? MaxPrice { get; set; }

        [Column("IsActive")]
        public bool IsActive { get; set; } = true;

        [Column("CreatedAt")]
        public DateTime? CreatedAt { get; set; }

        // Compatibility helpers (not mapped) to keep existing code working
        [NotMapped]
        public string Name { get => ServiceName; set => ServiceName = value; }

        [NotMapped]
        public decimal? Price { get => MinPrice; set { MinPrice = value; MaxPrice = value; } }

        [NotMapped]
        public string? Category { get; set; }

        [NotMapped]
        public string? SubCategory { get; set; }
    }
} 