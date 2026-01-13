
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace SpaBookingWeb.Models
{
    [Table("Vouchers")]
    public class Voucher 
    {
        [Key]
        public int VoucherId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required, StringLength(50)]
        public string Code { get; set; }

        public string Description { get; set; }

        [Required, StringLength(20)]
        public string DiscountType { get; set; } // Percent

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountValue { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal MinSpend { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MaxDiscountAmount { get; set; }

        public int UsageLimit { get; set; }
        public int UsageCount { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; } = true;
        
        // Giữ lại IsDeleted để hỗ trợ xóa mềm (không xóa mất dữ liệu lịch sử)
        public bool IsDeleted { get; set; } = false;
    }

    [Table("Appointments")]
    public class Appointment
    {
        [Key]
        public int AppointmentId { get; set; }

        public int CustomerId { get; set; }
        [ForeignKey("CustomerId")]
        public virtual Customer Customer { get; set; }

        // Nhân viên tạo lịch hoặc nhân viên chính phụ trách
        public int? EmployeeId { get; set; }
        [ForeignKey("EmployeeId")]
        public virtual Employee Employee { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }

        [StringLength(50)]
        public string Status { get; set; } = "Pending";

        public string Notes { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DepositAmount { get; set; }

        public bool IsDepositPaid { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public bool IsDeleted { get; set; } = false;

        public virtual ICollection<AppointmentDetail> AppointmentDetails { get; set; }
        public virtual Invoice Invoice { get; set; } // 1-1 Relation
        public virtual ICollection<Review> Reviews { get; set; }
    }

    [Table("AppointmentDetails")]
    public class AppointmentDetail
    {
        [Key]
        public int AppointmentDetailId { get; set; }

        public int AppointmentId { get; set; }
        [ForeignKey("AppointmentId")]
        public virtual Appointment Appointment { get; set; }

        public int? ServiceId { get; set; }
        public virtual Service Service { get; set; }

        public int? ComboId { get; set; }
        public virtual Combo Combo { get; set; }

        public int? TechnicianId { get; set; }
        [ForeignKey("TechnicianId")]
        public virtual Employee Technician { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PriceAtBooking { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = "Pending";

        public bool IsDeleted { get; set; } = false;

        public virtual ICollection<AppointmentConsumable> AppointmentConsumables { get; set; }
    }

    [Table("Invoices")]
    public class Invoice
    {
        [Key]
        public int InvoiceId { get; set; }

        public int AppointmentId { get; set; }
        [ForeignKey("AppointmentId")]
        public virtual Appointment Appointment { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        public int? VoucherId { get; set; }
        public virtual Voucher Voucher { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DepositDeduction { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal FinalAmount { get; set; }

        [StringLength(50)]
        public string PaymentStatus { get; set; } = "Unpaid";
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public bool IsDeleted { get; set; } = false;

        public virtual ICollection<Payment> Payments { get; set; }
    }

    [Table("Payments")]
    public class Payment
    {
        [Key]
        public int PaymentId { get; set; }

        public int InvoiceId { get; set; }
        [ForeignKey("InvoiceId")]
        public virtual Invoice Invoice { get; set; }

        [Required, StringLength(50)]
        public string PaymentMethod { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public bool IsDeleted { get; set; } = false;

        [Required, StringLength(20)]
        public string TransactionType { get; set; } = "Settlement";

        public DateTime PaymentDate { get; set; } = DateTime.Now;
    }

    [Table("AppointmentConsumables")]
    public class AppointmentConsumable
    {
        [Key]
        public int UsageId { get; set; }

        public int AppointmentDetailId { get; set; }
        [ForeignKey("AppointmentDetailId")]
        public virtual AppointmentDetail AppointmentDetail { get; set; }

        public int ProductId { get; set; }
        public virtual Product Product { get; set; }

        public int StandardQuantity { get; set; }
        public int ActualQuantity { get; set; }

        public bool IsDeleted { get; set; } = false;
        public string Reason { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }

    [Table("Reviews")]
    public class Review
    {
        [Key]
        public int ReviewId { get; set; }

        public int AppointmentId { get; set; }
        [ForeignKey("AppointmentId")]
        public virtual Appointment Appointment { get; set; }

        [Range(1, 5)]
        public int Rating { get; set; }

        public string Comment { get; set; }

        public bool IsDeleted { get; set; } = false;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }

    [Table("Operations")]
    public class Operations
    {
        [Key]
        public int Id { get; set; }

        // Liên kết với bảng User
        public string UserId { get; set; }
        [ForeignKey("UserId")]
        public ApplicationUser User { get; set; }

        public DateTime CreateDate { get; set; } = DateTime.Now;
        public DateTime? BookingDate { get; set; } // Ngày khách đặt lịch đến

        // Trạng thái đơn: 1 (Hoàn thành), 0 (Mới/Chờ), -1 (Hủy)
        public int Status { get; set; }

        public decimal TotalAmount { get; set; }

        public bool IsDeleted { get; set; } = false;
        public string? Note { get; set; }
    }


}