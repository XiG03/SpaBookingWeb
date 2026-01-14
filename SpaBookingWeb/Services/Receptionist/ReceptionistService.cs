using Microsoft.EntityFrameworkCore;
using SpaBookingWeb.Data;
using SpaBookingWeb.Models;
using SpaBookingWeb.ViewModels.Receptionist;

namespace SpaBookingWeb.Services.Receptionist
{
    public class ReceptionistService : IReceptionistService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration; // <--- KHAI BÁO BIẾN
        private readonly IEmailSenderReceptionist _emailSender; // <--- Thêm dòng này

        public ReceptionistService(ApplicationDbContext context, IConfiguration configuration, IEmailSenderReceptionist emailSender)
        {
            _context = context;
            _configuration = configuration; // <--- KHỞI TẠO BIẾN
            _emailSender = emailSender; // <--- Gán giá trị
        }

        public async Task<CalendarVM> GetDashboardDataAsync(DateTime date)
        {
            var startOfDay = date.Date;
            var endOfDay = startOfDay.AddDays(1);

            // 1. LẤY KTV, CA LÀM VIỆC & KỸ NĂNG (Update Logic)
            var techniciansData = await _context.Employees
                .Include(e => e.TechnicianDetail)
                .Include(e => e.TechnicianServices) // <--- JOIN BẢNG KỸ NĂNG
                .Where(e => e.IsActive && !e.IsDeleted && e.TechnicianDetail != null)
                .Select(e => new {
                    Employee = e,
                    Schedule = _context.WorkSchedules
                                .Include(ws => ws.Shift)
                                .FirstOrDefault(ws => ws.EmployeeId == e.EmployeeId
                                                   && ws.WorkDate.Date == date.Date
                                                   && !ws.IsDeleted)
                })
                .ToListAsync();

            var techResources = techniciansData.Select(t => new TechnicianResource
            {
                Id = t.Employee.EmployeeId,
                Name = t.Employee.FullName,
                Avatar = !string.IsNullOrEmpty(t.Employee.Avatar) ? t.Employee.Avatar : "default",
                SkillLevel = t.Employee.TechnicianDetail?.SkillLevel ?? "KTV",

                ShiftStartMinutes = t.Schedule?.Shift != null ? (int)t.Schedule.Shift.StartTime.TotalMinutes : 0,
                ShiftEndMinutes = t.Schedule?.Shift != null ? (int)t.Schedule.Shift.EndTime.TotalMinutes : 0,

                // [THÊM MỚI] Logic lấy giờ nghỉ (Early Break)
                // Nếu IsOnBreak = true và có giờ BreakStartTime -> Lấy tổng phút.
                // Ngược lại để 0.
                IsOnBreak = t.Schedule?.IsOnBreak ?? false,
                BreakStartMinutes = (t.Schedule?.IsOnBreak == true && t.Schedule?.BreakStartTime != null)
                                    ? (int)t.Schedule.BreakStartTime.Value.TotalMinutes
                                    : 0,

                // === THÊM LOGIC LẤY KỸ NĂNG ===
                AllowedServiceIds = t.Employee.TechnicianServices
                                    .Where(ts => !ts.IsDeleted)
                                    .Select(ts => ts.ServiceId)
                                    .ToList()
            }).ToList();

            var groups = new List<TechnicianGroup>
            {
                new TechnicianGroup { GroupName = "Kỹ Thuật Viên", Technicians = techResources }
            };

            // LẤY LUẬT ĐẶT CỌC (Mới)
            var depositRules = await _context.DepositRules
                .Where(d => d.IsActive && (d.ApplyToType == "OrderTotal" || d.ApplyToType == "All")) // Lấy luật theo tổng đơn
                .OrderByDescending(d => d.Priority) // Ưu tiên luật quan trọng trước
                .Select(d => new DepositRuleItem
                {
                    MinOrderValue = d.MinOrderValue ?? 0,
                    DepositType = d.DepositType,
                    DepositValue = d.DepositValue
                })
                .ToListAsync();

            // 2. LẤY DỮ LIỆU LỊCH HẸN (Appointments + Details)
            var appointments = await _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.AppointmentDetails)
                    .ThenInclude(ad => ad.Service) // Để lấy Duration
                .Where(a => !a.IsDeleted
                            && a.StartTime >= startOfDay
                            && a.StartTime < endOfDay
                            && a.Status != "Cancelled")
                .ToListAsync();

            // 3. TÍNH TOÁN THỜI GIAN CHO TỪNG DETAIL (Thuật toán Sequential Time)
            var calendarEvents = new List<CalendarEvent>();

