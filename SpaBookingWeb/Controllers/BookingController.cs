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
        private readonly UserManager<ApplicationUser> _userManager; // [MỚI] Khai báo UserManager

        private readonly MomoService _momoService;

        private readonly ILogger<BookingController> _logger;

        private readonly ApplicationDbContext _context;

        // [MỚI] Inject UserManager vào Constructor
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
                            Name = "Tôi",
                            SelectedServiceIds = new List<int> { id } 
                        } 
                    }
                };
                _bookingService.SaveSession(session);
                return RedirectToAction("Step2_Services");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi BookService id={Id}", id);
                return RedirectToAction("Index");
            }
        }

        // [MỚI] Action nhận booking COMBO -> Lấy hết service con -> Đi thẳng Step 2
         [HttpGet]
        public async Task<IActionResult> BookCombo(int id)
        {
            try 
            {
                // Kiểm tra Combo tồn tại
                var combo = await _context.Combos.FirstOrDefaultAsync(c => c.ComboId == id && !c.IsDeleted);

                if (combo == null)
                {
                    TempData["ErrorMessage"] = "Combo không tồn tại.";
                    return RedirectToAction("Index", "Services");
                }

                // Thay vì lấy list service con, ta lấy chính ID combo và đổi dấu thành âm
                // Ví dụ: Combo ID 1 -> SelectedServiceId = -1
                var comboItemId = -id;

                var session = new BookingSessionModel
                {
                    IsGroupBooking = false,
                    Members = new List<BookingMember> 
                    { 
                        new BookingMember 
                        { 
                            MemberIndex = 1, 
                            Name = "Tôi",
                            SelectedServiceIds = new List<int> { comboItemId } // Lưu ID âm
                        } 
                    }
                };

                _bookingService.SaveSession(session);
                return RedirectToAction("Step2_Services");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi BookCombo id={Id}", id);
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
                    Members = new List<BookingMember> { new BookingMember { MemberIndex = 1, Name = "Tôi" } }
                };

                // [MỚI] Kiểm tra xem có dịch vụ nào được chọn trước từ trang Services không
                if (TempData["PreSelectedServiceId"] is int serviceId)
                {
                    session.Members[0].SelectedServiceIds.Add(serviceId);
                    
                    // Giữ lại TempData cho request tiếp theo (đề phòng) hoặc để hiển thị thông báo
                    TempData.Keep("PreSelectedServiceId"); 
                }

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

                var data = await _bookingService.GetBookingPageDataAsync();
                var allServices = data.ServiceCategories.SelectMany(c => c.Services).ToList();

                foreach (var member in session.Members)
                {
                    member.SelectedServices = new List<ServiceItemViewModel>();

                    foreach (var id in member.SelectedServiceIds)
                    {
                        var item = allServices.FirstOrDefault(s => s.Id == id);
                        if (item == null) continue;

                        // Add chính item đó (Combo hoặc Dịch vụ lẻ) vào list để View hiển thị
                        member.SelectedServices.Add(item);

                        // QUAN TRỌNG: Khởi tạo key trong Map cho các dịch vụ con nếu là Combo
                        // Điều này giúp View có thể binding dữ liệu KTV cho từng child
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
                        // Nếu là dịch vụ lẻ
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
                    // Tính tổng tiền dựa trên item gốc (Combo tính giá Combo, Dịch vụ tính giá dịch vụ)
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
                _logger.LogError(ex, "Lỗi Step3");
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

                if (!User.Identity.IsAuthenticated)
                    return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Step5_Confirm", "Booking") });

                // Nếu là đơn hàng cũ (Resume), không cần tính toán lại quá nhiều để tránh sai lệch
                // Tuy nhiên, để hiển thị đầy đủ thông tin dịch vụ, vẫn cần load lại Staff/Service info từ DB
                var data = await _bookingService.GetBookingPageDataAsync();
                var allSvcs = data.ServiceCategories.SelectMany(c => c.Services).ToList();

                foreach (var mem in session.Members)
                {
                    // Map lại chi tiết dịch vụ để hiển thị tên, giá trên UI
                    mem.SelectedServices = allSvcs.Where(s => mem.SelectedServiceIds.Contains(s.Id)).ToList();
                }

                // Nếu là đơn mới, tính lại tiền. Nếu đơn cũ, giữ nguyên TotalAmount từ Session (đã load từ DB)
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
                TempData["ErrorMessage"] = "Có lỗi khi tải trang xác nhận.";
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
                    // _bookingService.ClearSession();

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
                    await _bookingService.UpdateDepositStatusAsync(appId, transid); // Send Email

                    _bookingService.ClearSession();

                    // Chuyển đến trang thành công
                    return RedirectToAction("Step6_Success", new { id = appId });
                }
            }

            TempData["ErrorMessage"] = !string.IsNullOrEmpty(localMessage) 
                                        ? $"Thanh toán thất bại: {localMessage}" 
                                        : "Giao dịch bị hủy hoặc thất bại. Vui lòng thử lại.";

            // Giao dịch thất bại hoặc bị hủy
            TempData["ErrorMessage"] = $"Thanh toán thất bại";

            // Redirect về trang chủ hoặc trang quản lý lịch sử (vì đơn hàng đã tạo rồi nhưng chưa cọc)
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

        // --- STEP 6: THÀNH CÔNG ---
        public async Task<IActionResult> Step6_Success(int id)
        {
            if (id <= 0) return RedirectToAction("Index");

            // Lấy lại thông tin từ DB để hiển thị
            var model = await _bookingService.GetAppointmentSuccessInfoAsync(id);

            if (model == null) return RedirectToAction("Index");

            return View("Step6_Success", model);
        }

         [HttpPost]
        public async Task<IActionResult> CheckVoucher(string code)
        {
            var session = _bookingService.GetSession();
            if (session == null) return Json(new { isValid = false, message = "Phiên làm việc hết hạn." });

            // 1. Validate Voucher
            var result = await _bookingService.ValidateVoucherAsync(code, session.TotalAmount);
            
            if (result.IsValid && result.Voucher != null)
            {
                // 2. Tính toán số tiền giảm (Chỉ để hiển thị)
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

                // 3. Tính toán số tiền ước tính còn lại phải trả tại quầy
                // Công thức UI: Tổng - Cọc - Giảm giá = Còn lại (Hiển thị cho khách vui)
                decimal remainingUI = session.TotalAmount - session.DepositAmount - discount;
                if (remainingUI < 0) remainingUI = 0;

                // LƯU Ý: KHÔNG GỌI _bookingService.SaveSession(session) ĐỂ LƯU VOUCHER

                return Json(new { 
                    isValid = true, 
                    message = result.Message,
                    discountAmount = discount,
                    remainingAmount = remainingUI, // Số tiền hiển thị trên UI (Đã trừ voucher)
                    totalAmount = session.TotalAmount,
                    depositAmount = session.DepositAmount
                });
            }
            else
            {
                // Tính lại remaining mặc định (Tổng - Cọc)
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

        // Class DTO nhận dữ liệu từ Client
        public class VoucherCheckRequest
        {
            public string Code { get; set; }
        }
    }
}