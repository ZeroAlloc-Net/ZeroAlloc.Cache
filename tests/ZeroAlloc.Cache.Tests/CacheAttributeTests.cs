using System.Reflection;
using ZeroAlloc.Cache;

namespace ZeroAlloc.Cache.Tests;

public sealed class CacheAttributeTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var attr = new CacheAttribute { TtlMs = 1000 };
        attr.TtlMs.Should().Be(1000);
        attr.Sliding.Should().BeFalse();
        attr.MaxEntries.Should().Be(0);
        attr.UseHybridCache.Should().BeFalse();
    }

    [Fact]
    public void CanSetAllProperties()
    {
        var attr = new CacheAttribute
        {
            TtlMs = 5000,
            Sliding = true,
            MaxEntries = 500,
            UseHybridCache = true
        };
        attr.TtlMs.Should().Be(5000);
        attr.Sliding.Should().BeTrue();
        attr.MaxEntries.Should().Be(500);
        attr.UseHybridCache.Should().BeTrue();
    }

    [Fact]
    public void AttributeUsage_AllowsInterfaceAndMethod_NotMultiple()
    {
        var usage = typeof(CacheAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();
        (usage.ValidOn & AttributeTargets.Interface).Should().Be(AttributeTargets.Interface);
        (usage.ValidOn & AttributeTargets.Method).Should().Be(AttributeTargets.Method);
        usage.AllowMultiple.Should().BeFalse();
    }
}
