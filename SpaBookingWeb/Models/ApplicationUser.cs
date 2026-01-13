
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SpaBookingWeb.Models
{
    // Mở rộng IdentityUser (Map với bảng AspNetUsers)
    public class ApplicationUser : IdentityUser
    {
        // Các trường custom nếu cần
        [PersonalData]
        public string FullName { get; set; }

        [PersonalData]
        public string? Address { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now; // Mặc định lấy ngày tạo là hiện tại
    }

    [Table("Employees")]
    public class Employee
    {
        [Key]
        public int EmployeeId { get; set; }

        [Required]
        public string IdentityUserId { get; set; }
        [ForeignKey("IdentityUserId")]
        public virtual ApplicationUser? ApplicationUser { get; set; }

        [Required, StringLength(100)]
        public string FullName { get; set; }

        [StringLength(10)]
        public string Gender { get; set; }

        public DateTime? DateOfBirth { get; set; }

        [StringLength(255)]
        public string Address { get; set; }

        public DateTime HireDate { get; set; } = DateTime.Now;

        [Column(TypeName = "decimal(18,2)")]
        public decimal BaseSalary { get; set; }

        [StringLength(500)]
        public string Avatar { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsDeleted { get; set; } = false;

        // Navigation Properties
        public virtual TechnicianDetail TechnicianDetail { get; set; }
        public virtual ICollection<WorkSchedule> WorkSchedules { get; set; }
        public virtual ICollection<TechnicianService> TechnicianServices { get; set; }
        public virtual ICollection<Appointment> Appointments { get; set; } // Nhân viên book/tạo
        public virtual ICollection<AppointmentDetail> ServiceAppointments { get; set; } // KTV làm dịch vụ
        public virtual ICollection<Salary> Salaries { get; set; }
    }

    [Table("TechnicianDetails")]
    public class TechnicianDetail
    {
        [Key, ForeignKey("Employee")]
        public int EmployeeId { get; set; }

        [StringLength(50)]
        public string SkillLevel { get; set; }

        public string Bio { get; set; }

        public double CommissionRate { get; set; }
        public bool IsDeleted { get; set; } = false;

        public virtual Employee Employee { get; set; }
    }
    [Table("Salaries")]
    public class Salary
    {
        [Key]
        public int SalaryId { get; set; }

        public int EmployeeId { get; set; }
        public virtual Employee Employee { get; set; }

        public int Month { get; set; }
        public int Year { get; set; }
        public double TotalWorkHours { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCommission { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal Bonus { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal Deduction { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalSalary { get; set; }

        [StringLength(20)]
        public string Status { get; set; }

        // Tổng hợp tiền típ nhân viên kiếm được trong kỳ lương (để báo cáo)
        public decimal TotalTips { get; set; }

        public DateTime FromDate { get; set; } // Ngày bắt đầu chu kỳ lương
        public DateTime ToDate { get; set; }   // Ngày kết thúc chu kỳ lương

        public bool IsDeleted { get; set; } = false;
    }
}