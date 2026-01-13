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
        [Required(ErrorMessage = "Please enter the post title")]
        [MaxLength(200, ErrorMessage = "Title cannot exceed 200 characters")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Please enter the content")]
        public string Content { get; set; }

        [Display(Name = "Thumbnail")]
        public IFormFile ImageFile { get; set; } // Upload ảnh

        [Display(Name = "Publish Immediately")]
        public bool IsPublished { get; set; }
    }

    // Dùng cho Form Cập nhật
    public class UpdateBlogPostRequest : CreateBlogPostRequest
    {
        public int Id { get; set; }
        public string ExistingImageUrl { get; set; } // Giữ ảnh cũ nếu không up ảnh mới
    }
}