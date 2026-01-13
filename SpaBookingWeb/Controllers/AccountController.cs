using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using SpaBookingWeb.Models;
using SpaBookingWeb.ViewModels;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using SpaBookingWeb.Services;
using Microsoft.EntityFrameworkCore;

namespace SpaBookingWeb.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager; // Đã thêm
        private readonly ILogger<AccountController> _logger;
        private readonly IEmailService _emailService;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager, // Inject thêm RoleManager
            ILogger<AccountController> logger,
            IEmailService emailService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
            _emailService = emailService;
        }

        // --- LOGIN ---
        [HttpGet]
        public IActionResult Login(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        private async Task<IActionResult> RedirectToRoleAsync(ApplicationUser user, string returnUrl)
        {
            try
            {
                var roles = await _userManager.GetRolesAsync(user);

                // If Manager, Receptionist or Technician, show Role Selection page
                if (roles.Contains("Manager") || roles.Contains("Receptionist") || roles.Contains("Technician"))
                {
                    return RedirectToAction("RoleSelection");
                }

                if (roles.Contains("Manager") || roles.Contains("Admin"))
                {
                    return RedirectToAction("Index", "Home", new { area = "Manager" });
                }
                else if (roles.Contains("Technician") || roles.Contains("Receptionist") || roles.Contains("Staff"))
                {
                    return RedirectToAction("Index", "Schedule", new { area = "Staff" });
                }

                // Check valid returnUrl
                if (Url.IsLocalUrl(returnUrl) && returnUrl != "/")
                {
                    return LocalRedirect(returnUrl);
                }
                return RedirectToAction("Index", "Home");
            }
            catch
            {
                return RedirectToAction("Index", "Home");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModels model, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            returnUrl ??= Url.Content("~/");

            // Common validation: Only need Email and Password
            ModelState.Remove("StaffId");
            ModelState.Remove("StaffCode");
            ModelState.Remove("LoginType"); 

            if (string.IsNullOrEmpty(model.Email) || string.IsNullOrEmpty(model.Password))
            {
                ModelState.AddModelError("", "Please enter Email and Password.");
            }

            if (ModelState.IsValid)
            {
                // Find user by Email
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user != null)
                {
                    // Try login with Password
                    // Note: SignInManager uses UserName to login, so must pass user.UserName
                    var result = await _signInManager.PasswordSignInAsync(user.UserName, model.Password, model.RememberMe, lockoutOnFailure: false);
                    
                    if (result.Succeeded)
                    {
                        return await RedirectToRoleAsync(user, returnUrl);
                    }
                }

                // If failed (wrong pass or user not found)
                if (user != null)
                {
                     await CheckLoginAsync(model); // This function will add ModelState error if specific issue
                     if(ModelState.IsValid) ModelState.AddModelError(string.Empty, "Login failed. Incorrect password.");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Account does not exist.");
                }
            }

            return View(model);
        }

        [HttpGet]
        [Authorize]
        public IActionResult RoleSelection()
        {
            return View();
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> RoleRedirect(string mode)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login");

            if (mode == "Employee")
            {
                var roles = await _userManager.GetRolesAsync(user);
                // Old employee navigation logic
                if (roles.Contains("Manager") || roles.Contains("Admin"))
                {
                    return RedirectToAction("Index", "Home", new { area = "Manager" });
                }
                if (roles.Contains("Technician") || roles.Contains("Receptionist") || roles.Contains("Staff"))
                {
                    return RedirectToAction("Index", "Schedule", new { area = "Staff" });
                }
            }

            // Default hoặc Customer
            return RedirectToAction("HomeClient", "Home");
        }

        public class LoginCheckResult
        {
            public bool IsValid { get; set; }
            public string ErrorMessage { get; set; }
        }
        public async Task<LoginCheckResult> CheckLoginAsync(LoginViewModels model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);

            if (user == null)
            {
                return new LoginCheckResult
                {
                    IsValid = false,
                    ErrorMessage = "Account does not exist"
                };
            }

            if (!await _userManager.CheckPasswordAsync(user, model.Password))
            {
                return new LoginCheckResult
                {
                    IsValid = false,
                    ErrorMessage = "Incorrect password"
                };
            }

            if (!await _userManager.IsEmailConfirmedAsync(user))
            {
                return new LoginCheckResult
                {
                    IsValid = false,
                    ErrorMessage = "Email not confirmed"
                };
            }

            if (await _userManager.IsLockedOutAsync(user))
            {
                return new LoginCheckResult
                {
                    IsValid = false,
                    ErrorMessage = "Account locked"
                };
            }

            // OK
            return new LoginCheckResult
            {
                IsValid = true
            };
        }


        // --- REGISTER ---
        [HttpGet]
        public IActionResult Register(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            returnUrl ??= Url.Content("~/");

            if (ModelState.IsValid)
            {
                // 1. Check if email exists
                var existingUser = await _userManager.FindByEmailAsync(model.Email);
                if (existingUser != null)
                {
                    ModelState.AddModelError("Email", "This Email is already in use.");
                    return View(model);
                }

                var existingPhone = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == model.PhoneNumber);
                if(existingPhone != null)
                {
                    ModelState.AddModelError("PhoneNumber", "This Phone Number is already registered");
                    return View(model);
                }
                // 2. Create User (Not active yet)
                var user = new ApplicationUser 
                { 
                    UserName = model.Email, 
                    Email = model.Email, 
                    FullName = model.FullName, 
                    PhoneNumber = model.PhoneNumber, 
                    CreatedDate = DateTime.Now,
                    EmailConfirmed = false 
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    // Automatically create Customer role if not exists - Ensure no error
                    if (!await _roleManager.RoleExistsAsync("Customer"))
                    {
                        await _roleManager.CreateAsync(new IdentityRole("Customer"));
                    }
                    await _userManager.AddToRoleAsync(user, "Customer");

                    // 3. Generate Verification Code (Token)
                    // Note: Default Identity Token is too long.
                    // To generate 6-digit code, we can use `GenerateTwoFactorTokenAsync` or generate random number.
                    // Here I use simple method: Generate random number and save to User Token (or temporary Claim).
                    
                    var token = new Random().Next(100000, 999999).ToString();
                    
                    // Save this token to DB to verify later (Use SetAuthenticationTokenAsync)
                    await _userManager.SetAuthenticationTokenAsync(user, "Default", "EmailConfirmation", token);

                    // 4. Send real Email via EmailService
                    string subject = "Account Registration Verification Code - Lotus Spa";
                    string message = $@"
                        <h3>Hello {user.FullName},</h3>
                        <p>Thank you for registering at Lotus Spa.</p>
                        <p>Your verification code is: <strong style='font-size: 24px; color: #ec4899;'>{token}</strong></p>
                        <p>Please enter this code to activate your account.</p>
                        <p>Best regards,<br/>Lotus Spa Team</p>";

                    bool sentSuccess = false;
                    try 
                    {
                        await _emailService.SendEmailAsync(user.Email, subject, message);
                        sentSuccess = true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Email send error: {ex.Message}");
                        
                        // IMPORTANT: Delete user if email fails to allow re-registration
                        await _userManager.DeleteAsync(user);
                        
                        ModelState.AddModelError(string.Empty, "Cannot send verification email. Please check your email address or try again later.");
                        return View(model);
                    }

                    if (sentSuccess)
                    {
                        _logger.LogInformation($"OTP for {user.Email}: {token}"); 
                        return RedirectToAction("VerifyEmail", new { email = user.Email, returnUrl });
                    } 
                }

                foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult VerifyEmail(string email, string returnUrl = null)
        {
            if (string.IsNullOrEmpty(email)) return RedirectToAction("Register");
            
            ViewData["ReturnUrl"] = returnUrl;
            ViewData["Email"] = email;
            
            // Return View to enter code
            return View(); 
        }

        [HttpGet]
        public IActionResult ConfirmEmailCode(string email, string returnUrl = null)
        {
            if (string.IsNullOrEmpty(email)) return RedirectToAction("Register");
            ViewData["Email"] = email;
            ViewData["ReturnUrl"] = returnUrl;
            return View(); // Need to create ConfirmEmailCode.cshtml View
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmEmailCode(string email, string code, string returnUrl = null)
        {
            ViewData["Email"] = email;
            ViewData["ReturnUrl"] = returnUrl;
            returnUrl = returnUrl ?? Url.Content("/Home/HomeClient");

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(code))
            {
                ModelState.AddModelError("", "Please enter verification code.");
                return View();
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null) return RedirectToAction("Register");

            // --- CHECK OTP CODE FROM DB ---
            // Get token saved during registration
            var savedToken = await _userManager.GetAuthenticationTokenAsync(user, "Default", "EmailConfirmation");
            
            bool isCodeValid = (savedToken == code);

            if (isCodeValid)
            {
                // 1. Activate user
                user.EmailConfirmed = true;
                await _userManager.UpdateAsync(user);
                
                // Delete token after use (Optional)
                await _userManager.RemoveAuthenticationTokenAsync(user, "Default", "EmailConfirmation");

                // 2. Login immediately
                await _signInManager.SignInAsync(user, isPersistent: false);

                  TempData["SuccessMessage"] = "Account verified successfully! Welcome to SpaBookingWeb";

                // 3. Redirect
                return await RedirectToRoleAsync(user, returnUrl);
            }
            else
            {
                ModelState.AddModelError("", "Incorrect verification code.");
                return View();
            }
        }
        // --- LOGOUT ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("HomeClient", "Home");
        }

        // --- GOOGLE LOGIN ---
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public IActionResult ExternalLogin(string provider, string returnUrl = null)
        {
            var redirectUrl = Url.Action("ExternalLoginCallback", "Account", new { returnUrl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return Challenge(properties, provider);
        }

        [HttpGet]
        public async Task<IActionResult> ExternalLoginCallback(string returnUrl = null, string remoteError = null)
        {
            returnUrl = returnUrl ?? Url.Content("~/");
            if (remoteError != null)
            {
                ModelState.AddModelError(string.Empty, $"Error from provider: {remoteError}");
                return View("Login");
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                _logger.LogError("ExternalLoginCallback: Info is NULL. RemoteError: {Error}", remoteError);
                ModelState.AddModelError(string.Empty, "Error loading login info from Google.");
                return View("Login");
            }

            // Sign in the user with this external login provider if the user already has a login.
            var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
            if (result.Succeeded)
            {
                _logger.LogInformation("User logged in with {Provider} provider.", info.LoginProvider);
                
                // Find user to redirect by Role
                var user = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
                if (user != null)
                {
                    return await RedirectToRoleAsync(user, returnUrl);
                }
                
                return LocalRedirect(returnUrl);
            }

            if (result.IsLockedOut)
            {
                return RedirectToAction("Lockout");
            }
            else
            {
                // If the user does not have an account, then ask the user to create an account.
                var email = info.Principal.FindFirstValue(ClaimTypes.Email);
                var name = info.Principal.FindFirstValue(ClaimTypes.Name) ?? "Google User";
                
                if (email != null)
                {
                    var user = await _userManager.FindByEmailAsync(email);
                    if (user == null)
                    {
                        user = new ApplicationUser
                        {
                            UserName = email,
                            Email = email,
                            FullName = name,
                            CreatedDate = DateTime.Now,
                            EmailConfirmed = true // Google emails are verified
                        };

                        var createResult = await _userManager.CreateAsync(user);
                        if (createResult.Succeeded)
                        {
                            // Assign Customer role
                            try 
                            { 
                                if (!await _roleManager.RoleExistsAsync("Customer")) await _roleManager.CreateAsync(new IdentityRole("Customer"));
                                await _userManager.AddToRoleAsync(user, "Customer");
                            } 
                            catch (Exception ex) 
                            {
                                _logger.LogError(ex, "Error assigning Customer role to new Google user.");
                            }

                            createResult = await _userManager.AddLoginAsync(user, info);
                            if (createResult.Succeeded)
                            {
                                await _signInManager.SignInAsync(user, isPersistent: false);
                                return await RedirectToRoleAsync(user, returnUrl);
                            }
                        }
                        foreach (var error in createResult.Errors)
                        {
                            ModelState.AddModelError(string.Empty, error.Description);
                        }
                    }
                    else
                    {
                        // User exists, link account
                        var linkResult = await _userManager.AddLoginAsync(user, info);
                        
                        // Ensure role exists
                        try 
                        {
                             if (!await _roleManager.RoleExistsAsync("Customer")) await _roleManager.CreateAsync(new IdentityRole("Customer"));
                             if (!await _userManager.IsInRoleAsync(user, "Customer"))
                             {
                                 await _userManager.AddToRoleAsync(user, "Customer");
                             }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error checking/assigning role for existing user linking Google.");
                        }

                        if (linkResult.Succeeded)
                        {
                            await _signInManager.SignInAsync(user, isPersistent: false);
                            return await RedirectToRoleAsync(user, returnUrl);
                        }
                        else 
                        {
                             // Link failed (maybe already linked?) - Try checking if we can just sign in
                             // or show error
                             foreach(var err in linkResult.Errors)
                                ModelState.AddModelError("", err.Description);
                        }
                    }
                }
                else
                {
                     ModelState.AddModelError("", "Email not found from Google account.");
                }

                ViewData["ReturnUrl"] = returnUrl;
                return View("Login");
            }
        }

        // --- FORGOT PASSWORD & RECOVERY ---

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
                {
                    // Don't reveal that the user does not exist or is not confirmed
                    // But for this specific flow "System provided password", we kinda have to imply something worked.
                    // Let's just say "check email".
                    return RedirectToAction("ForgotPasswordConfirmation");
                }

                // 1. Generate Random Readable Password
                 string tempPassword = GenerateRandomPassword(8);

                // 2. Force Reset Password
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var result = await _userManager.ResetPasswordAsync(user, token, tempPassword);

                if (result.Succeeded)
                {
                    // 3. Send Email
                    string subject = "Password Recovery - Lotus Spa";
                    string message = $@"
                        <h3>Hello {user.FullName},</h3>
                        <p>You (or someone else) requested a password recovery.</p>
                        <p>Your temporary password is: <strong style='font-size: 20px; color: #ec4899;'>{tempPassword}</strong></p>
                        <p>Please use this password to login and change to a new password.</p>
                        <p><a href='{Url.Action("LoginWithRecovery", "Account", new { email = user.Email }, Request.Scheme)}'>Click here to change password now</a></p>
                        <p>Best regards,<br/>Lotus Spa Team</p>";

                    try
                    {
                        await _emailService.SendEmailAsync(user.Email, subject, message);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send recovery email");
                        ModelState.AddModelError("", "Cannot send email. Please try again later.");
                        return View(model);
                    }

                    return RedirectToAction("LoginWithRecovery", new { email = user.Email });
                }
                
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        [HttpGet]
        public IActionResult LoginWithRecovery(string email)
        {
            if (email == null) return RedirectToAction("ForgotPassword");
            var model = new RecoveryPasswordViewModel { Email = email };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LoginWithRecovery(RecoveryPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                ModelState.AddModelError("", "Account does not exist.");
                return View(model);
            }

            // 1. Verify Temp Password by trying to Change it
            // We can't just "CheckPassword" then "ChangePassword" because of race conditions or lockout policies, but it's safe enough here.
            // Actually, ChangePasswordAsync takes (user, current, new). This is exactly what we need.

            var result = await _userManager.ChangePasswordAsync(user, model.SystemPassword, model.NewPassword);
            
            if (result.Succeeded)
            {
                // 2. Sign In
                await _signInManager.SignInAsync(user, isPersistent: false);
                TempData["SuccessMessage"] = "Password changed successfully!";
                return await RedirectToRoleAsync(user, null);
            }
            else
            {
                foreach (var error in result.Errors)
                {
                    // Map "Incorrect password" to "Incorrect system provided password"
                    if (error.Code == "PasswordMismatch")
                        ModelState.AddModelError("SystemPassword", "Incorrect system provided password.");
                    else
                        ModelState.AddModelError("", error.Description);
                }
                return View(model);
            }
        }

        private string GenerateRandomPassword(int length)
        {
            const string chars = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz0123456789!@$";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }

    }
}