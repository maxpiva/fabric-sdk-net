using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Hyperledger.Fabric.SDK.Logging;

namespace Hyperledger.Fabric.SDK.Helper
{
    public class DaemonTask<T, S>
    {
        private static readonly ILog logger = LogProvider.GetLogger(typeof(DaemonTask<T, S>));
        private TaskCompletionSource<bool> _completeTask;
        private bool _needCompletition;

        //A Task that wait for connection, but keeps recv events from the GRPC channel.
        private CancellationTokenSource _tokenSource;
        private bool canceling;
        public ADStreamingCall<T, S> Sender { get; private set; }

        public Task ConnectAsync(ADStreamingCall<T, S> call, Func<S, ProcessResult> process_function, Action<Exception> exception_function, CancellationToken token)
        {
            Cancel();
            canceling = false;
            Sender = call;
            _tokenSource = new CancellationTokenSource();
            _completeTask = new TaskCompletionSource<bool>();
            _needCompletition = true;
            token.Register(() => { _tokenSource?.Cancel(); });
#pragma warning disable 4014
            Task.Run(async () =>
#pragma warning restore 4014
            {
                try
                {
                    while (await call.Call.ResponseStream.MoveNext(_tokenSource.Token).ConfigureAwait(false))
                    {
                        _tokenSource.Token.ThrowIfCancellationRequested();
                        S evnt = call.Call.ResponseStream.Current;
                        ProcessResult result = process_function(evnt);
                        if (result == ProcessResult.ConnectionComplete)
                            ShouldCompleteConnection();
                        if (result == ProcessResult.Exit)
                            break;
                    }
                }
                catch (TaskCanceledException f)
                {
                    logger.Debug($"Stream Task Canceled");
                    ShouldCompleteConnectionWithError(f);
                }
                catch (Exception e)
                {
                    if (!canceling)
                    {
                        logger.Debug($"Stream Exception {e.Message}");
                        //RpcException rpc = e as RpcException;
                        if (!ShouldCompleteConnectionWithError(e))
                        {
                            exception_function(e);
                        }
                    }
                    else
                        logger.Debug($"Stream Cancel in progress");
                }

                logger.Debug($"Stream completed");
            }, _tokenSource.Token);
            return _completeTask.Task;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Cancel()
        {
            canceling = true;
            Sender?.Dispose();
            _tokenSource?.Cancel();
            _tokenSource = null;
            _needCompletition = false;
            Sender = null;
        }

        private void ShouldCompleteConnection()
        {
            if (_needCompletition)
            {
                _completeTask.SetResult(true);
                _needCompletition = false;
            }
        }

        private bool ShouldCompleteConnectionWithError(Exception e)
        {
            if (_needCompletition)
            {
                _completeTask.SetException(e);
                _needCompletition = false;
                return true;
            }

            return false;
        }
    }

    public enum ProcessResult
    {
        ConnectionComplete,
        Ok,
        Exit
    }
}