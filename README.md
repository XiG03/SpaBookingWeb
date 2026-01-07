# SpaBookingWeb - Hướng dẫn chạy với Docker

Tài liệu này hướng dẫn cách build và chạy ứng dụng SpaBookingWeb sử dụng Docker và Docker Compose.

## Yêu cầu hệ thống
- **Docker Desktop** (trên Windows/macOS) hoặc **Docker Engine** & **Docker Compose** (trên Linux).

## Cấu trúc Service
File `docker-compose.yml` định nghĩa 2 service chính:
1. **web**: Ứng dụng ASP.NET Core MVC (SpaBookingWeb).
2. **sqlserver**: Microsoft SQL Server 2022.

## Hướng dẫn chạy
version: "3.9"

services:
  web:
    image: xig03/spabookingwebmvc:v0.0.3
    container_name: spabookingweb
    build:
      context: .
      dockerfile: SpaBookingWeb/Dockerfile
    ports:
      - "5000:8080"
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__DefaultConnection: >
        Server=sqlserver,1433; Database=SpaBookingDb; User Id=sa; Password=Your_password123; TrustServerCertificate=True;
      Authentication__Google__ClientId: ...
      Authentication__Google__ClientSecret: ...  
       
    depends_on:
      - sqlserver
    networks:
      - spabooking_network

  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    container_name: spabookingweb_sqlserver
    ports:
      - "1433:1433"
    environment:
      ACCEPT_EULA: "Y"
      SA_PASSWORD: "Your_password123"
    volumes:
      - sql_data:/var/opt/mssql
    networks:
      - spabooking_network

volumes:
  sql_data:


networks:
  spabooking_network:
    driver: bridge


### 1. Build và Khởi động
Mở terminal (PowerShell, CMD hoặc Git Bash) tại thư mục chứa file `docker-compose.yml` (Copy lệnh ở phía trên) và chạy lệnh:

```bash
docker compose up -d --build
```

- `-d`: Chạy dưới nền (detached mode).
- `--build`: Build lại image web nếu có thay đổi (lần đầu tiên là bắt buộc).

Nếu bạn muốn đảm bảo build sạch hoàn toàn (không dùng cache), hãy dùng lệnh:
```bash
docker compose build --no-cache
docker compose up -d
```

### 2. Truy cập Ứng dụng
Sau khi các container đã khởi động xong:

- **Web Application**: Truy cập trình duyệt tại địa chỉ [http://localhost:8080](http://localhost:8080)
  *(Lưu ý: Port này phụ thuộc vào cấu hình mapping trong `docker-compose.yml`. Mặc định thường là 8080 hoặc 5000).*

### 3. Thông tin Database (SQL Server)
SQL Server chạy trong container riêng biệt.
- **Host**: `localhost` (khi kết nối từ máy host) hoặc `sqlserver` (khi kết nối từ container web).
- **Port**: `1433`
- **Tài khoản mặc định** (cấu hình trong docker-compose.yml):
  - **Username**: `sa`
  - **Password**: `Your_password123` (Kiểm tra lại file docker-compose.yml để chắc chắn).

### 4. Các lệnh thường dùng

**Xem logs của các container:**
```bash
docker compose logs -f
```

**Dừng và xóa các container:**
```bash
docker compose down
```

**Dừng và xóa container kèm theo volumes data (CẢNH BÁO: Mất dữ liệu DB):**
```bash
docker compose down -v
```

## File cấu hình mẫu (docker-compose.yml)

Đây là cấu hình mẫu cho `docker-compose.yml`. **Lưu ý:** Các thông tin bảo mật đã được thay thế bằng placeholder `<...>`, vui lòng điền thông tin thực tế của bạn trước khi chạy.

```yaml
version: "3.9"

services:
  web:
    container_name: spabookingweb_MVC
    build:
      context: .
      dockerfile: SpaBookingWeb/Dockerfile
    ports:
      - "8080:8080"
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      # Cấu hình kết nối Database
      ConnectionStrings__DefaultConnection: "Server=sqlserver,1433; Database=SpaBookingDb; User Id=sa; Password=<YOUR_DB_PASSWORD>; TrustServerCertificate=True"
      # Cấu hình Google Authentication (Nếu có)
      Authentication__Google__ClientId: "<YOUR_GOOGLE_CLIENT_ID>"
      Authentication__Google__ClientSecret: "<YOUR_GOOGLE_CLIENT_SECRET>"
    depends_on:
      - sqlserver
    networks:
      - spabooking_network

  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest  
    container_name: spabookingweb_sqlserver
    ports:
      - "1433:1433"
    environment:
      ACCEPT_EULA: "Y"
      SA_PASSWORD: "<YOUR_DB_PASSWORD>" # Mật khẩu phải khớp với ConnectionString ở trên
    volumes:
      - sql_data:/var/opt/mssql
    networks:
      - spabooking_network

volumes:
  sql_data:

networks:
  spabooking_network:
    driver: bridge
```
