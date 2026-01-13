using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SpaBookingWeb.Data;
using SpaBookingWeb.Models;
using SpaBookingWeb.Services.Manager;
using SpaBookingWeb.ViewModels.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text;

namespace SpaBookingWeb.Services.Client
{
    public class BookingService : IBookingService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ISystemSettingService _systemSettingService;
        private readonly IEmailService _emailService;
        private readonly ILogger<BookingService> _logger;

        public BookingService(ApplicationDbContext context,
                              IHttpContextAccessor httpContextAccessor,
                              ISystemSettingService systemSettingService,
                              IEmailService emailService,
                              ILogger<BookingService> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _systemSettingService = systemSettingService;
            _emailService = emailService;
            _logger = logger;
        }

        // --- SESSION ---
        private ISession Session => _httpContextAccessor.HttpContext?.Session ?? throw new InvalidOperationException("Session is not available");

        public BookingSessionModel GetSession()
        {
            var json = Session.GetString("BookingSession");
            return json == null ? null : JsonConvert.DeserializeObject<BookingSessionModel>(json);
        }

        public void SaveSession(BookingSessionModel session)
        {
            var json = JsonConvert.SerializeObject(session);
            Session.SetString("BookingSession", json);
        }

        public void ClearSession()
        {
            Session.Remove("BookingSession");
        }

        // --- DATA PROVIDERS ---
         public async Task<BookingPageViewModel> GetBookingPageDataAsync()
        {
            var model = new BookingPageViewModel();
            
            // 1. Lấy Dịch vụ
            var services = await _context.Services
                .Include(s => s.Category)
                .Include(s => s.ServiceConsumables).ThenInclude(sc => sc.Product).ThenInclude(p => p.Unit)
                .Where(s => s.IsActive && !s.IsDeleted)
                .ToListAsync();

            var serviceGroups = services.GroupBy(s => s.Category?.CategoryName ?? "Khác")
                .Select(g => new ServiceCategoryGroupViewModel 
                { 
                    CategoryName = g.Key, 
                    Services = g.Select(s => new ServiceItemViewModel 
                    { 
                        Id = s.ServiceId, // ID DƯƠNG
                        Name = s.ServiceName, 
                        Description = s.Description, 
                        Price = s.Price, 
                        DurationMinutes = s.DurationMinutes, 
                        Type = "Service",
                        Consumables = s.ServiceConsumables.Where(sc => !sc.IsDeleted).Select(sc => $"{sc.Product.ProductName} ({sc.Quantity} {sc.Product.Unit?.UnitName ?? ""})").ToList()
                    }).ToList() 
                }).ToList();

            // 2. Lấy Combo và biến đổi thành "ServiceItem" với ID Âm
            var combos = await _context.Combos
                .Include(c => c.ComboDetails)
                    .ThenInclude(cd => cd.Service)
                        .ThenInclude(s => s.ServiceConsumables)
                            .ThenInclude(sc => sc.Product)
                                .ThenInclude(p => p.Unit)
                .Where(c => !c.IsDeleted)
                .ToListAsync();

            if (combos.Any())
            {
                var comboItems = combos.Select(c => 
                {
                    var descriptionBuilder = new StringBuilder();
                    var allConsumables = new List<string>();
                    // [MỚI] Danh sách dịch vụ con để hiển thị tách biệt ở Step 3
                    var childServicesList = new List<ServiceItemViewModel>();

                    if (!string.IsNullOrEmpty(c.Description))
                    {
                        descriptionBuilder.AppendLine(c.Description);
                    }

                    descriptionBuilder.AppendLine("Gói bao gồm:");
                    foreach (var cd in c.ComboDetails)
                    {
                        descriptionBuilder.AppendLine($"- {cd.Service.ServiceName} ({cd.Service.DurationMinutes}p)");
                        
                        // Gom sản phẩm cho mô tả
                        foreach(var sc in cd.Service.ServiceConsumables.Where(x => !x.IsDeleted))
                        {
                            allConsumables.Add($"{sc.Product.ProductName} ({sc.Quantity} {sc.Product.Unit?.UnitName ?? ""}) - [Trong {cd.Service.ServiceName}]");
                        }

                        // [MỚI] Thêm vào danh sách ChildServices
                        childServicesList.Add(new ServiceItemViewModel
                        {
                            Id = cd.Service.ServiceId, // ID DƯƠNG của Service con
                            Name = cd.Service.ServiceName,
                            DurationMinutes = cd.Service.DurationMinutes,
                            Price = 0, // Giá hiển thị là 0 vì đã tính trong gói Combo
                            Type = "ServiceInCombo",
                            Description = "Nằm trong gói combo"
                        });
                    }

                    return new ServiceItemViewModel
                    {
                        Id = -c.ComboId, // ID ÂM để phân biệt
                        Name = $"[Combo] {c.ComboName}",
                        Description = descriptionBuilder.ToString(),
                        Price = c.Price, 
                        DurationMinutes = c.ComboDetails.Sum(cd => cd.Service.DurationMinutes),
                        Type = "Combo",
                        Consumables = allConsumables,
                        // [QUAN TRỌNG] Gán danh sách con vào đây
                        ChildServices = childServicesList 
                    };
                }).ToList();

                serviceGroups.Insert(0, new ServiceCategoryGroupViewModel
                {
                    CategoryName = "Gói Combo Siêu Tiết Kiệm",
                    Services = comboItems
                });
            }

            model.ServiceCategories = serviceGroups;
            model.Staffs = await _context.Employees.Where(e => e.IsActive && !e.IsDeleted).Select(e => new StaffViewModel { Id = e.EmployeeId, Name = e.FullName, Role = "Kỹ thuật viên", Avatar = e.Avatar ?? "/img/default-avatar.png" }).ToListAsync();
            
            return model;
        }

