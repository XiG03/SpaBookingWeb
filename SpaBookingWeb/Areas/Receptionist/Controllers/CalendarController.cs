using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpaBookingWeb.Areas.Receptionist.Filters;
using SpaBookingWeb.Data;
using SpaBookingWeb.Models;
using SpaBookingWeb.Services;
using SpaBookingWeb.Services.Receptionist;
using SpaBookingWeb.ViewModels.Receptionist;

namespace SpaBookingWeb.Areas.Receptionist.Controllers
{
    [Area("Receptionist")]
    [Authorize(Roles = "Receptionist,Admin,Manager")] // [THAY ĐỔI IDENTITY] Thêm Authorize
    // [THÊM DÒNG NÀY] Chặn toàn bộ hành động trong Controller này nếu chưa Check-in
    [ServiceFilter(typeof(RequireReceptionistAttendanceFilter))]
    public class CalendarController : Controller
    {
        private readonly IReceptionistService _receptionistService;
        private readonly ApplicationDbContext _context;
        private readonly MomoService _momoService; // Inject Momo Service
        private readonly UserManager<ApplicationUser> _userManager; // [THAY ĐỔI IDENTITY]

        public CalendarController(IReceptionistService receptionistService, ApplicationDbContext context, MomoService momoService, UserManager<ApplicationUser> userManager)
        {
            _receptionistService = receptionistService;
            _context = context;
            _momoService = momoService;
            _userManager = userManager; // [THAY ĐỔI IDENTITY]
        }

        [HttpGet]
        public async Task<IActionResult> Index(DateTime? date)
        {
            var selectedDate = date ?? DateTime.Today;
            var viewModel = await _receptionistService.GetDashboardDataAsync(selectedDate);
            return View(viewModel);
        }

