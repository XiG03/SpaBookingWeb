using System.Collections.Generic;
using System.Threading.Tasks;
using SpaBookingWeb.ViewModels.BlogPosts;

namespace SpaBookingWeb.Services.Interfaces
{
    public interface IBlogPostService
    {
        // Get all posts (pagination can be added later)
        Task<List<BlogPostViewModel>> GetAllAsync();
        
        // Get post detail to view or edit
        Task<BlogPostViewModel> GetByIdAsync(int id);
        
        // Create new post (return newly created ID)
        Task<int> CreateAsync(CreateBlogPostRequest request, string userId);
        
        // Update post
        Task UpdateAsync(UpdateBlogPostRequest request);
        
        // Delete post
        Task DeleteAsync(int id);
    }
}