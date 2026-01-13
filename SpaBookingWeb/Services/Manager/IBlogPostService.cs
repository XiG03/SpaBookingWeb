using System.Collections.Generic;
using System.Threading.Tasks;
using SpaBookingWeb.ViewModels.BlogPosts;

namespace SpaBookingWeb.Services.Interfaces
{
    public interface IBlogPostService
    {
        // Lấy tất cả bài viết (có thể thêm phân trang sau này)
        Task<List<BlogPostViewModel>> GetAllAsync();
        
        // Lấy chi tiết bài viết để xem hoặc sửa
        Task<BlogPostViewModel> GetByIdAsync(int id);
        
        // Tạo bài viết mới (trả về ID bài vừa tạo)
        Task<int> CreateAsync(CreateBlogPostRequest request, string userId);
        
        // Cập nhật bài viết
        Task UpdateAsync(UpdateBlogPostRequest request);
        
        // Xóa bài viết
        Task DeleteAsync(int id);
    }
}