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

        // --- ĐĂNG NHẬP ---
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

                // Nếu là Manager, Receptionist hoặc Technician thì hiện trang chọn Role
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

                // Kiểm tra returnUrl hợp lệ
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

            // Validate chung cho tất cả: Chỉ cần Email và Password
            ModelState.Remove("StaffId");
            ModelState.Remove("StaffCode");
            ModelState.Remove("LoginType"); 

            if (string.IsNullOrEmpty(model.Email) || string.IsNullOrEmpty(model.Password))
            {
                ModelState.AddModelError("", "Vui lòng nhập Email và Mật khẩu.");
            }

            if (ModelState.IsValid)
            {
                // Tìm user bằng Email
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user != null)
                {
                    // Thử đăng nhập bằng Password
                    // Lưu ý: SignInManager dùng UserName để đăng nhập, nên phải truyền user.UserName
                    var result = await _signInManager.PasswordSignInAsync(user.UserName, model.Password, model.RememberMe, lockoutOnFailure: false);
                    
                    if (result.Succeeded)
                    {
                        return await RedirectToRoleAsync(user, returnUrl);
                    }
                }

                // Nếu failed (sai pass hoặc ko tìm thấy user)
                if (user != null)
                {
                     await CheckLoginAsync(model); // Hàm này sẽ add ModelState error nếu có vấn đề cụ thể
                     if(ModelState.IsValid) ModelState.AddModelError(string.Empty, "Đăng nhập thất bại. Sai mật khẩu.");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Tài khoản không tồn tại.");
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
                // Logic điều hướng employee cũ
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
                    ErrorMessage = "Tài khoản không tồn tại"
                };
            }

            if (!await _userManager.CheckPasswordAsync(user, model.Password))
            {
                return new LoginCheckResult
                {
                    IsValid = false,
                    ErrorMessage = "Sai mật khẩu"
                };
            }

            if (!await _userManager.IsEmailConfirmedAsync(user))
            {
                return new LoginCheckResult
                {
                    IsValid = false,
                    ErrorMessage = "Chưa xác nhận email"
                };
            }

            if (await _userManager.IsLockedOutAsync(user))
            {
                return new LoginCheckResult
                {
                    IsValid = false,
                    ErrorMessage = "Tài khoản bị khóa"
                };
            }

            // OK
            return new LoginCheckResult
            {
                IsValid = true
            };
        }


        // --- ĐĂNG KÝ ---
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
                // 1. Kiểm tra email tồn tại
                var existingUser = await _userManager.FindByEmailAsync(model.Email);
                if (existingUser != null)
                {
                    ModelState.AddModelError("Email", "Email này đã được sử dụng.");
                    return View(model);
                }

                var existingPhone = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == model.PhoneNumber);
                if(existingPhone != null)
                {
                    ModelState.AddModelError("PhoneNumber", "Số điện thoại này đã được đăng ký");
                    return View(model);
                }
                // 2. Tạo User (Chưa active)
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
                    // Tự động tạo role Customer nếu chưa có - Đảm bảo không bao giờ lỗi
                    if (!await _roleManager.RoleExistsAsync("Customer"))
                    {
                        await _roleManager.CreateAsync(new IdentityRole("Customer"));
                    }
                    await _userManager.AddToRoleAsync(user, "Customer");

                    // 3. Sinh mã xác thực (Token)
                    // Lưu ý: Token mặc định của Identity khá dài và chứa ký tự đặc biệt, không phù hợp để nhập tay 6 số.
                    // Để tạo mã 6 số, ta có thể dùng `GenerateTwoFactorTokenAsync` hoặc tự sinh số ngẫu nhiên rồi lưu vào AuthenticationToken.
                    // Ở đây tôi dùng cách đơn giản: Sinh số ngẫu nhiên và lưu vào User Token (hoặc Claim tạm).
                    
                    var token = new Random().Next(100000, 999999).ToString();
                    
                    // Lưu mã token này vào DB để đối chiếu sau (Dùng SetAuthenticationTokenAsync)
                    await _userManager.SetAuthenticationTokenAsync(user, "Default", "EmailConfirmation", token);

                    // 4. Gửi Email thật qua EmailService
                    string subject = "Mã xác thực đăng ký tài khoản - Lotus Spa";
                    string message = $@"
                        <h3>Chào {user.FullName},</h3>
                        <p>Cảm ơn bạn đã đăng ký tài khoản tại Lotus Spa.</p>
                        <p>Mã xác thực của bạn là: <strong style='font-size: 24px; color: #ec4899;'>{token}</strong></p>
                        <p>Vui lòng nhập mã này để kích hoạt tài khoản.</p>
                        <p>Trân trọng,<br/>Đội ngũ Lotus Spa</p>";

                    bool sentSuccess = false;
                    try 
                    {
                        await _emailService.SendEmailAsync(user.Email, subject, message);
                        sentSuccess = true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Lỗi gửi email: {ex.Message}");
                        
                        // QUAN TRỌNG: Xóa user nếu gửi mail thất bại để cho phép đăng ký lại
                        await _userManager.DeleteAsync(user);
                        
                        ModelState.AddModelError(string.Empty, "Không thể gửi email xác thực. Vui lòng kiểm tra lại địa chỉ email hoặc thử lại sau.");
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
            
            // Return View nhập mã (sẽ tạo ở bước tiếp theo)
            return View(); 
        }

        [HttpGet]
        public IActionResult ConfirmEmailCode(string email, string returnUrl = null)
        {
            if (string.IsNullOrEmpty(email)) return RedirectToAction("Register");
            ViewData["Email"] = email;
            ViewData["ReturnUrl"] = returnUrl;
            return View(); // Cần tạo View ConfirmEmailCode.cshtml
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmEmailCode(string email, string code, string returnUrl = null)
        {
            ViewData["Email"] = email;
            ViewData["ReturnUrl"] = returnUrl;
            returnUrl = returnUrl ?? Url.Content("~/");

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(code))
            {
                ModelState.AddModelError("", "Vui lòng nhập mã xác thực.");
                return View();
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null) return RedirectToAction("Register");

            // --- KIỂM TRA MÃ OTP TỪ DB ---
            // Lấy token đã lưu lúc đăng ký
            var savedToken = await _userManager.GetAuthenticationTokenAsync(user, "Default", "EmailConfirmation");
            
            bool isCodeValid = (savedToken == code);

            if (isCodeValid)
            {
                // 1. Kích hoạt user
                user.EmailConfirmed = true;
                await _userManager.UpdateAsync(user);
                
                // Xóa token sau khi dùng xong (Optional)
                await _userManager.RemoveAuthenticationTokenAsync(user, "Default", "EmailConfirmation");

                // 2. Đăng nhập ngay
                await _signInManager.SignInAsync(user, isPersistent: false);

                  TempData["SuccessMessage"] = "Xác thực tài khoản thành công! Chào mừng bạn đến với SpaBookingWeb";

                // 3. Chuyển hướng
                return await RedirectToRoleAsync(user, returnUrl);
            }
            else
            {
                ModelState.AddModelError("", "Mã xác thực không chính xác.");
                return View();
            }
        }
        // --- ĐĂNG XUẤT ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("HomeClient", "Home");
        }

        // --- ĐĂNG NHẬP GOOGLE ---
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
                ModelState.AddModelError(string.Empty, $"Lỗi từ nhà cung cấp: {remoteError}");
                return View("Login");
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                _logger.LogError("ExternalLoginCallback: Info is NULL. RemoteError: {Error}", remoteError);
                ModelState.AddModelError(string.Empty, "Lỗi tải thông tin đăng nhập từ Google.");
                return View("Login");
            }

            // Sign in the user with this external login provider if the user already has a login.
            var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
            if (result.Succeeded)
            {
                _logger.LogInformation("User logged in with {Provider} provider.", info.LoginProvider);
                
                // Tìm user để redirect theo Role
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
                            // Gán role Customer
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
                     ModelState.AddModelError("", "Không tìm thấy Email từ tài khoản Google.");
                }

                ViewData["ReturnUrl"] = returnUrl;
                return View("Login");
            }
        }

        // --- QUÊN MẬT KHẨU & KHÔI PHỤC ---

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
                    string subject = "Cấp lại mật khẩu - Lotus Spa";
                    string message = $@"
                        <h3>Chào {user.FullName},</h3>
                        <p>Bạn (hoặc ai đó) đã yêu cầu khôi phục mật khẩu.</p>
                        <p>Mật khẩu tạm thời của bạn là: <strong style='font-size: 20px; color: #ec4899;'>{tempPassword}</strong></p>
                        <p>Vui lòng sử dụng mật khẩu này để đăng nhập và đổi sang mật khẩu mới.</p>
                        <p><a href='{Url.Action("LoginWithRecovery", "Account", new { email = user.Email }, Request.Scheme)}'>Nhấn vào đây để đổi mật khẩu ngay</a></p>
                        <p>Trân trọng,<br/>Lotus Spa Team</p>";

                    try
                    {
                        await _emailService.SendEmailAsync(user.Email, subject, message);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send recovery email");
                        ModelState.AddModelError("", "Không thể gửi email. Vui lòng thử lại sau.");
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
                ModelState.AddModelError("", "Tài khoản không tồn tại.");
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
                TempData["SuccessMessage"] = "Đổi mật khẩu thành công!";
                return await RedirectToRoleAsync(user, null);
            }
            else
            {
                foreach (var error in result.Errors)
                {
                    // Map "Incorrect password" to "Mật khẩu hệ thống cấp không đúng"
                    if (error.Code == "PasswordMismatch")
                        ModelState.AddModelError("SystemPassword", "Mật khẩu hệ thống cấp không đúng.");
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