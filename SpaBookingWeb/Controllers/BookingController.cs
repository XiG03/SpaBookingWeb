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

namespace SpaBookingWeb.Controllers
{
    public class BookingController : Controller
    {
        private readonly IBookingService _bookingService;
        private readonly ISystemSettingService _systemSettingService;
        private readonly UserManager<ApplicationUser> _userManager; // [MỚI] Khai báo UserManager

        private readonly MomoService _momoService;

        private readonly ILogger<BookingController> _logger;

        // [MỚI] Inject UserManager vào Constructor
        public BookingController(
            IBookingService bookingService,
            ISystemSettingService systemSettingService,
            UserManager<ApplicationUser> userManager,
            MomoService momoService,
            ILogger<BookingController> logger)
        {
            _logger = logger;
            _bookingService = bookingService;
            _systemSettingService = systemSettingService;
            _userManager = userManager;
            _momoService = momoService;

        }


        // --- STEP 1: CHỌN LOẠI LỊCH ---
        [HttpGet]
        public IActionResult Index()
        {
            try
            {
                // _bookingService.ClearSession();
                // Step 1 không cần model phức tạp, truyền null hoặc object rỗng
                return View("Step1_Type");
            }
            catch (Exception ex)
            {
                return View("Error");
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
                    Members = new List<BookingMember> { new BookingMember { MemberIndex = 1, Name = "Tôi" } }
                };
                _bookingService.SaveSession(session);
                return RedirectToAction("Step2_Services");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Có lỗi xảy ra: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // --- STEP 2: CHỌN DỊCH VỤ ---
        [HttpGet]
        public async Task<IActionResult> Step2_Services()
        {
            try
            {
                var session = _bookingService.GetSession();
                if (session == null) return RedirectToAction("Index");

                var data = await _bookingService.GetBookingPageDataAsync();

                // ĐẢM BẢO TRUYỀN ĐÚNG Step2ViewModel
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
                Name = $"Khách {newIndex}"
            });

            // Nếu chuyển từ cá nhân -> nhóm, cập nhật cờ
            if (!session.IsGroupBooking) session.IsGroupBooking = true;

            _bookingService.SaveSession(session);
            return RedirectToAction("Step2_Services");
        }

        [HttpPost]
        public IActionResult RemoveMember(int index)
        {
            var session = _bookingService.GetSession();
            if (session == null) return RedirectToAction("Index");

            // Không cho xóa thành viên số 1
            if (index > 1)
            {
                var member = session.Members.FirstOrDefault(m => m.MemberIndex == index);
                if (member != null) session.Members.Remove(member);

                // Reset lại index cho đẹp nếu cần, hoặc giữ nguyên
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
                    member = new BookingMember { MemberIndex = memberIndex, Name = $"Khách {memberIndex}" };
                    session.Members.Add(member);
                }
                member.SelectedServiceIds = serviceIds;
                _bookingService.SaveSession(session);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest("Lỗi cập nhật dịch vụ: " + ex.Message);
            }
        }

        [HttpPost]
        public IActionResult CompleteStep2()
        {
            return RedirectToAction("Step3_Staff");
        }

