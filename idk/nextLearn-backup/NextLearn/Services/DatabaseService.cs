using Microsoft.EntityFrameworkCore;
using NextLearn.Data;
using NextLearn.Models;

namespace NextLearn.Services;

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
