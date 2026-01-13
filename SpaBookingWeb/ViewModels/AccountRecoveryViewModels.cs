using System.ComponentModel.DataAnnotations;

namespace SpaBookingWeb.ViewModels
{
    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "Please enter Email")]
        [EmailAddress(ErrorMessage = "Invalid Email")]
        public string Email { get; set; }
    }

    public class RecoveryPasswordViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required(ErrorMessage = "Please enter the system-provided password")]
        [DataType(DataType.Password)]
        [Display(Name = "System-provided Password")]
        public string SystemPassword { get; set; }

        [Required(ErrorMessage = "Please enter new password")]
        [StringLength(100, ErrorMessage = "{0} must be at least {2} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm New Password")]
        [Compare("NewPassword", ErrorMessage = "The confirmation password does not match.")]
        public string ConfirmPassword { get; set; }
    }
}
