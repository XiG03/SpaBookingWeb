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
            
            // 1. Get Services
            var services = await _context.Services
                .Include(s => s.Category)
                .Include(s => s.ServiceConsumables).ThenInclude(sc => sc.Product).ThenInclude(p => p.Unit)
                .Where(s => s.IsActive && !s.IsDeleted)
                .ToListAsync();

            var serviceGroups = services.GroupBy(s => s.Category?.CategoryName ?? "Other")
                .Select(g => new ServiceCategoryGroupViewModel 
                { 
                    CategoryName = g.Key, 
                    Services = g.Select(s => new ServiceItemViewModel 
                    { 
                        Id = s.ServiceId, // POSITIVE ID
                        Name = s.ServiceName, 
                        Description = s.Description, 
                        Price = s.Price, 
                        DurationMinutes = s.DurationMinutes, 
                        Type = "Service",
                        Consumables = s.ServiceConsumables.Where(sc => !sc.IsDeleted).Select(sc => $"{sc.Product.ProductName} ({sc.Quantity} {sc.Product.Unit?.UnitName ?? ""})").ToList()
                    }).ToList() 
                }).ToList();

            // 2. Get Combo and transform to "ServiceItem" with Negative ID
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
                    // [NEW] List of child services to display separately in Step 3
                    var childServicesList = new List<ServiceItemViewModel>();

                    if (!string.IsNullOrEmpty(c.Description))
                    {
                        descriptionBuilder.AppendLine(c.Description);
                    }

                    descriptionBuilder.AppendLine("Package includes:");
                    foreach (var cd in c.ComboDetails)
                    {
                        descriptionBuilder.AppendLine($"- {cd.Service.ServiceName} ({cd.Service.DurationMinutes}p)");
                        
                        // Aggregate products for description
                        foreach(var sc in cd.Service.ServiceConsumables.Where(x => !x.IsDeleted))
                        {
                            allConsumables.Add($"{sc.Product.ProductName} ({sc.Quantity} {sc.Product.Unit?.UnitName ?? ""}) - [In {cd.Service.ServiceName}]");
                        }

                        // [NEW] Add to ChildServices list
                        childServicesList.Add(new ServiceItemViewModel
                        {
                            Id = cd.Service.ServiceId, // POSITIVE ID of child Service
                            Name = cd.Service.ServiceName,
                            DurationMinutes = cd.Service.DurationMinutes,
                            Price = 0, // Display price is 0 because included in Combo package
                            Type = "ServiceInCombo",
                            Description = "Included in combo"
                        });
                    }

                    return new ServiceItemViewModel
                    {
                        Id = -c.ComboId, // NEGATIVE ID to distinguish
                        Name = $"[Combo] {c.ComboName}",
                        Description = descriptionBuilder.ToString(),
                        Price = c.Price, 
                        DurationMinutes = c.ComboDetails.Sum(cd => cd.Service.DurationMinutes),
                        Type = "Combo",
                        Consumables = allConsumables,
                        // [IMPORTANT] Assign child list here
                        ChildServices = childServicesList 
                    };
                }).ToList();

                serviceGroups.Insert(0, new ServiceCategoryGroupViewModel
                {
                    CategoryName = "Super Saver Combos",
                    Services = comboItems
                });
            }

            model.ServiceCategories = serviceGroups;
            model.Staffs = await _context.Employees.Where(e => e.IsActive && !e.IsDeleted).Select(e => new StaffViewModel { Id = e.EmployeeId, Name = e.FullName, Role = "Technician", Avatar = e.Avatar ?? "/img/default-avatar.png" }).ToListAsync();
            
            return model;
        }

        // --- SMART AVAILABLE TIME SLOT LOGIC ---
        public async Task<List<string>> GetAvailableTimeSlotsAsync(DateTime date, BookingSessionModel session)
        {
            var settings = await _systemSettingService.GetCurrentSettingsAsync();
            var openTime = settings.OpenTime;
            var closeTime = settings.CloseTime;
            var currentTime = DateTime.Now;
            bool isToday = date.Date == currentTime.Date;

            // [NEW] Get CUSTOMER'S busy intervals (If logged in)
            var customerBusyIntervals = new List<(TimeSpan Start, TimeSpan End)>();
            if (_httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true)
            {
                var userEmail = _httpContextAccessor.HttpContext.User.Identity.Name;
                var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Email == userEmail && !c.IsDeleted);
                
                if (customer != null)
                {
                    // Get other appointments of the customer in the day (exclude itself if editing/repaying - however session doesn't have ID if creating new here)
                    // Here assume creating completely new or rebook creating new
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
            
            // Logic calculate session duration (handle negative ID)
            int sessionMaxDuration = 0;
            foreach (var member in session.Members)
            {
                int memberDuration = 0;
                foreach (var id in member.SelectedServiceIds)
                {
                    if (id > 0) // Service
                    {
                        var d = await _context.Services.Where(s => s.ServiceId == id).Select(s => s.DurationMinutes).FirstOrDefaultAsync();
                        memberDuration += d;
                    }
                    else // Combo (Negative ID)
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

            // [NEW] Get STAFF'S Work Schedule & Busy Schedule
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

            // Build staff busy map
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

            // Staff skills (to know who can do which service)
            // Note: Combo needs check skill for each child service, here simplified check odd service
            var technicianSkills = await _context.TechnicianServices
                .Where(ts => !ts.IsDeleted)
                .Select(ts => new { ts.EmployeeId, ts.ServiceId })
                .ToListAsync();

            // Logic find available slots 
            var availableSlots = new List<string>();
            var slotDuration = TimeSpan.FromMinutes(15);
            for (var time = openTime; time < closeTime; time = time.Add(slotDuration))
            {
                if (isToday && time < currentTime.TimeOfDay.Add(TimeSpan.FromMinutes(30))) continue;
                
                var estimatedEndTime = time.Add(TimeSpan.FromMinutes(sessionMaxDuration));

                // Check 1: Customer busy?
                if (IsOverlapping(time, estimatedEndTime, customerBusyIntervals)) continue;

                // Check 2: Past closing time?
                if (estimatedEndTime > closeTime) continue;

                // Check 3: Staff free? (Use existing helper method, updated for negative IDs)
                // To simplify in this context file while keeping old logic, assume check staff here
                // If want to integrate detailed staff check logic, need update IsSessionFitAsync to handle negative IDs
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
            // Copy map to simulate (avoid modifying original data)
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
                    if (requiredStaffId.HasValue) // Specific pick
                    {
                        if (IsStaffAvailable(requiredStaffId.Value, memberCurrentTime, serviceEndTime, tempBusyMap))
                        {
                            tempBusyMap[requiredStaffId.Value].Add((memberCurrentTime, serviceEndTime));
                            foundStaff = true;
                        }
                    }
                    else // Random pick (find free staff with skill)
                    {
                        var skilledStaffIds = new List<int>();
                        // Filter staff with skill (dynamic convert)
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

                    if (!foundStaff) return false; // No staff -> Slot fail
                    memberCurrentTime = serviceEndTime;
                }
            }
            return true;
        }

        private bool IsOverlapping(TimeSpan start, TimeSpan end, List<(TimeSpan Start, TimeSpan End)> busyIntervals)
        {
            foreach (var interval in busyIntervals)
            {
                // Overlap when: (StartA < EndB) and (EndA > StartB)
                if (start < interval.End && end > interval.Start)
                {
                    return true;
                }
            }
            return false;
        }

        // Helper: Check if entire Session can start at 'startTime'
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

                    // Logic check staff for Combo is a bit complex because combo includes many child services
                    // Here we simplify: Assume 1 staff does entire combo or skip skill check for combo
                    // If id > 0 (Odd Service), check normally
                    
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
                            // Find list of skilled staff
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
                        // With Combo, temporarily only check if any staff is free during period (skip skill check for each item)
                        // Or assign random free person
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
            if (!busyMap.ContainsKey(staffId)) return false; // Employee not working today

            foreach (var interval in busyMap[staffId])
            {
                // Check Overlap: (StartA < EndB) and (EndA > StartB)
                if (start < interval.End && end > interval.Start)
                {
                    return false; // Overlap
                }
            }
            return true;
        }

        // --- OTHER METHODS KEEP AS IS ---
        public async Task<int> SaveBookingAsync(BookingSessionModel session)
        {
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.PhoneNumber == session.CustomerInfo.Phone);
            if (customer == null)
            {
                customer = new Customer { FullName = session.CustomerInfo.FullName, PhoneNumber = session.CustomerInfo.Phone, Email = session.CustomerInfo.Email };
                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();
            }

            // Recalculate EndTime
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

            // Save Detail
            foreach (var member in session.Members)
            {
                var currentServiceStartTime = appointmentStartTime;

                foreach (var id in member.SelectedServiceIds)
                {
                    if (id > 0) // Odd Service
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
                    else // Combo (Negative ID)
                    {
                        var comboId = -id;
                        var combo = await _context.Combos
                             .Include(c => c.ComboDetails).ThenInclude(cd => cd.Service)
                             .FirstOrDefaultAsync(c => c.ComboId == comboId);

                        if (combo != null && combo.ComboDetails.Any())
                        {
                            // Split Combo into child Service lines but still link to ComboId
                            // Calculate distributed price (so child sum = combo price)
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
                                    ComboId = combo.ComboId, // Mark as belonging to this Combo
                                    Status = "Pending"
                                };
                                
                                // Price distribution
                                if (totalOriginal > 0)
                                {
                                    if (i == detailsList.Count - 1) // Last item takes remainder
                                        subDetail.PriceAtBooking = combo.Price - currentTotalSaved;
                                    else
                                        subDetail.PriceAtBooking = Math.Round(cd.Service.Price * ratio, 0); // Round 0 decimals for VND
                                }
                                else
                                {
                                     subDetail.PriceAtBooking = (i==0) ? combo.Price : 0;
                                }
                                currentTotalSaved += subDetail.PriceAtBooking;

                                // Assign Technician: Find in map with Key as ServiceId (of child service)
                                // Note: In Step 3 we saved staff for child services into ServiceStaffMap
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
            // 1. Get Appointment info with Customer to send email
            var appointment = await _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.AppointmentDetails).ThenInclude(ad => ad.Service) // Get service details to display in mail
                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId);

            if (appointment != null)
            {
                // Update status
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

                // 2. [NEW] SEND CONFIRMATION EMAIL
                if (!string.IsNullOrEmpty(appointment.Customer.Email))
                {
                    try 
                    {
                        string emailSubject = $"[SpaBookingWebsite] Booking Confirmation Success #{appointment.AppointmentId}";
                        string emailBody = BuildConfirmationEmailBody(appointment);
                        
                        await _emailService.SendEmailAsync(appointment.Customer.Email, emailSubject, emailBody);
                    } 
                    catch
                    {
                        // Log mail error but do not throw exception to avoid rolling back payment transaction
                        _logger.LogError("Email sending failed...");
                    }
                }
            }
        }

        // Helper: Create HTML Email Body
        private string BuildConfirmationEmailBody(Appointment appointment)
        {
            var sb = new StringBuilder();
            sb.Append($@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e0e0e0; border-radius: 8px; overflow: hidden;'>
                    <div style='background-color: #ec4899; padding: 20px; text-align: center; color: white;'>
                        <h2 style='margin: 0;'>Booking Confirmation Successful</h2>
                    </div>
                    <div style='padding: 20px;'>
                        <p>Hello <strong>{appointment.Customer.FullName}</strong>,</p>
                        <p>Thank you for choosing <strong>SpaBookingWeb</strong>. Your appointment has been confirmed.</p>
                        
                        <div style='background-color: #f9f9f9; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                            <p style='margin: 5px 0;'><strong>Appointment ID:</strong> #{appointment.AppointmentId}</p>
                            <p style='margin: 5px 0;'><strong>Time:</strong> {appointment.StartTime:HH:mm - dd/MM/yyyy}</p>
                            <p style='margin: 5px 0;'><strong>Deposit Paid:</strong> <span style='color: #ec4899; font-weight: bold;'>{appointment.DepositAmount:N0} đ</span> (Via MoMo)</p>
                        </div>

                        <h3>Booked Services:</h3>
                        <ul style='padding-left: 20px;'>");

            foreach (var detail in appointment.AppointmentDetails)
            {
                var serviceName = detail.Service?.ServiceName ?? "Service";
                sb.Append($"<li>{serviceName} ({detail.PriceAtBooking:N0} đ)</li>");
            }

            sb.Append($@"
                        </ul>

                        <p style='margin-top: 20px; font-size: 13px; color: #666;'>
                            * Please arrive 10 minutes early for the best service.<br/>
                            * If you need to reschedule, please contact hotline: 1900-123-456.
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
                    StaffName = ad.Technician?.FullName ?? "System Selected",
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
            // 1. Find Customer by Email
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Email == userEmail);
            if (customer == null) return new List<AppointmentHistoryViewModel>();

            // 2. Get Appointment list
            var appointments = await _context.Appointments
                .Include(a => a.AppointmentDetails).ThenInclude(ad => ad.Service)
                .Include(a => a.AppointmentDetails).ThenInclude(ad => ad.Technician)
                .Include(a => a.Invoice)
                .Where(a => a.CustomerId == customer.CustomerId)
                .OrderByDescending(a => a.StartTime) // Newest first
                .ToListAsync();

            // 3. Map to ViewModel
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
                    ServiceName = ad.Service?.ServiceName ?? "Service",
                    Duration = ad.Service?.DurationMinutes ?? 0,
                    Price = ad.PriceAtBooking,
                    StaffName = ad.Technician?.FullName ?? "Unassigned",
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
                    ServiceName = ad.Service?.ServiceName ?? "Service",
                    Duration = ad.Service?.DurationMinutes ?? 0,
                    Price = ad.PriceAtBooking,
                    StaffName = ad.Technician?.FullName ?? "Unassigned",
                    StaffAvatar = ad.Technician?.Avatar ?? "/img/default-avatar.png"
                }).ToList()
            };
        }

        // [NEW] Get Completed/Cancelled history
        public async Task<List<AppointmentHistoryViewModel>> GetBookingHistoryArchiveAsync(string userEmail)
        {
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Email == userEmail);
            if (customer == null) return new List<AppointmentHistoryViewModel>();

            var appointments = await _context.Appointments
                .Include(a => a.AppointmentDetails).ThenInclude(ad => ad.Service)
                .Include(a => a.AppointmentDetails).ThenInclude(ad => ad.Technician)
                .Include(a => a.Invoice)
                .Where(a => a.CustomerId == customer.CustomerId
                            && (a.Status == "Completed" || a.Status == "Cancelled")) // Filter status
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
                    ServiceName = ad.Service?.ServiceName ?? "Service",
                    Duration = ad.Service?.DurationMinutes ?? 0,
                    Price = ad.PriceAtBooking,
                    StaffName = ad.Technician?.FullName ?? "Unknown",
                    StaffAvatar = ad.Technician?.Avatar
                }).ToList()
            }).ToList();
        }

        // [NEW] Re-book Logic
        public async Task<bool> RebookAsync(int appointmentId)
        {
            var oldAppt = await _context.Appointments
                .Include(a => a.AppointmentDetails)
                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId);

            if (oldAppt == null) return false;

            // Create new session from old info
            var session = new BookingSessionModel
            {
                IsGroupBooking = false, // Default to individual
                Members = new List<BookingMember>
                {
                    new BookingMember
                    {
                        MemberIndex = 1,
                        Name = "Me",
                        SelectedServiceIds = oldAppt.AppointmentDetails
                                            .Where(d => d.ServiceId.HasValue)
                                            .Select(d => d.ServiceId.Value)
                                            .ToList()
                    }
                }
            };

            // Save session and ready to redirect to Step 2
            SaveSession(session);
            return true;
        }
        public async Task<VoucherCheckResult> ValidateVoucherAsync(string code, decimal orderTotal)
        {
            if (string.IsNullOrEmpty(code))
            {
                return new VoucherCheckResult { IsValid = false, Message = "Please enter a voucher code." };
            }

            var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == code && !v.IsDeleted);

            // 1. Check existence and Active
            if (voucher == null || !voucher.IsActive)
            {
                return new VoucherCheckResult { IsValid = false, Message = "Voucher code does not exist or has expired." };
            }

            // 2. Check time
            var now = DateTime.Now;
            if (now < voucher.StartDate || now > voucher.EndDate)
            {
                return new VoucherCheckResult { IsValid = false, Message = "Voucher code has not started or has expired." };
            }

            // 3. Check quantity
            if (voucher.UsageLimit > 0 && voucher.UsageCount >= voucher.UsageLimit)
            {
                return new VoucherCheckResult { IsValid = false, Message = "Voucher code usage limit reached." };
            }

            // 4. Check minimum order value
            if (orderTotal < voucher.MinSpend)
            {
                return new VoucherCheckResult
                {
                    IsValid = false,
                    Message = $"Order needs minimum {voucher.MinSpend.ToString("N0")}đ to use this code."
                };
            }

            // Valid
            string discountInfo = voucher.DiscountType == "Percent"
                ? $"off {voucher.DiscountValue}%"
                : $"off {voucher.DiscountValue.ToString("N0")}đ";

            return new VoucherCheckResult
            {
                IsValid = true,
                Message = $"Code valid! You will get {discountInfo} for the total order. Please provide this code to the staff at the counter when completing payment.",
                Voucher = voucher
            };
        }

        public async Task<bool> ResumeBookingAsync(int appointmentId)
        {
            var appt = await _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.AppointmentDetails)
                .Include(a => a.Invoice) // Need Invoice to get accurate TotalAmount
                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId);

            if (appt == null) return false;

            var session = new BookingSessionModel
            {
                // [IMPORTANT] Assign old ID so Controller knows not to create new
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
                // Keep deposit % to display correctly
                DepositPercentage = (appt.Invoice?.TotalAmount > 0) ? (int)((appt.DepositAmount / appt.Invoice.TotalAmount) * 100) : 20 
            };

            var member = new BookingMember 
            { 
                MemberIndex = 1, 
                Name = "Me", 
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