        // API TÌM KHÁCH HÀNG (Đây là hàm bạn đang thiếu!)
        [HttpGet]
        public async Task<IActionResult> GetCustomerByPhone(string phone)
        {
            if (string.IsNullOrEmpty(phone))
            {
                return Json(new { success = false, message = "Please enter your phone number." });
            }

            var customer = await _context.Customers
                .Include(c => c.MembershipType)
                .FirstOrDefaultAsync(c => c.PhoneNumber == phone && !c.IsDeleted);

            if (customer != null)
            {
                return Json(new
                {
                    success = true,
                    isNew = false,
                    data = new
                    {
                        fullName = customer.FullName,
                        email = customer.Email,
                        membership = customer.MembershipType?.TypeName ?? "Regular members",
                        discount = customer.MembershipType?.DiscountPercent ?? 0
                    }
                });
            }
            else
            {
                return Json(new { success = true, isNew = true });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SaveBooking([FromBody] BookingSubmissionVM model)
        {
            using var transaction = _context.Database.BeginTransaction();
            try
            {
                // [THAY ĐỔI IDENTITY] 1. Tìm ID nhân viên của Lễ tân đang đăng nhập
                var currentUser = await _userManager.GetUserAsync(User);
                // Giả định bảng Employees có cột ApplicationUserId hoặc Email liên kết
                // Ở đây tìm Employee dựa trên ApplicationUserId
                var receptionistEmployee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.IdentityUserId == currentUser.Id);

                // Nếu không tìm thấy (lỗi cấu hình), có thể gán null hoặc throw error.
                // Để an toàn, ta gán ID null hoặc ID admin mặc định nếu cần, nhưng tốt nhất là báo lỗi.
                if (receptionistEmployee == null)
                {
                    return BadRequest("No employee profile was found for the currently logged-in account.");
                }
                // [LOGIC MỚI] Xác định Trạng thái
                string initialStatus = model.IsWalkIn ? "InProgress" : "Confirmed";

                // A. TÍNH TOÁN THỜI GIAN TRƯỚC (Để kiểm tra trùng)
                // Nếu là Walk-in -> Lấy giờ hiện tại chính xác (DateTime.Now) làm chuẩn
                DateTime bookingStartDateTime = model.IsWalkIn
                    ? DateTime.Now
                    : model.BookingDate.Add(TimeSpan.Parse(model.StartTime));
                DateTime maxEndTime = bookingStartDateTime;

                // Giả lập tính toán EndTime dựa trên các dịch vụ khách chọn
                foreach (var guest in model.Guests)
                {
                    foreach (var item in guest.Items)
                    {
                        // Logic cộng giờ (như cũ)
                        DateTime itemStart = model.BookingDate.Add(TimeSpan.Parse(item.StartTime));
                        DateTime itemEnd = model.BookingDate.Add(TimeSpan.Parse(item.EndTime));
                        if (itemEnd > maxEndTime) maxEndTime = itemEnd;
                    }
                }

                // B. === [CHECK LOGIC MỚI] KIỂM TRA KHÁCH CÓ BẬN KHÔNG? ===
                // Tìm xem SĐT này đã có lịch nào (Confirmed/Pending) chen vào khung giờ mới không?
                // Công thức trùng: (StartA < EndB) && (StartB < EndA)
                var isCustomerBusy = await _context.Appointments
                    .Include(a => a.Customer)
                    .AnyAsync(a => a.Customer.PhoneNumber == model.CustomerPhone
                                   && a.Status != "Cancelled" // Bỏ qua đơn đã hủy
                                   && a.StartTime < maxEndTime
                                   && a.EndTime > bookingStartDateTime);

                if (isCustomerBusy)
                {
                    return BadRequest($"Customer ({model.CustomerPhone}) already has another appointment that coincides with this time slot. Please check again!");
                }
                // ==========================================================

                // 1. Xử lý Khách hàng
                var customer = await _context.Customers.FirstOrDefaultAsync(c => c.PhoneNumber == model.CustomerPhone);
                if (customer == null)
                {
                    customer = new Customer
                    {
                        FullName = model.CustomerName,
                        PhoneNumber = model.CustomerPhone,
                        Email = !string.IsNullOrEmpty(model.CustomerEmail) ? model.CustomerEmail : model.CustomerPhone + "@noemail.com",
                        MembershipTypeId = 1,
                        IsDeleted = false
                    };
                    _context.Customers.Add(customer);
                    await _context.SaveChangesAsync();
                }

                var appointment = new Appointment
                {
                    CustomerId = customer.CustomerId,
                    EmployeeId = receptionistEmployee.EmployeeId, // [THAY ĐỔI IDENTITY] Sử dụng ID lấy được từ Identity
                    StartTime = bookingStartDateTime,
                    EndTime = maxEndTime, // Đã tính ở trên
                    Status = initialStatus, // Hoặc Pending
                    Notes = model.Notes,
                    DepositAmount = model.IsWalkIn ? 0 : 0,
                    IsDepositPaid = model.IsWalkIn ? false : model.IsDepositPaid,
                    CreatedDate = DateTime.Now,
                    IsDeleted = false
                };
                _context.Appointments.Add(appointment);
                await _context.SaveChangesAsync();

                // Biến tính tổng tiền để xét luật cọc
                decimal totalBookingValue = 0;

                // 3. Tạo Details (Con)
                foreach (var guest in model.Guests)
                {
                    foreach (var item in guest.Items)
                    {
                        // Ghép ngày + giờ JS gửi
                        DateTime itemStart = model.BookingDate.Add(TimeSpan.Parse(item.StartTime));
                        DateTime itemEnd = model.BookingDate.Add(TimeSpan.Parse(item.EndTime));

                        if (itemEnd > maxEndTime) maxEndTime = itemEnd;

                        decimal finalPriceToSave = 0;

                        // === [LOGIC MỚI] KIỂM TRA NẾU LÀ COMBO ===
                        if (item.ComboId != null)
                        {
                            // 1. Lấy thông tin Combo để biết giá tổng (550k)
                            var combo = await _context.Combos
                                .Include(c => c.ComboDetails).ThenInclude(cd => cd.Service)
                                .FirstOrDefaultAsync(c => c.ComboId == item.ComboId);

                            if (combo != null)
                            {
                                // 2. Lấy giá gốc của dịch vụ hiện tại (để tính tỷ trọng)
                                var currentServiceOriginalPrice = await _context.Services
                                    .Where(s => s.ServiceId == item.ServiceId)
                                    .Select(s => s.Price)
                                    .FirstOrDefaultAsync();

                                // 3. Tính tổng giá gốc các món trong combo (để làm mẫu số)
                                // (Ví dụ: Gội 100k + Massage 500k = Tổng gốc 600k)
                                decimal totalOriginalPrice = combo.ComboDetails.Sum(cd => cd.Service.Price);

                                // 4. Tính giá chia sẻ theo tỷ lệ (Weighted Distribution)
                                // Giá lưu = Giá Combo * (Giá lẻ / Tổng giá lẻ)
                                // VD: Massage = 550k * (500k / 600k) = 458,333đ
                                if (totalOriginalPrice > 0)
                                {
                                    finalPriceToSave = combo.Price * (currentServiceOriginalPrice / totalOriginalPrice);
                                    // Làm tròn số tiền cho đẹp (không lẻ xu)
                                    finalPriceToSave = Math.Round(finalPriceToSave, 0);
                                }
                            }
                        }
                        else
                        {
                            // === LOGIC CŨ: DỊCH VỤ LẺ ===
                            finalPriceToSave = await _context.Services
                                .Where(s => s.ServiceId == item.ServiceId)
                                .Select(s => s.Price)
                                .FirstOrDefaultAsync();
                        }
                        // ==========================================

                        totalBookingValue += finalPriceToSave;

                        var detail = new AppointmentDetail
                        {
                            AppointmentId = appointment.AppointmentId,
                            ServiceId = item.ServiceId,
                            ComboId = item.ComboId,
                            TechnicianId = item.TechnicianId,
                            GuestName = guest.GuestName == model.CustomerName ? null : guest.GuestName, // Nếu trùng tên khách chính thì để null
                            PriceAtBooking = finalPriceToSave,
                            Status = "Pending",
                            IsDeleted = false
                        };
                        _context.AppointmentDetails.Add(detail);
                    }
                }

                // 4. === [LOGIC MỚI] TÍNH VÀ CẬP NHẬT TIỀN CỌC ===
                // Chỉ tính nếu khách ĐÃ TRẢ CỌC (checkbox = true)
                if (!model.IsWalkIn &&  model.IsDepositPaid)
                {
                    // Lấy luật cọc phù hợp nhất (Active, Giá trị đơn hàng >= Mức tối thiểu)
                    // Sắp xếp Priority giảm dần (hoặc MinOrderValue giảm dần) để lấy mức cao nhất thỏa mãn
                    var rule = await _context.DepositRules
                        .Where(r => r.IsActive && totalBookingValue >= r.MinOrderValue)
                        .OrderByDescending(r => r.MinOrderValue)
                        .FirstOrDefaultAsync();

                    if (rule != null)
                    {
                        decimal requiredDeposit = 0;
                        if (rule.DepositType == "Percent")
                        {
                            requiredDeposit = totalBookingValue * (rule.DepositValue / 100m);
                        }
                        else // Fixed
                        {
                            requiredDeposit = rule.DepositValue;
                        }

                        // Cập nhật vào appointment
                        appointment.DepositAmount = requiredDeposit;
                    }
                }
                // ================================================

                // Cập nhật lại EndTime tổng
                appointment.EndTime = maxEndTime;
                _context.Appointments.Update(appointment);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok("Success");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAppointmentDetails(int id)
        {
            var appt = await _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.Employee) // Lễ tân tạo
                .Include(a => a.AppointmentDetails)
                    .ThenInclude(ad => ad.Service)
                .Include(a => a.AppointmentDetails)
                    .ThenInclude(ad => ad.Technician) // Lấy tên KTV
                .Include(a => a.AppointmentDetails)
                    .ThenInclude(ad => ad.Combo)
                .FirstOrDefaultAsync(a => a.AppointmentId == id);

            if (appt == null) return NotFound();

            // Tính thời lượng (phút) để Frontend dùng cho việc lọc KTV khi dời lịch
            // Nếu EndTime null (ít khi xảy ra) thì mặc định 60p
            double durationMinutes = 60;
            if (appt.EndTime.HasValue)
            {
                durationMinutes = (appt.EndTime.Value - appt.StartTime).TotalMinutes;
            }

            DateTime start = appt.StartTime;
            DateTime end = appt.EndTime ?? start.AddMinutes(60);

            // Query tìm InvoiceId thủ công
            var paidInvoiceId = await _context.Invoices
                .Where(i => i.AppointmentId == id && i.PaymentStatus == "Paid")
                .Select(i => i.InvoiceId)
                .FirstOrDefaultAsync();

            // Map dữ liệu sang JSON gọn gàng
            var result = new
            {
                id = appt.AppointmentId,
                invoiceId = paidInvoiceId, // Gán giá trị vừa tìm được (int, mặc định 0 nếu ko thấy)
                customerName = appt.Customer?.FullName ?? "Walk-in customer",
                customerPhone = appt.Customer?.PhoneNumber ?? "",
                createdDate = appt.CreatedDate.ToString("dd/MM/yyyy HH:mm"),
                notes = appt.Notes, // <--- Lấy ghi chú
                duration = durationMinutes,
                totalAmount = appt.AppointmentDetails.Sum(ad => ad.PriceAtBooking), // Tính tổng tiền tạm tính
                status = appt.Status,
                startTime = appt.StartTime.ToString("yyyy-MM-dd HH:mm:ss"), // Lấy thời gian bắt đầu
                bookingDate = start.ToString("dd/MM/yyyy"), // VD: 10/01/2026
                timeRange = $"{start:HH:mm} - {end:HH:mm}", // VD: 14:00 - 15:30
                // Danh sách chi tiết các dịch vụ trong đơn này
                items = appt.AppointmentDetails.Select(ad => new
                {
                    detailId = ad.AppointmentDetailId, // <--- THÊM MỚI: ID chi tiết
                    guestName = ad.GuestName ?? appt.Customer?.FullName, // Nếu null thì là khách chính                                                 // === [THÊM 2 DÒNG NÀY] ===
                    serviceId = ad.ServiceId, // Để check Skill
                    comboId = ad.ComboId,
                    serviceName = ad.ComboId != null ? $"[Combo] {ad.Combo?.ComboName}" : ad.Service?.ServiceName,
                    price = ad.PriceAtBooking,
                    techId = ad.TechnicianId, // <--- THÊM MỚI: ID KTV hiện tại
                    techName = ad.Technician != null ? ad.Technician.FullName : "Not selected",
                    status = ad.Status,
                    // Lấy thời lượng để tính Availability (Mặc định 60p nếu null)
                    duration = ad.ComboId != null ? 90 : (ad.Service?.DurationMinutes ?? 60)
                }).ToList()
            };

            return Json(result);
        }

        // [BƯỚC 1] API Cập nhật trạng thái Lịch hẹn Tổng (Dành cho Lễ tân)
        [HttpPost]
        public async Task<IActionResult> UpdateAppointmentStatus(int id, string status)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null)
            {
                return Json(new { success = false, message = "No appointment found!" });
            }

            // Chỉ cho phép cập nhật các trạng thái hợp lệ
            if (status != "InProgress" && status != "Completed")
            {
                return Json(new { success = false, message = "Invalid status!" });
            }

            appointment.Status = status;

            // Nếu là Check-in -> Ghi nhận giờ bắt đầu thực tế
            if (status == "InProgress")
            {
                // Logic tùy chọn: Có thể lưu ActualStartTime vào DB nếu có cột đó
                // appointment.ActualStartTime = DateTime.Now; 
            }
            // Nếu là Check-out -> Ghi nhận giờ kết thúc
            else if (status == "Completed")
            {
                // appointment.ActualEndTime = DateTime.Now;
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Status update successful!" });
        }

        // 1. API Lấy thông tin Bill ban đầu (Load Modal)
        [HttpGet]
        public async Task<IActionResult> GetBillDetails(int id)
        {
            var appt = await _context.Appointments
                .Include(a => a.Customer).ThenInclude(c => c.MembershipType)
                .Include(a => a.AppointmentDetails).ThenInclude(ad => ad.Service)
                .Include(a => a.AppointmentDetails).ThenInclude(ad => ad.Technician)
                // Include thêm Invoice để lấy thông tin đã lưu lần trước (nếu có)
                .Include(a => a.Invoice).ThenInclude(i => i.Voucher)
                .FirstOrDefaultAsync(a => a.AppointmentId == id);

            if (appt == null) return NotFound();

            // Tìm hóa đơn treo (Unpaid)
            var pendingInvoice = (appt.Invoice != null && appt.Invoice.PaymentStatus == "Unpaid")
                                 ? appt.Invoice
                                 : null;

            var vm = new BillVM
            {
                AppointmentId = appt.AppointmentId,
                CustomerName = appt.Customer?.FullName ?? "Walk-in customer",
                MembershipLevel = appt.Customer?.MembershipType?.TypeName ?? "Normal",
                MembershipDiscountPercent = appt.Customer?.MembershipType?.DiscountPercent ?? 0,
                DepositAmount = appt.DepositAmount,

                // === [SỬA] PRE-FILL VOUCHER TỪ HÓA ĐƠN CŨ ===
                VoucherCode = pendingInvoice?.Voucher?.Code ?? "",
                // ============================================

                Items = appt.AppointmentDetails.Select(ad => new BillItemVM
                {
                    DetailId = ad.AppointmentDetailId,
                    ServiceName = ad.Service?.ServiceName ?? "Combo/else",
                    TechName = ad.Technician?.FullName ?? "Not selected",
                    Price = ad.PriceAtBooking,
                    // Nếu có hóa đơn treo -> Lấy Tip đã lưu trong Detail. 
                    // Nếu không -> Mặc định 0
                    TipAmount = (pendingInvoice != null) ? ad.TipAmount : 0,
                    IsSelected = ad.Status != "Cancelled"
                }).ToList()
            };

            return Json(vm);
        }

        // 2. API Tính toán tiền (Mỗi khi thay đổi input trên Modal)
        [HttpPost]
        public async Task<IActionResult> CalculateBill([FromBody] CheckoutRequestVM request)
        {
            var result = await _receptionistService.CalculateBillAsync(request);
            return Json(result);
        }

        // 3. API Thanh toán TIỀN MẶT
        [HttpPost]
        public async Task<IActionResult> CheckoutCash([FromBody] CheckoutRequestVM request)
        {
            try
            {
                request.PaymentMethod = "Cash";
                var invoiceId = await _receptionistService.ProcessCheckoutAsync(request);
                return Json(new { success = true, invoiceId = invoiceId, message = "Payment successful!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // 4. API Tạo thanh toán MOMO (Trả về link QR)
        [HttpPost]
        public async Task<IActionResult> CreateMomoPayment([FromBody] CheckoutRequestVM request)
        {
            try
            {
                // Lưu Hóa đơn trước (Trạng thái Unpaid) để giữ chỗ
                request.PaymentMethod = "Momo";
                // Gọi hàm vừa sửa ở bước 1 với isPaid = false
                var invoiceId = await _receptionistService.ProcessCheckoutAsync(request, isPaid: false);

                // a. Tính số tiền cần trả
                var calc = await _receptionistService.CalculateBillAsync(request);
                long amount = (long)calc.FinalAmount;

                // b. Tạo thông tin đơn hàng
                string orderId = $"{invoiceId}_{DateTime.Now.Ticks}"; // Unique ID
                string orderInfo = $"Pay for the order #{request.AppointmentId}";

                // c. Cấu hình URL (Thay bằng domain thực tế của bạn hoặc ngrok nếu test local)
                // Lưu ý: Momo không gọi được localhost. Bạn cần dùng Ngrok để test IPN.
                string domain = $"{Request.Scheme}://{Request.Host}"; // <-- SỬA CHỖ NÀY KHI DEPLOY
                string redirectUrl = $"{domain}/Receptionist/Calendar/MomoReturn";
                string ipnUrl = $"{domain}/Receptionist/Calendar/MomoIPN"; // Webhook

                // d. Gọi Momo Service
                string payUrl = await _momoService.CreatePaymentAsync(orderId, amount, orderInfo, redirectUrl, ipnUrl);

                // e. Lưu tạm OrderId vào Appointment để lát đối chiếu (Optional)
                // Hoặc đơn giản là trả về URL để frontend hiện QR
                return Json(new { success = true, payUrl = payUrl});
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // 5. API Polling (Frontend hỏi liên tục xem đơn xong chưa)
        [HttpGet]
        public async Task<IActionResult> CheckPaymentStatus(int appointmentId)
        {
            var appt = await _context.Appointments.FindAsync(appointmentId);
            if (appt != null && appt.Status == "Completed")
            {
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }

        // 6. MOMO IPN (Webhook) - Momo gọi vào đây khi khách thanh toán xong
        [HttpPost]
        [AllowAnonymous] // Momo gọi từ ngoài vào, không có authorize cookie
        public async Task<IActionResult> MomoIPN([FromBody] dynamic ipnData) // Hoặc tạo Model hứng response Momo
        {
            // Trong thực tế, bạn cần Verify Signature của Momo gửi sang để bảo mật
            // Ở đây tôi viết logic xử lý core:

            try
            {
                // Parse dữ liệu từ Momo (OrderId, ResultCode...)
                // string orderId = ipnData.orderId;
                // int resultCode = ipnData.resultCode;

                // Giả sử lấy được ID Lịch hẹn từ orderId (Format: 123_6354654...)
                // string[] parts = orderId.Split('_');
                // int apptId = int.Parse(parts[0]);

                // Nếu resultCode == 0 (Thành công)
                // Gọi ProcessCheckoutAsync()
                // Lưu ý: Vì IPN là request ngầm, bạn cần tự construct lại object CheckoutRequestVM
                // Hoặc lưu request tạm vào Cache/DB khi tạo link thanh toán để lôi ra xử lý.

                // --> GIẢI PHÁP ĐƠN GIẢN CHO BẠN:
                // Để code đơn giản, ta sẽ không dùng IPN để xử lý logic DB phức tạp.
                // Ta dùng IPN để update 1 flag "MomoPaid = true" vào DB.
                // Sau đó Frontend Polling thấy flag này -> Frontend tự gọi API "CheckoutCash" (nhưng method là Momo) để chốt đơn.
                // Cách này kém bảo mật hơn xíu nhưng dễ implement cho người mới.

                // TẠM THỜI: Return OK để Momo không spam
                return StatusCode(204);
            }
            catch
            {
                return StatusCode(500);
            }
        }

        [HttpGet]
        public async Task<IActionResult> MomoReturn(string orderId, string resultCode, string errorCode, string message)
        {
            // LẤY MÃ TRẢ VỀ (Ưu tiên resultCode, nếu không có thì lấy errorCode)
            string code = !string.IsNullOrEmpty(resultCode) ? resultCode : errorCode;
            // orderId có dạng: "105_6384756..." (InvoiceId_Ticks)
            // Cần cắt chuỗi để lấy phần đầu tiên là InvoiceId
            string invoiceIdStr = orderId;
            if (!string.IsNullOrEmpty(orderId) && orderId.Contains("_"))
            {
                invoiceIdStr = orderId.Split('_')[0];
            }

            // orderId chính là InvoiceId mình gửi đi lúc nãy
            if (int.TryParse(invoiceIdStr, out int invoiceId))
            {
                var invoice = await _context.Invoices
                    .Include(i => i.Appointment)
                    .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);

                if (invoice != null)
                {
                    int appId = invoice.AppointmentId;

                    if (code == "0") // 0 là Thành công
                    {
                        invoice.PaymentStatus = "Paid"; // Cập nhật đã trả tiền

                        // === [BỔ SUNG TẠI ĐÂY] CẬP NHẬT LỊCH HẸN ===
                        if (invoice.Appointment != null)
                        {
                            invoice.Appointment.Status = "Completed";
                            // Nếu có cột ActualEndTime thì cập nhật luôn
                            // invoice.Appointment.ActualEndTime = DateTime.Now; 
                        }

                        await _context.SaveChangesAsync();

                        // Chuyển hướng về lịch kèm thông báo thành công
                        return RedirectToAction("Index", new
                        {
                            msg = "Momo payment successful!",
                            printInvoiceId = invoiceId // <--- Lưu InvoiceId để frontend mở hóa đơn
                        });
                    }
                    else
                    {
                        string userMsg = message;
                        if (resultCode == "1006" || (message != null && (message.Contains("Bad request") || message.Contains("denied"))))
                        {
                            userMsg = "The transaction has been cancelled!.";
                        }

                        // Truyền thêm tham số 'reopenId' để frontend biết mà mở lại modal
                        return RedirectToAction("Index", new
                        {
                            error = $"Payment failed (Mã {code}): {userMsg}",
                            reopenId = appId
                        });
                    }
                }
            }
            return RedirectToAction("Index", new { error = "No order found!" });
        }

        // [BƯỚC 1] API HỦY LỊCH HẸN (Có ghi lý do vào Notes)
        [HttpPost]
        public async Task<IActionResult> CancelAppointment(int id, string reason)
        {
            // SỬA 1: Dùng Include để lấy luôn danh sách chi tiết con
            var appt = await _context.Appointments
                .Include(a => a.AppointmentDetails) // <--- QUAN TRỌNG
                .FirstOrDefaultAsync(a => a.AppointmentId == id);
            if (appt == null) return Json(new { success = false, message = "No appointment found." });

            // Logic chặn/cảnh báo được xử lý ở Frontend (Soft Block).
            // Ở Backend, ta thực hiện lệnh hủy theo yêu cầu của Lễ tân.

            appt.Status = "Cancelled";

            // SỬA 2: Duyệt qua các con và hủy hết
            if (appt.AppointmentDetails != null)
            {
                foreach (var detail in appt.AppointmentDetails)
                {
                    // Chỉ hủy những cái chưa hoàn thành (tránh sửa nhầm cái đã làm xong nếu có logic tách đơn sau này)
                    if (detail.Status != "Completed")
                    {
                        detail.Status = "Cancelled";

                        // (Tùy chọn) Nếu muốn reset tiền về 0 khi hủy để báo cáo doanh thu không bị tính
                        // detail.PriceAtBooking = 0; 
                    }
                }
            }

            // Ghi lý do vào Notes (Nối tiếp nội dung cũ nếu có)
            string timeStamp = DateTime.Now.ToString("dd/MM HH:mm");
            string oldNote = string.IsNullOrEmpty(appt.Notes) ? "" : $"{appt.Notes} | ";
            appt.Notes = $"{oldNote}[CANCELLED at {timeStamp}]: {reason}";

            // Xử lý Invoice (Nếu có hóa đơn Unpaid thì hủy luôn)
            var unpaidInvoice = await _context.Invoices.FirstOrDefaultAsync(i => i.AppointmentId == id && i.PaymentStatus == "Unpaid");
            if (unpaidInvoice != null)
            {
                unpaidInvoice.PaymentStatus = "Cancelled";
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        // [BƯỚC 2] API DỜI LỊCH (Reschedule)
        [HttpPost]
        public async Task<IActionResult> RescheduleAppointment([FromBody] BookingSubmissionVM model)
        {
            // Lưu ý: Ta tái sử dụng ViewModel BookingSubmissionVM nhưng sẽ xử lý logic Update
            // model.CustomerPhone sẽ đóng vai trò chứa ID lịch hẹn (Ta sẽ hack nhẹ ở Frontend để truyền ID vào đây hoặc tạo VM mới. 
            // Để đơn giản, ta sẽ dùng ID lịch hẹn truyền qua URL hoặc 1 property nào đó. 
            // Tốt nhất: Tạo 1 Class nhỏ nhận dữ liệu dời lịch để code sạch.

            // --> Dùng dynamic hoặc class riêng cho nhanh gọn
            return Json(new { success = false, message = "Vui lòng xem code cập nhật bên dưới cho API này" });
        }

        [HttpPost]
        public async Task<IActionResult> ProcessReschedule([FromBody] RescheduleRequest req)
        {
            var appt = await _context.Appointments
                .Include(a => a.AppointmentDetails)
                .FirstOrDefaultAsync(a => a.AppointmentId == req.AppointmentId);

            if (appt == null) return Json(new { success = false, message = "No appointment found." });

            // 1. Check Quy tắc 24h (Backend check thêm lần nữa cho chắc)
            // Check 24h lần cuối ở Server (Bảo mật)
            if ((appt.StartTime - DateTime.Now).TotalHours < 24)
            {
                return Json(new { success = false, message = "Error: Rescheduling deadline has passed (less than 24 hours)!" });
            }

            DateTime oldStart = appt.StartTime;

            // 2. Tính toán thời gian mới
            DateTime newStart = req.NewDate.Add(TimeSpan.Parse(req.NewTime));

            // Tính độ dài dịch vụ cũ để suy ra EndTime mới
            TimeSpan duration = (appt.EndTime ?? appt.StartTime) - appt.StartTime;

            // 3. Cập nhật Lịch hẹn
            appt.StartTime = newStart;
            appt.EndTime = newStart.Add(duration);
            appt.Status = "Confirmed"; // Reset trạng thái nếu đang Pending

            // 3. Cập nhật từng chi tiết (Phân công lại KTV)
            if (req.Details != null && req.Details.Any())
            {
                foreach (var item in req.Details)
                {
                    var detail = appt.AppointmentDetails.FirstOrDefault(ad => ad.AppointmentDetailId == item.DetailId);
                    if (detail != null)
                    {
                        detail.TechnicianId = item.TechnicianId;
                    }
                }
            }

            // Format: Từ 10/01 10:00 sang 10/01 14:00
            appt.Notes = (appt.Notes ?? "") + $" | [Reschedule]: From {oldStart:dd/MM HH:mm} to {newStart:dd/MM HH:mm}";

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        // [IN HÓA ĐƠN] Action hiển thị trang hóa đơn (Khổ K80)
        [HttpGet]
        public async Task<IActionResult> PrintInvoice(int id)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Voucher) 
                 // SỬA LẠI ĐOẠN INCLUDE NÀY:
                 // Thay vì Include(i => i.Customer) -> Ta đi từ Appointment
                .Include(i => i.Appointment)
                    .ThenInclude(a => a.Customer)
                    .ThenInclude(c => c.MembershipType) // Include để lấy % giảm giá Member
                .Include(i => i.Appointment)
                    .ThenInclude(a => a.AppointmentDetails)
                        .ThenInclude(ad => ad.Service)
                .Include(i => i.Appointment)
                    .ThenInclude(a => a.AppointmentDetails)
                        .ThenInclude(ad => ad.Combo)
                .Include(i => i.Appointment)
                    .ThenInclude(a => a.AppointmentDetails)
                        .ThenInclude(ad => ad.Technician)
                .Include(i => i.Employee) // Include thêm nhân viên thu ngân nếu cần
                .FirstOrDefaultAsync(i => i.InvoiceId == id);

            if (invoice == null) return NotFound("Invoice not found!");

            // --- TÍNH TOÁN TÁCH BIỆT GIẢM GIÁ ---
            // 1. Tổng tiền dịch vụ
            decimal subTotal = invoice.Appointment.AppointmentDetails.Sum(ad => ad.PriceAtBooking);

            // 2. Tính giảm giá Member (Theo công thức lúc tính tiền)
            double memPercent = invoice.Appointment?.Customer?.MembershipType?.DiscountPercent ?? 0;
            decimal memberDiscount = subTotal * (decimal)(memPercent / 100.0);

            // 3. Tính giảm giá Voucher (Phần còn lại)
            // Lưu ý: DiscountAmount trong DB là Tổng giảm (Member + Voucher)
            decimal totalDiscount = invoice.DiscountAmount;
            decimal voucherDiscount = totalDiscount - memberDiscount;
            // -------------------------------------

            // --- 2. XỬ LÝ DANH SÁCH ITEMS (GOM NHÓM COMBO) ---
            var rawDetails = invoice.Appointment.AppointmentDetails;

            // Tạo list động để chứa kết quả hiển thị
            var displayItems = new List<dynamic>();

            // A. Lọc các món LẺ (Không thuộc Combo)
            var singleItems = rawDetails.Where(x => x.ComboId == null).ToList();
            foreach (var item in singleItems)
            {
                displayItems.Add(new
                {
                    Name = item.Service.ServiceName,
                    Tech = item.Technician?.FullName ?? "N/A",
                    Price = item.PriceAtBooking,
                    IsHeader = false, // Dòng bình thường
                    IsChild = false,
                    Tip = item.TipAmount // <--- [FIX] Bổ sung Tip
                });
            }

            // B. Lọc và Gom nhóm COMBO
            var comboGroups = rawDetails.Where(x => x.ComboId != null).GroupBy(x => x.ComboId);

            foreach (var group in comboGroups)
            {
                // Lấy thông tin Combo từ phần tử đầu tiên trong nhóm
                var firstItem = group.First();
                string comboName = firstItem.Combo?.ComboName ?? "Combo";

                // TÍNH LẠI TỔNG TIỀN GỐC CỦA COMBO (Cộng dồn các phần tử con)
                // Vì lúc lưu ta đã chia nhỏ giá, giờ cộng lại sẽ ra đúng giá Combo gốc (550k)
                decimal groupTotal = group.Sum(x => x.PriceAtBooking);

                // 1. Thêm dòng Header (Tên Combo + Tổng tiền)
                displayItems.Add(new
                {
                    Name = $"[Combo] {comboName}",
                    Tech = "", // Header không cần tên KTV
                    Price = groupTotal,
                    IsHeader = true, // Để View in đậm
                    IsChild = false,
                    Tip = 0m // <--- [FIX] Bổ sung Tip (Header thì Tip = 0)
                });

                // 2. Thêm các dòng Con (Tên dịch vụ + Giá 0/Ẩn)
                foreach (var child in group)
                {
                    displayItems.Add(new
                    {
                        Name = child.Service?.ServiceName ?? "Service",
                        Tech = child.Technician?.FullName ?? "N/A",
                        Price = 0m, // Giá con để 0
                        IsHeader = false,
                        IsChild = true, // Để View thụt đầu dòng
                        Tip = child.TipAmount // <--- [FIX] Bổ sung Tip cho từng món con
                    });
                }
            }
            // ----------------------------------------------------

            var model = new
            {
                InvoiceId = invoice.InvoiceId,
                CreatedDate = invoice.CreatedDate.ToString("dd/MM/yyyy HH:mm"),

                // SỬA LẠI CÁCH LẤY THÔNG TIN KHÁCH HÀNG:
                // Lấy từ invoice.Appointment.Customer thay vì invoice.Customer
                CustomerName = invoice.Appointment?.Customer?.FullName ?? "Walk-in customer",
                CustomerPhone = invoice.Appointment?.Customer?.PhoneNumber ?? "",

                PaymentMethod = invoice.PaymentMethod,
                // Truyền danh sách đã xử lý gom nhóm sang View
                Items = displayItems,

                // Các con số tài chính
                TotalAmount = invoice.FinalAmount, // Lấy FinalAmount (số tiền chốt cuối cùng)
                SubTotal = subTotal, // Tổng giá trị thực

                // Tổng Tip (Lấy từ Invoice hoặc tính tổng lại từ items đều được)
                TotalTip = invoice.TipAmount,

                // Nếu muốn hiện giảm giá/cọc
                Discount = invoice.DiscountAmount,
                Deposit = invoice.DepositDeduction,

                // === [UPDATE] TRUYỀN CÁC BIẾN MỚI SANG VIEW ===
                MemberDiscount = memberDiscount,
                MemberLevel = invoice.Appointment?.Customer?.MembershipType?.TypeName ?? "Normal",

                VoucherDiscount = voucherDiscount,
                VoucherCode = invoice.Voucher?.Code ?? "", // Mã Voucher
                // ==============================================

                // Thêm tên thu ngân (nếu có)
                Cashier = invoice.Employee?.FullName ?? "Admin"
            };

            return View(model);
        }

        // API: Lấy danh sách KTV và Lịch bận theo ngày (Dùng cho Booking Modal)
        [HttpGet]
        public async Task<IActionResult> GetTechsAvailability(string date)
        {
            if (!DateTime.TryParse(date, out DateTime selectedDate))
                return Json(new { success = false, message = "Invalid date!" });

            // Tận dụng lại Service cũ để đảm bảo logic tính toán Shift/Event nhất quán
            var dashboardData = await _receptionistService.GetDashboardDataAsync(selectedDate);

            // Trích xuất dữ liệu cần thiết
            // 1. Danh sách KTV kèm Ca làm việc (Shift) của ngày đó
            var techs = dashboardData.TechnicianGroups.SelectMany(g => g.Technicians).ToList();

            // 2. Danh sách sự kiện (Lịch đã đặt) của ngày đó để check trùng giờ
            var events = dashboardData.Events;

            // === [SỬA ĐOẠN NÀY] ===
            // Sử dụng JsonSerializerOptions để ép kiểu trả về là PascalCase (Viết Hoa)
            // Khớp với cách code JS đang gọi (tech.Name, tech.Id...)
            return Json(new { success = true, techs = techs, events = events }, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = null // null = Giữ nguyên tên gốc (PascalCase)
            });
            // ======================
        }
    }

    // API Dời lịch chính thức (Dùng Model riêng cho chuẩn)
    public class RescheduleRequest
    {
        public int AppointmentId { get; set; }
        public DateTime NewDate { get; set; } // Ngày mới
        public string NewTime { get; set; }   // Giờ mới (HH:mm)
                                              // Thay vì TechnicianId đơn lẻ, ta dùng List
        public List<RescheduleDetailItem> Details { get; set; }
    }

    public class RescheduleDetailItem
    {
        public int DetailId { get; set; }
        public int TechnicianId { get; set; }
    }
}
