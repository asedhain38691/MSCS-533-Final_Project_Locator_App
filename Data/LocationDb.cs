using SQLite;
using MauiHeatMap.Models;

namespace MauiHeatMap.Data;

public class LocationDb
{
    private readonly SQLiteAsyncConnection _db;

    public LocationDb(string dbPath)
    {
        _db = new SQLiteAsyncConnection(dbPath);
    }

    public async Task InitAsync()
    {
        await _db.CreateTableAsync<LocationPoint>();
    }

    public Task<int> InsertAsync(LocationPoint p) => _db.InsertAsync(p);

    public Task<int> ClearAllAsync() => _db.DeleteAllAsync<LocationPoint>();

    public Task<List<LocationPoint>> GetAllAsync() =>
        _db.Table<LocationPoint>()
           .OrderBy(p => p.TimestampUtc)
           .ToListAsync();
}