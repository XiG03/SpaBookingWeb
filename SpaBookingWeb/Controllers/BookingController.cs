using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SpaBookingWeb.Data;
using SpaBookingWeb.Services.Client;
using SpaBookingWeb.Services.Manager;
using SpaBookingWeb.ViewModels.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SpaBookingWeb.Models;
using SpaBookingWeb.Services;
using Microsoft.CodeAnalysis.Elfie.Serialization;
using Microsoft.EntityFrameworkCore;

namespace SpaBookingWeb.Controllers
{
    public class BookingController : Controller
    {
        private readonly IBookingService _bookingService;
        private readonly ISystemSettingService _systemSettingService;
        private readonly UserManager<ApplicationUser> _userManager; // [NEW] Declare UserManager

        private readonly MomoService _momoService;

        private readonly ILogger<BookingController> _logger;

        private readonly ApplicationDbContext _context;

        // [NEW] Inject UserManager into Constructor
        public BookingController(
            IBookingService bookingService,
            ISystemSettingService systemSettingService,
            UserManager<ApplicationUser> userManager,
            MomoService momoService,
            ILogger<BookingController> logger,
            ApplicationDbContext context)
        {
            _logger = logger;
            _bookingService = bookingService;
            _systemSettingService = systemSettingService;
            _userManager = userManager;
            _momoService = momoService;
            _context = context;

        }


        // --- STEP 1: CHOOSE BOOKING TYPE ---
        [HttpGet]
        public IActionResult Index()
        {
            try
            {
                // _bookingService.ClearSession();
                // Step 1 doesn't need complex model, pass null or empty object
                return View("Step1_Type");
            }
            catch (Exception ex)
            {
                return View("Error");
            }
        }

         [HttpGet]
        public IActionResult BookService(int id)
        {
            try 
            {
                var session = new BookingSessionModel
                {
                    IsGroupBooking = false,
                    Members = new List<BookingMember> 
                    { 
                        new BookingMember 
                        { 
                            MemberIndex = 1, 
                            Name = "Me",
                            SelectedServiceIds = new List<int> { id } 
                        } 
                    }
                };
                _bookingService.SaveSession(session);
                return RedirectToAction("Step2_Services");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when BookService id={Id}", id);
                return RedirectToAction("Index");
            }
        }

        // [NEW] Action to receive COMBO booking -> Get all child services -> Go straight to Step 2
         [HttpGet]
        public async Task<IActionResult> BookCombo(int id)
        {
            try 
            {
                // Check if Combo exists
                var combo = await _context.Combos.FirstOrDefaultAsync(c => c.ComboId == id && !c.IsDeleted);

                if (combo == null)
                {
                    TempData["ErrorMessage"] = "Combo does not exist.";
                    return RedirectToAction("Index", "Services");
                }

                // Instead of getting child services list, take Combo ID and negate it
                // Example: Combo ID 1 -> SelectedServiceId = -1
                var comboItemId = -id;

                var session = new BookingSessionModel
                {
                    IsGroupBooking = false,
                    Members = new List<BookingMember> 
                    { 
                        new BookingMember 
                        { 
                            MemberIndex = 1, 
                            Name = "Me",
                            SelectedServiceIds = new List<int> { comboItemId } // Save negative ID
                        } 
                    }
                };

                _bookingService.SaveSession(session);
                return RedirectToAction("Step2_Services");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when BookCombo id={Id}", id);
                return RedirectToAction("Index", "Services");
            }
        }