        // --- STEP 3: CHỌN KTV ---
        [HttpGet]
        public async Task<IActionResult> Step3_Staff()
        {
            try
            {
                var session = _bookingService.GetSession();
                if (session == null) return RedirectToAction("Index");

                // 1. Lấy tất cả dữ liệu cần thiết
                var data = await _bookingService.GetBookingPageDataAsync();
                var allServices = data.ServiceCategories.SelectMany(c => c.Services).ToList();

                // 2. Populate dữ liệu chi tiết cho từng thành viên (để hiển thị tên dịch vụ)
                foreach (var member in session.Members)
                {
                    member.SelectedServices = allServices
                        .Where(s => member.SelectedServiceIds.Contains(s.Id))
                        .ToList();

                    // Khởi tạo map nếu chưa có
                    foreach (var service in member.SelectedServices)
                    {
                        if (!member.ServiceStaffMap.ContainsKey(service.Id))
                        {
                            member.ServiceStaffMap[service.Id] = null; // Mặc định là Random
                        }
                    }
                }

                // 3. Tính toán sơ bộ cho Sidebar
                decimal total = 0;
                int duration = 0;
                foreach (var m in session.Members)
                {
                    total += m.SelectedServices.Sum(s => s.Price);
                    duration += m.SelectedServices.Sum(s => s.DurationMinutes);
                }

                var model = new Step3ViewModel
                {
                    CurrentSession = session,
                    Staffs = data.Staffs,
                    TotalAmount = total,
                    TotalDuration = duration
                };

                // Cập nhật lại session với các thông tin đã populate (nếu cần dùng sau)
                _bookingService.SaveSession(session);

                return View("Step3_Staff", model);
            }
            catch (Exception ex)
            {
                // Log error
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
                    // Cập nhật nhân viên cho dịch vụ cụ thể
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
            // Hàm tiện ích nếu muốn chọn 1 người cho tất cả (Option mở rộng)
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

        // --- STEP 4: CHỌN GIỜ ---
        [HttpGet]
        public async Task<IActionResult> Step4_Time(DateTime? date)
        {
            try
            {
                var session = _bookingService.GetSession();
                if (session == null) return RedirectToAction("Index");

                // Mặc định là ngày đã chọn hoặc hôm nay
                var selectedDate = date ?? session.SelectedDate ?? DateTime.Today;
                session.SelectedDate = selectedDate; // Tạm lưu ngày đang xem
                _bookingService.SaveSession(session);

                // 1. Lấy Slots theo logic 15p
                var slots = await _bookingService.GetAvailableTimeSlotsAsync(selectedDate, session);

                // 2. Lấy dữ liệu Service & Staff để hiển thị Sidebar
                var pageData = await _bookingService.GetBookingPageDataAsync();
                var allServices = pageData.ServiceCategories.SelectMany(c => c.Services).ToList();

                // 3. Tính toán lại chi tiết cho View
                decimal totalAmount = 0;
                int totalDuration = 0;

                foreach (var member in session.Members)
                {
                    // Map lại Service Detail để lấy tên & giá
                    member.SelectedServices = allServices
                        .Where(s => member.SelectedServiceIds.Contains(s.Id))
                        .ToList();

                    totalAmount += member.SelectedServices.Sum(s => s.Price);
                    totalDuration += member.SelectedServices.Sum(s => s.DurationMinutes);
                }

                // 4. Lấy giờ mở cửa (để hiển thị UI nếu cần)
                var settings = await _systemSettingService.GetCurrentSettingsAsync();

                var model = new Step4ViewModel
                {
                    CurrentSession = session,
                    AvailableTimeSlots = slots,
                    Staffs = pageData.Staffs, // Truyền list nhân viên sang để tra cứu tên
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
                return BadRequest("Lỗi chọn giờ: " + ex.Message);
            }
        }

        // --- STEP 5: XÁC NHẬN ---
        [HttpGet]
        public async Task<IActionResult> Step5_Confirm()
        {
            try
            {
                var session = _bookingService.GetSession();
                if (session == null) return RedirectToAction("Index");

                // [LOGIC MỚI] Kiểm tra đăng nhập
                if (!User.Identity.IsAuthenticated)
                {
                    return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Step5_Confirm", "Booking") });
                }

                // [LOGIC MỚI] Auto-fill thông tin nếu chưa có
                if (string.IsNullOrEmpty(session.CustomerInfo.FullName))
                {
                    var user = await _userManager.GetUserAsync(User);
                    if (user != null)
                    {
                        session.CustomerInfo.FullName = user.FullName ?? "";
                        session.CustomerInfo.Phone = user.PhoneNumber ?? "";
                        session.CustomerInfo.Email = user.Email ?? "";
                    }
                }

                // Tính tổng tiền lại từ DB để an toàn
                decimal total = 0;
                var data = await _bookingService.GetBookingPageDataAsync();
                var allSvcs = data.ServiceCategories.SelectMany(c => c.Services).ToList();

                foreach (var mem in session.Members)
                {
                    // Update lại chi tiết dịch vụ để có tên hiển thị
                    mem.SelectedServices = allSvcs.Where(s => mem.SelectedServiceIds.Contains(s.Id)).ToList();

                    foreach (var s in mem.SelectedServices)
                    {
                        total += s.Price;
                    }
                }

                var settings = await _systemSettingService.GetCurrentSettingsAsync();
                session.TotalAmount = total;
                session.DepositPercentage = settings.DepositPercentage;
                session.DepositAmount = total * settings.DepositPercentage / 100;
                _bookingService.SaveSession(session);

                return View("Step5_Confirm", new Step5ViewModel
                {
                    CurrentSession = session,
                    DepositAmount = session.DepositAmount,
                    DepositPercent = session.DepositPercentage,
                    Staffs = data.Staffs // [MỚI] Truyền data nhân viên sang View
                });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Có lỗi khi tải trang xác nhận. Vui lòng thử lại.";
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

                // Validate lại lần cuối
                if (!ModelState.IsValid)
                {
                    var data = await _bookingService.GetBookingPageDataAsync();
                    var settings = await _systemSettingService.GetCurrentSettingsAsync();
                    // Load lại data cho View Step 5
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

                // 1. LƯU BOOKING VÀO DB TRƯỚC (Trạng thái Unpaid/Pending)
                // Phải lưu trước để có AppointmentId (OrderId) gửi sang MoMo
                var appointmentId = await _bookingService.SaveBookingAsync(session);

                // 2. ĐIỀU HƯỚNG THANH TOÁN
                if (payment_method == "momo")
                {
                    var orderId = $"ORDER_{appointmentId}_{DateTime.Now.Ticks}";
                    // Lấy số tiền cọc (đã tính ở Step 5)
                    long amount = (long)session.DepositAmount;
                    string orderInfo = $"Dat coc lich hen #{appointmentId} tai SpaBookingWeb";

                    // Tạo URL Callback: Khi thanh toán xong MoMo sẽ gọi về đây
                    var redirectUrl = Url.Action("PaymentCallback", "Booking", null, Request.Scheme);
                    var ipnUrl = "http://localhost:5329/Booking/PaymentCallback"; // URL này cần public (host thật) mới nhận được IPN

                    // Gọi service Momo để lấy URL thanh toán
                    var payUrl = await _momoService.CreatePaymentAsync(orderId, amount, orderInfo, redirectUrl, ipnUrl);

                    if (string.IsNullOrWhiteSpace(payUrl))
                    {
                        _logger.LogError("MoMo trả về payUrl NULL hoặc rỗng. AppointmentId: {Id}", appointmentId);
                        return BadRequest("Không tạo được link thanh toán MoMo.");
                    }

                    // Xóa session booking vì đã lưu vào DB
                    _bookingService.ClearSession();

                    // Chuyển hướng người dùng sang trang MoMo
                    return Redirect(payUrl);
                }
                else
                {
                    // Thanh toán sau (Tại quầy) -> Chuyển thẳng tới trang thành công
                    _bookingService.ClearSession();
                    return RedirectToAction("Step6_Success", new { id = appointmentId });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dữ liệu truyền sang MoMo không hợp lệ");
                TempData["ErrorMessage"] = "Lỗi khi xử lý: " + ex.Message;
                return RedirectToAction("Step5_Confirm");
            }
        }

        // --- NHẬN KẾT QUẢ TỪ MOMO (Action này Momo sẽ gọi khi xong) ---
        [HttpGet]
        public async Task<IActionResult> PaymentCallback(string partnerCode, string accessKey, string requestId, long amount, string orderId, string orderInfo,
                                                        string orderType, long transId, string message, string localMessage, string responseTime, int errorCode,
                                                        string payType, string extraData, string signature)
        {
            // Momo trả về các tham số qua QueryString
            // var collection = Request.Query;
            // string resultCode = collection["errorCode"]; // 0 = Thành công
            // string orderId = collection["orderId"]; // Chính là appointmentId mình gửi đi
            // string orderInfo = collection["orderInfo"];
            string transid = transId.ToString();


            if (errorCode == 0) // Giao dịch thành công
            {
                if (TryParseAppointmentId(orderId, out int appId))
                {
                    // Cập nhật trạng thái đã thanh toán cọc trong DB
                    await _bookingService.UpdateDepositStatusAsync(appId, transid);

                    // Chuyển đến trang thành công
                    return RedirectToAction("Step6_Success", new { id = appId });
                }
            }

            // Giao dịch thất bại hoặc bị hủy
            TempData["ErrorMessage"] = $"Thanh toán thất bại";

            // Redirect về trang chủ hoặc trang quản lý lịch sử (vì đơn hàng đã tạo rồi nhưng chưa cọc)
            return RedirectToAction("Index");
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

        // --- STEP 6: THÀNH CÔNG ---
        public async Task<IActionResult> Step6_Success(int id)
        {
            if (id <= 0) return RedirectToAction("Index");

            // Lấy lại thông tin từ DB để hiển thị
            var model = await _bookingService.GetAppointmentSuccessInfoAsync(id);

            if (model == null) return RedirectToAction("Index");

            return View("Step6_Success", model);
        }
    }
}