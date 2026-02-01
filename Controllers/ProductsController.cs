using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using DoAnTotNghiep.Data;
using DoAnTotNghiep.Models;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.ComponentModel.DataAnnotations;

namespace DoAnTotNghiep.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private const int MaxPageSize = 100;

        public ProductsController(IDbContextFactory<ApplicationDbContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

       
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? q = null,
            [FromQuery] int? categoryId = null,
            [FromQuery] decimal? minPrice = null,
            [FromQuery] decimal? maxPrice = null,
            [FromQuery] string? sort = null,
            CancellationToken cancellationToken = default)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > MaxPageSize) pageSize = MaxPageSize;

            await using var db = await _dbFactory.CreateDbContextAsync();

            var query = db.Products
                          .AsNoTracking()
                          .Where(p => p.IsVisible);

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(p => p.CategoryId == categoryId.Value);
            }

            if (minPrice.HasValue)
            {
                query = query.Where(p => p.Price >= minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                query = query.Where(p => p.Price <= maxPrice.Value);
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                var escaped = term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
                var pattern = $"%{escaped}%";

                query = query.Where(p =>
                    EF.Functions.Like(p.Name!, pattern) ||
                    (p.Description != null && EF.Functions.Like(p.Description, pattern))
                );
            }

            var total = await query.CountAsync(cancellationToken);

            switch ((sort ?? "id_desc").ToLowerInvariant())
            {
                case "id_asc": query = query.OrderBy(p => p.Id); break;
                case "price_asc": query = query.OrderBy(p => p.Price); break;
                case "price_desc": query = query.OrderByDescending(p => p.Price); break;
                case "name_asc": query = query.OrderBy(p => p.Name); break;
                case "name_desc": query = query.OrderByDescending(p => p.Name); break;
                case "created_asc": query = query.OrderBy(p => p.CreatedAt); break;
                case "created_desc": query = query.OrderByDescending(p => p.CreatedAt); break;
                default: query = query.OrderByDescending(p => p.Id); break; 
            }

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Price,
                    p.ImageUrl,
                    p.StockQuantity,
                    p.CategoryId,
                    CategoryName = p.Category != null ? p.Category.Name : null,
                    p.OwnerId,
                    p.IsVisible,
                    p.CreatedAt,
                    p.UpdatedAt
                })
                .ToListAsync(cancellationToken);

            var totalPages = (int)Math.Ceiling(total / (double)pageSize);

            var result = new
            {
                total,
                page,
                pageSize,
                totalPages,
                hasPrevious = page > 1,
                hasNext = page < totalPages,
                items
            };

            return Ok(result);
        }

      
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var p = await db.Products
                            .AsNoTracking()
                            .Where(x => x.Id == id)
                            .Select(x => new
                            {
                                x.Id,
                                x.Name,
                                x.Price,
                                x.ImageUrl,
                                x.StockQuantity,
                                x.CategoryId,
                                CategoryName = x.Category != null ? x.Category.Name : null,
                                x.Description,
                                x.OwnerId,
                                x.IsVisible,
                                x.CreatedAt,
                                x.UpdatedAt
                            })
                            .FirstOrDefaultAsync();

            if (p == null) return NotFound();
            return Ok(p);
        }

       
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ProductCreateDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
            if (string.IsNullOrWhiteSpace(userId)) return Forbid();

            var product = new Product
            {
                Name = dto.Name,
                Price = dto.Price,
                ImageUrl = dto.ImageUrl,
                CategoryId = dto.CategoryId,
                Description = dto.Description,
                StockQuantity = dto.StockQuantity,
                IsVisible = dto.IsVisible,
                OwnerId = userId,
                CreatedAt = DateTime.UtcNow
            };

            await using var db = await _dbFactory.CreateDbContextAsync();
            db.Products.Add(product);
            await db.SaveChangesAsync();

            return CreatedAtAction(nameof(Get), new { id = product.Id }, product);
        }

        
        [Authorize]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] ProductUpdateDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
            if (string.IsNullOrWhiteSpace(userId)) return Forbid();

            var isAdmin = User.IsInRole("Admin");
            var isWarehouse = User.IsInRole("Warehouse");

            await using var db = await _dbFactory.CreateDbContextAsync();
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id);
            if (product == null) return NotFound();

            if (!isAdmin && !string.Equals(product.OwnerId ?? string.Empty, userId, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            
            product.Name = dto.Name;
            product.Price = dto.Price;
            product.ImageUrl = dto.ImageUrl;
            product.CategoryId = dto.CategoryId;
            product.Description = dto.Description;
            product.IsVisible = dto.IsVisible;
            product.UpdatedAt = DateTime.UtcNow;

            if (dto.StockQuantity.HasValue)
            {
                if (isAdmin || isWarehouse)
                {
                    product.StockQuantity = dto.StockQuantity.Value;
                }
            }

            db.Products.Update(product);
            await db.SaveChangesAsync();

            return Ok(product);
        }

       
        [Authorize]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
            if (string.IsNullOrWhiteSpace(userId)) return Forbid();

            var isAdmin = User.IsInRole("Admin");

            await using var db = await _dbFactory.CreateDbContextAsync();
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id);
            if (product == null) return NotFound();

            if (!isAdmin && !string.Equals(product.OwnerId ?? string.Empty, userId, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            
            db.Products.Remove(product);
            await db.SaveChangesAsync();

            return NoContent();
        }

        #region DTOs

        public class ProductCreateDto
        {
            [Required]
            public string Name { get; set; } = string.Empty;

            public decimal Price { get; set; }

            public string? ImageUrl { get; set; }

            [Range(1, int.MaxValue)]
            public int CategoryId { get; set; }

            public string? Description { get; set; }

            [Range(0, int.MaxValue)]
            public int StockQuantity { get; set; } = 0;

            public bool IsVisible { get; set; } = true;
        }

        public class ProductUpdateDto
        {
            [Required]
            public string Name { get; set; } = string.Empty;

            public decimal Price { get; set; }

            public string? ImageUrl { get; set; }

            [Range(1, int.MaxValue)]
            public int CategoryId { get; set; }

            public string? Description { get; set; }

           
            [Range(0, int.MaxValue)]
            public int? StockQuantity { get; set; }

            public bool IsVisible { get; set; } = true;
        }

        #endregion
    }
}
