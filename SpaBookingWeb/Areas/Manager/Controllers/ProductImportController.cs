using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OfficeOpenXml; // Cần cài gói NuGet: EPPlus
using SpaBookingWeb.Models; // Namespace chứa các Entity của bạn
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SpaBookingWeb.Areas.Manager.Controllers
{
    [Area("Manager")]
    public class ProductImportController : Controller
    {
        // Inject DbContext vào đây
        // private readonly ApplicationDbContext _context;
        // public ProductImportController(ApplicationDbContext context) { _context = context; }

        // GET: Hiển thị form upload
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult DownloadTemplate()
        {
            // Cấu hình License (Bắt buộc)
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("MauNhapSanPham");

                // 1. Tạo Header (Tiêu đề cột)
                worksheet.Cells[1, 1].Value = "Tên sản phẩm (Bắt buộc)";
                worksheet.Cells[1, 2].Value = "Mã Danh mục (Số)";
                worksheet.Cells[1, 3].Value = "Mã Đơn vị (Số)";
                worksheet.Cells[1, 4].Value = "Giá nhập (VNĐ)";
                worksheet.Cells[1, 5].Value = "Giá bán (VNĐ)";
                worksheet.Cells[1, 6].Value = "Tồn kho";

                // 2. Định dạng Header cho đẹp (In đậm)
                using (var range = worksheet.Cells[1, 1, 1, 6])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    range.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                }

                // 3. Thêm một dòng dữ liệu mẫu ví dụ để người dùng dễ hiểu
                worksheet.Cells[2, 1].Value = "Mẫu: Kem Dưỡng Da";
                worksheet.Cells[2, 2].Value = 1; // Giả sử ID danh mục là 1
                worksheet.Cells[2, 3].Value = 2; // Giả sử ID đơn vị là 2
                worksheet.Cells[2, 4].Value = 150000;
                worksheet.Cells[2, 5].Value = 200000;
                worksheet.Cells[2, 6].Value = 50;

                // Tự động căn chỉnh độ rộng cột
                worksheet.Cells.AutoFitColumns();

                // 4. Xuất file ra stream
                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0; // Đặt con trỏ về đầu file để client có thể đọc

                string excelName = $"Product_Import_Template_{DateTime.Now:yyyyMMdd}.xlsx";
                
                // Trả về file cho trình duyệt tải xuống
                return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", excelName);
            }
        }

        // POST: Xử lý file Excel được upload lên
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Import(IFormFile file)
        {
            if (file == null || file.Length <= 0)
            {
                ModelState.AddModelError("", "Vui lòng chọn file Excel.");
                return View("Index");
            }

            if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("", "Chỉ hỗ trợ file Excel (.xlsx).");
                return View("Index");
            }

            // Cấu hình License cho EPPlus (Bắt buộc với bản 5+)
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            var listProducts = new List<Product>(); // Entity Product của bạn

            try
            {
                using (var stream = new MemoryStream())
                {
                    await file.CopyToAsync(stream);

                    using (var package = new ExcelPackage(stream))
                    {
                        // Lấy sheet đầu tiên
                        ExcelWorksheet worksheet = package.Workbook.Worksheets[0];
                        // Nếu worksheet rỗng hoặc không có dữ liệu, Dimension có thể null
                        if (worksheet.Dimension == null)
                        {
                            ModelState.AddModelError("", "File Excel rỗng.");
                            return View("Index");
                        }
                        
                        var rowCount = worksheet.Dimension.Rows;

                        // Bắt đầu đọc từ dòng 2 (vì dòng 1 là Header)
                        for (int row = 2; row <= rowCount; row++)
                        {
                            // 1. Tên sản phẩm (Cột A)
                            // FIX: Thêm ?.Trim() để tránh lỗi NullReferenceException nếu ô trống
                            var productName = worksheet.Cells[row, 1].Value?.ToString()?.Trim();

                            // 2. CategoryId (Cột B) - Nullable int
                            string catIdStr = worksheet.Cells[row, 2].Value?.ToString()?.Trim();
                            int? categoryId = null;
                            if (int.TryParse(catIdStr, out int cId))
                            {
                                categoryId = cId;
                            }

                            // 3. UnitId (Cột C) - Nullable int
                            string unitIdStr = worksheet.Cells[row, 3].Value?.ToString()?.Trim();
                            int? unitId = null;
                            if (int.TryParse(unitIdStr, out int uId))
                            {
                                unitId = uId;
                            }
                            
                            // 4. Giá nhập (Cột D) - decimal
                            decimal.TryParse(worksheet.Cells[row, 4].Value?.ToString(), out decimal purchasePrice);

                            // 5. Giá bán (Cột E) - decimal
                            decimal.TryParse(worksheet.Cells[row, 5].Value?.ToString(), out decimal salePrice);

                            // 6. Số lượng tồn (Cột F) - int
                            int.TryParse(worksheet.Cells[row, 6].Value?.ToString(), out int stockQuantity);
                            
                            // Kiểm tra dữ liệu bắt buộc (Tên sản phẩm)
                            if (string.IsNullOrEmpty(productName)) continue; 

                            // Tạo đối tượng Product theo đúng Model bạn cung cấp
                            var product = new Product
                            {
                                ProductName = productName,
                                CategoryId = categoryId,
                                UnitId = unitId,
                                PurchasePrice = purchasePrice,
                                SalePrice = salePrice,
                                StockQuantity = stockQuantity,
                                IsForSale = true // Mặc định là true theo model
                            };

                            listProducts.Add(product);
                        }
                    }
                }

                // Lưu vào Database
                if (listProducts.Count > 0)
                {
                    // _context.Products.AddRange(listProducts);
                    // await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Đã nhập thành công {listProducts.Count} sản phẩm!";
                }
                else
                {
                    ModelState.AddModelError("", "Không tìm thấy dữ liệu hợp lệ trong file.");
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Lỗi khi đọc file: {ex.Message}");
                return View("Index");
            }

            return RedirectToAction("Index", "Products"); // Quay về trang danh sách sản phẩm
        }
    }
}