            foreach (var appt in appointments)
            {
                // Lấy các detail có gán thợ
                var details = appt.AppointmentDetails
                    .Where(ad => !ad.IsDeleted && ad.TechnicianId != null && ad.Status != "Cancelled")
                    .OrderBy(ad => ad.AppointmentDetailId) // Giả định làm theo thứ tự thêm vào
                    .ToList();

                // Logic: Thời gian bắt đầu của Service đầu tiên = StartTime của Appointment
                // Thời gian của Service tiếp theo = Kết thúc của cái trước
                // LƯU Ý: Với logic "Nhóm" (nhiều người làm song song), ta cần tách luồng theo Khách (GuestName).

                // Gom nhóm các detail theo GuestName (để tính giờ nối tiếp cho từng người)
                var guestGroups = details.GroupBy(d => d.GuestName ?? "MainCustomer");

                foreach (var guestGroup in guestGroups)
                {
                    DateTime currentCursor = appt.StartTime; // Con trỏ thời gian

                    foreach (var detail in guestGroup)
                    {
                        int duration = detail.Service?.DurationMinutes ?? 60; // Mặc định 60p nếu null

                        var evt = new CalendarEvent
                        {
                            AppointmentDetailId = detail.AppointmentDetailId,
                            AppointmentId = appt.AppointmentId,
                            TechnicianId = detail.TechnicianId.Value,

                            CustomerName = appt.Customer?.FullName ?? "Vãng lai",
                            GuestName = detail.GuestName, // Hiển thị tên khách phụ
                            ServiceName = detail.Service?.ServiceName ?? "Dịch vụ",

                            StartTime = currentCursor,
                            EndTime = currentCursor.AddMinutes(duration),

                            Status = detail.Status, // Pending, Completed...
                            ColorClass = GetStatusColor(detail.Status)
                        };

                        calendarEvents.Add(evt);

                        // Cập nhật con trỏ thời gian cho dịch vụ tiếp theo của người này
                        currentCursor = evt.EndTime;
                    }
                }
            }

            // 4. LẤY DATA CHO BOOKING MODAL (Service & Combo)
            var services = await _context.Services
                .Where(s => s.IsActive && !s.IsDeleted)
                .Select(s => new ServiceItem
                {
                    Id = s.ServiceId,
                    Name = s.ServiceName,
                    Duration = s.DurationMinutes,
                    Price = s.Price,
                    CategoryId = s.CategoryId
                }).ToListAsync();

            var combos = await _context.Combos
                .Include(c => c.ComboDetails)
                .Where(c => !c.IsDeleted)
                .Select(c => new ComboItem
                {
                    Id = c.ComboId,
                    Name = c.ComboName,
                    Price = c.Price,
                    ServiceIds = c.ComboDetails.Where(cd => !cd.IsDeleted).Select(cd => cd.ServiceId).ToList()
                }).ToListAsync();

