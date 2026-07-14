using EBanking.AccountService.Data;
using EBanking.AccountService.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EBanking.AccountService.Controllers;

/// <summary>
/// Invoice/transaction lookup and statement download. All data access is parameterized
/// (EF Core), and file access is confined to the statements directory.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InvoiceController : ControllerBase
{
    private readonly AccountDbContext _context;
    private readonly ILogger<InvoiceController> _logger;

    public InvoiceController(AccountDbContext context, ILogger<InvoiceController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>Find transactions whose Reference matches the supplied value.</summary>
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<Transaction>>> Search([FromQuery] string reference)
    {
        reference ??= string.Empty;

        // Parameterized: input can never change the query structure.
        var results = await _context.Transactions
            .FromSqlInterpolated($"SELECT * FROM Transactions WHERE Reference = {reference}")
            .ToListAsync();

        return Ok(results);
    }

    /// <summary>Download a statement file by name from the statements directory.</summary>
    [HttpGet("download")]
    public IActionResult Download([FromQuery] string file)
    {
        file ??= string.Empty;
        var baseDir = Path.Combine(Directory.GetCurrentDirectory(), "statements");

        // Strip any directory components and confine access to the base directory.
        var safeName = Path.GetFileName(file);
        var fullBase = Path.GetFullPath(baseDir) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(baseDir, safeName));
        if (!fullPath.StartsWith(fullBase, StringComparison.Ordinal) || !System.IO.File.Exists(fullPath))
            return NotFound();

        return PhysicalFile(fullPath, "application/octet-stream", safeName);
    }
}
