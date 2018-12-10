using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Hyperledger.Fabric.SDK.Helper
{
    public static class SyncExtensions
    {
        public static T RunAndUnwrap<T>(this Task<T> func)
        {
            try
            {
                return func.GetAwaiter().GetResult();
            }
            catch (AggregateException e)
            {
                throw e.Flatten().InnerExceptions.First();
            }
        }

        public static void RunAndUnwrap(this Task func)
        {
            try
            {
                func.GetAwaiter().GetResult();
            }
            catch (AggregateException e)
            {
                throw e.Flatten().InnerExceptions.First();
            }
        }

        public static async Task<TResult> TimeoutAsync<TResult>(this Task<TResult> task, TimeSpan timeout, CancellationToken token)
        {

            using (var cancelSource = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                var firsttask = await Task.WhenAny(task, Task.Delay(timeout, cancelSource.Token)).ConfigureAwait(false);
                if (firsttask == task)
                {
                    cancelSource.Cancel();
                    return await task.ConfigureAwait(false); //Propagate Exceptions
                }
                throw new TimeoutException("The operation has timed out.");
            }
        }

        public static async Task TimeoutAsync(this Task task, TimeSpan timeout, CancellationToken token)
        {
            using (var cancelSource = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                var firsttask = await Task.WhenAny(task, Task.Delay(timeout, cancelSource.Token)).ConfigureAwait(false);
                if (firsttask == task)
                {
                    cancelSource.Cancel();
                    await task.ConfigureAwait(false); //Propagate Exceptions
                }
                else
                    throw new TimeoutException("The operation has timed out.");
            }
        }
    }
}