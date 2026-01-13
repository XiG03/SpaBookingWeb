using System;
using System.ComponentModel.DataAnnotations;

namespace SpaBookingWeb.ViewModels.Manager
{
    public class ProfileViewModel
    {
        public string Id { get; set; } // Identity User Id

        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [Display(Name = "Email")]
        public string Email { get; set; }

        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }

        [Display(Name = "Address")]
        public string Address { get; set; }

        [Display(Name = "Role")]
        public string Role { get; set; }

        [Display(Name = "Join Date")]
        public DateTime JoinDate { get; set; }
        
        public string Avatar { get; set; }
    }
}