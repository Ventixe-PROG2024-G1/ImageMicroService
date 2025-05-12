using Microsoft.Extensions.Caching.Memory;

namespace ImageServiceProvider.Services.Handlers;

public interface ICacheHandler<T>
{
    T? GetFromCache(string cacheKey);
    void RemoveCache(string cacheKey);
    T SetCache(string cacheKey, T data, int minutesToCache = 10);
    Task<T?> GetOrCreateAsync(string cacheKey, Func<Task<T?>> factory, int minutesToCache = 10);
}

public class CacheHandler<T>(IMemoryCache cache) : ICacheHandler<T>
{
    private readonly IMemoryCache _cache = cache;

    public async Task<T?> GetOrCreateAsync(string cacheKey, Func<Task<T?>> factory, int minutesToCache = 10)
    {
        return await _cache.GetOrCreateAsync(cacheKey, async cacheEntry =>
        {
            cacheEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(minutesToCache);

            T? data = await factory();
            return data;
        });
    }

    public T? GetFromCache(string cacheKey)
    {
        _cache.TryGetValue(cacheKey, out T? data);
        return data;
    }

    public T SetCache(string cacheKey, T data, int minutesToCache = 10)
    {
        var cacheEntryOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(minutesToCache)
        };
        _cache.Set(cacheKey, data, cacheEntryOptions);
        return data;
    }

    public void RemoveCache(string cacheKey)
        => _cache.Remove(cacheKey);
}

