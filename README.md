# Spa Booking & Management System (SpaBookingWeb)

Tài liệu này mô tả **đầy đủ theo góc nhìn kỹ thuật** về:
- Chức năng (function) của hệ thống theo từng vai trò.
- Cấu trúc cơ sở dữ liệu (database schema) theo entity/domain.
- Cách tổ chức source code và luồng xử lý chính của dự án.

---

## 1) Tổng quan hệ thống

SpaBookingWeb là ứng dụng ASP.NET Core MVC (.NET 8) dành cho quản lý spa, gồm cả:
- **Khách hàng (Client/Customer)**: xem dịch vụ/combo/blog, đặt lịch nhiều bước, đánh giá dịch vụ.
- **Lễ tân (Receptionist Area)**: tạo/chỉnh lịch hẹn tại quầy, thanh toán, checkout, theo dõi lịch theo ngày.
- **Kỹ thuật viên (Technician Area)**: chấm công, xem job, cập nhật trạng thái dịch vụ, quản lý vật tư tiêu hao.
- **Quản lý (Manager Area)**: quản lý danh mục vận hành (dịch vụ, combo, sản phẩm, nhân sự, voucher, cấu hình), báo cáo và tài chính.

### Công nghệ chính
- ASP.NET Core MVC + Razor Views
- ASP.NET Core Identity (xác thực + phân quyền)
- Entity Framework Core (Code First + Migration)
- SQL Server
- SignalR (thông báo realtime)
- Docker / Docker Compose

---

## 2) Kiến trúc tổng thể

Ứng dụng đi theo mô hình phân tầng:

1. **Presentation Layer**
   - `Controllers/`, `Areas/*/Controllers/`, `Views/`, `wwwroot/`
   - Chịu trách nhiệm nhận request HTTP, validate input và render View/JSON.

2. **Application/Business Layer**
   - `Services/` (chia theo `Client`, `Manager`, `Receptionist`, `Technician`)
   - Chứa nghiệp vụ chính: booking flow, tính bill, payroll, báo cáo, voucher, review...

3. **Data Access Layer**
   - `Data/ApplicationDbContext.cs`, `Data/Migrations/`, `Data/DbSeeder.cs`
   - Cấu hình schema, relationship, soft delete, audit log, migrate & seed dữ liệu.

4. **Domain Layer**
   - `Models/`
   - Định nghĩa entity cho catalog, customer, operations, CMS, finance, audit.

---

## 3) Chức năng (Function) theo module

## 3.1 Public Controllers (không thuộc Area)

### `AccountController`
- Đăng nhập/đăng ký tài khoản (`Login`, `Register`).
- Xác thực email (`VerifyEmail`, `ConfirmEmailCode`).
- Quên mật khẩu + recovery (`ForgotPassword`, `LoginWithRecovery`).
- OAuth Google (`ExternalLogin`, `ExternalLoginCallback`).
- Điều hướng vai trò (`RoleSelection`, `RoleRedirect`).

### `BookingController` (luồng đặt lịch nhiều bước)
- Chọn loại đặt lịch (`Step1_Type`, `SetBookingType`).
- Chọn dịch vụ/combo (`Step2_Services`, `BookService`, `BookCombo`, `AddMemberService`).
- Chọn kỹ thuật viên (`Step3_Staff`, `SetStaff`, `SetStaffAll`).
- Chọn thời gian (`Step4_Time`, `SetTime`).
- Xác nhận & tạo lịch (`Step5_Confirm`, `SubmitBooking`).
- Callback thanh toán + trang thành công (`PaymentCallback`, `Step6_Success`).
- Hỗ trợ voucher (`CheckVoucher`) và quản lý thành viên booking (`AddNewMember`, `RemoveMember`).

### `HomeController`
- Trang chủ khách (`HomeClient`) và trang mặc định.
- Endpoint phụ trợ test/notify thanh toán.

### `ServicesController`, `CombosController`, `PostsController`
- Danh sách/chi tiết dịch vụ, combo, bài viết.

