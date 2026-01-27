
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SpaBookingWeb.Models
{
    [Table("PostCategories")]
    public class PostCategory
    {
        [Key]
        public int PostCategoryId { get; set; }
        [Required, StringLength(100)]
        public string CategoryName { get; set; }
        public string Slug { get; set; }

        public bool IsDeleted { get; set; } = false;
        
        public virtual ICollection<Post> Posts { get; set; }
    }

    [Table("Posts")]
    public class Post
    {
        [Key]
        public int PostId { get; set; }
        [Required, StringLength(255)]
        public string Title { get; set; }
        public string Slug { get; set; }
        public string Summary { get; set; }
        public string Content { get; set; }
        public string Thumbnail { get; set; }

        public int? PostCategoryId { get; set; }

        public bool IsDeleted { get; set; } = false;
        public virtual PostCategory PostCategory { get; set; }

        public int? AuthorId { get; set; }
        [ForeignKey("AuthorId")]
        public virtual Employee Author { get; set; }

        public bool IsPublished { get; set; }
        public DateTime? PublishedDate { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }

    [Table("Shifts")]
    public class Shift
    {
        [Key]
        public int ShiftId { get; set; }
        [Required, StringLength(50)]
        public string ShiftName { get; set; }
        public bool IsDeleted { get; set; } = false;
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
    }

    [Table("WorkSchedules")]
    public class WorkSchedule
    {
        [Key]
        public int ScheduleId { get; set; }
        
        public int EmployeeId { get; set; }
        public virtual Employee Employee { get; set; }

        public int ShiftId { get; set; }
        public virtual Shift Shift { get; set; }

        public DateTime? CheckOutTime { get; set; }

        public bool IsDeleted { get; set; } = false;

        public DateTime WorkDate { get; set; }
        public bool IsCheckIn { get; set; }
        public DateTime? CheckInTime { get; set; }
        public bool IsOnBreak { get; set; }
        public TimeSpan? BreakStartTime { get; set; }
        public string Note { get; set; }
    }
}