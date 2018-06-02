using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hyperledger.Fabric.SDK.Helper
{
    public static class SyncExtensions
    {
        public static T RunAndUnwarp<T>(this Task<T> func)
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
        public static void RunAndUnwarp(this Task func)
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
        public static async Task<TResult> Timeout<TResult>(this Task<TResult> task, TimeSpan timeout)
        {

            using (var cancelSource = new CancellationTokenSource())
            {
                var firsttask = await Task.WhenAny(task, Task.Delay(timeout, cancelSource.Token));
                if (firsttask == task)
                {
                    cancelSource.Cancel();
                    return await task; //Propagate Exceptions
                }
                throw new TimeoutException("The operation has timed out.");
            }
        }
        public static async Task Timeout(this Task task, TimeSpan timeout)
        {

            using (var cancelSource = new CancellationTokenSource())
            {
                var firsttask = await Task.WhenAny(task, Task.Delay(timeout, cancelSource.Token));
                if (firsttask == task)
                {
                    cancelSource.Cancel();
                    await task; //Propagate Exceptions
                }
                throw new TimeoutException("The operation has timed out.");
            }
        }
    }
}
