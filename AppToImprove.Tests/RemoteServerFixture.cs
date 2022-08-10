using System.Threading;
using WireMock.Exceptions;
using WireMock.Server;

namespace AppToImprove.Tests
{
    /// <summary>
    /// Starts fake server which can be configured in integration tests.
    /// </summary>
    public class RemoteServerFixture
    {
        private static int _portShift = -1;

        public RemoteServerFixture()
        {
            for (var tryCount = 0; tryCount < 10; tryCount++)
            {
                this.Port = 36501 + Interlocked.Increment(ref _portShift);
                try
                {
                    this.Server = WireMockServer.Start(this.Port);
                    return;
                }
                catch (WireMockException ex)
                {
                }
            }
        }

        public int Port { get; }

        public WireMockServer Server { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Server?.Stop();
        }
    }
}
