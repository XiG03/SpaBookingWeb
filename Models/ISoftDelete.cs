namespace SpaBookingWeb.Models
{
    public interface ISoftDelete
    {
        bool IsDeleted { get; set; }
    }
}