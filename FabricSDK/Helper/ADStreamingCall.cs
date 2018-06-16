using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Grpc.Core;

namespace Hyperledger.Fabric.SDK.Helper
{
    public class ADStreamingCall<T,S> : IDisposable
    {
        public AsyncDuplexStreamingCall<T,S> Call { get; private set; }

        public ADStreamingCall(AsyncDuplexStreamingCall<T, S> call)
        {
            Call = call;
        }

        private bool _requestClosed = false;

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Close()
        {
            if (!_requestClosed)
            {
                _requestClosed = true;
                Call.RequestStream.CompleteAsync().GetAwaiter().GetResult();
            }
        }
        [MethodImpl(MethodImplOptions.Synchronized)]
        public Task CloseAsync()
        {
            if (!_requestClosed)
            {
                _requestClosed = true;
                return Call.RequestStream.CompleteAsync();
            }
            return Task.FromResult(0);
        }

        public void Dispose()
        {
            Close();
            Call?.Dispose();
            Call = null;
        }
    }

    public static class ADStreamingCallExtensions
    {
        public static ADStreamingCall<T,S> ToADStreamingCall<T,S>(this AsyncDuplexStreamingCall<T, S> call)
        {
            return new ADStreamingCall<T, S>(call);
        }
    }
}