        [HttpPost]
        public IActionResult SetBookingType(string type)
        {
            try
            {
                var session = new BookingSessionModel
                {
                    IsGroupBooking = (type == "group"),
                    Members = new List<BookingMember> { new BookingMember { MemberIndex = 1, Name = "Me" } }
                };

                // [NEW] Check if any service is pre-selected from Services page
                if (TempData["PreSelectedServiceId"] is int serviceId)
                {
                    session.Members[0].SelectedServiceIds.Add(serviceId);
                    
                    // Keep TempData for next request (precaution) or to display message
                    TempData.Keep("PreSelectedServiceId"); 
                }

                _bookingService.SaveSession(session);
                return RedirectToAction("Step2_Services");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // --- STEP 2: SELECT SERVICES ---
        [HttpGet]
        public async Task<IActionResult> Step2_Services()
        {
            try
            {
                var session = _bookingService.GetSession();
                if (session == null) return RedirectToAction("Index");

                var data = await _bookingService.GetBookingPageDataAsync();

                // ENSURE CORRECT Step2ViewModel PASSED
                var model = new Step2ViewModel
                {
                    IsGroup = session.IsGroupBooking,
                    CurrentSession = session,
                    ServiceCategories = data.ServiceCategories
                };
                return View("Step2_Services", model);
            }
            catch (Exception ex)
            {
                return RedirectToAction("Index");
            }
        }
        [HttpPost]
        public IActionResult AddNewMember()
        {
            var session = _bookingService.GetSession();
            if (session == null) return RedirectToAction("Index");

            int newIndex = session.Members.Count + 1;
            session.Members.Add(new BookingMember
            {
                MemberIndex = newIndex,
                Name = $"Guest {newIndex}"
            });

            // If switching from personal -> group, update flag
            if (!session.IsGroupBooking) session.IsGroupBooking = true;

            _bookingService.SaveSession(session);
            return RedirectToAction("Step2_Services");
        }

        [HttpPost]
        public IActionResult RemoveMember(int index)
        {
            var session = _bookingService.GetSession();
            if (session == null) return RedirectToAction("Index");

            // Do not allow deleting member 1
            if (index > 1)
            {
                var member = session.Members.FirstOrDefault(m => m.MemberIndex == index);
                if (member != null) session.Members.Remove(member);

                // Reset index if needed, or keep as is
                if (session.Members.Count == 1) session.IsGroupBooking = false;

                _bookingService.SaveSession(session);
            }
            return RedirectToAction("Step2_Services");
        }

        [HttpPost]
        public IActionResult AddMemberService(int memberIndex, List<int> serviceIds)
        {
            try
            {
                var session = _bookingService.GetSession();
                if (session == null) return BadRequest("Session expired");

                var member = session.Members.FirstOrDefault(m => m.MemberIndex == memberIndex);

                if (member == null)
                {
                    member = new BookingMember { MemberIndex = memberIndex, Name = $"Guest {memberIndex}" };
                    session.Members.Add(member);
                }
                member.SelectedServiceIds = serviceIds;
                _bookingService.SaveSession(session);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest("Error updating service: " + ex.Message);
            }
        }

        [HttpPost]
        public IActionResult CompleteStep2()
        {
            return RedirectToAction("Step3_Staff");
        }

        // --- STEP 3: SELECT TECHNICIAN ---
         [HttpGet]
        public async Task<IActionResult> Step3_Staff()
        {
            try
            {
                var session = _bookingService.GetSession();
                if (session == null) return RedirectToAction("Index");

                var data = await _bookingService.GetBookingPageDataAsync();
                var allServices = data.ServiceCategories.SelectMany(c => c.Services).ToList();

                foreach (var member in session.Members)
                {
                    member.SelectedServices = new List<ServiceItemViewModel>();

                    foreach (var id in member.SelectedServiceIds)
                    {
                        var item = allServices.FirstOrDefault(s => s.Id == id);
                        if (item == null) continue;

                        // Add exactly that item (Combo or Odd Service) to list for View to display
                        member.SelectedServices.Add(item);

                        // IMPORTANT: Initialize key in Map for child services if Combo
                        // This helps View to bind Technician data for each child
                        if (item.Id < 0 && item.ChildServices != null)
                        {
                            foreach (var child in item.ChildServices)
                            {
                                if (!member.ServiceStaffMap.ContainsKey(child.Id))
                                {
                                    member.ServiceStaffMap[child.Id] = null;
                                }
                            }
                        }
                        // If odd service
                        else if (item.Id > 0)
                        {
                            if (!member.ServiceStaffMap.ContainsKey(item.Id))
                            {
                                member.ServiceStaffMap[item.Id] = null;
                            }
                        }
                    }
                }

                decimal total = 0;
                int duration = 0;
                foreach (var m in session.Members)
                {
                    // Calculate total amount based on original item (Combo uses Combo price, Service uses service price)
                    foreach (var s in m.SelectedServices)
                    {
                         total += s.Price;
                         duration += s.DurationMinutes;
                    }
                }

                var model = new Step3ViewModel
                {
                    CurrentSession = session,
                    Staffs = data.Staffs,
                    TotalAmount = total,
                    TotalDuration = duration
                };

                _bookingService.SaveSession(session);
                return View("Step3_Staff", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error Step3");
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public IActionResult SetStaff(int memberIndex, int serviceId, int? staffId)
        {
            try
            {
                var session = _bookingService.GetSession();
                if (session == null) return BadRequest("Session expired");

                var member = session.Members.FirstOrDefault(m => m.MemberIndex == memberIndex);
                if (member != null)
                {
                    // Update staff for specific service
                    if (member.ServiceStaffMap.ContainsKey(serviceId))
                    {
                        member.ServiceStaffMap[serviceId] = staffId;
                    }
                    else
                    {
                        member.ServiceStaffMap.Add(serviceId, staffId);
                    }
                    _bookingService.SaveSession(session);
                }
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        public IActionResult SetStaffAll(int memberIndex, int? staffId)
        {
            // Utility function if want to select 1 person for all (Extended option)
            try
            {
                var session = _bookingService.GetSession();
                if (session == null) return BadRequest("Session expired");

                var member = session.Members.FirstOrDefault(m => m.MemberIndex == memberIndex);
                if (member != null)
                {
                    // Update all services to this staff (or null)
                    var keys = member.ServiceStaffMap.Keys.ToList();
                    foreach (var key in keys) member.ServiceStaffMap[key] = staffId;

                    _bookingService.SaveSession(session);
                }
                return Ok();
            }
            catch { return BadRequest(); }
        }

        // --- STEP 4: SELECT TIME ---
        [HttpGet]
        public async Task<IActionResult> Step4_Time(DateTime? date)
        {
            try
            {
                var session = _bookingService.GetSession();
                if (session == null) return RedirectToAction("Index");

                // Default is selected date or today
                var selectedDate = date ?? session.SelectedDate ?? DateTime.Today;
                session.SelectedDate = selectedDate; // Temporarily save viewing date
                _bookingService.SaveSession(session);

                // 1. Get Slots by 15min logic
                var slots = await _bookingService.GetAvailableTimeSlotsAsync(selectedDate, session);

                // 2. Get Service & Staff data to display Sidebar
                var pageData = await _bookingService.GetBookingPageDataAsync();
                var allServices = pageData.ServiceCategories.SelectMany(c => c.Services).ToList();

                // 3. Recalculate details for View
                decimal totalAmount = 0;
                int totalDuration = 0;

                foreach (var member in session.Members)
                {
                    // Remap Service Detail to get name & price
                    member.SelectedServices = allServices
                        .Where(s => member.SelectedServiceIds.Contains(s.Id))
                        .ToList();

                    totalAmount += member.SelectedServices.Sum(s => s.Price);
                    totalDuration += member.SelectedServices.Sum(s => s.DurationMinutes);
                }

                // 4. Get opening hours (to display UI if needed)
                var settings = await _systemSettingService.GetCurrentSettingsAsync();

                var model = new Step4ViewModel
                {
                    CurrentSession = session,
                    AvailableTimeSlots = slots,
                    Staffs = pageData.Staffs, // Pass staff list to lookup name
                    TotalAmount = totalAmount,
                    TotalDuration = totalDuration,
                    OpenTimeStr = settings.OpenTime.ToString(@"hh\:mm"),
                    CloseTimeStr = settings.CloseTime.ToString(@"hh\:mm")
                };

                return View("Step4_Time", model);
            }
            catch (Exception ex)
            {
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public IActionResult SetTime(DateTime date, string time)
        {
            try
            {
                var session = _bookingService.GetSession();
                if (session == null) return BadRequest("Session expired");

                session.SelectedDate = date;
                if (TimeSpan.TryParse(time, out var ts)) session.SelectedTime = ts;

                _bookingService.SaveSession(session);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest("Error selecting time: " + ex.Message);
            }
        }

        // --- STEP 5: CONFIRM ---
        [HttpGet]
        public async Task<IActionResult> Step5_Confirm()
        {
            try
            {
                var session = _bookingService.GetSession();
                if (session == null) return RedirectToAction("Index");

                if (!User.Identity.IsAuthenticated)
                    return RedirectToAction("Login", "Account", new { returnUrl = "/Booking/Step5_Confirm" });

                // If old order (Resume), no need to recalculate too much to avoid discrepancies
                // However, to display full service info, still need to reload Staff/Service info from DB
                var data = await _bookingService.GetBookingPageDataAsync();
                var allSvcs = data.ServiceCategories.SelectMany(c => c.Services).ToList();

                foreach (var mem in session.Members)
                {
                    // Remap service detail to display name, price on UI
                    mem.SelectedServices = allSvcs.Where(s => mem.SelectedServiceIds.Contains(s.Id)).ToList();
                }

                // If new order, recalculate price. If old order, keep TotalAmount from Session (already loaded from DB)
                if (!session.ExistingAppointmentId.HasValue)
                {
                    decimal total = 0;
                    foreach (var mem in session.Members)
                        foreach (var s in mem.SelectedServices) total += s.Price;
                    
                    var settings = await _systemSettingService.GetCurrentSettingsAsync();
                    session.TotalAmount = total;
                    session.DepositPercentage = settings.DepositPercentage;
                    session.DepositAmount = total * settings.DepositPercentage / 100;
                }

                // Load authenticated user info to prefill form
                // [FIX] Check if CustomerInfo is null OR empty (because BookingSessionModel initializes it by default)
                if (session.CustomerInfo == null || string.IsNullOrWhiteSpace(session.CustomerInfo.FullName))
                {
                    var user = await _userManager.GetUserAsync(User);
                    if (user != null)
                    {
                        if (session.CustomerInfo == null) session.CustomerInfo = new CustomerInfo();
                        
                        // Populate/Overwrite with User Profile data
                        session.CustomerInfo.FullName = user.FullName;
                        session.CustomerInfo.Phone = user.PhoneNumber;
                        session.CustomerInfo.Email = user.Email;
                        
                        _logger.LogInformation($"[STEP5] Auto-filled CustomerInfo from Auth User: {user.FullName}");
                    }
                    else
                    {
                        _logger.LogWarning("[STEP5] User is authenticated but GetUserAsync returned null!");
                    }
                }
                else
                {
                    _logger.LogInformation($"[STEP5] CustomerInfo already exists: {session.CustomerInfo.FullName}");
                }

                _bookingService.SaveSession(session);

                return View("Step5_Confirm", new Step5ViewModel
                {
                    CurrentSession = session,
                    DepositAmount = session.DepositAmount,
                    DepositPercent = session.DepositPercentage,
                    Staffs = data.Staffs
                });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading confirmation page.";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public async Task<IActionResult> SubmitBooking(CustomerInfo info, string payment_method)
        {
            try
            {
                var session = _bookingService.GetSession();
                if (session == null) return RedirectToAction("Index");

                // Validate one last time
                if (!ModelState.IsValid)
                {
                    var data = await _bookingService.GetBookingPageDataAsync();
                    var settings = await _systemSettingService.GetCurrentSettingsAsync();
                    // Reload data for Step 5 View
                    return View("Step5_Confirm", new Step5ViewModel
                    {
                        CurrentSession = session,
                        DepositAmount = session.DepositAmount,
                        DepositPercent = settings.DepositPercentage,
                        Staffs = data.Staffs
                    });
                }

                session.CustomerInfo = info;
                _bookingService.SaveSession(session);

                // 1. SAVE BOOKING TO DB FIRST (Status Unpaid/Pending)
                // Must save first to get AppointmentId (OrderId) to send to MoMo
                var appointmentId = await _bookingService.SaveBookingAsync(session, payment_method);

                // 2. REDIRECT TO PAYMENT
                if (payment_method == "momo")
                {
                    var orderId = $"ORDER_{appointmentId}_{DateTime.Now.Ticks}";
                    // Get deposit amount (calculated in Step 5)
                    long amount = (long)session.DepositAmount;
                    string orderInfo = $"Appointment Deposit #{appointmentId}";

                    // Create Callback URL: When payment finishes, MoMo will call back here
                    var redirectUrl = Url.Action("PaymentCallback", "Booking", null, Request.Scheme);
                    var ipnUrl = "http://localhost:5329/Booking/PaymentCallback"; // This URL needs to be public (real host) to receive IPN

                    // Call Momo service to get payment URL
                    var payUrl = await _momoService.CreatePaymentAsync(orderId, amount, orderInfo, redirectUrl, ipnUrl);

                    if (string.IsNullOrWhiteSpace(payUrl))
                    {
                        _logger.LogError("MoMo returned NULL or empty payUrl. AppointmentId: {Id}", appointmentId);
                        return BadRequest("Cannot create MoMo payment link.");
                    }

                    // Delete booking session because saved to DB
                    // _bookingService.ClearSession();

                    // Redirect user to MoMo page
                    return Redirect(payUrl);
                }
                else
                {
                    // Pay later (At counter) -> Redirect straight to Success page
                    _bookingService.ClearSession();
                    return RedirectToAction("Step6_Success", new { id = appointmentId });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing booking");
                var msg = ex.Message;
                if (ex.InnerException != null) msg += " | " + ex.InnerException.Message;
                
                TempData["ErrorMessage"] = "Error: " + msg;
                return RedirectToAction("Step5_Confirm");
            }
        }

        // --- RECEIVE RESULT FROM MOMO (Momo calls this Action when done) ---
        [HttpGet]
        public async Task<IActionResult> PaymentCallback(string partnerCode, string accessKey, string requestId, long amount, string orderId, string orderInfo,
                                                        string orderType, long transId, string message, string localMessage, string responseTime, int errorCode,
                                                        string payType, string extraData, string signature)
        {
            // Momo returns parameters via QueryString
            // var collection = Request.Query;
            // string resultCode = collection["errorCode"]; // 0 = Success
            // string orderId = collection["orderId"]; // This is appointmentId we sent
            // string orderInfo = collection["orderInfo"];
            string transid = transId.ToString();


            if (errorCode == 0) // Transaction successful
            {
                if (TryParseAppointmentId(orderId, out int appId))
                {
                    // Update deposit status in DB
                    await _bookingService.UpdateDepositStatusAsync(appId, transid); // Send Email

                    _bookingService.ClearSession();

                    // Redirect to success page
                    return RedirectToAction("Step6_Success", new { id = appId });
                }
            }

            TempData["ErrorMessage"] = !string.IsNullOrEmpty(localMessage) 
                                        ? $"Payment failed: {localMessage}" 
                                        : "Transaction cancelled or failed. Please try again.";

            // Transaction failed or cancelled
            TempData["ErrorMessage"] = $"Payment failed";

            // Redirect to home or history (since order created but not deposited)
            return RedirectToAction("Step5_Confirm");
        }

        private bool TryParseAppointmentId(string orderId, out int appointmentId)
        {
            appointmentId = 0;

            if (string.IsNullOrWhiteSpace(orderId))
                return false;

            var parts = orderId.Split('_');

            // ORDER_{appointmentId}_{ticks}
            if (parts.Length < 3)
                return false;

            return int.TryParse(parts[1], out appointmentId);
        }

        // --- STEP 6: SUCCESS ---
        public async Task<IActionResult> Step6_Success(int id)
        {
            if (id <= 0) return RedirectToAction("Index");

            // Retrieve info from DB to display
            var model = await _bookingService.GetAppointmentSuccessInfoAsync(id);

            if (model == null) return RedirectToAction("Index");

            return View("Step6_Success", model);
        }

         [HttpPost]
        public async Task<IActionResult> CheckVoucher(string code)
        {
            var session = _bookingService.GetSession();
            if (session == null) return Json(new { isValid = false, message = "Session expired." });

            // 1. Validate Voucher
            var result = await _bookingService.ValidateVoucherAsync(code, session.TotalAmount);
            
            if (result.IsValid && result.Voucher != null)
            {
                // 2. Calculate discount (For display only)
                decimal discount = 0;
                if (result.Voucher.DiscountType == "Percent")
                {
                    discount = session.TotalAmount * result.Voucher.DiscountValue / 100;
                }
                else
                {
                    discount = result.Voucher.DiscountValue;
                }

                if (result.Voucher.MaxDiscountAmount.HasValue && discount > result.Voucher.MaxDiscountAmount.Value)
                {
                    discount = result.Voucher.MaxDiscountAmount.Value;
                }

                // 3. Calculate estimated remaining amount to pay at counter
                // UI Formula: Total - Deposit - Discount = Remaining (Show for guest)
                decimal remainingUI = session.TotalAmount - session.DepositAmount - discount;
                if (remainingUI < 0) remainingUI = 0;

                // NOTE: DO NOT CALL _bookingService.SaveSession(session) TO SAVE VOUCHER

                return Json(new { 
                    isValid = true, 
                    message = result.Message,
                    discountAmount = discount,
                    remainingAmount = remainingUI, // Amount displayed on UI (Discount deducted)
                    totalAmount = session.TotalAmount,
                    depositAmount = session.DepositAmount
                });
            }
            else
            {
                // Recalculate default remaining (Total - Deposit)
                decimal remainingDefault = session.TotalAmount - session.DepositAmount;

                return Json(new { 
                    isValid = false, 
                    message = result.Message,
                    discountAmount = 0,
                    remainingAmount = remainingDefault,
                    totalAmount = session.TotalAmount,
                    depositAmount = session.DepositAmount
                });
            }
        }

        // DTO Class to receive data from Client
        public class VoucherCheckRequest
        {
            public string Code { get; set; }
        }
    }
}