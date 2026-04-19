using Microsoft.Extensions.Caching.Memory;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroAlloc.Cache.Tests;

public sealed class IMemoryCacheProxyTests : IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly TestServiceImpl _impl = new();
    private readonly ITestService _proxy;

    public IMemoryCacheProxyTests()
    {
        _proxy = new ITestServiceCacheProxy(_impl, _cache);
    }

    public void Dispose() => _cache.Dispose();

    [Fact]
    public async Task OnCacheMiss_CallsInner_AndCachesResult()
    {
        var result = await _proxy.GetAsync("abc", CancellationToken.None);

        result.Should().Be("result-abc");
        _impl.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task OnCacheHit_ReturnsCachedValue_WithoutCallingInner()
    {
        await _proxy.GetAsync("abc", CancellationToken.None);
        await _proxy.GetAsync("abc", CancellationToken.None);

        _impl.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task DifferentKeys_AreIndependent()
    {
        await _proxy.GetAsync("a", CancellationToken.None);
        await _proxy.GetAsync("b", CancellationToken.None);

        _impl.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task PassthroughMethod_AlwaysCallsInner()
    {
        await _proxy.SaveAsync("data", CancellationToken.None);
        _impl.SaveCallCount.Should().Be(1);
        _impl.CallCount.Should().Be(0); // GetAsync not called
    }

    [Fact]
    public async Task KeyFormat_IncludesInterfaceAndMethodAndParam()
    {
        await _proxy.GetAsync("xyz", CancellationToken.None);

        var hit = _cache.TryGetValue("ITestService.GetAsync:xyz", out string? val);
        hit.Should().BeTrue();
        val.Should().Be("result-xyz");
    }
}