        // --- LOGIC TÌM GIỜ TRỐNG THÔNG MINH ---
        public async Task<List<string>> GetAvailableTimeSlotsAsync(DateTime date, BookingSessionModel session)
        {
            var settings = await _systemSettingService.GetCurrentSettingsAsync();
            var openTime = settings.OpenTime;
            var closeTime = settings.CloseTime;
            var currentTime = DateTime.Now;
            bool isToday = date.Date == currentTime.Date;

            // [MỚI] Lấy danh sách khung giờ bận của KHÁCH HÀNG (Nếu đã đăng nhập)
            var customerBusyIntervals = new List<(TimeSpan Start, TimeSpan End)>();
            if (_httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true)
            {
                var userEmail = _httpContextAccessor.HttpContext.User.Identity.Name;
                var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Email == userEmail && !c.IsDeleted);
                
                if (customer != null)
                {
                    // Lấy các lịch hẹn khác của khách trong ngày (trừ chính nó nếu đang edit/repay - tuy nhiên ở đây session chưa có ID nếu tạo mới)
                    // Ở đây giả sử tạo mới hoàn toàn hoặc rebook tạo mới
                    var existingApps = await _context.Appointments
                        .Where(a => a.CustomerId == customer.CustomerId 
                                    && a.StartTime.Date == date.Date 
                                    && a.Status != "Cancelled" 
                                    && !a.IsDeleted)
                        .Select(a => new { a.StartTime, a.EndTime })
                        .ToListAsync();

                    foreach(var app in existingApps)
                    {
                        if(app.EndTime.HasValue)
                            customerBusyIntervals.Add((app.StartTime.TimeOfDay, app.EndTime.Value.TimeOfDay));
                    }
                }
            }
            
            // Logic tính tổng thời gian phiên (xử lý ID âm)
            int sessionMaxDuration = 0;
            foreach (var member in session.Members)
            {
                int memberDuration = 0;
                foreach (var id in member.SelectedServiceIds)
                {
                    if (id > 0) // Dịch vụ
                    {
                        var d = await _context.Services.Where(s => s.ServiceId == id).Select(s => s.DurationMinutes).FirstOrDefaultAsync();
                        memberDuration += d;
                    }
                    else // Combo (ID âm)
                    {
                        var comboId = -id;
                        var d = await _context.Combos.Where(c => c.ComboId == comboId)
                                       .Select(c => c.ComboDetails.Sum(cd => cd.Service.DurationMinutes))
                                       .FirstOrDefaultAsync();
                        memberDuration += d;
                    }
                }
                if (memberDuration > sessionMaxDuration) sessionMaxDuration = memberDuration;
            }

            // [MỚI] Lấy dữ liệu Lịch làm việc & Lịch bận của NHÂN VIÊN
            var workingStaffIds = await _context.WorkSchedules
                .Where(ws => ws.WorkDate.Date == date.Date && !ws.IsDeleted)
                .Select(ws => ws.EmployeeId)
                .ToListAsync();

            var staffBusyIntervals = new Dictionary<int, List<(TimeSpan Start, TimeSpan End)>>();
            foreach (var staffId in workingStaffIds) staffBusyIntervals[staffId] = new List<(TimeSpan, TimeSpan)>();

            var staffAppointments = await _context.Appointments
                .Include(a => a.AppointmentDetails)
                .Where(a => a.StartTime.Date == date.Date && a.Status != "Cancelled" && !a.IsDeleted)
                .ToListAsync();

            // Build bản đồ bận của nhân viên
            foreach (var appt in staffAppointments)
            {
                var currentStart = appt.StartTime.TimeOfDay;
                foreach (var detail in appt.AppointmentDetails.OrderBy(d => d.AppointmentDetailId))
                {
                    int duration = 0;
                    if (detail.ServiceId.HasValue)
                    {
                        duration = await _context.Services.Where(s => s.ServiceId == detail.ServiceId).Select(s => s.DurationMinutes).FirstOrDefaultAsync();
                    }
                    else if (detail.ComboId.HasValue)
                    {
                         duration = await _context.Combos.Where(c => c.ComboId == detail.ComboId)
                                       .Select(c => c.ComboDetails.Sum(cd => cd.Service.DurationMinutes))
                                       .FirstOrDefaultAsync();
                    }

                    var currentEnd = currentStart.Add(TimeSpan.FromMinutes(duration));

                    if (detail.TechnicianId.HasValue && staffBusyIntervals.ContainsKey(detail.TechnicianId.Value))
                    {
                        staffBusyIntervals[detail.TechnicianId.Value].Add((currentStart, currentEnd));
                    }
                    currentStart = currentEnd;
                }
            }

