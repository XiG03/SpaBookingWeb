# 📋 Hướng dẫn sử dụng Environment Variables

## 1. Cấu trúc file

- **`.env.example`** - Mẫu với hướng dẫn chi tiết (commit vào GitHub)
- **`.env`** - File thực tế với giá trị của bạn (⚠️ KHÔNG commit)

## 2. Setup ban đầu

```bash
# Copy file mẫu thành file thực tế
cp .env.example .env

# Hoặc trên Windows
copy .env.example .env
```

## 3. Chỉnh sửa `.env` với giá trị thực tế

Mở `.env` và cập nhật các giá trị:

### 📊 SQL Server
```
DB_PASSWORD=YourStrong@Password123!
```
- Dùng mật khẩu mạnh (ít nhất 8 ký tự, có số, ký tự đặc biệt)

### 🔐 Google OAuth
```
Authentication__Google__ClientId=YOUR_GOOGLE_CLIENT_ID_HERE
Authentication__Google__ClientSecret=YOUR_GOOGLE_CLIENT_SECRET_HERE
```

**Cách lấy:**
1. Vào https://console.cloud.google.com
2. Tạo project mới
3. Bật OAuth 2.0
4. Thêm Authorized redirect URIs:
   - Local: `http://localhost:5000/signin-google`
   - Production: `https://yourdomain.com/signin-google`
5. Copy Client ID & Secret

### 📧 Gmail Settings
```
EmailSettings__SenderEmail=your-email@gmail.com
EmailSettings__Password=xxxx xxxx xxxx xxxx
```

**Cách lấy:**
1. Bật 2-Step Verification trên Gmail
2. Vào https://myaccount.google.com/apppasswords
3. Tạo App Password cho "Mail" → "Windows Computer"
4. Copy mật khẩu 16 ký tự (không copy mật khẩu Gmail thường)

## 4. Chạy với Docker Compose

```bash
docker-compose up --build
```

Docker sẽ tự động đọc `.env` file và pass các biến vào container.

## 5. ✅ Kiểm tra

Sau khi container chạy:

```bash
# Kiểm tra logs
docker-compose logs web

# Truy cập app
http://localhost:5000
```

Nếu thấy lỗi database, kiểm tra:
- SQL Server password trong `.env`
- DB_SERVER tên đúng: `sqlserver` (không phải `localhost`)

## 6. 🚨 An ninh

- ✅ Giữ `.env` an toàn, không share
- ✅ Thêm `.env` vào `.gitignore` (đã làm)
- ✅ Commit `.env.example` để team biết cần những biến nào
- ✅ Trên production, dùng Docker Secrets hoặc environment variables từ CI/CD (GitHub Actions, GitLab CI, etc.)

## 7. Production Deployment

Thay vì file `.env`, sử dụng:

**Option A: GitHub Actions Secrets**
```yaml
env:
  ASPNETCORE_ENVIRONMENT: Production
  ConnectionStrings__DefaultConnection: ${{ secrets.DB_CONNECTION_STRING }}
  Authentication__Google__ClientSecret: ${{ secrets.GOOGLE_SECRET }}
```

**Option B: Docker Compose với secrets**
```yaml
secrets:
  db_password:
    external: true
  google_secret:
    external: true
```

**Option C: Kubernetes ConfigMap & Secrets**
```yaml
apiVersion: v1
kind: Secret
metadata:
  name: spabooking-secrets
data:
  db_password: ...base64...
```

## 8. Troubleshooting

| Lỗi | Nguyên nhân | Cách fix |
|-----|-----------|---------|
| `Invalid connection string` | DB_PASSWORD sai hoặc chứa ký tự đặc biệt | Thay trong `.env` |
| `Google OAuth không hoạt động` | ClientSecret sai hoặc redirect URI chưa thêm | Kiểm tra Google Console |
| `Email không gửi được` | App Password sai | Tạo mới trên Google Account |
| `Database connection timeout` | DB_SERVER hoặc DB_PORT sai | Dùng `sqlserver:1433` cho Docker |

---

**Câu hỏi?** Hãy kiểm tra logs: `docker-compose logs -f web`
