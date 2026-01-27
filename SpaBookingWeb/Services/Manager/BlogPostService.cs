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
            // Get list of posts not soft deleted (IsDeleted = false)
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
                    // Prioritize existing Summary, otherwise cut from Content
                    Summary = !string.IsNullOrEmpty(b.Summary) 
                              ? b.Summary 
                              : (b.Content.Length > 100 ? b.Content.Substring(0, 100) + "..." : b.Content),
                    Content = b.Content,
                    ImageUrl = b.Thumbnail, // Map Thumbnail to ViewModel ImageUrl
                    AuthorName = b.Author != null ? b.Author.FullName : "Unknown", // Assume Employee has FullName
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
            // 1. Handle image upload
            string imagePath = null;
            if (request.ImageFile != null)
            {
                imagePath = await SaveFileAsync(request.ImageFile);
            }

            // 2. Find Employee by userId
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.IdentityUserId == userId ); // Adjust according to Employee structure

            // 3. Create new Post Entity
            var post = new Post
            {
                Title = request.Title,
                Slug = GenerateSlug(request.Title), // Auto generate slug
                Summary = GetSummaryFromContent(request.Content), // Auto generate summary if needed
                Content = request.Content,
                Thumbnail = imagePath,
                IsPublished = request.IsPublished,
                IsDeleted = false,
                CreatedDate = DateTime.Now,
                LastUpdated = DateTime.Now,
                
                // Assign author if Employee found, else null
                AuthorId = int.Parse(employee?.IdentityUserId) ,
                
                // Default category is null or general category ID (if selection logic exists)
                PostCategoryId = null 
            };

            _context.Posts.Add(post);
            await _context.SaveChangesAsync();
            return post.PostId;
        }

        public async Task UpdateAsync(UpdateBlogPostRequest request)
        {
            var post = await _context.Posts.FirstOrDefaultAsync(x => x.PostId == request.Id && !x.IsDeleted);
            if (post == null) throw new Exception("Blog post does not exist or has been deleted");

            // Update info
            post.Title = request.Title;
            post.Slug = GenerateSlug(request.Title); // Update slug if title changed
            post.Content = request.Content;
            post.Summary = GetSummaryFromContent(request.Content); // Update summary
            post.IsPublished = request.IsPublished;
            post.LastUpdated = DateTime.Now; // Update modification time

            // Only update image if user uploaded new one
            if (request.ImageFile != null)
            {
                // Can add logic to delete old image here to save space
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
                // Soft Delete: Do not delete from DB, just mark as deleted
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

            // Add timestamp to avoid duplicate filename
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
            // Remove simple HTML tags (if content is HTML) to get plain text for summary
            var plainText = Regex.Replace(content, "<.*?>", String.Empty);
            return plainText.Length > 150 ? plainText.Substring(0, 150) + "..." : plainText;
        }

        private string GenerateSlug(string title)
        {
            if (string.IsNullOrEmpty(title)) return string.Empty;

            // Convert to lowercase
            string slug = title.ToLower().Trim();

            // Replace accented characters with unaccented (Example: ấ -> a)
            slug = ConvertToUnSign(slug);

            // Replace spaces and special characters with hyphens
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