            // Kỹ năng nhân viên (để biết ai làm được dịch vụ nào)
            // Lưu ý: Combo thì cần check kỹ năng cho từng service con, ở đây simplified check service lẻ
            var technicianSkills = await _context.TechnicianServices
                .Where(ts => !ts.IsDeleted)
                .Select(ts => new { ts.EmployeeId, ts.ServiceId })
                .ToListAsync();

            // Logic tìm giờ trống 
            var availableSlots = new List<string>();
            var slotDuration = TimeSpan.FromMinutes(15);
            for (var time = openTime; time < closeTime; time = time.Add(slotDuration))
            {
                if (isToday && time < currentTime.TimeOfDay.Add(TimeSpan.FromMinutes(30))) continue;
                
                var estimatedEndTime = time.Add(TimeSpan.FromMinutes(sessionMaxDuration));

                // Check 1: Khách bận?
                if (IsOverlapping(time, estimatedEndTime, customerBusyIntervals)) continue;

                // Check 2: Quá giờ đóng cửa?
                if (estimatedEndTime > closeTime) continue;

                // Check 3: Staff rảnh? (Sử dụng hàm helper đã có ở phiên bản trước, cập nhật cho ID âm)
                // Để đơn giản hóa trong context file này mà vẫn giữ logic cũ, ta giả định check staff ở đây
                // Nếu muốn tích hợp logic check staff chi tiết, cần cập nhật IsSessionFitAsync để handle ID âm
                if (await IsSessionFitAsync(time, session, staffBusyIntervals, technicianSkills, closeTime))
                {
                    availableSlots.Add(time.ToString(@"hh\:mm"));
                }
            }
            return availableSlots;
        }

        private async Task<bool> IsSessionFitAsync(
            TimeSpan startTime, 
            BookingSessionModel session, 
            Dictionary<int, List<(TimeSpan Start, TimeSpan End)>> staffBusyMap,
            dynamic technicianSkills)
        {
            // Copy map để mô phỏng (tránh sửa dữ liệu gốc)
            var tempBusyMap = new Dictionary<int, List<(TimeSpan Start, TimeSpan End)>>();
            foreach(var kvp in staffBusyMap) tempBusyMap[kvp.Key] = new List<(TimeSpan, TimeSpan)>(kvp.Value);

            foreach (var member in session.Members)
            {
                var memberCurrentTime = startTime;

                foreach (var serviceId in member.SelectedServiceIds)
                {
                    var duration = await _context.Services.Where(s => s.ServiceId == serviceId).Select(s => s.DurationMinutes).FirstOrDefaultAsync();
                    var serviceEndTime = memberCurrentTime.Add(TimeSpan.FromMinutes(duration));

                    int? requiredStaffId = null;
                    if (member.ServiceStaffMap != null && member.ServiceStaffMap.ContainsKey(serviceId))
                    {
                        requiredStaffId = member.ServiceStaffMap[serviceId];
                    }

                    bool foundStaff = false;
                    if (requiredStaffId.HasValue) // Chọn đích danh
                    {
                        if (IsStaffAvailable(requiredStaffId.Value, memberCurrentTime, serviceEndTime, tempBusyMap))
                        {
                            tempBusyMap[requiredStaffId.Value].Add((memberCurrentTime, serviceEndTime));
                            foundStaff = true;
                        }
                    }
                    else // Chọn ngẫu nhiên (tìm người rảnh có kỹ năng)
                    {
                        var skilledStaffIds = new List<int>();
                        // Lọc nhân viên có kỹ năng (chuyển đổi dynamic)
                        var skillsList = (IEnumerable<dynamic>)technicianSkills;
                        foreach(var item in skillsList)
                        {
                            if(item.ServiceId == serviceId) skilledStaffIds.Add(item.EmployeeId);
                        }

                        foreach (var staffId in skilledStaffIds)
                        {
                            if (IsStaffAvailable(staffId, memberCurrentTime, serviceEndTime, tempBusyMap))
                            {
                                tempBusyMap[staffId].Add((memberCurrentTime, serviceEndTime));
                                foundStaff = true;
                                break;
                            }
                        }
                    }

                    if (!foundStaff) return false; // Không có nhân viên -> Slot fail
                    memberCurrentTime = serviceEndTime;
                }
            }
            return true;
        }

        private bool IsOverlapping(TimeSpan start, TimeSpan end, List<(TimeSpan Start, TimeSpan End)> busyIntervals)
        {
            foreach (var interval in busyIntervals)
            {
                // Giao nhau khi: (StartA < EndB) và (EndA > StartB)
                if (start < interval.End && end > interval.Start)
                {
                    return true;
                }
            }
            return false;
        }

        // Helper: Kiểm tra xem toàn bộ Session có thể bắt đầu tại thời điểm 'startTime' không
       private async Task<bool> IsSessionFitAsync(
            TimeSpan startTime, 
            BookingSessionModel session, 
            Dictionary<int, List<(TimeSpan Start, TimeSpan End)>> staffBusyMap,
            dynamic technicianSkills,
            TimeSpan shopCloseTime)
        {
            var tempBusyMap = new Dictionary<int, List<(TimeSpan Start, TimeSpan End)>>();
            foreach(var kvp in staffBusyMap) tempBusyMap[kvp.Key] = new List<(TimeSpan, TimeSpan)>(kvp.Value);

            foreach (var member in session.Members)
            {
                var memberCurrentTime = startTime;

                foreach (var id in member.SelectedServiceIds)
                {
                    int duration = 0;
                    if (id > 0)
                    {
                        duration = await _context.Services.Where(s => s.ServiceId == id).Select(s => s.DurationMinutes).FirstOrDefaultAsync();
                    }
                    else
                    {
                        var comboId = -id;
                        duration = await _context.Combos.Where(c => c.ComboId == comboId).Select(c => c.ComboDetails.Sum(cd => cd.Service.DurationMinutes)).FirstOrDefaultAsync();
                    }

                    var serviceEndTime = memberCurrentTime.Add(TimeSpan.FromMinutes(duration));
                    if (serviceEndTime > shopCloseTime) return false;

                    // Logic check nhân viên cho Combo hơi phức tạp vì combo gồm nhiều service con
                    // Ở đây ta đơn giản hóa: Coi như 1 nhân viên làm hết combo hoặc skip check kỹ năng chi tiết cho combo
                    // Nếu id > 0 (Dịch vụ lẻ), check bình thường
                    
                    if (id > 0)
                    {
                        int? requiredStaffId = (member.ServiceStaffMap != null && member.ServiceStaffMap.ContainsKey(id)) ? member.ServiceStaffMap[id] : null;
                        bool foundStaff = false;

                        if (requiredStaffId.HasValue)
                        {
                            if (IsStaffAvailable(requiredStaffId.Value, memberCurrentTime, serviceEndTime, tempBusyMap))
                            {
                                tempBusyMap[requiredStaffId.Value].Add((memberCurrentTime, serviceEndTime));
                                foundStaff = true;
                            }
                        }
                        else
                        {
                            // Tìm list nhân viên có kỹ năng
                            var skilledStaffIds = new List<int>();
                            var skillsList = (IEnumerable<dynamic>)technicianSkills;
                            foreach(var item in skillsList) { if(item.ServiceId == id) skilledStaffIds.Add(item.EmployeeId); }

                            foreach (var staffId in skilledStaffIds)
                            {
                                if (IsStaffAvailable(staffId, memberCurrentTime, serviceEndTime, tempBusyMap))
                                {
                                    tempBusyMap[staffId].Add((memberCurrentTime, serviceEndTime));
                                    foundStaff = true;
                                    break;
                                }
                            }
                        }
                        if (!foundStaff) return false;
                    }
                    else 
                    {
                        // Với Combo, tạm thời chỉ check xem có nhân viên nào rảnh trong khoảng thời gian đó không (bỏ qua skill check từng món)
                        // Hoặc gán ngẫu nhiên 1 người rảnh
                        bool foundStaffForCombo = false;
                        foreach(var staffId in tempBusyMap.Keys)
                        {
                             if (IsStaffAvailable(staffId, memberCurrentTime, serviceEndTime, tempBusyMap))
                            {
                                tempBusyMap[staffId].Add((memberCurrentTime, serviceEndTime));
                                foundStaffForCombo = true;
                                break;
                            }
                        }
                        if(!foundStaffForCombo) return false;
                    }

                    memberCurrentTime = serviceEndTime;
                }
            }
            return true;
        }

        private bool IsStaffAvailable(int staffId, TimeSpan start, TimeSpan end, Dictionary<int, List<(TimeSpan Start, TimeSpan End)>> busyMap)
        {
            if (!busyMap.ContainsKey(staffId)) return false; // Nhân viên không đi làm hôm nay

            foreach (var interval in busyMap[staffId])
            {
                // Kiểm tra giao nhau (Overlap): (StartA < EndB) and (EndA > StartB)
                if (start < interval.End && end > interval.Start)
                {
                    return false; // Bị trùng
                }
            }
            return true;
        }

        // --- CÁC PHƯƠNG THỨC KHÁC GIỮ NGUYÊN ---
        public async Task<int> SaveBookingAsync(BookingSessionModel session)
        {
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.PhoneNumber == session.CustomerInfo.Phone);
            if (customer == null)
            {
                customer = new Customer { FullName = session.CustomerInfo.FullName, PhoneNumber = session.CustomerInfo.Phone, Email = session.CustomerInfo.Email };
                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();
            }

            // Tính lại EndTime
            int maxDuration = 0;
            foreach (var member in session.Members)
            {
                int d = 0;
                foreach(var id in member.SelectedServiceIds)
                {
                    if(id > 0) d += await _context.Services.Where(s=>s.ServiceId==id).Select(s=>s.DurationMinutes).FirstOrDefaultAsync();
                    else d += await _context.Combos.Where(c=>c.ComboId==-id).Select(c=>c.ComboDetails.Sum(cd=>cd.Service.DurationMinutes)).FirstOrDefaultAsync();
                }
                if(d > maxDuration) maxDuration = d;
            }
            
            var appointmentStartTime = session.SelectedDate.Value.Add(session.SelectedTime.Value);
            var appointment = new Appointment
            {
                CustomerId = customer.CustomerId,
                StartTime = appointmentStartTime,
                EndTime = appointmentStartTime.AddMinutes(maxDuration),
                Status = "Pending",
                Notes = session.CustomerInfo.Note,
                DepositAmount = session.DepositAmount,
                CreatedDate = DateTime.Now
            };
            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            // Lưu Chi tiết
            foreach (var member in session.Members)
            {
                var currentServiceStartTime = appointmentStartTime;

                foreach (var id in member.SelectedServiceIds)
                {
                    if (id > 0) // Dịch vụ lẻ
                    {
                        var service = await _context.Services.FindAsync(id);
                        if (service != null)
                        {
                            int? staffId = (member.ServiceStaffMap != null && member.ServiceStaffMap.ContainsKey(id)) ? member.ServiceStaffMap[id] : null;
                            if (staffId == null) staffId = await AutoAssignStaffAsync(id, currentServiceStartTime, service.DurationMinutes);

                            _context.AppointmentDetails.Add(new AppointmentDetail
                            {
                                AppointmentId = appointment.AppointmentId,
                                ServiceId = id,
                                TechnicianId = staffId,
                                PriceAtBooking = service.Price,
                                Status = "Pending"
                            });
                            currentServiceStartTime = currentServiceStartTime.AddMinutes(service.DurationMinutes);
                        }
                    }
                    else // Combo (ID Âm)
                    {
                        var comboId = -id;
                        var combo = await _context.Combos
                             .Include(c => c.ComboDetails).ThenInclude(cd => cd.Service)
                             .FirstOrDefaultAsync(c => c.ComboId == comboId);

                        if (combo != null && combo.ComboDetails.Any())
                        {
                            // Tách Combo thành các dòng Service con nhưng vẫn link về ComboId
                            // Tính toán giá phân bổ (để tổng giá con = giá combo)
                            decimal totalOriginal = combo.ComboDetails.Sum(x => x.Service.Price);
                            decimal ratio = totalOriginal > 0 ? combo.Price / totalOriginal : 1;
                            decimal currentTotalSaved = 0;
                            var detailsList = combo.ComboDetails.ToList();

                            for(int i = 0; i < detailsList.Count; i++)
                            {
                                var cd = detailsList[i];
                                var subDetail = new AppointmentDetail
                                {
                                    AppointmentId = appointment.AppointmentId,
                                    ServiceId = cd.ServiceId,
                                    ComboId = combo.ComboId, // Đánh dấu thuộc Combo này
                                    Status = "Pending"
                                };
                                
                                // Phân bổ giá
                                if (totalOriginal > 0)
                                {
                                    if (i == detailsList.Count - 1) // Item cuối chịu phần dư
                                        subDetail.PriceAtBooking = combo.Price - currentTotalSaved;
                                    else
                                        subDetail.PriceAtBooking = Math.Round(cd.Service.Price * ratio, 0); // Làm tròn 0 số lẻ cho đẹp tiền Việt
                                }
                                else
                                {
                                     subDetail.PriceAtBooking = (i==0) ? combo.Price : 0;
                                }
                                currentTotalSaved += subDetail.PriceAtBooking;

                                // Gán KTV: Tìm trong map với Key là ServiceId (của dịch vụ con)
                                // Lưu ý: Ở Step 3 ta sẽ lưu staff cho các service con vào ServiceStaffMap
                                int? staffId = null;
                                if (member.ServiceStaffMap != null && member.ServiceStaffMap.ContainsKey(cd.ServiceId))
                                {
                                    staffId = member.ServiceStaffMap[cd.ServiceId];
                                }
                                
                                if (staffId == null) 
                                    staffId = await AutoAssignStaffAsync(cd.ServiceId, currentServiceStartTime, cd.Service.DurationMinutes);

                                subDetail.TechnicianId = staffId;
                                _context.AppointmentDetails.Add(subDetail);
                                
                                currentServiceStartTime = currentServiceStartTime.AddMinutes(cd.Service.DurationMinutes);
                            }
                        }
                    }
                }
            }

            var invoice = new Invoice
            {
                AppointmentId = appointment.AppointmentId,
                TotalAmount = session.TotalAmount,
                DepositDeduction = session.DepositAmount,
                FinalAmount = session.TotalAmount - session.DepositAmount,
                PaymentStatus = "Unpaid",
                CreatedDate = DateTime.Now
            };
            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();

            return appointment.AppointmentId;
        }

        private async Task<int?> AutoAssignStaffAsync(int serviceId, DateTime startTime, int durationMinutes)
        {
            var endTime = startTime.AddMinutes(durationMinutes);
            var qualifiedStaffIds = await _context.TechnicianServices.Where(ts => ts.ServiceId == serviceId && !ts.IsDeleted).Select(ts => ts.EmployeeId).ToListAsync();
            if (!qualifiedStaffIds.Any()) return null;

            var workingStaffIds = await _context.WorkSchedules
                .Where(ws => qualifiedStaffIds.Contains(ws.EmployeeId) && ws.WorkDate.Date == startTime.Date && !ws.IsDeleted)
                .Select(ws => ws.EmployeeId).ToListAsync();
            if (!workingStaffIds.Any()) return null;

            var busyStaffIds = await _context.AppointmentDetails
                .Include(ad => ad.Appointment)
                .Where(ad => workingStaffIds.Contains(ad.TechnicianId.Value) && ad.Status != "Cancelled" && ad.Status != "Completed"
                             && ad.Appointment.StartTime < endTime && ad.Appointment.EndTime > startTime)
                .Select(ad => ad.TechnicianId.Value).ToListAsync();

            var availableCandidates = workingStaffIds.Except(busyStaffIds).ToList();
            if (!availableCandidates.Any()) return null;

            var workloadStats = await _context.AppointmentDetails
                .Include(ad => ad.Appointment)
                .Where(ad => availableCandidates.Contains(ad.TechnicianId.Value) && ad.Appointment.StartTime.Date == startTime.Date)
                .GroupBy(ad => ad.TechnicianId)
                .Select(g => new { StaffId = g.Key, Count = g.Count() }).ToListAsync();

            return availableCandidates.OrderBy(id => workloadStats.FirstOrDefault(w => w.StaffId == id)?.Count ?? 0).ThenBy(x => Guid.NewGuid()).FirstOrDefault();
        }

        public async Task UpdateDepositStatusAsync(int appointmentId, string transactionId)
        {
            // 1. Lấy thông tin Appointment kèm Customer để gửi mail
            var appointment = await _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.AppointmentDetails).ThenInclude(ad => ad.Service) // Lấy chi tiết dịch vụ để hiển thị trong mail
                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId);

            if (appointment != null)
            {
                // Cập nhật trạng thái
                appointment.IsDepositPaid = true;
                appointment.Status = "Confirmed";
                
                var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.AppointmentId == appointmentId);
                if (invoice != null)
                {
                    invoice.PaymentStatus = "DepositPaid";
                    _context.Payments.Add(new Payment 
                    { 
                        InvoiceId = invoice.InvoiceId, 
                        Amount = appointment.DepositAmount, 
                        PaymentMethod = "Momo", 
                        TransactionType = "Deposit", 
                        PaymentDate = DateTime.Now 
                    });
                }
                await _context.SaveChangesAsync();

                // 2. [MỚI] GỬI EMAIL XÁC NHẬN
                if (!string.IsNullOrEmpty(appointment.Customer.Email))
                {
                    try 
                    {
                        string emailSubject = $"[SpaBookingWebsite] Xác nhận đặt lịch thành công #{appointment.AppointmentId}";
                        string emailBody = BuildConfirmationEmailBody(appointment);
                        
                        await _emailService.SendEmailAsync(appointment.Customer.Email, emailSubject, emailBody);
                    } 
                    catch
                    {
                        // Log lỗi gửi mail nhưng không throw exception để tránh rollback giao dịch thanh toán
                        _logger.LogError("Gửi mail thất bại...");
                    }
                }
            }
        }

        // Helper: Tạo nội dung Email HTML đẹp
        private string BuildConfirmationEmailBody(Appointment appointment)
        {
            var sb = new StringBuilder();
            sb.Append($@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e0e0e0; border-radius: 8px; overflow: hidden;'>
                    <div style='background-color: #ec4899; padding: 20px; text-align: center; color: white;'>
                        <h2 style='margin: 0;'>Xác nhận đặt lịch thành công</h2>
                    </div>
                    <div style='padding: 20px;'>
                        <p>Xin chào <strong>{appointment.Customer.FullName}</strong>,</p>
                        <p>Cảm ơn bạn đã lựa chọn dịch vụ tại <strong>SpaBookingWeb</strong>. Lịch hẹn của bạn đã được xác nhận.</p>
                        
                        <div style='background-color: #f9f9f9; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                            <p style='margin: 5px 0;'><strong>Mã lịch hẹn:</strong> #{appointment.AppointmentId}</p>
                            <p style='margin: 5px 0;'><strong>Thời gian:</strong> {appointment.StartTime:HH:mm - dd/MM/yyyy}</p>
                            <p style='margin: 5px 0;'><strong>Số tiền đã cọc:</strong> <span style='color: #ec4899; font-weight: bold;'>{appointment.DepositAmount:N0} đ</span> (Qua MoMo)</p>
                        </div>

                        <h3>Dịch vụ đã đặt:</h3>
                        <ul style='padding-left: 20px;'>");

            foreach (var detail in appointment.AppointmentDetails)
            {
                var serviceName = detail.Service?.ServiceName ?? "Dịch vụ";
                sb.Append($"<li>{serviceName} ({detail.PriceAtBooking:N0} đ)</li>");
            }

            sb.Append($@"
                        </ul>

                        <p style='margin-top: 20px; font-size: 13px; color: #666;'>
                            * Vui lòng đến trước 10 phút để được phục vụ tốt nhất.<br/>
                            * Nếu cần thay đổi lịch, vui lòng liên hệ hotline: 1900-123-456.
                        </p>
                    </div>
                    <div style='background-color: #f1f1f1; padding: 10px; text-align: center; font-size: 12px; color: #888;'>
                        © 2024 Lotus Spa & Salon. All rights reserved.
                    </div>
                </div>");

            return sb.ToString();
        }



        public async Task<AppointmentSuccessViewModel> GetAppointmentSuccessInfoAsync(int appointmentId)
        {
            var app = await _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.AppointmentDetails).ThenInclude(ad => ad.Service)
                .Include(a => a.AppointmentDetails).ThenInclude(ad => ad.Technician)
                .Include(a => a.Invoice)
                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId);

            if (app == null) return null;

            return new AppointmentSuccessViewModel
            {
                AppointmentId = app.AppointmentId,
                CustomerName = app.Customer.FullName,
                TimeSlot = $"{app.StartTime:HH:mm} - {app.StartTime:ddd, dd/MM}",
                Services = app.AppointmentDetails.Select(ad => new ServiceSuccessItem
                {
                    ServiceName = ad.Service?.ServiceName,
                    StaffName = ad.Technician?.FullName ?? "Hệ thống tự chọn",
                    Duration = ad.Service?.DurationMinutes ?? 0,
                    Price = ad.PriceAtBooking
                }).ToList(),
                TotalAmount = app.Invoice.TotalAmount,
                DepositAmount = app.DepositAmount,
                IsDepositPaid = app.IsDepositPaid
            };
        }

        public async Task<List<AppointmentHistoryViewModel>> GetBookingHistoryAsync(string userEmail)
        {
            // 1. Tìm Customer theo Email
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Email == userEmail);
            if (customer == null) return new List<AppointmentHistoryViewModel>();

            // 2. Lấy danh sách Appointment
            var appointments = await _context.Appointments
                .Include(a => a.AppointmentDetails).ThenInclude(ad => ad.Service)
                .Include(a => a.AppointmentDetails).ThenInclude(ad => ad.Technician)
                .Include(a => a.Invoice)
                .Where(a => a.CustomerId == customer.CustomerId)
                .OrderByDescending(a => a.StartTime) // Mới nhất lên đầu
                .ToListAsync();

            // 3. Map sang ViewModel
            var result = appointments.Select(a => new AppointmentHistoryViewModel
            {
                AppointmentId = a.AppointmentId,
                StartTime = a.StartTime,
                EndTime = a.EndTime,
                Status = a.Status, // Pending, Confirmed, Cancelled
                IsDepositPaid = a.IsDepositPaid,
                TotalAmount = a.Invoice != null ? a.Invoice.TotalAmount : 0,
                DepositAmount = a.DepositAmount,
                // Map services
                Services = a.AppointmentDetails.Select(ad => new ServiceDetailViewModel
                {
                    ServiceName = ad.Service?.ServiceName ?? "Dịch vụ",
                    Duration = ad.Service?.DurationMinutes ?? 0,
                    Price = ad.PriceAtBooking,
                    StaffName = ad.Technician?.FullName ?? "Chưa phân công",
                    StaffAvatar = ad.Technician?.Avatar ?? "/img/default-avatar.png"
                }).ToList()
            }).ToList();

            return result;
        }

        public async Task<AppointmentHistoryViewModel> GetAppointmentDetailAsync(int appointmentId)
        {
            var appointment = await _context.Appointments
                .Include(a => a.AppointmentDetails).ThenInclude(ad => ad.Service)
                .Include(a => a.AppointmentDetails).ThenInclude(ad => ad.Technician)
                .Include(a => a.Invoice)
                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId);

            if (appointment == null) return null;

            return new AppointmentHistoryViewModel
            {
                AppointmentId = appointment.AppointmentId,
                StartTime = appointment.StartTime,
                EndTime = appointment.EndTime,
                Status = appointment.Status,
                IsDepositPaid = appointment.IsDepositPaid,
                TotalAmount = appointment.Invoice != null ? appointment.Invoice.TotalAmount : 0,
                DepositAmount = appointment.DepositAmount,
                Services = appointment.AppointmentDetails.Select(ad => new ServiceDetailViewModel
                {
                    ServiceName = ad.Service?.ServiceName ?? "Dịch vụ",
                    Duration = ad.Service?.DurationMinutes ?? 0,
                    Price = ad.PriceAtBooking,
                    StaffName = ad.Technician?.FullName ?? "Chưa phân công",
                    StaffAvatar = ad.Technician?.Avatar ?? "/img/default-avatar.png"
                }).ToList()
            };
        }

        // [MỚI] Lấy lịch sử Đã xong/Hủy
        public async Task<List<AppointmentHistoryViewModel>> GetBookingHistoryArchiveAsync(string userEmail)
        {
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Email == userEmail);
            if (customer == null) return new List<AppointmentHistoryViewModel>();

            var appointments = await _context.Appointments
                .Include(a => a.AppointmentDetails).ThenInclude(ad => ad.Service)
                .Include(a => a.AppointmentDetails).ThenInclude(ad => ad.Technician)
                .Include(a => a.Invoice)
                .Where(a => a.CustomerId == customer.CustomerId
                            && (a.Status == "Completed" || a.Status == "Cancelled")) // Lọc trạng thái
                .OrderByDescending(a => a.StartTime)
                .ToListAsync();

            return appointments.Select(a => new AppointmentHistoryViewModel
            {
                AppointmentId = a.AppointmentId,
                StartTime = a.StartTime,
                EndTime = a.EndTime,
                Status = a.Status,
                TotalAmount = a.Invoice?.TotalAmount ?? 0,
                Services = a.AppointmentDetails.Select(ad => new ServiceDetailViewModel
                {
                    ServiceName = ad.Service?.ServiceName ?? "Dịch vụ",
                    Duration = ad.Service?.DurationMinutes ?? 0,
                    Price = ad.PriceAtBooking,
                    StaffName = ad.Technician?.FullName ?? "Không xác định",
                    StaffAvatar = ad.Technician?.Avatar
                }).ToList()
            }).ToList();
        }

        // [MỚI] Logic Đặt lại (Re-book)
        public async Task<bool> RebookAsync(int appointmentId)
        {
            var oldAppt = await _context.Appointments
                .Include(a => a.AppointmentDetails)
                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId);

            if (oldAppt == null) return false;

            // Tạo session mới từ thông tin cũ
            var session = new BookingSessionModel
            {
                IsGroupBooking = false, // Mặc định về cá nhân
                Members = new List<BookingMember>
                {
                    new BookingMember
                    {
                        MemberIndex = 1,
                        Name = "Tôi",
                        SelectedServiceIds = oldAppt.AppointmentDetails
                                            .Where(d => d.ServiceId.HasValue)
                                            .Select(d => d.ServiceId.Value)
                                            .ToList()
                    }
                }
            };

            // Lưu session và sẵn sàng chuyển hướng sang Step 2
            SaveSession(session);
            return true;
        }
        public async Task<VoucherCheckResult> ValidateVoucherAsync(string code, decimal orderTotal)
        {
            if (string.IsNullOrEmpty(code))
            {
                return new VoucherCheckResult { IsValid = false, Message = "Vui lòng nhập mã giảm giá." };
            }

            var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == code && !v.IsDeleted);

            // 1. Kiểm tra tồn tại và Active
            if (voucher == null || !voucher.IsActive)
            {
                return new VoucherCheckResult { IsValid = false, Message = "Mã giảm giá không tồn tại hoặc hết hạn." };
            }

            // 2. Kiểm tra thời gian
            var now = DateTime.Now;
            if (now < voucher.StartDate || now > voucher.EndDate)
            {
                return new VoucherCheckResult { IsValid = false, Message = "Mã giảm giá chưa bắt đầu hoặc đã hết hạn." };
            }

            // 3. Kiểm tra số lượng
            if (voucher.UsageLimit > 0 && voucher.UsageCount >= voucher.UsageLimit)
            {
                return new VoucherCheckResult { IsValid = false, Message = "Mã giảm giá đã hết lượt sử dụng." };
            }

            // 4. Kiểm tra giá trị đơn hàng tối thiểu
            if (orderTotal < voucher.MinSpend)
            {
                return new VoucherCheckResult
                {
                    IsValid = false,
                    Message = $"Đơn hàng cần tối thiểu {voucher.MinSpend.ToString("N0")}đ để sử dụng mã này."
                };
            }

            // Hợp lệ
            string discountInfo = voucher.DiscountType == "Percent"
                ? $"giảm {voucher.DiscountValue}%"
                : $"giảm {voucher.DiscountValue.ToString("N0")}đ";

            return new VoucherCheckResult
            {
                IsValid = true,
                Message = $"Mã hợp lệ! Bạn sẽ được {discountInfo} cho tổng đơn hàng. Vui lòng cung cấp mã này cho nhân viên tại quầy khi hoàn tất thanh toán.",
                Voucher = voucher
            };
        }

        public async Task<bool> ResumeBookingAsync(int appointmentId)
        {
            var appt = await _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.AppointmentDetails)
                .Include(a => a.Invoice) // Cần Invoice để lấy TotalAmount chính xác
                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId);

            if (appt == null) return false;

            var session = new BookingSessionModel
            {
                // [QUAN TRỌNG] Gán ID cũ để Controller biết không tạo mới
                ExistingAppointmentId = appt.AppointmentId,

                IsGroupBooking = false, 
                SelectedDate = appt.StartTime.Date,
                SelectedTime = appt.StartTime.TimeOfDay,
                CustomerInfo = new CustomerInfo 
                { 
                    FullName = appt.Customer.FullName, 
                    Phone = appt.Customer.PhoneNumber,
                    Email = appt.Customer.Email,
                    Note = appt.Notes
                },
                Members = new List<BookingMember>(),
                TotalAmount = appt.Invoice?.TotalAmount ?? 0, 
                DepositAmount = appt.DepositAmount,
                // Giữ lại % cọc để hiển thị đúng
                DepositPercentage = (appt.Invoice?.TotalAmount > 0) ? (int)((appt.DepositAmount / appt.Invoice.TotalAmount) * 100) : 20 
            };

            var member = new BookingMember 
            { 
                MemberIndex = 1, 
                Name = "Tôi", 
                SelectedServiceIds = new List<int>(),
                ServiceStaffMap = new Dictionary<int, int?>()
            };

            foreach (var detail in appt.AppointmentDetails)
            {
                if (detail.ServiceId.HasValue) 
                {
                    member.SelectedServiceIds.Add(detail.ServiceId.Value);
                    if (detail.TechnicianId.HasValue)
                    {
                        member.ServiceStaffMap[detail.ServiceId.Value] = detail.TechnicianId.Value;
                    }
                }
            }
            session.Members.Add(member);

            SaveSession(session);
            return true;
        }
    }
}