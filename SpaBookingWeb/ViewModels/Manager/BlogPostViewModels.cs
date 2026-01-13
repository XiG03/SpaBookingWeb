using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace SpaBookingWeb.ViewModels.BlogPosts
{
    // Dùng để hiển thị danh sách hoặc chi tiết
    public class BlogPostViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; } // Tóm tắt ngắn
        public string Content { get; set; }
        public string ImageUrl { get; set; }
        public string AuthorName { get; set; }
        public bool IsPublished { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // Dùng cho Form Tạo mới
    public class CreateBlogPostRequest
    {
        [Required(ErrorMessage = "Vui lòng nhập tiêu đề bài viết")]
        [MaxLength(200, ErrorMessage = "Tiêu đề không quá 200 ký tự")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập nội dung")]
        public string Content { get; set; }

        [Display(Name = "Ảnh đại diện")]
        public IFormFile ImageFile { get; set; } // Upload ảnh

        [Display(Name = "Xuất bản ngay")]
        public bool IsPublished { get; set; }
    }

    // Dùng cho Form Cập nhật
    public class UpdateBlogPostRequest : CreateBlogPostRequest
    {
        public int Id { get; set; }
        public string ExistingImageUrl { get; set; } // Giữ ảnh cũ nếu không up ảnh mới
    }
}