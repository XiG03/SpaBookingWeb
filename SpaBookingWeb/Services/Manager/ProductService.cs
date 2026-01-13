using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SpaBookingWeb.Data;
using SpaBookingWeb.Models;
using SpaBookingWeb.ViewModels.Manager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SpaBookingWeb.Services.Manager;

namespace SpaBookingWeb.Services.Manager
{
    public class ProductService : IProductService
    {
        private readonly ApplicationDbContext _context;

        public ProductService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ProductDashboardViewModel> GetAllProductsAsync()
        {
            // 1. Get basic product list
            var products = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Unit)
                .OrderByDescending(p => p.ProductId)
                .ToListAsync();

            // 2. Get Consumables data
            // Group by ProductId to sum total used quantity
            var consumablesStats = await _context.AppointmentConsumables
                .GroupBy(ac => ac.ProductId)
                .Select(g => new { 
                    ProductId = g.Key, 
                    TotalUsed = g.Sum(ac => ac.ActualQuantity) 
                })
                .ToListAsync();

            // 3. Find service using the product most
            // Need to join tables: AppointmentConsumables -> AppointmentDetails -> Services
            var topServiceUsage = await _context.AppointmentConsumables
                .Include(ac => ac.AppointmentDetail)
                .ThenInclude(ad => ad.Service)
                .Where(ac => ac.AppointmentDetail.ServiceId != null)
                .GroupBy(ac => new { ac.ProductId, ac.AppointmentDetail.Service.ServiceName })
                .Select(g => new {
                    ProductId = g.Key.ProductId,
                    ServiceName = g.Key.ServiceName,
                    Count = g.Count() // Occurrences
                })
                .ToListAsync();

            // 4. Get Retail Data (SoldCount)
            // Assuming retail sales are saved in InvoiceDetails table or similar. 
            // Currently SQL Script doesn't have retail invoice detail table, 
            // so I will temporarily set SoldCount = 0 or get from similar logic if you have it.
            // Example assumption: var soldStats = ...

            var dtos = new List<ProductDto>();

            foreach (var p in products)
            {
                // Get used statistics
                var usedStat = consumablesStats.FirstOrDefault(c => c.ProductId == p.ProductId);
                int usedCount = usedStat?.TotalUsed ?? 0;

                // Get name of most used service
                var topService = topServiceUsage
                    .Where(x => x.ProductId == p.ProductId)
                    .OrderByDescending(x => x.Count)
                    .FirstOrDefault();
                string topServiceName = topService?.ServiceName ?? "Not used";

                dtos.Add(new ProductDto
                {
                    ProductId = p.ProductId,
                    ProductName = p.ProductName,
                    CategoryName = p.Category?.CategoryName ?? "N/A",
                    UnitName = p.Unit?.UnitName ?? "Piece",
                    PurchasePrice = p.PurchasePrice,
                    SalePrice = p.SalePrice,
                    StockQuantity = p.StockQuantity,
                    IsForSale = p.IsForSale,
                    
                    // Assign statistics data
                    UsedInServiceCount = usedCount,
                    SoldCount = 0, // Temporarily 0
                    TopServiceUsage = topServiceName
                });
            }

            return new ProductDashboardViewModel { Products = dtos };
        }

        public async Task<ProductViewModel> GetProductForCreateAsync()
        {
            var model = new ProductViewModel();
            await LoadDropdowns(model);
            return model;
        }

        public async Task<ProductViewModel?> GetProductForEditAsync(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return null;

            var model = new ProductViewModel
            {
                ProductId = product.ProductId,
                ProductName = product.ProductName,
                CategoryId = product.CategoryId,
                UnitId = product.UnitId,
                PurchasePrice = product.PurchasePrice,
                SalePrice = product.SalePrice,
                StockQuantity = product.StockQuantity,
                IsForSale = product.IsForSale
            };
            
            await LoadDropdowns(model);
            return model;
        }

        public async Task<Product?> GetProductByIdAsync(int id)
        {
            return await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Unit)
                .FirstOrDefaultAsync(p => p.ProductId == id);
        }

        public async Task CreateProductAsync(ProductViewModel model)
        {
            var product = new Product
            {
                ProductName = model.ProductName,
                CategoryId = model.CategoryId,
                UnitId = model.UnitId,
                PurchasePrice = model.PurchasePrice,
                SalePrice = model.SalePrice,
                StockQuantity = model.StockQuantity,
                IsForSale = model.IsForSale
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateProductAsync(ProductViewModel model)
        {
            var product = await _context.Products.FindAsync(model.ProductId);
            if (product == null) throw new Exception("Product not found");

            product.ProductName = model.ProductName;
            product.CategoryId = model.CategoryId;
            product.UnitId = model.UnitId;
            product.PurchasePrice = model.PurchasePrice;
            product.SalePrice = model.SalePrice;
            product.StockQuantity = model.StockQuantity;
            product.IsForSale = model.IsForSale;

            _context.Products.Update(product);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteProductAsync(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
            }
        }

        private async Task LoadDropdowns(ProductViewModel model)
        {
            model.Categories = await _context.Categories
                .Select(c => new SelectListItem { Value = c.CategoryId.ToString(), Text = c.CategoryName })
                .ToListAsync();

            model.Units = await _context.Units
                .Select(u => new SelectListItem { Value = u.UnitId.ToString(), Text = u.UnitName })
                .ToListAsync();
        }
    }
}