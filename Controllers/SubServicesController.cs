using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mitrayana.Api.Data;
using Mitrayana.Api.Models;

namespace Mitrayana.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SubServicesController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        public SubServicesController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var list = await _db.SubServices.Include(s => s.Service).ToListAsync();
                return Ok(list);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error fetching subservices: " + ex);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var sub = await _db.SubServices.Include(s => s.Service).FirstOrDefaultAsync(s => s.SubServiceId == id);
            if (sub == null) return NotFound();
            return Ok(sub);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] SubService sub)
        {
            _db.SubServices.Add(sub);
            await _db.SaveChangesAsync();
            return Ok(sub);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] SubService sub)
        {
            var existing = await _db.SubServices.FindAsync(id);
            if (existing == null) return NotFound();
            existing.Name = sub.Name;
            existing.Price = sub.Price;
            existing.Description = sub.Description;
            // allow changing the parent service id when provided
            if (sub.ServiceId != 0) existing.ServiceId = sub.ServiceId;
            existing.IsActive = sub.IsActive;
            await _db.SaveChangesAsync();
            return Ok(existing);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var sub = await _db.SubServices.FindAsync(id);
            if (sub == null) return NotFound();
            _db.SubServices.Remove(sub);
            await _db.SaveChangesAsync();
            return Ok();
        }
    }
}