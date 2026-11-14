using JobTracker.Api.Data;
using JobTracker.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ApplicationsController : ControllerBase
    {
        private readonly AppDbContext _db;
        public ApplicationsController(AppDbContext db) => _db = db;

        // GET /api/applications?q=&status=&page=&pageSize=
        [HttpGet]
        public async Task<IActionResult> GetAll(string? q, AppStatus? status, int page = 1, int pageSize = 200)
        {
            var query = _db.Applications.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(a => a.Company.Contains(q) || a.Role.Contains(q));
            if (status.HasValue)
                query = query.Where(a => a.Status == status);

            var total = await query.CountAsync();
            var items = await query.OrderByDescending(a => a.DateApplied)
                                   .Skip((page - 1) * pageSize)
                                   .Take(pageSize)
                                   .ToListAsync();
            return Ok(new { total, items });
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id) =>
            await _db.Applications.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id) is { } a ? Ok(a) : NotFound();

        [HttpPost]
        public async Task<IActionResult> Create(JobApplication a)
        {
            a.Id = Guid.NewGuid();
            _db.Add(a);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = a.Id }, a);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, JobApplication updated)
        {
            var a = await _db.Applications.FindAsync(id);
            if (a is null) return NotFound();

            a.Company = updated.Company;
            a.Role = updated.Role;
            a.Location = updated.Location;
            a.Source = updated.Source;
            a.DateApplied = updated.DateApplied;
            a.Status = updated.Status;
            a.Notes = updated.Notes;

            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var a = await _db.Applications.FindAsync(id);
            if (a is null) return NotFound();
            _db.Remove(a);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}