### `ProfileController`
- Lịch hẹn của user, lịch sử sử dụng dịch vụ.
- Rebook/continue booking từ lịch sử.

### `ReviewController`
- Form đánh giá và submit đánh giá sau sử dụng dịch vụ.

### `ErrorController`
- Xử lý lỗi 404/500 và status code handler.

---

## 3.2 Manager Area (`/Manager/*`)

### Vận hành & danh mục
- `ServiceController`: CRUD dịch vụ, lọc dịch vụ.
- `ComboController`: CRUD combo và mapping service-combo.
- `ProductController`: CRUD sản phẩm.
- `ProductImportController`: import sản phẩm từ file Excel template.
- `VoucherController`: CRUD voucher khuyến mãi.
- `SystemSettingController`: cấu hình hệ thống, unit, deposit rule, promote customer.

### Nhân sự
- `EmployeeController`: CRUD nhân sự, lịch làm việc, chấm công, tip payout, payroll.
- `ProfileController`: hồ sơ manager.

### Khách hàng & nội dung
- `CustomerController`: CRUD khách hàng.
- `ReviewController`: danh sách và xóa review.
- `BlogPostsController`: CRUD bài viết CMS.

### Tài chính & báo cáo
- `CashBookController`: sổ thu/chi.
- `BudgetController`: ngân sách theo danh mục.
- `ReportController`: xem & export báo cáo doanh thu.
- `ActivityLogController`: lịch sử thao tác hệ thống (audit log).
- `HomeController`: dashboard tổng quan + lịch hẹn.

---

## 3.3 Receptionist Area (`/Receptionist/*`)

### Điều phối lịch hẹn
- `CalendarController`:
  - Lấy thông tin khách từ SĐT.
  - Tạo booking tại quầy.
  - Xem/sửa trạng thái lịch hẹn.
  - Đổi lịch, hủy lịch.
  - Tra cứu khả dụng kỹ thuật viên.

### Thanh toán & đối soát
- `CalendarController`:
  - Tính bill (`CalculateBill`), checkout tiền mặt (`CheckoutCash`).
  - Tạo thanh toán MoMo, check trạng thái, xử lý return/IPN.
  - In hóa đơn.
- `PayoutController`: xác nhận payout cho kỹ thuật viên.

### Chấm công & hồ sơ
- `AttendanceController`: check-in/check-out theo ca.
- `ProfileController`: hồ sơ lễ tân + xác nhận lương.
- `BookingListController`: danh sách lịch hẹn theo bộ lọc.

---

## 3.4 Technician Area (`/Technician/*`)

- `AttendanceController`: check-in/check-out kỹ thuật viên.
- `JobController`:
  - Xem chi tiết job được giao.
  - Cập nhật trạng thái thực hiện dịch vụ.
  - Ghi nhận vật tư tiêu hao thực tế.
- `ProfileController`: thông tin hồ sơ.
- `HomeController`: dashboard riêng kỹ thuật viên.

---

## 4) Cấu trúc Database (EF Core Code-First)

> Tất cả DbSet được khai báo trong `ApplicationDbContext` và migrate bằng EF Core.

## 4.1 Danh sách bảng (theo domain)

### A. Identity & Nhân sự
- `AspNetUsers` (kế thừa `ApplicationUser`): tài khoản đăng nhập.
- `Employees`: hồ sơ nhân sự liên kết với user identity.
- `TechnicianDetails`: thông tin chuyên môn kỹ thuật viên (1-1 với Employee).
- `Salaries`: bảng chốt lương theo kỳ.
- `WorkSchedules`: lịch làm/chấm công theo ngày & ca.
- `Shifts`: định nghĩa ca làm.

### B. Khách hàng & Membership
- `Customers`: thông tin khách.
- `MembershipTypes`: hạng thành viên, ngưỡng chi tiêu, % ưu đãi.

