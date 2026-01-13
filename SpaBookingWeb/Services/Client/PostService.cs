using Microsoft.EntityFrameworkCore;
using SpaBookingWeb.Data;
using SpaBookingWeb.ViewModels.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SpaBookingWeb.Services.Client
{
    public class PostService : IPostService
    {
        private readonly ApplicationDbContext _context;

        public PostService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PostListViewModel> GetPostListAsync(string search, int? categoryId, int page = 1, int pageSize = 9)
        {
            var model = new PostListViewModel
            {
                CurrentSearch = search,
                CurrentCategoryId = categoryId,
                CurrentPage = page
            };

            // 1. Lấy danh mục
            model.Categories = await _context.PostCategories
                .Where(c => !c.IsDeleted)
                .Select(c => new ClientPostCategoryViewModel
                {
                    Id = c.PostCategoryId,
                    Name = c.CategoryName,
                    PostCount = c.Posts.Count(p => !p.IsDeleted && p.IsPublished),
                    IsSelected = c.PostCategoryId == categoryId
                })
                .ToListAsync();

            // 2. Query Posts
            var query = _context.Posts
                .Include(p => p.PostCategory)
                .Include(p => p.Author)
                .Where(p => !p.IsDeleted && p.IsPublished);

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p => p.Title.Contains(search) || p.Summary.Contains(search));
            }

            if (categoryId.HasValue)
            {
                query = query.Where(p => p.PostCategoryId == categoryId);
            }

            // Sắp xếp bài mới nhất lên đầu
            query = query.OrderByDescending(p => p.PublishedDate ?? p.CreatedDate);

            // 3. Phân trang
            int totalItems = await query.CountAsync();
            model.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            model.Posts = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ClientPostItemViewModel
                {
                    Id = p.PostId,
                    Title = p.Title,
                    Slug = p.Slug,
                    Summary = p.Summary,
                    Thumbnail = string.IsNullOrEmpty(p.Thumbnail) ? "https://lh3.googleusercontent.com/aida-public/AB6AXuCLHUwV-Z7x3Pyl8s-3qZ77YyV9-k5W7qX9zR1P3uL5mN7vJ9oK4wE8rT6yS2dF1gH0jA4bC3xQ5vM8nL2kP9oJ4hG7fD6sA1wE3rT5yU8iO9pL2kM4nJ6vH0gX3zF5cR8bA9dE7w" : p.Thumbnail,
                    CategoryName = p.PostCategory != null ? p.PostCategory.CategoryName : "Tin tức",
                    AuthorName = p.Author != null ? p.Author.FullName : "Admin",
                    PublishedDateStr = (p.PublishedDate ?? p.CreatedDate).ToString("dd/MM/yyyy")
                })
                .ToListAsync();

            return model;
        }

        public async Task<PostDetailViewModel> GetPostDetailAsync(int id)
        {
            var post = await _context.Posts
                .Include(p => p.PostCategory)
                .Include(p => p.Author)
                .FirstOrDefaultAsync(p => p.PostId == id && !p.IsDeleted && p.IsPublished);

            if (post == null) return null;

            var model = new PostDetailViewModel
            {
                Id = post.PostId,
                Title = post.Title,
                Content = post.Content, // Nội dung HTML
                Thumbnail = string.IsNullOrEmpty(post.Thumbnail) ? "https://lh3.googleusercontent.com/aida-public/AB6AXuCLHUwV-Z7x3Pyl8s-3qZ77YyV9-k5W7qX9zR1P3uL5mN7vJ9oK4wE8rT6yS2dF1gH0jA4bC3xQ5vM8nL2kP9oJ4hG7fD6sA1wE3rT5yU8iO9pL2kM4nJ6vH0gX3zF5cR8bA9dE7w" : post.Thumbnail,
                AuthorName = post.Author != null ? post.Author.FullName : "Admin",
                AuthorAvatar = post.Author?.Avatar ?? "https://lh3.googleusercontent.com/aida-public/AB6AXuCu-TjZAydZtuktz7Wqw25aW_vPCzYIbnylRw9JitpFPJxL_VIRZI3lwTMalaUH0rZxsncacs6lgYsGID-B0dfP9C3McdKs586DoHEljj3HsMUiBgRY3bDS_9TjgEykM_bDTbmrgMtT-Uy2HbBFjIIMS1ArZ20uTZG16glule-8Dai2IyeaeITtWhMrXHPJXuB-eQKOE0gtmTNEC4HPAxyaZYQWGYDzA5ynpmZS-6UIVh9pqg9XxPBUy3x4Tzecnl4B93xS_8jLkUU",
                PublishedDateStr = (post.PublishedDate ?? post.CreatedDate).ToString("dd 'Tháng' MM, yyyy"),
                CategoryName = post.PostCategory?.CategoryName ?? "Tin tức",
                CategoryId = post.PostCategoryId
            };

            // Lấy bài viết liên quan (cùng danh mục, trừ bài hiện tại)
            if (post.PostCategoryId.HasValue)
            {
                model.RelatedPosts = await _context.Posts
                    .Where(p => p.PostCategoryId == post.PostCategoryId && p.PostId != post.PostId && !p.IsDeleted && p.IsPublished)
                    .OrderByDescending(p => p.PublishedDate)
                    .Take(3)
                    .Select(p => new ClientPostItemViewModel
                    {
                        Id = p.PostId,
                        Title = p.Title,
                        Slug = p.Slug,
                        Thumbnail = string.IsNullOrEmpty(p.Thumbnail) ? "https://lh3.googleusercontent.com/aida-public/AB6AXuCLHUwV-Z7x3Pyl8s-3qZ77YyV9-k5W7qX9zR1P3uL5mN7vJ9oK4wE8rT6yS2dF1gH0jA4bC3xQ5vM8nL2kP9oJ4hG7fD6sA1wE3rT5yU8iO9pL2kM4nJ6vH0gX3zF5cR8bA9dE7w" : p.Thumbnail,
                        CategoryName = p.PostCategory.CategoryName,
                        PublishedDateStr = (p.PublishedDate ?? p.CreatedDate).ToString("dd/MM/yyyy")
                    })
                    .ToListAsync();
            }

            return model;
        }
    }
}