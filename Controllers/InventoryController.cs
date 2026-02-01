using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoAnTotNghiep.Data;
using DoAnTotNghiep.Models;

namespace DoAnTotNghiep.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InventoryController : ControllerBase
    {
        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

        public InventoryController(IDbContextFactory<ApplicationDbContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        [HttpGet("logs/{productId:int}")]
        public async Task<IActionResult> GetLogs(int productId, DateTime? from = null, DateTime? to = null)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            IQueryable<InventoryLog> q = db.InventoryLogs.AsNoTracking()
                                                          .Where(l => l.ProductId == productId);

            if (from.HasValue)
            {
                q = q.Where(l => l.Timestamp >= from.Value);
            }

            if (to.HasValue)
            {
                q = q.Where(l => l.Timestamp <= to.Value);
            }

            var list = await q.OrderByDescending(l => l.Timestamp)
                              .ThenByDescending(l => l.Id)
                              .ToListAsync();

            return Ok(list);
        }

        [HttpGet("log/{id:int}")]
        public async Task<IActionResult> GetLogById(int id)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var log = await db.InventoryLogs.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id);
            if (log == null) return NotFound();
            return Ok(log);
        }

       
        [HttpPost("log")]
        public async Task<IActionResult> CreateLog([FromBody] InventoryLog model)
        {
            if (model == null) return BadRequest("Model null");

            if (model.Timestamp == default) model.Timestamp = DateTime.UtcNow;

            await using var db = await _dbFactory.CreateDbContextAsync();
            db.InventoryLogs.Add(model);
            await db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetLogById), new { id = model.Id }, model);
        }
    }
}
