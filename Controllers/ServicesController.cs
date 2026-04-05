using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mitrayana.Api.Data;
using Mitrayana.Api.Models;

namespace Mitrayana.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ServicesController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        public ServicesController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var list = await _db.Set<Service>().ToListAsync();
                return Ok(list);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error fetching services: " + ex);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            try
            {
                // Load services and compute categories in-memory as the DB schema may not have a category column
                var all = await _db.Set<Service>().ToListAsync();
                var cats = all.Where(s => !string.IsNullOrEmpty(s.Category)).Select(s => s.Category).Distinct().ToList();
                return Ok(cats);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error getting categories: " + ex);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpGet("subcategories")]
        public async Task<IActionResult> GetSubcategories([FromQuery] string category)
        {
            try
            {
                // Same: fetch services then filter in-memory to avoid querying non-existent columns
                var all = await _db.Set<Service>().ToListAsync();
                if (!string.IsNullOrEmpty(category)) all = all.Where(s => s.Category == category).ToList();
                var subs = all.Where(s => !string.IsNullOrEmpty(s.SubCategory)).Select(s => s.SubCategory).Distinct().ToList();
                return Ok(subs);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error getting subcategories: " + ex);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] Service svc)
        {
            try
            {
                _db.Set<Service>().Add(svc);
                await _db.SaveChangesAsync();
                Console.WriteLine("Service created: " + svc.Name);
                return Ok(svc);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error creating service: " + ex.Message);
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] Service svc)
        {
            var existing = await _db.Set<Service>().FindAsync(id);
            if (existing == null) return NotFound();

            existing.Name = svc.Name ?? existing.Name;
            existing.Category = svc.Category ?? existing.Category;
            existing.SubCategory = svc.SubCategory ?? existing.SubCategory;
            existing.Price = svc.Price;
            existing.Description = svc.Description ?? existing.Description;
            existing.IsActive = svc.IsActive;

            await _db.SaveChangesAsync();
            return Ok(existing);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await _db.Set<Service>().FindAsync(id);
            if (existing == null) return NotFound();
            _db.Set<Service>().Remove(existing);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
