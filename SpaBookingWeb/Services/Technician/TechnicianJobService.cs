using Microsoft.EntityFrameworkCore;
using SpaBookingWeb.Data;
using SpaBookingWeb.Models;
using SpaBookingWeb.ViewModels.Technician;
using Microsoft.Extensions.Configuration; // Nhớ using cái này

namespace SpaBookingWeb.Services.Technictian
{
    public class TechnicianJobService : ITechnicianJobService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration; // Thêm cái này

        public TechnicianJobService(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration; // Khởi tạo trong constructor
        }

        public async Task<int?> GetCurrentEmployeeIdAsync(string userId)
        {
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.IdentityUserId == userId && e.IsActive);
            return employee?.EmployeeId;
        }

        public async Task<List<CalendarEventVM>> GetCalendarEventsAsync(int employeeId, int month, int year)
        {
            // Lấy các việc được giao cho KTV này trong tháng/năm chỉ định
            var jobs = await _context.AppointmentDetails
                .Include(ad => ad.Appointment)
                    .ThenInclude(a => a.Customer)
                .Include(ad => ad.Service)
                .Include(ad => ad.Combo)
                .Where(ad => ad.TechnicianId == employeeId
                             //&& ad.Appointment.StartTime.Month == month
                             //&& ad.Appointment.StartTime.Year == year
                             && ad.Status != "Cancelled")
                .ToListAsync();

            // Chuyển đổi dữ liệu sang ViewModel
            return jobs.Select(ad => {
                // Xử lý logic hiển thị tên Dịch vụ (Lẻ vs Combo)
                string displayService = "";
                bool isCombo = ad.ComboId != null;

                if (isCombo)
                {
                    string servicePart = ad.Service != null ? ad.Service.ServiceName : "Dịch vụ";
                    displayService = $"{servicePart} (Gói: {ad.Combo.ComboName})";
                }
                else
                {
                    displayService = ad.Service?.ServiceName ?? "Dịch vụ tùy chỉnh";
                }

                // Xử lý màu sắc
                string bgClass, textClass;
                if (ad.Status == "Completed")
                {
                    bgClass = "bg-gray-100 text-gray-800 dark:bg-gray-800 dark:text-gray-300";
                    textClass = "text-gray-600 dark:text-gray-400";
                }
                else if (isCombo)
                {
                    bgClass = "bg-purple-100 text-purple-800 dark:bg-purple-900/50 dark:text-purple-200";
                    textClass = "text-purple-700 dark:text-purple-300";
                }
                else
                {
                    bgClass = "bg-green-100 text-green-800 dark:bg-green-900/50 dark:text-green-200";
                    textClass = "text-green-700 dark:text-green-300";
                }

                return new CalendarEventVM
                {
                    Id = ad.AppointmentDetailId,
                    Title = ad.Appointment.Customer.FullName,
                    ServiceName = displayService,
                    Start = ad.Appointment.StartTime.ToString("yyyy-MM-ddTHH:mm:ss"), // ISO Format chuẩn
                    ColorClass = bgClass,
                    SubColor = textClass
                };
            }).ToList();
        }

        // Hàm lấy chi tiết (Giữ lại để đảm bảo Interface không lỗi)
        public async Task<JobDetailVM> GetJobDetailAsync(int appointmentDetailId, int employeeId)
        {
            var job = await _context.AppointmentDetails
               .Include(ad => ad.Appointment).ThenInclude(a => a.Customer)
               .Include(ad => ad.Service)
               .Include(ad => ad.Combo)
               .Include(ad => ad.AppointmentConsumables).ThenInclude(ac => ac.Product)
               .FirstOrDefaultAsync(ad => ad.AppointmentDetailId == appointmentDetailId && ad.TechnicianId == employeeId);

            if (job == null) return null;

            return new JobDetailVM
            {
                AppointmentDetailId = job.AppointmentDetailId,
                CustomerName = job.Appointment.Customer.FullName,
                CustomerPhone = job.Appointment.Customer.PhoneNumber,
                StartTime = job.Appointment.StartTime,
                EndTime = job.Appointment.EndTime,
                Status = job.Status,
                ServiceName = job.Service?.ServiceName ?? job.Combo?.ComboName ?? "Dịch vụ tùy chỉnh",
                DurationMinutes = job.Service?.DurationMinutes ?? 0,
                TipAmount = job.TipAmount,
                IsDirectTip = job.IsDirectTip,
                TipPayoutDate = job.TipPayoutDate,
                Consumables = job.AppointmentConsumables.ToList()
            };
        }

        public async Task<bool> ChangeJobStatusAsync(int appointmentDetailId, int employeeId, string newStatus, decimal? tipAmount = null)
        {
            var job = await _context.AppointmentDetails
                .Include(ad => ad.Appointment) // <--- QUAN TRỌNG: Phải join để check status cha
                .FirstOrDefaultAsync(ad => ad.AppointmentDetailId == appointmentDetailId
                                           && ad.TechnicianId == employeeId);

            if (job == null) return false; // Không tìm thấy hoặc không phải việc của người này

            // LOGIC CHỐT CHẶN: Ngăn KTV Check-in nếu Lễ tân chưa Check-in khách
            if (newStatus == "InProgress")
            {
                // Nếu Lịch cha chưa là 'InProgress' -> Chặn lại
                if (job.Appointment.Status != "InProgress")
                {
                    throw new Exception("Lễ tân CHƯA Check-in cho khách này. Vui lòng nhắc Lễ tân Check-in trước!");
                }
            }

            // LOGIC HỦY DỊCH VỤ (Cancel)
            if (newStatus == "Cancelled")
            {
                // Cho phép hủy thoải mái, không cần check điều kiện gì đặc biệt
                // (Nếu muốn kỹ hơn: Chỉ cho hủy khi chưa Completed)
                if (job.Status == "Completed")
                {
                    throw new Exception("Dịch vụ đã hoàn thành, không thể hủy!");
                }
            }

            // Cập nhật trạng thái
            job.Status = newStatus;

            // --- LOGIC MỚI: LƯU TIỀN TIP ---
            // Chỉ lưu khi trạng thái là Completed và có nhập tiền tip
            if (newStatus == "Completed" && tipAmount.HasValue)
            {
                job.TipAmount = tipAmount.Value;
                // Nếu có tip -> Đánh dấu là Direct Tip (Tip trực tiếp)
                if (tipAmount.Value > 0)
                {
                    job.IsDirectTip = true;
                }
            }
            // -------------------------------

            await _context.SaveChangesAsync();
            return true;
        }

        // 1. Lấy danh sách tiêu hao (Logic thông minh)
        public async Task<List<ConsumableItemVM>> GetConsumablesAsync(int appointmentDetailId)
        {
            // A. Kiểm tra xem đã từng lưu chưa?
            var existingItems = await _context.AppointmentConsumables
                .Include(ac => ac.Product).ThenInclude(p => p.Unit)
                .Where(ac => ac.AppointmentDetailId == appointmentDetailId)
                .ToListAsync();

            if (existingItems.Any())
            {
                // Nếu có rồi -> Trả về danh sách đã lưu
                return existingItems.Select(x => new ConsumableItemVM
                {
                    UsageId = x.UsageId,
                    ProductId = x.ProductId,
                    ProductName = x.Product.ProductName,
                    UnitName = x.Product.Unit?.UnitName ?? "Đơn vị",
                    StandardQuantity = x.StandardQuantity,
                    ActualQuantity = x.ActualQuantity,
                    Reason = x.Reason
                }).ToList();
            }

            // B. Nếu chưa có -> Lấy định mức từ bảng ServiceConsumables (Gợi ý)
            var job = await _context.AppointmentDetails.FindAsync(appointmentDetailId);
            if (job?.ServiceId != null)
            {
                var standardItems = await _context.ServiceConsumables
                    .Include(sc => sc.Product).ThenInclude(p => p.Unit)
                    .Where(sc => sc.ServiceId == job.ServiceId)
                    .ToListAsync();

                return standardItems.Select(x => new ConsumableItemVM
                {
                    UsageId = 0, // Đánh dấu là mới
                    ProductId = x.ProductId,
                    ProductName = x.Product.ProductName,
                    UnitName = x.Product.Unit?.UnitName ?? "Đơn vị",
                    StandardQuantity = x.Quantity,
                    ActualQuantity = x.Quantity, // Mặc định thực dùng = định mức
                    Reason = ""
                }).ToList();
            }

            return new List<ConsumableItemVM>(); // Không có gì cả
        }

        // 2. Lấy danh sách tất cả sản phẩm (để chọn thêm)
        public async Task<List<Product>> GetAvailableProductsAsync()
        {
            // Chỉ lấy sản phẩm có thể dùng (ví dụ: không phải là dịch vụ)
            // Tạm thời lấy hết sản phẩm
            return await _context.Products.Include(p => p.Unit).ToListAsync();
        }

        // 3. Lưu danh sách tiêu hao (Xóa cũ nạp mới cho đơn giản và chính xác)
        public async Task SaveConsumablesAsync(int appointmentDetailId, List<ConsumableItemVM> items)
        {
            // Chiến lược: Xóa hết cái cũ của job này đi, thêm lại danh sách mới gửi lên.
            // (Đây là cách dễ nhất để xử lý cả Thêm/Sửa/Xóa cùng lúc)

            var oldItems = _context.AppointmentConsumables
                .Where(x => x.AppointmentDetailId == appointmentDetailId);
            _context.AppointmentConsumables.RemoveRange(oldItems);

            // Thêm danh sách mới
            foreach (var item in items)
            {
                _context.AppointmentConsumables.Add(new AppointmentConsumable
                {
                    AppointmentDetailId = appointmentDetailId,
                    ProductId = item.ProductId,
                    StandardQuantity = item.StandardQuantity,
                    ActualQuantity = item.ActualQuantity,
                    Reason = item.Reason ?? "", // Tránh null
                    CreatedDate = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();
        }

        public async Task<bool> IsCheckedInTodayAsync(int employeeId)
        {
            var today = DateTime.Today;

            // Tìm xem có ca nào đang trạng thái "Đang làm việc" không
            return await _context.WorkSchedules
                .AnyAsync(ws => ws.EmployeeId == employeeId
                                && ws.WorkDate == today
                                && ws.CheckInTime != null     // Đã vào
                                && ws.CheckOutTime == null    // Chưa ra
                                && !ws.IsDeleted);
        }

        public async Task<WorkSchedule?> GetTodayScheduleAsync(int employeeId)
        {
            return await GetSmartScheduleAsync(employeeId);
        }

        public async Task<string> PerformAttendanceAsync(int employeeId, string clientIp)
        {
            // 1. Kiểm tra IP (Logic whitelist)
            var allowedIps = _configuration["AttendanceSettings:AllowedIPs"]?.Split(';') ?? Array.Empty<string>();

            // Map localhost về IP chuẩn nếu cần
            if (clientIp == "::1") clientIp = "127.0.0.1";

            // Nếu clientIp là localhost (::1) thì phải xử lý chút để so sánh
            if (!allowedIps.Contains(clientIp))
            {
                return $"IP của bạn ({clientIp}) không hợp lệ. Vui lòng kết nối Wi-Fi Spa!";
            }

            // B. Lấy lịch thông minh
            var schedule = await GetSmartScheduleAsync(employeeId);

            if (schedule == null) return "Hôm nay bạn không có lịch làm việc, hoặc đã hết ca!";

            // C. Logic Check-in / Check-out
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
            return null; // Thành công
        }

        public async Task<TechnicianProfileVM?> GetTechnicianProfileAsync(int employeeId)
        {
            // 1. Lấy thông tin cơ bản (Join bảng Employee & User)
            var emp = await _context.Employees
                .Include(e => e.ApplicationUser) // Để lấy Email, Phone
                .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);

            if (emp == null) return null;

            // 2. Tính toán thống kê THÁNG HIỆN TẠI
            var now = DateTime.Now;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1);
            // Mốc cho HÔM NAY (Mới)
            var startOfToday = DateTime.Today; // 00:00 hôm nay
            var endOfToday = startOfToday.AddDays(1); // 00:00 hôm sau

            // a. Tổng Tip & Số khách (Dựa trên AppointmentDetails đã Completed)
            var monthJobs = await _context.AppointmentDetails
                .Where(ad => ad.TechnicianId == employeeId
                             && ad.Status == "Completed"
                             && ad.Appointment.StartTime >= startOfMonth
                             && ad.Appointment.StartTime < endOfMonth)
                .Select(ad => new { ad.TipAmount, ad.Appointment.StartTime }) // Chỉ lấy cột cần thiết
                .ToListAsync();

            int customerCount = monthJobs.Count;
            decimal totalMonthTip = monthJobs.Sum(x => x.TipAmount);

            // Tính Tip HÔM NAY (Lọc từ list monthJobs ra cho nhanh, đỡ phải query DB lần nữa)
            // Logic: Lấy những job trong list tháng mà có StartTime nằm trong khoảng Hôm nay
            decimal totalTodayTip = monthJobs
                .Where(x => x.StartTime >= startOfToday && x.StartTime < endOfToday)
                .Sum(x => x.TipAmount);

            // b. Số ngày công & Giờ làm (Dựa trên WorkSchedules)
            var schedules = await _context.WorkSchedules
                .Where(ws => ws.EmployeeId == employeeId
                             && ws.WorkDate >= startOfMonth
                             && ws.WorkDate < endOfMonth
                             && ws.CheckInTime != null
                             && ws.CheckOutTime != null
                             && !ws.IsDeleted)
                .ToListAsync();

            int workDays = schedules.Count;

            // Tính tổng giờ làm (CheckOut - CheckIn)
            double totalHours = 0;
            foreach (var schedule in schedules)
            {
                if (schedule.CheckInTime.HasValue && schedule.CheckOutTime.HasValue)
                {
                    var duration = schedule.CheckOutTime.Value - schedule.CheckInTime.Value;
                    totalHours += duration.TotalHours;
                }
            }

            // 3. Đóng gói dữ liệu trả về
            return new TechnicianProfileVM
            {
                EmployeeId = emp.EmployeeId,
                FullName = emp.FullName,
                Email = emp.ApplicationUser?.Email ?? "Chưa cập nhật",
                PhoneNumber = emp.ApplicationUser?.PhoneNumber ?? "Chưa cập nhật",
                Address = emp.Address ?? "Chưa cập nhật",
                HireDate = emp.HireDate,
                BaseSalary = emp.BaseSalary,
                IsActive = emp.IsActive,
                AvatarUrl = "https://ui-avatars.com/api/?name=" + Uri.EscapeDataString(emp.FullName) + "&background=random&size=256", // Tạo avatar chữ cái

                ServedCustomers = customerCount,
                TotalTip = totalMonthTip, // Tip Tháng
                TodayTip = totalTodayTip, // <--- GÁN GIÁ TRỊ MỚI VÀO ĐÂY
                WorkDays = workDays,
                TotalWorkHours = Math.Round(totalHours, 1) // Làm tròn 1 số thập phân
            };
        }

        // === [HÀM PHỤ TRỢ] LOGIC TÌM CA THEO GIỜ ===
        private async Task<WorkSchedule?> GetSmartScheduleAsync(int employeeId)
        {
            var today = DateTime.Today;
            var now = DateTime.Now.TimeOfDay; // Giờ hiện tại (VD: 19:30)

            // 1. Lấy tất cả các ca trong ngày của nhân viên
            var schedules = await _context.WorkSchedules
                .Include(ws => ws.Shift)
                .Where(ws => ws.EmployeeId == employeeId
                             && ws.WorkDate == today
                             && !ws.IsDeleted)
                .ToListAsync();

            if (!schedules.Any()) return null;

            // 2. ƯU TIÊN 1: Tìm ca đang diễn ra (Start <= Now <= End)
            // VD: Bây giờ 19h -> Chọn Ca Tối (17h-21h)
            var activeShift = schedules.FirstOrDefault(s =>
                s.Shift != null && s.Shift.StartTime <= now && s.Shift.EndTime >= now);

            if (activeShift != null) return activeShift;

            // 3. ƯU TIÊN 2: Tìm ca đang làm dở (đã Check-in nhưng chưa Check-out)
            var pendingShift = schedules.FirstOrDefault(s => s.CheckInTime != null && s.CheckOutTime == null);
            if (pendingShift != null) return pendingShift;

            // 4. ƯU TIÊN 3: Ca sắp diễn ra gần nhất (Check-in sớm)
            var upcomingShift = schedules
                .Where(s => s.CheckInTime == null && s.Shift?.StartTime > now)
                .MinBy(s => s.Shift?.StartTime);

            if (upcomingShift != null) return upcomingShift;

            // 5. CÙNG ĐƯỜNG: Lấy ca chưa làm bất kỳ cái nào
            return schedules.FirstOrDefault(s => s.CheckInTime == null) ?? schedules.LastOrDefault();
        }
    }
}
