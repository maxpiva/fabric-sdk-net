/*
 *
 *  Copyright 2016,2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
 *
 *  Licensed under the Apache License, Version 2.0 (the "License");
 *  you may not use this file except in compliance with the License.
 *  You may obtain a copy of the License at
 *     http://www.apache.org/licenses/LICENSE-2.0
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed under the License is distributed on an "AS IS" BASIS,
 *  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *  See the License for the specific language governing permissions and
 *  limitations under the License.
 *
 */

using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Hyperledger.Fabric.Protos.Common;
using Hyperledger.Fabric.Protos.Orderer;
using Hyperledger.Fabric.Protos.Peer.PeerEvents;
using Hyperledger.Fabric.SDK.Builders;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Logging;
using Config = Hyperledger.Fabric.SDK.Helper.Config;
using DeliverResponse = Hyperledger.Fabric.Protos.Peer.PeerEvents.DeliverResponse;
using Status = Hyperledger.Fabric.Protos.Common.Status;

namespace Hyperledger.Fabric.SDK
{
    /**
     * Sample client code that makes gRPC calls to the server.
     */
    public class PeerEventServiceClient
    {
        private static readonly ILog logger = LogProvider.GetLogger(typeof(PeerEventServiceClient));
        private readonly Endpoint channelBuilder;
        private readonly string channelName;
        private readonly byte[] clientTLSCertificateDigest;
        private readonly DaemonTask<Envelope, DeliverResponse> dtask = new DaemonTask<Envelope, DeliverResponse>();
        private readonly bool filterBlock;
        private readonly string name;
        private readonly long PEER_EVENT_RECONNECTION_WARNING_RATE = Config.Instance.GetPeerEventReconnectionWarningRate();
        private readonly long PEER_EVENT_REGISTRATION_WAIT_TIME = Config.Instance.GetPeerEventRegistrationWaitTime();
        private readonly long peerEventRegistrationWaitTimeMilliSecs;

        private readonly Channel.PeerOptions peerOptions;

        private readonly string url;

        private Channel.ChannelEventQue channelEventQue;

        [NonSerialized] private Grpc.Core.Channel managedChannel;


        [NonSerialized] private Peer peer;
        private string toString;

        private bool shutdown;
        private bool retry;

