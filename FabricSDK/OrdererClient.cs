/*
 *  Copyright 2016, 2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
 *
 *  Licensed under the Apache License, Version 2.0 (the "License");
 *  you may not use this file except in compliance with the License.
 *  You may obtain a copy of the License at
 *    http://www.apache.org/licenses/LICENSE-2.0
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed under the License is distributed on an "AS IS" BASIS,
 *  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *  See the License for the specific language governing permissions and
 *  limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Hyperledger.Fabric.Protos.Common;
using Hyperledger.Fabric.Protos.Orderer;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Logging;
using Config = Hyperledger.Fabric.SDK.Helper.Config;

namespace Hyperledger.Fabric.SDK
{
    /**
     * Sample client code that makes gRPC calls to the server.
     */
    public class OrdererClient
    {
        private static readonly ILog logger = LogProvider.GetLogger(typeof(OrdererClient));
        private static readonly bool IS_TRACE_LEVEL = logger.IsTraceEnabled();
        private readonly string channelName;
        private readonly Endpoint endPoint;
        private readonly string name;
        private readonly long ORDERER_WAIT_TIME = Config.Instance.GetOrdererWaitTime();
        private readonly long ordererWaitTimeMilliSecs;
        private readonly string url;
        private Grpc.Core.Channel managedChannel;
        private readonly String toString;
        private bool shutdown;

        /**
         * Construct client for accessing Orderer server using the existing managedChannel.
         */
        public OrdererClient(Orderer orderer, Endpoint endPoint, Properties properties)
        {
            this.endPoint = endPoint;
            name = orderer.Name;
            
            url = orderer.Url;
            channelName = orderer.Channel.Name;
            toString = $"OrdererClient{{id: {Config.Instance.GetNextID()}, channel: {channelName}, name: {name}, url: {url}}}";

            ordererWaitTimeMilliSecs = ORDERER_WAIT_TIME;

            if (properties != null && properties.Contains("ordererWaitTimeMilliSecs"))
            {
                string ordererWaitTimeMilliSecsString = properties["ordererWaitTimeMilliSecs"];
                if (!long.TryParse(ordererWaitTimeMilliSecsString, out ordererWaitTimeMilliSecs))
                {
                    logger.Warn($"Orderer {this} wait time {ordererWaitTimeMilliSecsString} not parsable.");
                }
            }
        }

  

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Shutdown(bool force)
        {
            if (IS_TRACE_LEVEL)
            {
                logger.Trace($"{ToString()} shutdown called force: {force}, shutdown: {shutdown}, managedChannel: {managedChannel}");
            }
            if (shutdown)
            {
                return;
            }

            logger.Debug($"Shutdown {this}");
            shutdown = true;
            Grpc.Core.Channel lchannel = managedChannel;
            managedChannel = null;
            if (lchannel == null)
            {
                return;
            }

            if (force)
            {
                try
                {
                    lchannel.ShutdownAsync().Wait();
                }
                catch (Exception e)
                {
                    logger.WarnException(e.Message, e);
                }
            }
            else
            {
                bool isTerminated = false;

                try
                {
                    isTerminated = lchannel.ShutdownAsync().Wait(3 * 1000);
                }
                catch (Exception e)
                {
                    logger.DebugException(ToString(), e); //best effort
                }

                if (!isTerminated)
                {
                    try
                    {
                        lchannel.ShutdownAsync().Wait();
                    }
                    catch (Exception e)
                    {
                        logger.WarnException(e.Message, e);
                    }
                }
            }
        }

        ~OrdererClient()
        {
            Shutdown(true);
        }

        public BroadcastResponse SendTransaction(Envelope envelope)
        {
            return SendTransactionAsync(envelope).RunAndUnwrap();
        }

        private async Task<BroadcastResponse> BroadcastAsync(ADStreamingCall<Envelope, BroadcastResponse> call, Envelope envelope, CancellationToken token)
        {
            BroadcastResponse resp = null;
            var rtask = Task.Run(async () =>
            {
                if (await call.Call.ResponseStream.MoveNext(token).ConfigureAwait(false))
                {
                    token.ThrowIfCancellationRequested();
                    resp = call.Call.ResponseStream.Current;
                    logger.Debug($"{this} resp status value: {resp.Status}, resp: {resp.Info}");
//TODO: mpiva: Review this, original JAVA code will throw exception on no success, channel init code, check for the status (Not Found) for retrying.
//TODO: mpiva: Report this as an error to the original JAVA SDK repo.
                    /*
                    if (shutdown)
                        throw new OperationCanceledException($"Channel {channelName}, sendTransaction were canceled");
                    throw new TransactionException($"Channel {channelName} orderer {name} status returned failure code {resp.Status} ({resp.Info}) during order registration");*/
                }
            }, token);
            await call.Call.RequestStream.WriteAsync(envelope).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            await rtask.TimeoutAsync(TimeSpan.FromMilliseconds(ordererWaitTimeMilliSecs),token).ConfigureAwait(false);
            logger.Trace($"{this} Broadcast Complete.");
            return resp;
        }

        private async Task<List<DeliverResponse>> DeliverAsync(ADStreamingCall<Envelope, DeliverResponse> call, Envelope envelope, CancellationToken token)
        {
            List<DeliverResponse> ret = new List<DeliverResponse>();
            var rtask = Task.Run(async () =>
            {
                while (await call.Call.ResponseStream.MoveNext(token).ConfigureAwait(false))
                {
                    token.ThrowIfCancellationRequested();
                    DeliverResponse resp = call.Call.ResponseStream.Current;
                    logger.Debug($"{this} resp status value: {resp.Status}, type case: {resp.TypeCase}");
                    if (resp.TypeCase == DeliverResponse.TypeOneofCase.Status)
                    {
                        ret.Insert(0, resp);
//TODO: mpiva: Review this, original JAVA code will throw exception on no success, channel init code, check for the status (Not Found) for retrying.
//TODO: mpiva: Report this as an error to the original JAVA SDK repo.
//                      if (resp.Status == Status.Success)
                        return;
                        /*if (shutdown)
                            throw new OperationCanceledException($"Channel {channelName}, sendDeliver were canceled");
                        throw new TransactionException($"Channel {channelName} orderer {name} status finished with failure code {resp.Status} during order registration");
                        */
                    }

                    ret.Add(resp);
                }
            }, token);
            await call.Call.RequestStream.WriteAsync(envelope).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            await rtask.TimeoutAsync(TimeSpan.FromMilliseconds(ordererWaitTimeMilliSecs),token).ConfigureAwait(false);
            logger.Trace($"{this} Deliver Complete.");
            return ret;
        }

        private Exception BuildException(Exception e, string funcname)
        {
            if (e is TimeoutException)
            {
                TransactionException ste = new TransactionException($"Channel {channelName}, send {funcname} failed on orderer {this}. Reason:  timeout after {ordererWaitTimeMilliSecs} ms.", e);
                logger.ErrorException($"{ToString()} send{funcname} error {ste.Message}", ste);
                return ste;
            }

            if (e is RpcException sre)
                logger.Error($"grpc status Code:{sre.StatusCode}, Description {sre.Status.Detail} {sre.Message}");
            if (e is OperationCanceledException opc)
                return opc;
            if (e is TransactionException tra)
            {
                logger.ErrorException($"{ToString()} send{funcname} error {tra.Message}", tra);
                return tra;
            }

            TransactionException ste2 = new TransactionException($"Channel {channelName}, send {funcname} failed on orderer {this}. Reason: {e?.Message ?? "Unknown"}", e);
            logger.ErrorException($"{ToString()} send{funcname} error {ste2.Message}", ste2);
            return ste2;
        }

        public async Task<BroadcastResponse> SendTransactionAsync(Envelope envelope, CancellationToken token = default(CancellationToken))
        {
            logger.Trace($"{this} OrdererClient.sendTransaction entered.");
            if (shutdown)
                throw new TransactionException("Orderer client is shutdown");
            Grpc.Core.Channel lmanagedChannel = managedChannel;
            if (IS_TRACE_LEVEL && lmanagedChannel != null)
            {
                logger.Trace($"{this} managed channel State: {lmanagedChannel.State.ToString()}");
            }

            if (lmanagedChannel == null || lmanagedChannel.State == ChannelState.TransientFailure || lmanagedChannel.State == ChannelState.Shutdown)
            {
                if (lmanagedChannel != null && lmanagedChannel.State == ChannelState.TransientFailure)
                {
                    logger.Warn($"{this} managed channel was marked Transient Failure");
                }

                if (lmanagedChannel != null && lmanagedChannel.State == ChannelState.Shutdown)
                {
                    logger.Warn($"{this} managed channel was marked Shutdown");
                }

                lmanagedChannel = endPoint.BuildChannel();
                managedChannel = lmanagedChannel;
            }

            if (IS_TRACE_LEVEL && lmanagedChannel != null)
            {
                logger.Trace($"{this} managed channel State: {lmanagedChannel.State.ToString()}");
            }

            AtomicBroadcast.AtomicBroadcastClient nso = new AtomicBroadcast.AtomicBroadcastClient(lmanagedChannel);
            using (var call = nso.Broadcast(null, null, token).ToADStreamingCall())
            {
                try
                {
                    return await BroadcastAsync(call, envelope, token).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    managedChannel = null;
                    throw BuildException(e, "transaction");
                }
            }
        }

        public List<DeliverResponse> SendDeliver(Envelope envelope)
        {
            return SendDeliverAsync(envelope).RunAndUnwrap();
        }

        public async Task<List<DeliverResponse>> SendDeliverAsync(Envelope envelope, CancellationToken token = default(CancellationToken))
        {
            logger.Trace($"{this} OrdererClient.sendDeliver entered.");


            if (shutdown)
                throw new TransactionException("Orderer client is shutdown");
            Grpc.Core.Channel lmanagedChannel = managedChannel;
            if (IS_TRACE_LEVEL && lmanagedChannel != null)
            {
                logger.Trace($"{this} managed channel State: {lmanagedChannel.State.ToString()}");
            }

            if (lmanagedChannel == null || lmanagedChannel.State == ChannelState.TransientFailure || lmanagedChannel.State == ChannelState.Shutdown)
            {
                if (lmanagedChannel != null && lmanagedChannel.State == ChannelState.TransientFailure)
                {
                    logger.Warn($"{this} managed channel was marked Transient Failure");
                }

                if (lmanagedChannel != null && lmanagedChannel.State == ChannelState.Shutdown)
                {
                    logger.Warn($"{this} managed channel was marked Shutdown");
                }

                lmanagedChannel = endPoint.BuildChannel();
                managedChannel = lmanagedChannel;
            }

            if (IS_TRACE_LEVEL && lmanagedChannel != null)
            {
                logger.Trace($"{this} managed channel State: {lmanagedChannel.State.ToString()}");
            }

            AtomicBroadcast.AtomicBroadcastClient nso = new AtomicBroadcast.AtomicBroadcastClient(lmanagedChannel);
            using (var call = nso.Deliver(null, null, token).ToADStreamingCall())
            {
                try
                {
                    return await DeliverAsync(call, envelope, token).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    managedChannel = null;
                    throw BuildException(e, "deliver");
                }
            }
        }
        public override string ToString()
        {
            return toString;
        }
        public virtual bool IsChannelActive
        {
            get
            {
                Grpc.Core.Channel lchannel = managedChannel;
                if (null == lchannel)
                {
                    logger.Trace($"{ToString()} Grpc channel needs creation.");
                    return false;
                }
                bool isTransientFailure = lchannel.State == ChannelState.TransientFailure;
                bool isShutdown = lchannel.State == ChannelState.Shutdown;
                bool ret = !isShutdown && !isTransientFailure;
                if (IS_TRACE_LEVEL)
                    logger.Trace("%s grpc channel isActive: %b, isShutdown: %b, isTransientFailure: %b, state: %s ");
                return ret;
            }
        }

    }
}