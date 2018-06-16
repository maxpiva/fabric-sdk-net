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
        private bool shutdown;


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

        public Channel.PeerOptions GetPeerOptions()
        {
            return peerOptions.Clone();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Shutdown(bool force)
        {
            if (shutdown)
            {
                return;
            }
            logger.Debug("Starting shutdown");
            dtask.Cancel();
            shutdown = true;
            Grpc.Core.Channel lchannel = managedChannel;
            managedChannel = null;
            lchannel?.ShutdownAsync().Wait();
            peer = null;
            channelEventQue = null;
        }

        ~PeerEventServiceClient()
        {
            Shutdown(true);
        }

        public void ProcessException(Exception e, CancellationToken token = default(CancellationToken))
        {
            logger.Debug("Processing Exception " + e.Message + " Cancelation afterwards");
            dtask.Cancel();
            Exception fnal = null;
            if (e is TimeoutException)
            {
                string msg = $"Channel {channelName} connect time exceeded for peer eventing service {name}, timed out at {peerEventRegistrationWaitTimeMilliSecs} ms.";
                TransactionException ex = new TransactionException(msg, e);
                logger.ErrorException(msg, e);
                fnal = ex;
            }
            else if (e is RpcException sre)
                logger.Error($"grpc status Code:{sre.StatusCode}, Description {sre.Status.Detail} {sre.Message}");
            else if (e is OperationCanceledException)
                logger.Error($"(Peer Eventing service {name} canceled on channel {channelName}");
            else if (e is TransactionException tra)
                fnal = tra;

            if (fnal == null)
                fnal = new TransactionException($"Channel {channelName}, send eventing service failed on orderer {name}. Reason: {e.Message}", e);
            Grpc.Core.Channel lmanagedChannel = managedChannel;
            if (lmanagedChannel != null)
            {
                lmanagedChannel.ShutdownAsync().GetAwaiter().GetResult();
                managedChannel = null;
            }

            if (!shutdown)
            {
                long reconnectCount = peer.ReconnectCount;
                if (PEER_EVENT_RECONNECTION_WARNING_RATE > 1 && reconnectCount % PEER_EVENT_RECONNECTION_WARNING_RATE == 1)
                    logger.Warn($"Received error on peer eventing service on channel {channelName}, peer {name}, url {url}, attempts {reconnectCount}. {e.Message}");
                else
                    logger.Trace($"Received error on peer eventing service on channel {channelName}, peer {name}, url {url}, attempts {reconnectCount}. {e.Message}");
                peer.ReconnectPeerEventServiceClient(this, fnal, token);
            }
            else
                logger.Trace($"{name} was shutdown.");
        }

        private async Task Deliver(Deliver.DeliverClient broadcast, Envelope envelope, CancellationToken token)
        {
            var call = filterBlock ? broadcast.DeliverFiltered().ToADStreamingCall() : broadcast.Deliver().ToADStreamingCall();
            try
            {
                Task connect = dtask.Connect(call, (resp) =>
                {
                    logger.Trace($"DeliverResponse channel {channelName} peer {peer.Name} resp status value:{resp.Status} typecase {resp.TypeCase}");
                    switch (resp.TypeCase)
                    {
                        case DeliverResponse.TypeOneofCase.Status:
                            logger.Debug($"DeliverResponse channel {channelName} peer {peer.Name} setting done.");
                            if (resp.Status == Status.Success)
                            {
                                // unlike you may think this only happens when all blocks are fetched.
                                peer.LastConnectTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                                peer.ResetReconnectCount();
                                return ProcessResult.ConnectionComplete;
                            }

                            throw new TransactionException($"Channel {channelName} peer {peer.Name} Status returned failure code {resp.Status} during peer service event registration");
                        case DeliverResponse.TypeOneofCase.FilteredBlock:
                        case DeliverResponse.TypeOneofCase.Block:
                            if (resp.TypeCase == DeliverResponse.TypeOneofCase.Block)
                                logger.Trace($"Channel {channelName} peer {peer.Name} got event block hex hashcode: {resp.Block.GetHashCode():X8}, block number: {resp.Block.Header.Number}");
                            else
                                logger.Trace($"Channel {channelName} peer {peer.Name} got event block hex hashcode: {resp.FilteredBlock.GetHashCode():X8}, block number: {resp.FilteredBlock.Number}");
                            peer.LastConnectTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                            long reconnectCount = peer.ReconnectCount;
                            if (reconnectCount > 1)
                                logger.Info($"Peer eventing service reconnected after {reconnectCount} attempts on channel {channelName}, peer {name}, url {url}");
                            peer.ResetReconnectCount();
                            BlockEvent blockEvent = new BlockEvent(peer, resp);
                            peer.SetLastBlockSeen(blockEvent);
                            channelEventQue.AddBEvent(blockEvent);
                            return ProcessResult.ConnectionComplete;
                        default:
                            logger.Error($"Channel {channelName} peer {peer.Name} got event block with unknown type: {resp.TypeCase}");
                            throw new TransactionException($"Channel  {channelName} peer {peer.Name} Status got unknown type {resp.TypeCase}");
                    }
                }, (ex) => ProcessException(ex), token);
                await call.Call.RequestStream.WriteAsync(envelope);
                if (token.IsCancellationRequested)
                    dtask.Cancel();
                token.ThrowIfCancellationRequested();
                await connect.Timeout(TimeSpan.FromMilliseconds(peerEventRegistrationWaitTimeMilliSecs));
                logger.Debug($"DeliverResponse onCompleted channel {channelName} peer {peer.Name} setting done.");
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


        private async Task ConnectEnvelope(Envelope envelope, CancellationToken token)
        {
            if (shutdown)
                throw new TransactionException("Peer eventing client is shutdown");
            Grpc.Core.Channel lmanagedChannel = managedChannel;
            if (lmanagedChannel == null || lmanagedChannel.State == ChannelState.Shutdown || lmanagedChannel.State == ChannelState.TransientFailure)
            {
                lmanagedChannel = channelBuilder.BuildChannel();
                managedChannel = lmanagedChannel;
            }
            await Deliver(new Deliver.DeliverClient(lmanagedChannel), envelope, token);
        }

        public bool IsChannelActive()
        {
            Grpc.Core.Channel lchannel = managedChannel;
            return lchannel != null && lchannel.State != ChannelState.Shutdown && lchannel.State != ChannelState.TransientFailure;
        }

        public async Task Connect(TransactionContext tcontext, CancellationToken token = default(CancellationToken))
        {
            await PeerVent(tcontext, token);
        }

        //=========================================================
        // Peer eventing
        public async Task PeerVent(TransactionContext tcontext, CancellationToken token = default(CancellationToken))
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
                await ConnectEnvelope(envelope, token);
            }
            catch (CryptoException e)
            {
                throw new TransactionException(e);
            }
        }
    }
}