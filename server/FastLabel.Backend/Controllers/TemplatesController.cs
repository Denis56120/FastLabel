using FastLabel.Backend.Data;
using FastLabel.Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FastLabel.Backend.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class TemplatesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var templates = await db.LabelTemplates.ToListAsync();
        return Ok(templates);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var template = await db.LabelTemplates.FindAsync(id);
        return template is not null ? Ok(template) : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] LabelTemplate template)
    {
        db.LabelTemplates.Add(template);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = template.Id }, template);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] LabelTemplate updated)
    {
        var template = await db.LabelTemplates.FindAsync(id);
        if (template is null) return NotFound();

        template.Name = updated.Name;
        template.JsonData = updated.JsonData;

        await db.SaveChangesAsync();
        return Ok(template);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var template = await db.LabelTemplates.FindAsync(id);
        if (template is null) return NotFound();

        db.LabelTemplates.Remove(template);
        await db.SaveChangesAsync();
        return NoContent();
    }
}