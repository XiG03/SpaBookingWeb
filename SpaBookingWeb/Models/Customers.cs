
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SpaBookingWeb.Models
{
    [Table("MembershipTypes")]
    public class MembershipType
    {
        [Key]
        public int MembershipTypeId { get; set; }

        [Required, StringLength(50)]
        public string TypeName { get; set; }

        public double DiscountPercent { get; set; }

        public bool IsDeleted { get; set; } = false;

        [Column(TypeName = "decimal(18,2)")]
        public decimal MinSpendRequirement { get; set; }

        public virtual ICollection<Customer> Customers { get; set; }
    }

    [Table("Customers")]
    public class Customer
    {
        [Key]
        public int CustomerId { get; set; }

        [Required, StringLength(100)]
        public string FullName { get; set; }

        [Required, StringLength(20)]
        public string PhoneNumber { get; set; }

        [StringLength(100)]
        public string Email { get; set; }

        public bool IsDeleted { get; set; } = false;

        public int? MembershipTypeId { get; set; }
        [ForeignKey("MembershipTypeId")]
        public virtual MembershipType MembershipType { get; set; }

        public int Point { get; set; } = 0;

        public virtual ICollection<Appointment> Appointments { get; set; }
    }
}