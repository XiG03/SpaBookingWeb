using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using SpaBookingWeb.Data;
using SpaBookingWeb.Models;
using SpaBookingWeb.Services.Interfaces;
using SpaBookingWeb.ViewModels.BlogPosts;

namespace SpaBookingWeb.Services.Implements
{
    public class BlogPostService : IBlogPostService
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public BlogPostService(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<List<BlogPostViewModel>> GetAllAsync()
        {
            // Lấy danh sách bài viết chưa bị xóa mềm (IsDeleted = false)
            return await _context.Posts
                .AsNoTracking()
                .Where(x => !x.IsDeleted) 
                .Include(b => b.Author)
                .Include(b => b.PostCategory)
                .OrderByDescending(b => b.CreatedDate)
                .Select(b => new BlogPostViewModel
                {
                    Id = b.PostId,
                    Title = b.Title,
                    // Ưu tiên lấy Summary có sẵn, nếu không thì cắt từ Content
                    Summary = !string.IsNullOrEmpty(b.Summary) 
                              ? b.Summary 
                              : (b.Content.Length > 100 ? b.Content.Substring(0, 100) + "..." : b.Content),
                    Content = b.Content,
                    ImageUrl = b.Thumbnail, // Map Thumbnail sang ImageUrl của ViewModel
                    AuthorName = b.Author != null ? b.Author.FullName : "Unknown", // Giả định Employee có FullName
                    IsPublished = b.IsPublished,
                    CreatedAt = b.CreatedDate
                })
                .ToListAsync();
        }

        public async Task<BlogPostViewModel> GetByIdAsync(int id)
        {
            var post = await _context.Posts
                .Include(x => x.Author)
                .Include(x => x.PostCategory)
                .FirstOrDefaultAsync(x => x.PostId == id && !x.IsDeleted);

            if (post == null) return null;

            return new BlogPostViewModel
            {
                Id = post.PostId,
                Title = post.Title,
                Summary = post.Summary,
                Content = post.Content,
                ImageUrl = post.Thumbnail,
                AuthorName = post.Author?.FullName,
                IsPublished = post.IsPublished,
                CreatedAt = post.CreatedDate
            };
        }

        public async Task<int> CreateAsync(CreateBlogPostRequest request, string userId)
        {
            // 1. Xử lý lưu ảnh thumbnail
            string imagePath = null;
            if (request.ImageFile != null)
            {
                imagePath = await SaveFileAsync(request.ImageFile);
            }

            // 2. Tìm EmployeeId dựa trên UserId (Identity)
            // Lưu ý: Cần đảm bảo bảng Employees có trường liên kết với User (ví dụ AppUserId hoặc Email)
            // Ở đây tôi dùng logic giả định là tìm Employee theo UserId
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.IdentityUserId == userId ); // Điều chỉnh tùy theo cấu trúc Employee

            // 3. Tạo Entity Post mới
            var post = new Post
            {
                Title = request.Title,
                Slug = GenerateSlug(request.Title), // Tự động tạo slug
                Summary = GetSummaryFromContent(request.Content), // Tự động tạo summary nếu cần
                Content = request.Content,
                Thumbnail = imagePath,
                IsPublished = request.IsPublished,
                IsDeleted = false,
                CreatedDate = DateTime.Now,
                LastUpdated = DateTime.Now,
                
                // Gán tác giả nếu tìm thấy Employee, nếu không thì null
                AuthorId = int.Parse(employee?.IdentityUserId) ,
                
                // Mặc định category là null hoặc ID danh mục chung (nếu có logic chọn category)
                PostCategoryId = null 
            };

            _context.Posts.Add(post);
            await _context.SaveChangesAsync();
            return post.PostId;
        }

        public async Task UpdateAsync(UpdateBlogPostRequest request)
        {
            var post = await _context.Posts.FirstOrDefaultAsync(x => x.PostId == request.Id && !x.IsDeleted);
            if (post == null) throw new Exception("Bài viết không tồn tại hoặc đã bị xóa");

            // Cập nhật thông tin
            post.Title = request.Title;
            post.Slug = GenerateSlug(request.Title); // Cập nhật lại slug nếu đổi tên
            post.Content = request.Content;
            post.Summary = GetSummaryFromContent(request.Content); // Cập nhật lại summary
            post.IsPublished = request.IsPublished;
            post.LastUpdated = DateTime.Now; // Cập nhật thời gian sửa đổi

            // Chỉ cập nhật ảnh nếu người dùng upload ảnh mới
            if (request.ImageFile != null)
            {
                // Có thể thêm logic xóa ảnh cũ ở đây nếu muốn tiết kiệm dung lượng
                post.Thumbnail = await SaveFileAsync(request.ImageFile);
            }

            _context.Posts.Update(post);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var post = await _context.Posts.FindAsync(id);
            if (post != null)
            {
                // Soft Delete: Không xóa khỏi DB mà chỉ đánh dấu là đã xóa
                post.IsDeleted = true;
                post.LastUpdated = DateTime.Now;
                
                _context.Posts.Update(post);
                await _context.SaveChangesAsync();
            }
        }

        // --- Helper Methods ---

        private async Task<string> SaveFileAsync(Microsoft.AspNetCore.Http.IFormFile file)
        {
            string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads/blog");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            // Thêm timestamp để tránh trùng tên file
            string extension = Path.GetExtension(file.FileName);
            string uniqueFileName = $"{Guid.NewGuid()}{extension}";
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            return "/uploads/blog/" + uniqueFileName;
        }

        private string GetSummaryFromContent(string content)
        {
            if (string.IsNullOrEmpty(content)) return string.Empty;
            // Loại bỏ thẻ HTML đơn giản (nếu content là HTML) để lấy text thuần làm summary
            var plainText = Regex.Replace(content, "<.*?>", String.Empty);
            return plainText.Length > 150 ? plainText.Substring(0, 150) + "..." : plainText;
        }

        private string GenerateSlug(string title)
        {
            if (string.IsNullOrEmpty(title)) return string.Empty;

            // Chuyển về chữ thường
            string slug = title.ToLower().Trim();

            // Thay thế ký tự có dấu thành không dấu (Ví dụ: ấ -> a)
            slug = ConvertToUnSign(slug);

            // Thay thế khoảng trắng và ký tự đặc biệt bằng dấu gạch ngang
            slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = Regex.Replace(slug, @"\s+", "-");
            slug = Regex.Replace(slug, @"-+", "-");

            return slug;
        }

        private string ConvertToUnSign(string s)
        {
            Regex regex = new Regex("\\p{IsCombiningDiacriticalMarks}+");
            string temp = s.Normalize(NormalizationForm.FormD);
            return regex.Replace(temp, String.Empty).Replace('\u0111', 'd').Replace('\u0110', 'D');
        }
    }
}