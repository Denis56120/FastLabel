using FastLabel.Backend.Data;
using FastLabel.Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FastLabel.Backend.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PrintJobsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? limit)
    {
        var query = db.PrintJobs
            .Include(j => j.Printer)
            .OrderByDescending(j => j.CreatedAt);

        var jobs = limit.HasValue
            ? await query.Take(limit.Value).ToListAsync()
            : await query.Take(100).ToListAsync();

        return Ok(jobs);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var job = await db.PrintJobs
            .Include(j => j.Printer)
            .FirstOrDefaultAsync(j => j.Id == id);

        return job is not null ? Ok(job) : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> CreateJob([FromBody] PrintJob job)
    {
        job.CreatedAt = DateTime.UtcNow;

        db.PrintJobs.Add(job);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = job.Id }, job);
    }
}