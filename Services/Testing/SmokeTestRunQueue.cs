using System.Threading.Channels;

namespace Abs.FixedAssets.Services.Testing
{
    public class SmokeTestRunRequest
    {
        public Guid RunId { get; set; }
        public DateTime RequestedAt { get; set; }
        public CancellationToken CancellationToken { get; set; }
    }

    public interface ISmokeTestRunQueue
    {
        void Enqueue(SmokeTestRunRequest request);
        ValueTask<SmokeTestRunRequest> DequeueAsync(CancellationToken cancellationToken);
    }

    public class SmokeTestRunQueue : ISmokeTestRunQueue
    {
        private readonly Channel<SmokeTestRunRequest> _queue;

        public SmokeTestRunQueue()
        {
            _queue = Channel.CreateBounded<SmokeTestRunRequest>(new BoundedChannelOptions(10)
            {
                FullMode = BoundedChannelFullMode.Wait
            });
        }

        public void Enqueue(SmokeTestRunRequest request)
        {
            if (!_queue.Writer.TryWrite(request))
            {
                throw new InvalidOperationException("Smoke test queue is full");
            }
        }

        public async ValueTask<SmokeTestRunRequest> DequeueAsync(CancellationToken cancellationToken)
        {
            return await _queue.Reader.ReadAsync(cancellationToken);
        }
    }
}
