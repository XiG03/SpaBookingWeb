using SpaBookingWeb.ViewModels.Client;
using System.Threading.Tasks;

namespace SpaBookingWeb.Services.Client
{
    public interface IClientHomeService
    {
        Task<ClientHomeViewModel> GetHomeDataAsync();
    }
}