using System.Collections.Generic;

namespace SpaBookingWeb.ViewModels.Manager
{
    public class PermissionViewModel
    {
        public string RoleId { get; set; }
        public string RoleName { get; set; }
        public List<RoleClaimsDto> RoleClaims { get; set; }
    }

    public class RoleClaimsDto
    {
        public string Type { get; set; }
        public string Value { get; set; } // Ví dụ: Permissions.Products.Delete
        public bool IsSelected { get; set; } // True nếu Role đã có quyền này
    }
}