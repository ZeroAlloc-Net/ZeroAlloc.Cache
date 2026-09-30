using System.Threading;
using System.Threading.Tasks;

namespace ZeroAlloc.Cache.Tests;

/// <summary>
/// Two nested [Cache] interfaces with the same name and method, each with its own proxy and
/// Add...Cache extension, sharing the one IMemoryCache the container registers. #194
/// </summary>
public static partial class Catalog
{
    public static partial class Products
    {
        [Cache(TtlMs = 60_000)]
        public interface ILookup
        {
            ValueTask<string> GetAsync(int id, CancellationToken ct);
        }

        public sealed class Lookup : ILookup
        {
            public int Calls { get; private set; }

            public ValueTask<string> GetAsync(int id, CancellationToken ct)
            {
                Calls++;
                return ValueTask.FromResult($"product-{id}");
            }
        }
    }

    public static partial class Orders
    {
        [Cache(TtlMs = 60_000, MaxEntries = 10)]
        public interface ILookup
        {
            ValueTask<string> GetAsync(int id, CancellationToken ct);
        }

        public sealed class Lookup : ILookup
        {
            public ValueTask<string> GetAsync(int id, CancellationToken ct) => ValueTask.FromResult($"order-{id}");
        }
    }

    public static partial class Customers
    {
        [Cache(TtlMs = 60_000)]
        public interface ILookup
        {
            ValueTask<string> GetAsync(int id, CancellationToken ct);
        }

        public sealed class Lookup : ILookup
        {
            public ValueTask<string> GetAsync(int id, CancellationToken ct) => ValueTask.FromResult($"customer-{id}");
        }
    }
}
