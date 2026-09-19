using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace ZeroAlloc.Cache.Tests;

/// <summary>
/// #122 was reported as a compile error, but compiling is only half of it — the adopter also
/// asked in #121 whether the inner method still runs. The impl returns a fresh value per call,
/// so a repeated result proves the cache served the second call.
/// </summary>
public sealed class ValueTypeCachingTests
{
    private static IValueTypeService BuildProxy()
    {
        var services = new ServiceCollection();
        services.AddValueTypeServiceCache<ValueTypeServiceImpl>();
        return services.BuildServiceProvider().GetRequiredService<IValueTypeService>();
    }

    [Fact]
    public async Task ValueTypeReturn_SecondCall_IsServedFromCache()
    {
        var proxy = BuildProxy();

        var first = await proxy.CountAsync("a", CancellationToken.None);
        var second = await proxy.CountAsync("a", CancellationToken.None);

        first.Should().Be(1);
        second.Should().Be(1, "the second call must hit the cache, not re-invoke the inner method");
    }

    [Fact]
    public async Task NullableValueTypeReturn_SecondCall_IsServedFromCache()
    {
        var proxy = BuildProxy();

        var first = await proxy.MaybeCountAsync("a", CancellationToken.None);
        var second = await proxy.MaybeCountAsync("a", CancellationToken.None);

        first.Should().Be(1);
        second.Should().Be(1);
    }

    [Fact]
    public async Task CustomStructReturn_SecondCall_IsServedFromCache()
    {
        var proxy = BuildProxy();

        var first = await proxy.TotalAsync("a", CancellationToken.None);
        var second = await proxy.TotalAsync("a", CancellationToken.None);

        first.Amount.Should().Be(1m);
        second.Amount.Should().Be(1m);
    }

    [Fact]
    public async Task DifferentKeys_DoNotShareACacheEntry()
    {
        var proxy = BuildProxy();

        var a = await proxy.CountAsync("a", CancellationToken.None);
        var b = await proxy.CountAsync("b", CancellationToken.None);

        a.Should().Be(1);
        b.Should().Be(2, "a different key must miss and re-invoke the inner method");
    }
}