### C. Catalog dịch vụ/sản phẩm
- `Categories`: danh mục cho service/product.
- `Units`: đơn vị tính.
- `Services`: dịch vụ spa.
- `Products`: sản phẩm bán/tiêu hao.
- `ServiceConsumables`: định mức sản phẩm tiêu hao cho mỗi dịch vụ (N-N).
- `Combos`: gói combo.
- `ComboDetails`: mapping dịch vụ trong combo (N-N).
- `TechnicianServices`: mapping kỹ thuật viên có thể làm dịch vụ nào + custom giá/thời lượng.

### D. Booking & Billing
- `Appointments`: lịch hẹn tổng.
- `AppointmentDetails`: dòng chi tiết dịch vụ/combo trong một lịch hẹn.
- `AppointmentConsumables`: tiêu hao thực tế theo appointment detail.
- `Invoices`: hóa đơn (1-1 với appointment).
- `Payments`: giao dịch thanh toán của hóa đơn.
- `Vouchers`: mã giảm giá.
- `DepositRules`: quy tắc đặt cọc.
- `Reviews`: đánh giá sau dịch vụ.

### E. CMS
- `PostCategories`: danh mục blog.
- `Posts`: bài viết.

### F. Tài chính quản trị
- `TransactionCategories`: nhóm thu/chi.
- `Transactions`: sổ giao dịch thu/chi.
- `Budgets`: ngân sách theo danh mục/tháng.

### G. System & Audit
- `SystemSettings`: key-value cấu hình hệ thống.
- `ActivityLogs`: nhật ký thay đổi dữ liệu.
- `Operations`: bảng nghiệp vụ theo dõi thao tác booking/vận hành (legacy/internal).

---

## 4.2 Quan hệ dữ liệu quan trọng

- **Customer 1-N Appointments**.
- **Appointment 1-N AppointmentDetails** (cascade delete ở detail).
- **Appointment 1-1 Invoice**.
- **Invoice 1-N Payments**.
- **Service N-N Product** qua `ServiceConsumables`.
- **Combo N-N Service** qua `ComboDetails`.
- **Employee N-N Service** qua `TechnicianServices`.
- **Employee 1-N WorkSchedules / Salaries / AppointmentDetails (TechnicianId)**.

Các khóa tổng hợp được cấu hình trong `OnModelCreating`:
- `ServiceConsumable (ServiceId, ProductId)`
- `ComboDetail (ComboId, ServiceId)`
- `TechnicianService (EmployeeId, ServiceId)`

---

## 4.3 Cơ chế Soft Delete & Audit

### Soft Delete toàn cục
- Hệ thống áp dụng `IsDeleted` + `HasQueryFilter(e => !IsDeleted)` cho hầu hết entity.
- Khi xóa, EF chuyển `Deleted -> Modified` và set `IsDeleted = true`.
- `ActivityLog` được loại trừ khỏi global query filter để ghi log đầy đủ.

### Audit Log tự động
- Override `SaveChangesAsync` để:
  - Ghi snapshot thay đổi (old/new values, affected columns).
  - Lưu vào `ActivityLogs`.
  - Bắn notification nội bộ theo loại entity thay đổi.

---

## 5) Tổ chức source code

```text
SpaBookingWeb/
├── Areas/
│   ├── Manager/          # Backoffice quản lý
│   ├── Receptionist/     # Nghiệp vụ lễ tân
│   ├── Technician/       # Nghiệp vụ kỹ thuật viên
│   └── Identity/         # Razor pages Identity (nếu dùng)
├── Controllers/          # Public controllers cho client/customer
├── Services/
│   ├── Client/           # Service lớp nghiệp vụ cho khách
│   ├── Manager/          # Service nghiệp vụ quản trị
│   ├── Receptionist/     # Service nghiệp vụ lễ tân
│   └── Technician/       # Service nghiệp vụ kỹ thuật viên
├── Models/               # Domain entities (EF)
├── Data/
│   ├── ApplicationDbContext.cs
│   ├── Migrations/
│   └── DbSeeder.cs
├── ViewModels/           # DTO/View state cho MVC views
├── Views/                # Razor views cho public module
├── Hubs/                 # SignalR hub (notification)
├── Settings/             # Mapping option settings (Email,...)
└── wwwroot/              # Static assets (css/js/images/uploads)
```

