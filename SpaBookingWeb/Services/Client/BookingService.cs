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
using System.Threading.Tasks;

namespace SpaBookingWeb.Services.Client
{
    public class BookingService : IBookingService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ISystemSettingService _systemSettingService;

        public BookingService(ApplicationDbContext context,
                              IHttpContextAccessor httpContextAccessor,
                              ISystemSettingService systemSettingService)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _systemSettingService = systemSettingService;
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
            var services = await _context.Services.Include(s => s.Category).Where(s => s.IsActive && !s.IsDeleted).ToListAsync();
            model.ServiceCategories = services.GroupBy(s => s.Category?.CategoryName ?? "Khác").Select(g => new ServiceCategoryGroupViewModel { CategoryName = g.Key, Services = g.Select(s => new ServiceItemViewModel { Id = s.ServiceId, Name = s.ServiceName, Description = s.Description, Price = s.Price, DurationMinutes = s.DurationMinutes, Type = "Service" }).ToList() }).ToList();
            model.Staffs = await _context.Employees.Where(e => e.IsActive && !e.IsDeleted).Select(e => new StaffViewModel { Id = e.EmployeeId, Name = e.FullName, Role = "Kỹ thuật viên", Avatar = e.Avatar ?? "/img/default-avatar.png" }).ToListAsync();
            return model;
        }

        // --- LOGIC TÌM GIỜ TRỐNG THÔNG MINH ---
        public async Task<List<string>> GetAvailableTimeSlotsAsync(DateTime date, BookingSessionModel session)
        {
            // 1. Lấy cấu hình giờ mở cửa
            var settings = await _systemSettingService.GetCurrentSettingsAsync();
            var openTime = settings.OpenTime;
            var closeTime = settings.CloseTime;
            var currentTime = DateTime.Now;
            bool isToday = date.Date == currentTime.Date;

            // 2. Lấy dữ liệu cần thiết từ DB cho ngày đã chọn
            // - Lấy tất cả nhân viên có lịch làm việc (WorkSchedule) trong ngày
            var workingStaffIds = await _context.WorkSchedules
                .Where(ws => ws.WorkDate.Date == date.Date && !ws.IsDeleted)
                .Select(ws => ws.EmployeeId)
                .ToListAsync();

            // - Lấy tất cả các cuộc hẹn đã có trong ngày (để biết ai bận giờ nào)
            //   Lưu ý: Cần lấy cả chi tiết để biết thời gian cụ thể của từng task nếu muốn chính xác cao
            //   Ở đây ta dùng logic đơn giản: Lấy Appointment và các Detail của nó
            var existingAppointments = await _context.Appointments
                .Include(a => a.AppointmentDetails)
                .Where(a => a.StartTime.Date == date.Date && a.Status != "Cancelled" && !a.IsDeleted)
                .ToListAsync();

            // - Lấy kỹ năng của nhân viên (Ai làm được dịch vụ nào)
            var technicianSkills = await _context.TechnicianServices
                .Where(ts => !ts.IsDeleted)
                .Select(ts => new { ts.EmployeeId, ts.ServiceId })
                .ToListAsync();

            // 3. Xây dựng bản đồ bận (Busy Intervals) cho từng nhân viên
            var staffBusyIntervals = new Dictionary<int, List<(TimeSpan Start, TimeSpan End)>>();
            
            // Khởi tạo list cho những nhân viên có đi làm
            foreach (var staffId in workingStaffIds) staffBusyIntervals[staffId] = new List<(TimeSpan, TimeSpan)>();

            foreach (var appt in existingAppointments)
            {
                var apptStart = appt.StartTime.TimeOfDay;
                
                // Giả định: Các dịch vụ trong 1 appointment diễn ra nối tiếp nhau
                // Ta cần reconstruct lại timeline của appointment đó
                var currentTaskStart = apptStart;
                
                foreach (var detail in appt.AppointmentDetails.OrderBy(d => d.AppointmentDetailId)) // Sắp xếp để giữ thứ tự
                {
                    // Lấy duration từ service gốc (hoặc lưu trong detail nếu có)
                    // Vì Detail chưa lưu Duration, ta query tạm hoặc giả định đã join (cần tối ưu sau)
                    // Ở đây để nhanh, ta truy vấn duration từ DB nếu chưa có
                    var serviceDuration = await _context.Services
                        .Where(s => s.ServiceId == detail.ServiceId)
                        .Select(s => s.DurationMinutes)
                        .FirstOrDefaultAsync();

                    var currentTaskEnd = currentTaskStart.Add(TimeSpan.FromMinutes(serviceDuration));

                    if (detail.TechnicianId.HasValue && staffBusyIntervals.ContainsKey(detail.TechnicianId.Value))
                    {
                        staffBusyIntervals[detail.TechnicianId.Value].Add((currentTaskStart, currentTaskEnd));
                    }

                    currentTaskStart = currentTaskEnd; // Dịch chuyển thời gian cho task sau
                }
            }

            // 4. Duyệt qua từng slot thời gian để kiểm tra
            var availableSlots = new List<string>();
            var slotDuration = TimeSpan.FromMinutes(15); // Bước nhảy 15p

            for (var time = openTime; time < closeTime; time = time.Add(slotDuration))
            {
                // Nếu là hôm nay, bỏ qua các giờ trong quá khứ (+30p buffer)
                if (isToday && time < currentTime.TimeOfDay.Add(TimeSpan.FromMinutes(30))) continue;

                // Kiểm tra xem SESSION hiện tại có thể đặt vào giờ này không
                if (await IsSessionFitAsync(time, session, staffBusyIntervals, technicianSkills, closeTime))
                {
                    availableSlots.Add(time.ToString(@"hh\:mm"));
                }
            }

            return availableSlots;
        }

        // Helper: Kiểm tra xem toàn bộ Session có thể bắt đầu tại thời điểm 'startTime' không
        private async Task<bool> IsSessionFitAsync(
            TimeSpan startTime, 
            BookingSessionModel session, 
            Dictionary<int, List<(TimeSpan Start, TimeSpan End)>> staffBusyMap,
            dynamic technicianSkills,
            TimeSpan shopCloseTime)
        {
            // Tạo bản sao của BusyMap để mô phỏng (vì một nhân viên có thể làm cho nhiều người trong Group Booking nếu giờ không trùng)
            // Tuy nhiên, logic Group Booking thường là làm song song.
            // Nếu Member 1 làm Service A (60p) với Staff X. Staff X sẽ bận trong 60p đó.
            // Nếu Member 2 cũng muốn chọn Staff X, phải đợi sau 60p 
            var tempBusyMap = new Dictionary<int, List<(TimeSpan Start, TimeSpan End)>>();
            
            // Copy dữ liệu gốc
            foreach(var kvp in staffBusyMap) 
                tempBusyMap[kvp.Key] = new List<(TimeSpan, TimeSpan)>(kvp.Value);

            // Duyệt qua từng thành viên trong booking
            foreach (var member in session.Members)
            {
                var memberCurrentTime = startTime;

                foreach (var serviceId in member.SelectedServiceIds)
                {
                    var serviceDuration = await _context.Services
                        .Where(s => s.ServiceId == serviceId)
                        .Select(s => s.DurationMinutes)
                        .FirstOrDefaultAsync();

                    var serviceEndTime = memberCurrentTime.Add(TimeSpan.FromMinutes(serviceDuration));

                    // Check 1: Quá giờ đóng cửa?
                    if (serviceEndTime > shopCloseTime) return false;

                    // Check 2: Tìm nhân viên phù hợp
                    int? requiredStaffId = null;
                    if (member.ServiceStaffMap != null && member.ServiceStaffMap.ContainsKey(serviceId))
                    {
                        requiredStaffId = member.ServiceStaffMap[serviceId];
                    }

                    bool foundStaff = false;

                    if (requiredStaffId.HasValue) // Khách chọn đích danh
                    {
                        if (IsStaffAvailable(requiredStaffId.Value, memberCurrentTime, serviceEndTime, tempBusyMap))
                        {
                            // Đánh dấu nhân viên này bận trong khoảng này (để không bị book trùng bởi member khác trong cùng session)
                            tempBusyMap[requiredStaffId.Value].Add((memberCurrentTime, serviceEndTime));
                            foundStaff = true;
                        }
                    }
                    else // Chọn "Bất kỳ ai"
                    {
                        // Tìm list nhân viên có kỹ năng làm service này
                        // Lưu ý: technicianSkills là list anonymous object {EmployeeId, ServiceId}
                        // Cần cast hoặc query lại nếu dynamic khó dùng
                        // Ở đây giả sử ta filter trên list in-memory
                        var skilledStaffIds = new List<int>();
                        foreach(var item in technicianSkills)
                        {
                            if(item.ServiceId == serviceId) skilledStaffIds.Add(item.EmployeeId);
                        }

                        // Tìm 1 người rảnh
                        foreach (var staffId in skilledStaffIds)
                        {
                            if (IsStaffAvailable(staffId, memberCurrentTime, serviceEndTime, tempBusyMap))
                            {
                                tempBusyMap[staffId].Add((memberCurrentTime, serviceEndTime));
                                foundStaff = true;
                                break; // Tìm được 1 người là đủ
                            }
                        }
                    }

                    if (!foundStaff) return false; // Không tìm được nhân viên cho dịch vụ này -> Slot này fail

                    // Dịch chuyển thời gian cho dịch vụ tiếp theo của member này
                    memberCurrentTime = serviceEndTime;
                }
            }

            return true; // Tất cả dịch vụ của tất cả thành viên đều xếp được
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
            // 1. Tạo Customer
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.PhoneNumber == session.CustomerInfo.Phone);
            if (customer == null)
            {
                customer = new Customer { FullName = session.CustomerInfo.FullName, PhoneNumber = session.CustomerInfo.Phone, Email = session.CustomerInfo.Email };
                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();
            }

            // 2. Tính toán lại tổng thời gian để set EndTime cho Appointment
            int totalDurationAllMembers = 0;
            foreach (var m in session.Members)
            {
                foreach (var sId in m.SelectedServiceIds)
                {
                    var duration = await _context.Services.Where(s => s.ServiceId == sId).Select(s => s.DurationMinutes).FirstOrDefaultAsync();
                    totalDurationAllMembers += duration;
                }
            }

            var appointmentStartTime = session.SelectedDate.Value.Add(session.SelectedTime.Value);
            
            var appointment = new Appointment
            {
                CustomerId = customer.CustomerId,
                StartTime = appointmentStartTime,
                EndTime = appointmentStartTime.AddMinutes(totalDurationAllMembers),
                Status = "Pending",
                Notes = session.CustomerInfo.Note + (session.IsGroupBooking ? $" [Nhóm {session.Members.Count} người]" : ""),
                DepositAmount = session.DepositAmount,
                IsDepositPaid = false,
                CreatedDate = DateTime.Now
            };
            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            // 3. Tạo Details & Tự động xếp nhân viên (Logic Assignment thật)
            foreach (var member in session.Members)
            {
                var currentServiceStartTime = appointmentStartTime;

                foreach (var serviceId in member.SelectedServiceIds)
                {
                    var service = await _context.Services.FindAsync(serviceId);
                    if (service == null) continue;

                    int? finalStaffId = null;

                    if (member.ServiceStaffMap != null && member.ServiceStaffMap.ContainsKey(serviceId) && member.ServiceStaffMap[serviceId] != null)
                    {
                        finalStaffId = member.ServiceStaffMap[serviceId];
                    }
                    else
                    {
                        // Auto Assign (Logic tương tự như đã thảo luận trước đó, nhưng áp dụng cho việc LƯU)
                        finalStaffId = await AutoAssignStaffAsync(serviceId, currentServiceStartTime, service.DurationMinutes);
                    }

                    _context.AppointmentDetails.Add(new AppointmentDetail
                    {
                        AppointmentId = appointment.AppointmentId,
                        ServiceId = serviceId,
                        TechnicianId = finalStaffId,
                        PriceAtBooking = service.Price,
                        Status = "Pending"
                    });

                    currentServiceStartTime = currentServiceStartTime.AddMinutes(service.DurationMinutes);
                }
            }

            // 4. Tạo Invoice
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
            var appointment = await _context.Appointments.FindAsync(appointmentId);
            if (appointment != null)
            {
                appointment.IsDepositPaid = true;
                appointment.Status = "Confirmed";
                var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.AppointmentId == appointmentId);
                if (invoice != null)
                {
                    invoice.PaymentStatus = "DepositPaid";
                    _context.Payments.Add(new Payment { InvoiceId = invoice.InvoiceId, Amount = appointment.DepositAmount, PaymentMethod = "Momo", TransactionType = "Deposit", PaymentDate = DateTime.Now });
                }
                await _context.SaveChangesAsync();
            }
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
    }
}