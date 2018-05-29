using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hyperledger.Fabric.SDK.Helper
{
    public class AsyncLock : IDisposable
    {
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

        public void Dispose()
        {
            semaphore.Release();
        }

        public async Task<AsyncLock> LockAsync()
        {
            await semaphore.WaitAsync();
            return this;
        }

        public async Task<AsyncLock> LockAsync(CancellationToken token)
        {
            await semaphore.WaitAsync(token);
            return this;
        }

        public AsyncLock Lock()
        {
            semaphore.Wait();
            return this;
        }
    }
}