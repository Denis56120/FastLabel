using FastLabel.Backend.Data;
using FastLabel.Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FastLabel.Backend.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PrintersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var printers = await db.Printers.ToListAsync();
        return Ok(printers);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var printer = await db.Printers.FindAsync(id);
        return printer is not null ? Ok(printer) : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Printer printer)
    {
        db.Printers.Add(printer);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = printer.Id }, printer);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] Printer updated)
    {
        var printer = await db.Printers.FindAsync(id);
        if (printer is null) return NotFound();

        printer.Name = updated.Name;
        printer.Port = updated.Port;

        await db.SaveChangesAsync();
        return Ok(printer);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var printer = await db.Printers.FindAsync(id);
        if (printer is null) return NotFound();

        db.Printers.Remove(printer);
        await db.SaveChangesAsync();
        return NoContent();
    }
}