        /**
         * Construct client for accessing Peer eventing service using the existing managedChannel.
         */
        public PeerEventServiceClient(Peer peer, Endpoint endpoint, Properties properties, Channel.PeerOptions peerOptions)
        {
            channelBuilder = endpoint;
            filterBlock = peerOptions.IsRegisterEventsForFilteredBlocks;
            this.peer = peer;
            name = peer.Name;
            url = peer.Url;
            channelName = peer.Channel.Name;
            toString = $"PeerEventServiceClient{{id: {Config.Instance.GetNextID()}, channel: {channelName}, peerName: {name}, url: {url}}}";
            this.peerOptions = peerOptions;
            clientTLSCertificateDigest = endpoint.GetClientTLSCertificateDigest();

            channelEventQue = peer.Channel.ChannelEventQueue;

            if (null == properties)
            {
                peerEventRegistrationWaitTimeMilliSecs = PEER_EVENT_REGISTRATION_WAIT_TIME;
            }
            else
            {
 
                peerEventRegistrationWaitTimeMilliSecs = properties.GetLongProperty("peerEventRegistrationWaitTime", PEER_EVENT_REGISTRATION_WAIT_TIME);
            }
        }
        public override string ToString()
        {
            return toString;
        }
        public Channel.PeerOptions GetPeerOptions()
        {
            return peerOptions.Clone();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Shutdown(bool force)
        {
            if (shutdown)
                return;
            string me = ToString();
            logger.Debug($"{me} is shutting down.");
            try
            {
                dtask.Cancel();
            }
            catch (Exception e)
            {
                logger.Error(ToString() + " error message: " + e.Message, e);
            }
            shutdown = true;
            Grpc.Core.Channel lchannel = managedChannel;
            managedChannel = null;

            if (lchannel != null)
            {
                if (force)
                {
                    try
                    {
                        lchannel.ShutdownAsync().GetAwaiter().GetResult();
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
                        logger.DebugException(e.Message, e); //best effort
                    }

                    if (!isTerminated)
                    {
                        try
                        {
                            lchannel.ShutdownAsync().GetAwaiter().GetResult();
                        }
                        catch (Exception e)
                        {
                            logger.Debug(me + " error message: " + e.Message, e); //best effort
                        }
                    }
                }
            }
            channelEventQue = null;
            logger.Debug($"{me} is down.");
        }

        ~PeerEventServiceClient()
        {
            Shutdown(true);
        }

        public void ProcessException(Exception e, CancellationToken token = default(CancellationToken))
        {
            logger.Debug("{ToString()} Processing Exception " + e.Message + " Cancelation afterwards");
            dtask.Cancel();
            Exception fnal = null;
            if (e is TimeoutException)
            {
                string msg = $"Channel {channelName} connect time exceeded for peer eventing service {name}, timed out at {peerEventRegistrationWaitTimeMilliSecs} ms.";
                TransactionException ex = new TransactionException(msg, e);
                logger.ErrorException($"{ToString()}{msg}", e);
                fnal = ex;
            }
            else if (e is RpcException sre)
                logger.Error($"{ToString()} grpc status Code:{sre.StatusCode}, Description {sre.Status.Detail} {sre.Message}");
            else if (e is OperationCanceledException)
                logger.Error($"{ToString()} Peer Eventing service {name} canceled on channel {channelName}");
            else if (e is TransactionException tra)
                fnal = tra;

            if (fnal == null)
                fnal = new TransactionException($"{ToString()} Channel {channelName}, send eventing service failed on orderer {name}. Reason: {e.Message}", e);
            Grpc.Core.Channel lmanagedChannel = managedChannel;
            if (lmanagedChannel != null)
            {
                try
                {
                    lmanagedChannel.ShutdownAsync().GetAwaiter().GetResult();
                    managedChannel = null;
                }
                catch (Exception)
                {
                    logger.WarnException($"{ToString()} Received error on peer eventing service on channel {channelName}, peer {name}, url {url}, attempts {(peer?.ReconnectCount ?? -1).ToString()}. {e.Message} shut down of grpc channel.",e);
                }
            }

            if (!shutdown)
            {
                if (peer != null)
                {
                    long reconnectCount = peer.ReconnectCount;
                    if (PEER_EVENT_RECONNECTION_WARNING_RATE > 1 && reconnectCount % PEER_EVENT_RECONNECTION_WARNING_RATE == 1)
                        logger.Warn($"{ToString()} Received error on peer eventing service on channel {channelName}, peer {name}, url {url}, attempts {reconnectCount}. {e.Message}");
                    else
                        logger.Trace($"{ToString()} Received error on peer eventing service on channel {channelName}, peer {name}, url {url}, attempts {reconnectCount}. {e.Message}");
                    if (retry)
                        peer.ReconnectPeerEventServiceClient(this, fnal, token);
                }
                retry = false;
            }
            else
                logger.Trace($"{ToString()} was shutdown.");
        }

        private async Task DeliverAsync(Grpc.Core.Channel channel, Envelope envelope, CancellationToken token)
        {
            retry = true;
            try
            {
                var ch = filterBlock ? new Deliver.DeliverClient(channel).DeliverFiltered() : new Deliver.DeliverClient(channel).Deliver();

                Task connect = dtask.ConnectAsync(ch,
                    (resp) =>
                {
                    logger.Trace($"{ToString()}DeliverResponse channel {channelName} peer {peer.Name} resp status value:{resp.Status} typecase {resp.TypeCase}");
                    switch (resp.TypeCase)
                    {
                        case DeliverResponse.TypeOneofCase.Status:
                            logger.Debug($"{ToString()}DeliverResponse channel {channelName} peer {peer.Name} setting done.");
                            if (resp.Status == Hyperledger.Fabric.Protos.Common.Status.Success)
                            {
                                // unlike you may think this only happens when all blocks are fetched.
                                peer.LastConnectTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                                peer.ResetReconnectCount();
                                return ProcessResult.ConnectionComplete;
                            }

                            long rec = peer.ReconnectCount;
                            PeerEventingServiceException peerEventingServiceException = new PeerEventingServiceException($"Channel {channelName} peer {peer.Name} attempts {rec} Status returned failure code {(int) resp.Status} ({resp.Status.ToString()}) during peer service event registration");
                            peerEventingServiceException.Response = resp;
                            if (rec % 10 == 0)
                                logger.Warn(peerEventingServiceException.Message);
                            throw peerEventingServiceException;
                        case DeliverResponse.TypeOneofCase.FilteredBlock:
                        case DeliverResponse.TypeOneofCase.Block:
                            if (resp.TypeCase == DeliverResponse.TypeOneofCase.Block)
                                logger.Trace($"{ToString()} Channel {channelName} peer {peer.Name} got event block hex hashcode: {resp.Block.GetHashCode():X8}, block number: {resp.Block.Header.Number}");
                            else
                                logger.Trace($"{ToString()} Channel {channelName} peer {peer.Name} got event block hex hashcode: {resp.FilteredBlock.GetHashCode():X8}, block number: {resp.FilteredBlock.Number}");
                            peer.LastConnectTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                            long reconnectCount = peer.ReconnectCount;
                            if (reconnectCount > 1)
                                logger.Info($"{ToString()} Peer eventing service reconnected after {reconnectCount} attempts on channel {channelName}, peer {name}, url {url}");
                            peer.ResetReconnectCount();
                            BlockEvent blockEvent = new BlockEvent(peer, resp);
                            peer.SetLastBlockSeen(blockEvent);
                            channelEventQue.AddBEvent(blockEvent);
                            return ProcessResult.ConnectionComplete;
                        default:
                            PeerEventingServiceException peerEventingServiceException2 = new PeerEventingServiceException($"Channel {channelName} peer {peer.Name} got event block with unknown type: {resp.TypeCase.ToString()}, {(int) resp.TypeCase}");
                            peerEventingServiceException2.Response = resp;
                            throw peerEventingServiceException2;
                    }
                }, 
                    (ex) => ProcessException(ex, token),
                     token);
                if (token.IsCancellationRequested)
                    dtask.Cancel();
                token.ThrowIfCancellationRequested();
                await dtask.Sender.Call.RequestStream.WriteAsync(envelope).ConfigureAwait(false);
                await connect.TimeoutAsync(TimeSpan.FromMilliseconds(peerEventRegistrationWaitTimeMilliSecs),token).ConfigureAwait(false);
                logger.Debug($"{ToString()} DeliverResponse onCompleted channel {channelName} peer {peer.Name} setting done.");
            }
            catch (TimeoutException)
            {
                PeerEventingServiceException ex = new PeerEventingServiceException($"{ToString()} Channel {channelName} connect time exceeded for peer eventing service {name}, timed out at {peerEventRegistrationWaitTimeMilliSecs} ms.");
                ex.TimedOut=peerEventRegistrationWaitTimeMilliSecs;
                ProcessException(ex,token);
            }
            catch (Exception e)
            {
                ProcessException(e, token);
            }
           
        }

        /**
         * Get the last block received by this peer.
         *
         * @return The last block received by this peer. May return null if no block has been received since first reactivated.
         */


        private Task ConnectEnvelopeAsync(Envelope envelope, CancellationToken token)
        {
            if (shutdown)
            {
                logger.Warn($"{ToString()} not connecting is shutdown.");
                return Task.FromResult(0);
            }
            Grpc.Core.Channel lmanagedChannel = managedChannel;
            if (lmanagedChannel == null || lmanagedChannel.State == ChannelState.Shutdown || lmanagedChannel.State == ChannelState.TransientFailure)
            {
                lmanagedChannel = channelBuilder.BuildChannel();
                managedChannel = lmanagedChannel;
            }
            return DeliverAsync(lmanagedChannel, envelope, token);
        }

        public bool IsChannelActive()
        {
            Grpc.Core.Channel lchannel = managedChannel;
            return lchannel != null && lchannel.State != ChannelState.Shutdown && lchannel.State != ChannelState.TransientFailure;
        }

        public Task ConnectAsync(TransactionContext tcontext, CancellationToken token = default(CancellationToken))
        {
            if (shutdown)
                return Task.FromResult(0);
            return PeerVentAsync(tcontext, token);
        }

        //=========================================================
        // Peer eventing
        public Task PeerVentAsync(TransactionContext tcontext, CancellationToken token = default(CancellationToken))
        {
            try
            {
                SeekPosition start = new SeekPosition();
                if (peerOptions.Newest != null)
                {
                    start.Newest = new SeekNewest();
                }
                else if (peerOptions.StartEventsBlock != null)
                {
                    start.Specified = new SeekSpecified {Number = (ulong) peerOptions.StartEventsBlock.Value};
                }
                else
                {
                    start.Newest = new SeekNewest();
                }

                //   properties.

                Envelope envelope = ProtoUtils.CreateSeekInfoEnvelope(tcontext, start, new SeekPosition {Specified = new SeekSpecified {Number = (ulong) peerOptions.StopEventsBlock}}, SeekInfo.Types.SeekBehavior.BlockUntilReady, clientTLSCertificateDigest);
                return ConnectEnvelopeAsync(envelope, token);
            }
            catch (CryptoException e)
            {
                throw new TransactionException($"{ToString()} error message {e.Message}",e);
            }
        }

        public string Status
        {
            get
            {
                Grpc.Core.Channel lmanagedChannel = managedChannel;
                if (lmanagedChannel == null)
                {
                    return "No grpc managed channel active. peer eventing client service is shutdown: " + shutdown;
                }
                else
                {
                    StringBuilder sb = new StringBuilder(1000);

                    sb.Append("peer eventing client service is shutdown: ").Append(shutdown)
                        .Append(", grpc isShutdown: ").Append(lmanagedChannel.State==ChannelState.Shutdown)
                        .Append(", grpc isTransientFailure: ").Append(lmanagedChannel.State==ChannelState.TransientFailure)
                        .Append(", grpc state: ").Append("" + lmanagedChannel.State);
                    return sb.ToString();
                }
            }


        }
    }
}