            return new CalendarVM
            {
                SelectedDate = date,
                TechnicianGroups = groups,
                Events = calendarEvents,
                Services = services,
                Combos = combos,
                DepositRules = depositRules // <--- GÁN VÀO ĐÂY
            };
        }

        private string GetStatusColor(string status)
        {
            return status switch
            {
                "Pending" => "bg-yellow-100 border-yellow-300 text-yellow-800", // Chờ
                "Confirmed" => "bg-blue-100 border-blue-300 text-blue-800", // Đã xác nhận
                "InProgress" => "bg-green-100 border-green-300 text-green-800", // Đang làm
                "Completed" => "bg-gray-100 border-gray-300 text-gray-600", // Xong
                "Cancelled" => "bg-red-50 border-red-200 text-red-400 opacity-50",
                _ => "bg-purple-100 border-purple-300 text-purple-800"
            };
        }

        // === TRIỂN KHAI HÀM LẤY HỒ SƠ ===
        public async Task<ReceptionistProfileVM> GetReceptionistProfileAsync(string userId)
        {
            // 1. TÌM NHÂN VIÊN (Giữ nguyên logic cũ)
            var employee = await _context.Employees
                .Include(e => e.ApplicationUser)
                .FirstOrDefaultAsync(e => e.IdentityUserId == userId);

            // --- GIẢ LẬP ID ĐỂ TEST (Giữ nguyên logic test của bạn) ---
            if (employee == null)
            {
                int debugReceptionistId = 6;
                employee = await _context.Employees.Include(e => e.ApplicationUser).FirstOrDefaultAsync(e => e.EmployeeId == debugReceptionistId);
                if (employee == null) employee = await _context.Employees.Include(e => e.ApplicationUser).FirstOrDefaultAsync();
            }
            if (employee == null) return null;

            // 2. CHUẨN BỊ MỐC THỜI GIAN
            var today = DateTime.Today;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1);

            // Lấy mốc 6 tháng trước để vẽ biểu đồ
            var startOf6MonthsAgo = startOfMonth.AddMonths(-5);

            // 3. LẤY DỮ LIỆU THỐNG KÊ (Query 1 lần lấy hết)
            // Lọc các đơn hàng do Lễ tân này tạo trong 6 tháng qua
            var appointments = await _context.Appointments
                .Include(a => a.AppointmentDetails)
                .Where(a => a.EmployeeId == employee.EmployeeId
                            && a.CreatedDate >= startOf6MonthsAgo
                            && a.CreatedDate < endOfMonth)
                .ToListAsync();

            // 4. TÍNH TOÁN CHỈ SỐ THÁNG HIỆN TẠI (Logic cũ)
            var currentMonthAppts = appointments.Where(a => a.CreatedDate >= startOfMonth).ToList();

            var sales = currentMonthAppts
                .Where(a => a.Status == "Completed")
                .SelectMany(a => a.AppointmentDetails)
                .Sum(ad => ad.PriceAtBooking);

            var served = currentMonthAppts.Count(a => a.Status == "InProgress" || a.Status == "Completed");

            int total = currentMonthAppts.Count;
            int cancelled = currentMonthAppts.Count(a => a.Status == "Cancelled");
            double cancelRate = total > 0 ? Math.Round((double)cancelled / total * 100, 1) : 0;

            // 5. [MỚI] TÍNH TOÁN DỮ LIỆU BIỂU ĐỒ (Group theo tháng)
            var stats = new List<MonthlyStat>();

            // Vòng lặp tạo 6 tháng (để đảm bảo tháng nào không có đơn thì hiện số 0 chứ không bị mất cột)
            for (int i = 0; i < 6; i++)
            {
                var targetMonth = startOf6MonthsAgo.AddMonths(i);
                var monthLabel = $"T{targetMonth.Month}"; // T1, T2...

                // Lọc đơn trong tháng targetMonth
                var apptsInMonth = appointments
                    .Where(a => a.CreatedDate.Year == targetMonth.Year && a.CreatedDate.Month == targetMonth.Month)
                    .ToList();

                stats.Add(new MonthlyStat
                {
                    MonthLabel = monthLabel,
                    // Doanh số: Chỉ tính đơn Completed
                    TotalRevenue = apptsInMonth.Where(a => a.Status == "Completed")
                                               .SelectMany(a => a.AppointmentDetails)
                                               .Sum(ad => ad.PriceAtBooking),
                    // Số khách: Tính cả InProgress và Completed
                    CustomerCount = apptsInMonth.Count(a => a.Status == "InProgress" || a.Status == "Completed")
                });
            }

            // === [MỤC 1] TÍNH TOÁN CHẤM CÔNG (Giữ nguyên như lần trước) ===
            var attendanceInfo = new AttendanceInfo { WorkDaysStandard = 26 };

            var schedules = await _context.WorkSchedules
                .Include(ws => ws.Shift)
                .Where(ws => ws.EmployeeId == employee.EmployeeId
                             && ws.WorkDate >= startOfMonth && ws.WorkDate < endOfMonth && !ws.IsDeleted)
                .OrderByDescending(ws => ws.WorkDate).ToListAsync();

            attendanceInfo.WorkDaysActual = schedules.Count(s => s.IsCheckIn);

            foreach (var sch in schedules)
            {
                string status = "Chưa làm";
                string color = "text-gray-400 bg-gray-100";
                if (sch.IsCheckIn && sch.CheckInTime.HasValue)
                {
                    var shiftStart = sch.Shift?.StartTime ?? TimeSpan.Zero;
                    var actualIn = sch.CheckInTime.Value.TimeOfDay;
                    if (actualIn > shiftStart.Add(TimeSpan.FromMinutes(15)))
                    {
                        status = "Đi muộn"; color = "text-red-600 bg-red-50 border-red-100";
                        attendanceInfo.LateMinutesTotal += (int)(actualIn - shiftStart).TotalMinutes;
                    }
                    else
                    {
                        status = "Đúng giờ"; color = "text-green-600 bg-green-50 border-green-100";
                    }
                }
                else if (sch.WorkDate < DateTime.Today) { status = "Vắng"; color = "text-gray-500 bg-gray-200"; }

                attendanceInfo.RecentLogs.Add(new DailyLog
                {
                    Date = sch.WorkDate,
                    CheckIn = sch.CheckInTime?.TimeOfDay,
                    CheckOut = sch.CheckOutTime?.TimeOfDay,
                    Status = status,
                    StatusColor = color
                });
            }

            // === [MỤC 4] PHÂN TÍCH CHẤT LƯỢNG (Giữ nguyên như lần trước) ===
            var qualityInfo = new QualityInfo();
            if (currentMonthAppts.Any())
            {
                // 1. Tỷ lệ Upsell & Top Services (Giữ nguyên code cũ)
                int upsellCount = currentMonthAppts.Count(a => a.AppointmentDetails.Any(d => d.ComboId != null) || a.AppointmentDetails.Count > 1);
                qualityInfo.UpsellRate = Math.Round((double)upsellCount / currentMonthAppts.Count * 100, 1);

                qualityInfo.TopServices = currentMonthAppts
                    .SelectMany(a => a.AppointmentDetails).Where(d => d.Status == "Completed")
                    .GroupBy(d => d.Service != null ? d.Service.ServiceName : (d.Combo != null ? d.Combo.ComboName : "Khác"))
                    .Select(g => new TopServiceItem { ServiceName = g.Key, Quantity = g.Count(), TotalRevenue = g.Sum(x => x.PriceAtBooking) })
                    .OrderByDescending(x => x.Quantity).Take(4).ToList();

                // 2. [MỚI] TÍNH TRẠNG THÁI LỊCH HẸN (ĐỂ LẤP ĐẦY CHỖ TRỐNG)
                qualityInfo.StatusStats = new StatusBreakdown
                {
                    Pending = currentMonthAppts.Count(a => a.Status == "Pending" || a.Status == "Confirmed"),
                    InProgress = currentMonthAppts.Count(a => a.Status == "InProgress"),
                    Completed = currentMonthAppts.Count(a => a.Status == "Completed"),
                    Cancelled = currentMonthAppts.Count(a => a.Status == "Cancelled")
                };
            }

            // === [MỤC 3 - MỚI] NHẬT KÝ HOẠT ĐỘNG (Thay thế KPI) ===
            var activities = new List<ActivityItem>();

            // A. Lấy 5 lịch hẹn gần nhất do Lễ tân này tạo
            var recentBookings = await _context.Appointments
                .Where(a => a.EmployeeId == employee.EmployeeId)
                .OrderByDescending(a => a.CreatedDate).Take(5)
                .Select(a => new { a.CreatedDate, a.Customer.FullName, a.AppointmentId, Type = "Booking" })
                .ToListAsync();

            // B. Lấy 5 hóa đơn gần nhất do Lễ tân này thanh toán
            var recentInvoices = await _context.Invoices
                .Where(i => i.EmployeeId == employee.EmployeeId)
                .OrderByDescending(i => i.CreatedDate).Take(5)
                .Select(i => new { i.CreatedDate, i.FinalAmount, i.InvoiceId, Type = "Payment" })
                .ToListAsync();

            // C. Trộn và map dữ liệu
            foreach (var item in recentBookings)
            {
                activities.Add(new ActivityItem
                {
                    Timestamp = item.CreatedDate,
                    ActionType = "Booking",
                    Title = "Tạo lịch hẹn mới",
                    Description = $"Khách hàng: {item.FullName} (#{item.AppointmentId})",
                    Icon = "calendar_add_on",
                    ColorClass = "bg-blue-100 text-blue-600"
                });
            }
            foreach (var item in recentInvoices)
            {
                activities.Add(new ActivityItem
                {
                    Timestamp = item.CreatedDate,
                    ActionType = "Payment",
                    Title = "Thanh toán thành công",
                    Description = $"Thu {item.FinalAmount:N0}đ (HĐ #{item.InvoiceId})",
                    Icon = "payments",
                    ColorClass = "bg-green-100 text-green-600"
                });
            }

            // Sắp xếp giảm dần theo thời gian và lấy 6 cái mới nhất
            var finalActivities = activities.OrderByDescending(x => x.Timestamp).Take(6).ToList();

            // [THÊM MỚI] 5. LẤY LỊCH SỬ LƯƠNG
            var salaryList = await _context.Salaries
                .Where(s => s.EmployeeId == employee.EmployeeId && !s.IsDeleted)
                .OrderByDescending(s => s.Year).ThenByDescending(s => s.Month)
                .Select(s => new SalaryHistoryItem
                {
                    SalaryId = s.SalaryId,
                    Month = s.Month,
                    Year = s.Year,
                    BaseSalary = s.Employee.BaseSalary, // Hoặc lấy từ bảng Salary nếu có lưu snapshot
                    Commission = s.TotalCommission,
                    Bonus = s.Bonus,
                    Deduction = s.Deduction,
                    TotalSalary = s.TotalSalary,
                    Status = s.Status // "Pending", "Completed"
                })
                .ToListAsync();

            // 6. TRẢ VỀ VIEWMODEL
            return new ReceptionistProfileVM
            {
                // Thông tin cá nhân
                FullName = employee.FullName,
                EmployeeCode = $"LT-{employee.EmployeeId.ToString("000")}",
                Avatar = !string.IsNullOrEmpty(employee.Avatar) ? employee.Avatar : "https://via.placeholder.com/150",
                JobTitle = "Lễ Tân",
                IsActive = employee.IsActive,
                HireDate = employee.HireDate,
                SeniorityMonths = (today.Year - employee.HireDate.Year) * 12 + today.Month - employee.HireDate.Month,
                BaseSalary = employee.BaseSalary,
                PhoneNumber = employee.ApplicationUser?.PhoneNumber ?? "Chưa cập nhật",
                Email = employee.ApplicationUser?.Email ?? "Chưa cập nhật",
                Address = employee.Address ?? "Chưa cập nhật",

                // Chỉ số tháng hiện tại
                CurrentMonthSales = sales,
                CustomersServed = served,
                CancellationRate = cancelRate,

                // [MỚI] Dữ liệu biểu đồ
                MonthlyStats = stats,
                // [GÁN DỮ LIỆU MỚI]
                Attendance = attendanceInfo,
                Quality = qualityInfo,
                RecentActivities = finalActivities, // Mục 3
                SalaryHistory = salaryList // <--- Gán danh sách vừa lấy vào đây
            };
        }


        // 1. TÍNH TOÁN HÓA ĐƠN (Logic Lũy Tiến)
        public async Task<BillCalculationResult> CalculateBillAsync(CheckoutRequestVM request)
        {
            var result = new BillCalculationResult();

            // A. Lấy thông tin Appointment & Khách hàng
            var appt = await _context.Appointments
                .Include(a => a.Customer).ThenInclude(c => c.MembershipType)
                .FirstOrDefaultAsync(a => a.AppointmentId == request.AppointmentId);

            if (appt == null) return result;

            // B. Tính Subtotal (Chỉ tính các món được tick chọn)
            // Lưu ý: request.Items chứa Tip và trạng thái Selected từ Client gửi lên
            decimal subTotal = 0;
            decimal totalTip = 0;

            foreach (var item in request.Items)
            {
                if (item.IsSelected)
                {
                    // Lấy giá gốc từ DB để an toàn (tránh hack client sửa giá)
                    var dbItem = await _context.AppointmentDetails.FindAsync(item.DetailId);
                    if (dbItem != null)
                    {
                        subTotal += dbItem.PriceAtBooking;
                    }
                }
                // Tip thì tính hết (kể cả không làm dịch vụ vẫn có thể tip nếu muốn, hoặc tùy logic)
                // Ở đây ta chỉ tính tip cho các món được chọn cho chặt chẽ
                if (item.IsSelected)
                {
                    totalTip += item.TipAmount;
                }
            }

            result.SubTotal = subTotal;
            result.TotalTip = totalTip;
            result.Deposit = appt.DepositAmount;

            // C. Giảm giá Thành viên (Ưu tiên 1)
            double memberRate = appt.Customer?.MembershipType?.DiscountPercent ?? 0;
            result.MemberDiscount = subTotal * (decimal)(memberRate / 100.0);

            // D. Số dư sau khi giảm Member (Dùng để tính Voucher lũy tiến)
            decimal amountAfterMember = subTotal - result.MemberDiscount;

            // E. Giảm giá Voucher (Ưu tiên 2)
            if (!string.IsNullOrEmpty(request.VoucherCode))
            {
                var today = DateTime.Now;
                var voucher = await _context.Vouchers
                    .FirstOrDefaultAsync(v => v.Code == request.VoucherCode
                                              && v.IsActive
                                              && v.StartDate <= today
                                              && v.EndDate >= today);

                if (voucher != null && voucher.UsageCount < voucher.UsageLimit)
                {
                    // Check điều kiện tối thiểu
                    if (subTotal >= voucher.MinSpend)
                    {
                        result.IsVoucherValid = true;

                        if (voucher.DiscountType == "Percent")
                        {
                            // Lũy tiến: Tính trên số dư còn lại
                            result.VoucherDiscount = amountAfterMember * (decimal)(voucher.DiscountValue / (decimal)100.0);

                            // (Optional) Nếu có MaxDiscountAmount thì clamp lại
                            if (result.VoucherDiscount > voucher.MaxDiscountAmount) result.VoucherDiscount = voucher.MaxDiscountAmount ?? 0;
                        }
                        else // Fixed Amount
                        {
                            result.VoucherDiscount = voucher.DiscountValue;
                        }

                        // Không được giảm quá số tiền còn lại (tránh âm tiền)
                        if (result.VoucherDiscount > amountAfterMember)
                            result.VoucherDiscount = amountAfterMember;

                        result.VoucherMessage = $"Áp dụng mã {voucher.Code}: -{result.VoucherDiscount:N0}đ";
                    }
                    else
                    {
                        result.VoucherMessage = $"Đơn hàng chưa đạt tối thiểu {voucher.MinSpend:N0}đ";
                    }
                }
                else
                {
                    result.VoucherMessage = "Mã không hợp lệ hoặc đã hết hạn!";
                }
            }

            // F. TỔNG KẾT
            // Final = (SubTotal - Member - Voucher) + Tip - Deposit
            decimal final = (subTotal - result.MemberDiscount - result.VoucherDiscount) + totalTip - result.Deposit;

            // Tránh số âm (Trường hợp cọc quá nhiều -> Trả lại khách thì hiển thị số âm cũng được, nhưng ở đây ta set 0 cho đơn giản)
            // Hoặc giữ nguyên số âm để biết cần thối lại tiền
            result.FinalAmount = final;

            return result;
        }

        // 2. CHỐT ĐƠN (Transaction) - Dùng chung cho cả Cash và Momo
        public async Task<int> ProcessCheckoutAsync(CheckoutRequestVM request, bool isPaid = true)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Tính toán lại lần cuối (Server verify)
                var calc = await CalculateBillAsync(request);

                var appt = await _context.Appointments
                    .Include(a => a.AppointmentDetails)
                    .FirstOrDefaultAsync(a => a.AppointmentId == request.AppointmentId);

                // 2. Cập nhật từng chi tiết dịch vụ
                foreach (var itemReq in request.Items)
                {
                    var detail = appt.AppointmentDetails.FirstOrDefault(ad => ad.AppointmentDetailId == itemReq.DetailId);
                    if (detail != null)
                    {
                        if (itemReq.IsSelected)
                        {
                            detail.Status = "Completed";
                            // Cập nhật Tip (Gián tiếp)
                            detail.TipAmount = itemReq.TipAmount;
                            detail.IsDirectTip = false; // Theo yêu cầu
                        }
                        else
                        {
                            detail.Status = "Cancelled";
                            detail.PriceAtBooking = 0; // Hủy thì không tính tiền
                            detail.TipAmount = 0;
                        }
                    }
                }

                // 3. XỬ LÝ VOUCHER (Logic Update thông minh)
                // Tìm hóa đơn cũ trước để so sánh
                var existingInvoiceVoucher = await _context.Invoices
                    .Include(i => i.Voucher) // Include để check mã cũ
                    .FirstOrDefaultAsync(i => i.AppointmentId == request.AppointmentId && i.PaymentStatus == "Unpaid");

                if (calc.IsVoucherValid && !string.IsNullOrEmpty(request.VoucherCode))
                {
                    var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == request.VoucherCode);
                    if (voucher != null)
                    {
                        bool shouldIncrement = true;

                        // Nếu đang cập nhật hóa đơn cũ
                        if (existingInvoiceVoucher != null)
                        {
                            // Case 1: Mã mới GIỐNG mã cũ -> Không cộng nữa (Vì lần trước cộng rồi)
                            if (existingInvoiceVoucher.VoucherId == voucher.VoucherId)
                            {
                                shouldIncrement = false;
                            }
                            // Case 2: Mã mới KHÁC mã cũ -> Trừ mã cũ đi, cộng mã mới
                            else if (existingInvoiceVoucher.VoucherId != null)
                            {
                                var oldVoucher = await _context.Vouchers.FindAsync(existingInvoiceVoucher.VoucherId);
                                if (oldVoucher != null) oldVoucher.UsageCount -= 1; // Hoàn lại lượt dùng cũ
                                shouldIncrement = true;
                            }
                        }

                        // Thực hiện cộng lượt dùng
                        if (shouldIncrement)
                        {
                            voucher.UsageCount += 1;
                            if (voucher.UsageCount > voucher.UsageLimit) throw new Exception("Voucher vừa hết lượt sử dụng!");
                        }
                    }
                }
                // Trường hợp khách bỏ Voucher (Lần trước có dùng, lần này không dùng)
                else if (existingInvoiceVoucher != null && existingInvoiceVoucher.VoucherId != null)
                {
                    var oldVoucher = await _context.Vouchers.FindAsync(existingInvoiceVoucher.VoucherId);
                    if (oldVoucher != null) oldVoucher.UsageCount -= 1; // Hoàn lại
                }

                // 4. TÌM VOUCHER ID ĐỂ LƯU (Logic bổ sung)
                int? voucherIdToSave = null;
                if (calc.IsVoucherValid && !string.IsNullOrEmpty(request.VoucherCode))
                {
                    var v = await _context.Vouchers.FirstOrDefaultAsync(x => x.Code == request.VoucherCode);
                    if (v != null) voucherIdToSave = v.VoucherId;
                }

                // TẠO HOẶC CẬP NHẬT HÓA ĐƠN (LOGIC UPSERT QUAN TRỌNG)
                // Tìm xem đã có hóa đơn nào đang treo (Unpaid) của lịch này chưa?
                var existingInvoice = await _context.Invoices
                    .FirstOrDefaultAsync(i => i.AppointmentId == request.AppointmentId && i.PaymentStatus == "Unpaid");

                int invoiceIdResult = 0;

                Invoice finalInvoice = null;

                if (existingInvoice != null)
                {
                    // === CASE A: CẬP NHẬT HÓA ĐƠN CŨ (REUSE) ===
                    existingInvoice.CreatedDate = DateTime.Now; // Cập nhật lại giờ
                    existingInvoice.TotalAmount = calc.SubTotal;
                    existingInvoice.DiscountAmount = calc.MemberDiscount + calc.VoucherDiscount;
                    existingInvoice.FinalAmount = calc.FinalAmount;
                    existingInvoice.TipAmount = calc.TotalTip;
                    existingInvoice.EmployeeId = appt.EmployeeId;
                    existingInvoice.DepositDeduction = calc.Deposit;

                    // === [FIX QUAN TRỌNG] LƯU VOUCHER ID VÀO INVOICE ===
                    existingInvoice.VoucherId = voucherIdToSave;
                    // ===================================================

                    // Quan trọng: Update phương thức và trạng thái mới
                    existingInvoice.PaymentMethod = request.PaymentMethod;
                    existingInvoice.PaymentStatus = isPaid ? "Paid" : "Unpaid";

                    _context.Invoices.Update(existingInvoice);
                    invoiceIdResult = existingInvoice.InvoiceId;

                    finalInvoice = existingInvoice;
                }
                else
                {
                    // 4. Tạo Hóa Đơn (Invoice)
                    var newInvoice = new Invoice
                    {
                        AppointmentId = appt.AppointmentId,
                        CreatedDate = DateTime.Now,
                        TotalAmount = calc.SubTotal, // Tổng giá trị thực tế làm
                        DiscountAmount = calc.MemberDiscount + calc.VoucherDiscount, // Tổng giảm
                        FinalAmount = calc.FinalAmount, // Số tiền khách trả thực tế (đã trừ cọc)
                        PaymentStatus = isPaid ? "Paid" : "Unpaid",
                        PaymentMethod = request.PaymentMethod, // Cash hoặc Momo
                        EmployeeId = appt.EmployeeId, // Lễ tân thu tiền
                        TipAmount = calc.TotalTip,
                        VoucherId = voucherIdToSave,
                        DepositDeduction = calc.Deposit
                    };
                    _context.Invoices.Add(newInvoice);
                    // Save trước để lấy ID
                    await _context.SaveChangesAsync();
                    invoiceIdResult = newInvoice.InvoiceId;

                    finalInvoice = newInvoice;
                }

                // 5. Chốt lịch hẹn
                // Chỉ set Completed khi đã thanh toán xong (Paid)
                // Hoặc nếu bạn muốn Momo (Unpaid) cũng set Completed luôn thì bỏ điều kiện if.
                if (isPaid)
                {
                    appt.Status = "Completed";
                }
                
                // appt.ActualEndTime = DateTime.Now; // Nếu có cột này

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // === [BẮT ĐẦU CODE GỬI MAIL] ===
                try
                {
                    var appointmentForMail = await _context.Appointments
                        .Include(a => a.Customer)
                        .FirstOrDefaultAsync(a => a.AppointmentId == request.AppointmentId);

                    if (appointmentForMail != null && appointmentForMail.Customer != null && !string.IsNullOrEmpty(appointmentForMail.Customer.Email))
                    {
                        string emailBody = GenerateInvoiceHtml(
                            appointmentForMail.Customer.FullName,
                            finalInvoice.InvoiceId.ToString(), // [SỬA 4] Dùng finalInvoice thay vì invoice
                            DateTime.Now,
                            finalInvoice.FinalAmount           // [SỬA 5] Dùng finalInvoice thay vì invoice
                        );

                        _ = _emailSender.SendEmailAsync(
                            appointmentForMail.Customer.Email,
                            $"[LushSpa] Hóa đơn điện tử #{finalInvoice.InvoiceId}",
                            emailBody
                        );
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Lỗi gửi mail: " + ex.Message);
                }
                // ===============================

                return invoiceIdResult;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // === [THÊM MỚI] 1. LẤY LỊCH LÀM VIỆC HÔM NAY ===
        public async Task<WorkSchedule?> GetTodayScheduleAsync(int employeeId)
        {
            return await GetSmartScheduleAsync(employeeId);
        }

        // === [THÊM MỚI] 2. THỰC HIỆN ĐIỂM DANH (CHECK IP) ===
        public async Task<string> PerformAttendanceAsync(int employeeId, string clientIp)
        {
            // 1. Kiểm tra IP (Logic whitelist lấy từ appsettings)
            var allowedIps = _configuration["AttendanceSettings:AllowedIPs"]?.Split(';') ?? Array.Empty<string>();

            // Nếu clientIp là localhost (::1) thì map về 127.0.0.1 để so sánh
            if (clientIp == "::1") clientIp = "127.0.0.1";

            if (!allowedIps.Contains(clientIp))
            {
                return $"IP của bạn ({clientIp}) không hợp lệ. Vui lòng kết nối Wi-Fi Spa!";
            }

            // 2. Lấy lịch làm việc phù hợp nhất với giờ hiện tại
            var schedule = await GetSmartScheduleAsync(employeeId);

            if (schedule == null) return "Hôm nay bạn không có lịch làm việc!";

            // 3. Xử lý Logic Check-in / Check-out
            if (schedule.CheckInTime == null)
            {
                // --- CHECK IN ---
                schedule.CheckInTime = DateTime.Now;
                schedule.IsCheckIn = true;
            }
            else if (schedule.CheckOutTime == null)
            {
                // --- CHECK OUT ---
                schedule.CheckOutTime = DateTime.Now;
            }
            else
            {
                return "Ca làm việc này đã hoàn thành (đã Check-out)!";
            }

            await _context.SaveChangesAsync();
            return null; // Null nghĩa là thành công
        }

        private string GenerateInvoiceHtml(string customerName, string invoiceId, DateTime date, decimal totalAmount)
        {
            // Bạn có thể tùy chỉnh HTML này đẹp hơn tùy thích
            return $@"
        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #eee; border-radius: 10px;'>
            <div style='text-align: center; background-color: #13c8ec; padding: 20px; border-radius: 10px 10px 0 0;'>
                <h2 style='color: white; margin: 0;'>CẢM ƠN QUÝ KHÁCH!</h2>
            </div>
            <div style='padding: 20px;'>
                <p>Xin chào <strong>{customerName}</strong>,</p>
                <p>Cảm ơn bạn đã sử dụng dịch vụ tại LushSpa. Dưới đây là thông tin hóa đơn thanh toán của bạn:</p>
                
                <table style='width: 100%; margin-top: 20px; border-collapse: collapse;'>
                    <tr style='background-color: #f9f9f9;'>
                        <td style='padding: 10px; border-bottom: 1px solid #ddd;'>Mã hóa đơn:</td>
                        <td style='padding: 10px; border-bottom: 1px solid #ddd; font-weight: bold;'>#{invoiceId}</td>
                    </tr>
                    <tr>
                        <td style='padding: 10px; border-bottom: 1px solid #ddd;'>Ngày thanh toán:</td>
                        <td style='padding: 10px; border-bottom: 1px solid #ddd;'>{date:dd/MM/yyyy HH:mm}</td>
                    </tr>
                    <tr style='background-color: #f9f9f9;'>
                        <td style='padding: 10px; border-bottom: 1px solid #ddd;'>Tổng thanh toán:</td>
                        <td style='padding: 10px; border-bottom: 1px solid #ddd; color: #13c8ec; font-size: 18px; font-weight: bold;'>
                            {totalAmount:N0} đ
                        </td>
                    </tr>
                </table>

                <p style='margin-top: 30px; font-style: italic; color: #666;'>Hẹn gặp lại bạn trong lần chăm sóc tiếp theo!</p>
            </div>
            <div style='text-align: center; font-size: 12px; color: #999; margin-top: 20px; border-top: 1px solid #eee; padding-top: 10px;'>
                LushSpa - 123 Đường ABC, Quận XYZ, TP.HCM<br>
                Hotline: 0909999888
            </div>
        </div>";
        }

        // === [HÀM PHỤ MỚI] LOGIC TÌM CA THÔNG MINH ===
        // Hàm này giúp chọn đúng ca Sáng/Chiều/Tối dựa vào giờ hiện tại
        private async Task<WorkSchedule?> GetSmartScheduleAsync(int employeeId)
        {
            var today = DateTime.Today;
            var now = DateTime.Now.TimeOfDay;

            // 1. Lấy TẤT CẢ các ca trong ngày của nhân viên
            var schedules = await _context.WorkSchedules
                .Include(ws => ws.Shift)
                .Where(ws => ws.EmployeeId == employeeId
                             && ws.WorkDate == today
                             && !ws.IsDeleted)
                .ToListAsync();

            if (!schedules.Any()) return null;

            // 2. ƯU TIÊN 1: Ca đang diễn ra (Giờ hiện tại nằm trong khung giờ ca)
            // Ví dụ: Bây giờ 19h, Ca 3 (18h-22h) sẽ được chọn.
            var activeShift = schedules.FirstOrDefault(s =>
                s.Shift != null && s.Shift.StartTime <= now && s.Shift.EndTime >= now);

            if (activeShift != null) return activeShift;

            // 3. ƯU TIÊN 2: Ca chưa Check-out (Đang làm dở)
            var pendingShift = schedules.FirstOrDefault(s => s.CheckInTime != null && s.CheckOutTime == null);
            if (pendingShift != null) return pendingShift;

            // 4. ƯU TIÊN 3: Ca sắp diễn ra gần nhất (Check-in sớm)
            var upcomingShift = schedules
                .Where(s => s.CheckInTime == null && s.Shift?.StartTime > now)
                .MinBy(s => s.Shift?.StartTime);

            if (upcomingShift != null) return upcomingShift;

            // 5. CÙNG ĐƯỜNG: Lấy ca chưa làm bất kỳ cái nào (fallback)
            var anyUnfinished = schedules.FirstOrDefault(s => s.CheckInTime == null);
            if (anyUnfinished != null) return anyUnfinished;

            // 6. Nếu làm xong hết rồi thì lấy cái cuối cùng để hiện trạng thái "Done"
            return schedules.LastOrDefault();
        }

        // Hàm xác nhận đã nhận lương
        public async Task<bool> ConfirmSalaryReceiptAsync(int salaryId, string userId)
        {
            // 1. Tìm nhân viên từ UserId (để bảo mật, tránh xác nhận hộ người khác)
            var emp = await _context.Employees.FirstOrDefaultAsync(e => e.IdentityUserId == userId);
            if (emp == null) throw new Exception("Không tìm thấy hồ sơ nhân viên.");

            // 2. Tìm phiếu lương
            var salary = await _context.Salaries.FirstOrDefaultAsync(s => s.SalaryId == salaryId);

            if (salary == null) throw new Exception("Không tìm thấy bảng lương.");

            // 3. Kiểm tra quyền sở hữu
            if (salary.EmployeeId != emp.EmployeeId) throw new Exception("Bạn không có quyền thao tác trên bảng lương này.");

            // 4. Kiểm tra trạng thái
            if (salary.Status == "Completed") throw new Exception("Lương tháng này đã được xác nhận trước đó rồi.");

            // 5. Cập nhật
            salary.Status = "Completed";
            // salary.PaymentDate = DateTime.Now; // Nếu có cột này thì uncomment

            await _context.SaveChangesAsync();
            return true;
        }
    }
}
