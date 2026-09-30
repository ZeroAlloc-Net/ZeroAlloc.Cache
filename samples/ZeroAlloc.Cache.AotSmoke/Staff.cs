using System.Threading;
using System.Threading.Tasks;
using ZeroAlloc.Cache;

namespace ZeroAlloc.Cache.AotSmoke;

// A nested [Cache] interface: its proxy is generated inside Staff, and its DI extension is
// AddStaff_PeopleCache. See #194.
public static partial class Staff
{
    [Cache(TtlMs = 60_000)]
    public interface IPeople
    {
        ValueTask<string> GetNameAsync(int id, CancellationToken ct);
    }

    public sealed class People : IPeople
    {
        private int _calls;

        public int CallCount => Volatile.Read(ref _calls);

        public ValueTask<string> GetNameAsync(int id, CancellationToken ct)
        {
            Interlocked.Increment(ref _calls);
            return ValueTask.FromResult($"person-{id}");
        }
    }
}
