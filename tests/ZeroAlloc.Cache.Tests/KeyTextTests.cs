using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace ZeroAlloc.Cache.Tests;

/// <summary>
/// #185: a hit on a plain <see cref="MemoryCache"/> looks the key up as a span, not a string. The
/// key text must stay exactly what it was, because users read and evict entries by it, and every
/// entry is still stored under that string. These tests pin the text from both directions: an
/// entry stored under the string is a hit, and a miss stores under the string.
/// </summary>
public sealed class KeyTextTests : IDisposable
{
    private static readonly Guid SomeGuid = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");
    private static readonly DateTime SomeDate = new(2026, 9, 30, 13, 45, 10, DateTimeKind.Utc);
    private static readonly string LongText = new('x', 300);

    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly KeyTextServiceImpl _impl = new();
    private readonly IKeyTextService _proxy;

    public KeyTextTests()
    {
        _proxy = new IKeyTextServiceCacheProxy(_impl, _cache);
    }

    public void Dispose() => _cache.Dispose();

    public static TheoryData<string> Cultures => new() { "", "en-US", "nl-NL", "de-DE" };

    // Each case: the call, and the key text the string key has always had for it. The expected
    // text is interpolated here the way the generator did before #185, under the same culture.
    private static (Func<IKeyTextService, ValueTask<string?>> Call, string Key)[] Cases() =>
    [
        (async p => await p.ByValuesAsync(7, 8_000_000_000, SomeGuid, SomeDate, 1.5, CancellationToken.None).ConfigureAwait(false),
            $"IKeyTextService.ByValuesAsync:{7}:{8_000_000_000}:{SomeGuid}:{SomeDate}:{1.5}"),
        (async p => await p.ByKindAsync(KeyKind.Second, CancellationToken.None).ConfigureAwait(false),
            "IKeyTextService.ByKindAsync:Second"),
        (async p => await p.ByNullableAsync(5, CancellationToken.None).ConfigureAwait(false),
            "IKeyTextService.ByNullableAsync:5"),
        (async p => await p.ByNullableAsync(null, CancellationToken.None).ConfigureAwait(false),
            "IKeyTextService.ByNullableAsync:"),
        (p => p.ByTextAsync("abc", CancellationToken.None), "IKeyTextService.ByTextAsync:abc"),
        (p => p.ByTextAsync(null, CancellationToken.None), "IKeyTextService.ByTextAsync:"),
        // Longer than the stack buffer, so the lookup falls back to the string key.
        (p => p.ByTextAsync(LongText, CancellationToken.None), "IKeyTextService.ByTextAsync:" + LongText),
    ];

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task EntryStoredUnderTheStringKey_IsAHit(string culture)
    {
        using var scope = new CultureScope(culture);
        foreach (var (call, key) in Cases())
        {
            _cache.Set(key, "seeded-" + key);

            (await call(_proxy)).Should().Be("seeded-" + key, "the lookup must find the entry stored under \"{0}\"", key);
        }

        _impl.CallCount.Should().Be(0);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task Miss_StoresUnderTheStringKey(string culture)
    {
        using var scope = new CultureScope(culture);
        foreach (var (call, key) in Cases())
        {
            var result = await call(_proxy);

            _cache.TryGetValue(key, out object? stored).Should().BeTrue("the entry must be stored under \"{0}\"", key);
            stored.Should().Be(result);
        }
    }

    [Fact]
    public async Task RemovingTheStringKey_EvictsTheEntry()
    {
        (await _proxy.ByValuesAsync(1, 2, SomeGuid, SomeDate, 0.5, CancellationToken.None)).Should().Be("inner-1");
        (await _proxy.ByValuesAsync(1, 2, SomeGuid, SomeDate, 0.5, CancellationToken.None)).Should().Be("inner-1");

        _cache.Remove($"IKeyTextService.ByValuesAsync:{1}:{2}:{SomeGuid}:{SomeDate}:{0.5}");

        (await _proxy.ByValuesAsync(1, 2, SomeGuid, SomeDate, 0.5, CancellationToken.None)).Should().Be("inner-2");
    }

    [Fact]
    public async Task BoundedCache_RemovingTheStringKey_EvictsTheEntry()
    {
        using var holder = new IBoundedServiceBoundedCache();
        var log = new CallLog();
        var proxy = new IBoundedServiceCacheProxy(new BoundedServiceImpl(log), holder);

        await proxy.GetAsync("a", CancellationToken.None);
        await proxy.GetAsync("a", CancellationToken.None);
        log.Bounded.Should().Be(1);

        holder.Cache.Remove("IBoundedService.GetAsync:a");

        await proxy.GetAsync("a", CancellationToken.None);
        log.Bounded.Should().Be(2);
    }

    [Fact]
    public async Task DecoratingCache_IsConsultedOnEveryLookup()
    {
        using var decorator = new DecoratingMemoryCache(new MemoryCache(new MemoryCacheOptions()));
        var impl = new TestServiceImpl();
        var proxy = new ITestServiceCacheProxy(impl, decorator);

        (await proxy.GetAsync("a", CancellationToken.None)).Should().Be("result-a");
        (await proxy.GetAsync("a", CancellationToken.None)).Should().Be("result-a");

        impl.CallCount.Should().Be(1);
        decorator.Lookups.Should().Be(2);
    }

    [Fact]
    public async Task MemoryCacheSubclass_ReimplementingIMemoryCache_IsConsultedOnEveryLookup()
    {
        using var cache = new ReimplementingMemoryCache();
        var impl = new TestServiceImpl();
        var proxy = new ITestServiceCacheProxy(impl, cache);

        (await proxy.GetAsync("a", CancellationToken.None)).Should().Be("result-a");
        (await proxy.GetAsync("a", CancellationToken.None)).Should().Be("result-a");

        impl.CallCount.Should().Be(1);
        cache.Lookups.Should().Be(2);
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _previous = CultureInfo.CurrentCulture;

        public CultureScope(string name) => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);

        public void Dispose() => CultureInfo.CurrentCulture = _previous;
    }
}
