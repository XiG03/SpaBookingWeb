using SpaBookingWeb.ViewModels.Client;
using System.Threading.Tasks;

namespace SpaBookingWeb.Services.Client
{
    public interface IPostService
    {
        Task<PostListViewModel> GetPostListAsync(string search, int? categoryId, int page = 1, int pageSize = 9);
        Task<PostDetailViewModel> GetPostDetailAsync(int id);
    }
}