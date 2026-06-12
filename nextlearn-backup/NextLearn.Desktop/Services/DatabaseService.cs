using Microsoft.EntityFrameworkCore;
using NextLearn.Desktop.Data;
using NextLearn.Desktop.Models;

namespace NextLearn.Desktop.Services;

public class DatabaseService
{
    private readonly AppDbContext _context;

    public DatabaseService()
    {
        _context = new AppDbContext();
        Initialize();
    }

    public AppDbContext GetContext() => _context;

    private void Initialize()
    {
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