---

## 6) Dependency Injection & đăng ký service

Trong `Program.cs`, hệ thống đăng ký các service chính:
- **Manager**: CustomerService, VoucherService, ServiceService, ComboService, EmployeeService, ReviewService, ProductService, SystemSettingService, BlogPostService, ProfileService, DashboardService.
- **Client**: ClientHomeService, ServiceListService, BookingService, ComboListService, PostService, ReviewClientService.
- **Receptionist**: ReceptionistService, filter xác thực chấm công.
- **Technician**: TechnicianJobService.
- **Core**: EmailService, NotificationService, MomoService, BookingCleanupService (hosted service).

---

## 7) Authentication/Authorization

- Dùng ASP.NET Core Identity với role:
  - `Manager`, `Technician`, `Receptionist`, `Customer`.
- Có Google OAuth (`AddGoogle`) cho external login.
- Cookie policy cấu hình `SameSite=None`, `SecurePolicy=Always` để hỗ trợ luồng external auth.

---

## 8) Luồng nghiệp vụ tiêu biểu

### 8.1 Luồng đặt lịch online (Customer)
1. Chọn loại đặt (service/combo).
2. Chọn dịch vụ cho từng thành viên.
3. Chọn kỹ thuật viên.
4. Chọn khung giờ.
5. Xác nhận + áp voucher + tính cọc.
6. Thanh toán cọc (nếu có) và tạo `Appointment` + `AppointmentDetails`.
7. Sau khi hoàn tất dịch vụ, tạo `Invoice`/`Payment`, khách đánh giá `Review`.

### 8.2 Luồng lễ tân tại quầy
1. Tạo lịch trực tiếp từ Calendar.
2. Điều phối kỹ thuật viên theo khả dụng.
3. Cập nhật trạng thái dịch vụ.
4. Tính bill cuối cùng, áp voucher, trừ cọc.
5. Thanh toán (cash hoặc MoMo), in hóa đơn.

### 8.3 Luồng quản trị
1. Quản lý danh mục dịch vụ/sản phẩm/combo/voucher.
2. Quản lý nhân sự, lịch làm, tip payout, chốt lương.
3. Theo dõi thu/chi, ngân sách, báo cáo doanh thu.
4. Theo dõi audit log toàn hệ thống.

---

## 9) Khởi động dự án

## 9.1 Chạy local (không Docker)

```bash
cd SpaBookingWeb
dotnet restore
dotnet ef database update
dotnet run
```

Mặc định ứng dụng sẽ tự:
- Tạo `appsettings.json` từ `appsettings.Example.json` nếu chưa có.
- `Migrate()` database khi startup.
- Seed dữ liệu mẫu qua `DbSeeder`.

## 9.2 Chạy với Docker Compose

Tại root repo:

```bash
docker compose up -d --build
```

- Web app: `http://localhost:8080`
- SQL Server: `localhost:1433`

---

## 10) Dữ liệu mẫu (seed)

`DbSeeder` tạo dữ liệu mẫu bao gồm:
- System settings, deposit rule, category/unit/service/product/combo.
- Roles mặc định + user mẫu cho Manager/Technician/Receptionist/Customer.
- Customer mẫu, shift/schedule, voucher, giao dịch tài chính, bài viết CMS.

Điều này giúp demo luồng end-to-end ngay sau khi migrate thành công.

---

## 11) Gợi ý mở rộng

- Tách rõ API layer (REST) để hỗ trợ mobile app.
- Áp dụng CQRS cho module tài chính/báo cáo lớn.
- Thêm test tự động cho booking flow, checkout flow, payroll flow.
- Tăng cường observability: OpenTelemetry + structured logging.
- Chuẩn hóa migration strategy theo môi trường staging/production.

---

Nếu bạn muốn, mình có thể tạo thêm:
1. **ERD dạng sơ đồ** (Mermaid) ngay trong README.
2. **Danh sách endpoint đầy đủ** (route + method + role).
3. **Runbook vận hành production** (backup DB, rotate secret, deploy rollback).
