using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SpaBookingWeb.Models
{
    [Table("ActivityLogs")]
    public class ActivityLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Action { get; set; } // "Create", "Update", "Delete"

        [Required]
        [StringLength(100)]
        public string EntityName { get; set; } // Tên bảng (Products, Users...)

        [StringLength(500)]
        public string EntityId { get; set; } // ID của dòng dữ liệu (Primary Key)

        public string? UserId { get; set; } // Người thực hiện

        [StringLength(50)]
        public string IpAddress { get; set; }

        public string Description { get; set; } // Mô tả ngắn gọn (Human-readable)

        public DateTime Timestamp { get; set; } = DateTime.Now;

        // --- CÁC TRƯỜNG MỚI ĐỂ LƯU CHI TIẾT ---
        
        // Lưu JSON giá trị cũ (Trước khi sửa/xóa)
        public string OldValues { get; set; } 

        // Lưu JSON giá trị mới (Sau khi sửa/thêm)
        public string NewValues { get; set; } 

        // Các cột bị thay đổi (Ví dụ: "Price, StockQuantity")
        public string AffectedColumns { get; set; } = string.Empty;
    }
}