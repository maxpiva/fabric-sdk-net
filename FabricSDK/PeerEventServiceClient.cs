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
/*
package org.hyperledger.fabric.sdk;

import java.util.ArrayList;
import java.util.List;
import java.util.Properties;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;

import io.grpc.ManagedChannel;
import io.grpc.ManagedChannelBuilder;
import io.grpc.stub.StreamObserver;
import org.apache.commons.logging.Log;
import org.apache.commons.logging.LogFactory;
import org.hyperledger.fabric.protos.common.Common;
import org.hyperledger.fabric.protos.common.Common.Envelope;
import org.hyperledger.fabric.protos.orderer.Ab;
import org.hyperledger.fabric.protos.orderer.Ab.SeekInfo;
import org.hyperledger.fabric.protos.peer.DeliverGrpc;
import org.hyperledger.fabric.protos.peer.PeerEvents.DeliverResponse;
import org.hyperledger.fabric.sdk.Channel.PeerOptions;
import org.hyperledger.fabric.sdk.exception.CryptoException;
import org.hyperledger.fabric.sdk.exception.TransactionException;
import org.hyperledger.fabric.sdk.helper.Config;
import org.hyperledger.fabric.sdk.transaction.TransactionContext;

import static java.lang.String.format;
import static org.hyperledger.fabric.protos.peer.PeerEvents.DeliverResponse.TypeCase.BLOCK;
import static org.hyperledger.fabric.protos.peer.PeerEvents.DeliverResponse.TypeCase.FILTERED_BLOCK;
import static org.hyperledger.fabric.protos.peer.PeerEvents.DeliverResponse.TypeCase.STATUS;
import static org.hyperledger.fabric.sdk.transaction.ProtoUtils.createSeekInfoEnvelope;
   */

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using Grpc.Core;
using Hyperledger.Fabric.Protos.Common;
using Hyperledger.Fabric.Protos.Orderer;
using Hyperledger.Fabric.Protos.Peer.PeerEvents;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Logging;

using Hyperledger.Fabric.SDK.Transaction;
using DeliverResponse = Hyperledger.Fabric.Protos.Peer.PeerEvents.DeliverResponse;
using Status = Hyperledger.Fabric.Protos.Common.Status;

namespace Hyperledger.Fabric.SDK
{


    /**
     * Sample client code that makes gRPC calls to the server.
     */
    public class PeerEventServiceClient
    {

        private readonly long PEER_EVENT_REGISTRATION_WAIT_TIME = Helper.Config.Instance.GetPeerEventRegistrationWaitTime();
        private readonly long PEER_EVENT_RECONNECTION_WARNING_RATE = Helper.Config.Instance.GetPeerEventReconnectionWarningRate();
        private static readonly ILog logger = LogProvider.GetLogger(typeof(PeerEventServiceClient));
        private readonly string channelName;
        private readonly Endpoint channelBuilder;
        private readonly string name;
        private readonly string url;
        private readonly long peerEventRegistrationWaitTimeMilliSecs;

        private readonly Channel.PeerOptions peerOptions;
        private readonly bool filterBlock;
        private byte[] clientTLSCertificateDigest;
        Properties properties = new Properties();
        AsyncDuplexStreamingCall<Envelope,DeliverResponse> nso = null;

        private Channel.ChannelEventQue channelEventQue;
        private bool shutdown = false;
        [NonSerialized]
        private Grpc.Core.Channel managedChannel = null;
    [NonSerialized]
        private TransactionContext transactionContext;
    [NonSerialized]
        private Peer peer;

        /**
         * Construct client for accessing Peer eventing service using the existing managedChannel.
         */
        public PeerEventServiceClient(Peer peer, Endpoint endpoint, Properties properties, Channel.PeerOptions peerOptions)
        {

            this.channelBuilder = endpoint;
            this.filterBlock = peerOptions.IsRegisterEventsForFilteredBlocks();
            this.peer = peer;
            name = peer.Name;
            url = peer.Url;
            channelName = peer.GetChannel().Name;
            this.peerOptions = peerOptions;
            clientTLSCertificateDigest = endpoint.GetClientTLSCertificateDigest();

            this.channelEventQue = peer.GetChannel().GetChannelEventQue();

            if (null == properties) {

                peerEventRegistrationWaitTimeMilliSecs = PEER_EVENT_REGISTRATION_WAIT_TIME;

            } else {
                this.properties = properties;
                peerEventRegistrationWaitTimeMilliSecs = properties.GetLongProperty("peerEventRegistrationWaitTime", PEER_EVENT_REGISTRATION_WAIT_TIME);
            }

        }

        public Channel.PeerOptions GetPeerOptions()
        {
            return peerOptions.Clone();
        }
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Shutdown(bool force) {

            if (shutdown) {
                return;
            }
            shutdown = true;
            var lsno = nso;
            nso = null;
            if (null != lsno) {
                try {
                    lsno.Dispose();
                } catch (Exception e) {
                   
                }
            }

            Grpc.Core.Channel lchannel = managedChannel;
            managedChannel = null;
            if (lchannel != null) {

                if (force) {
                    lchannel.ShutdownAsync().Wait();
                } else {
                    bool isTerminated = false;

                    try
                    {
                        lchannel.ShutdownAsync().Wait(3 * 1000);
                    } catch (Exception e) {
                        logger.DebugException(e.Message,e); //best effort
                    }
                    if (!isTerminated) {
                        lchannel.ShutdownAsync().Wait();
                    }
                }
            }
            peer = null;
            channelEventQue = null;

        }

        ~PeerEventServiceClient()
        {
            Shutdown(true);
        }

        /**
         * Get the last block received by this peer.
         *
         * @return The last block received by this peer. May return null if no block has been received since first reactivated.
         */

        void ConnectEnvelope(Envelope envelope) {

            if (shutdown) {
                throw new TransactionException("Peer eventing client is shutdown");
            }

            Grpc.Core.Channel lmanagedChannel = managedChannel;

            if (lmanagedChannel == null || lmanagedChannel.State==ChannelState.Shutdown || lmanagedChannel.State==ChannelState.TransientFailure) {

                lmanagedChannel = channelBuilder.BuildChannel();
                managedChannel = lmanagedChannel;

            }

            try
            {

                Deliver.DeliverClient broadcast = new Deliver.DeliverClient(lmanagedChannel);

                // final DeliverResponse[] ret = new DeliverResponse[1];
                //   final List<DeliverResponse> retList = new ArrayList<>();
                List<Exception> throwableList = new List<Exception>();
                CountDownLatch finishLatch = new CountDownLatch(1);

                using (nso = filterBlock ? broadcast.DeliverFiltered() : broadcast.Deliver())
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            while (await nso.ResponseStream.MoveNext())
                            {
                                DeliverResponse resp = nso.ResponseStream.Current;
                                logger.Trace($"DeliverResponse channel {channelName} peer {peer.Name} resp status value:{resp.Status} typecase {resp.TypeCase}");
                                switch (resp.TypeCase)
                                {
                                    case DeliverResponse.TypeOneofCase.Status:
                                        logger.Debug($"DeliverResponse channel {channelName} peer {peer.Name} setting done.");
                                        if (resp.Status == Status.Success)
                                        {
                                            // unlike you may think this only happens when all blocks are fetched.
                                            peer.SetLastConnectTime(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                                            peer.ResetReconnectCount();
                                        }
                                        else
                                        {
                                            throwableList.Add(new TransactionException($"Channel {channelName} peer {peer.Name} Status returned failure code {resp.Status} during peer service event registration"));
                                        }

                                        break;
                                    case DeliverResponse.TypeOneofCase.FilteredBlock:
                                    case DeliverResponse.TypeOneofCase.Block:
                                        if (resp.TypeCase == DeliverResponse.TypeOneofCase.Block)
                                        {
                                            logger.Trace($"Channel {channelName} peer {peer.Name} got event block hex hashcode: {resp.Block.GetHashCode():X8}, block number: {resp.Block.Header.Number}");
                                        }
                                        else
                                        {
                                            logger.Trace($"Channel {channelName} peer {peer.Name} got event block hex hashcode: {resp.FilteredBlock.GetHashCode():X8}, block number: {resp.FilteredBlock.Number}");
                                        }

                                        peer.SetLastConnectTime(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                                        long reconnectCount = peer.GetReconnectCount();
                                        if (reconnectCount > 1)
                                        {

                                            logger.Info($"Peer eventing service reconnected after {reconnectCount} attempts on channel {channelName}, peer {name}, url {url}");

                                        }

                                        peer.ResetReconnectCount();

                                        BlockEvent blockEvent = new BlockEvent(peer, resp);
                                        peer.SetLastBlockSeen(blockEvent);

                                        channelEventQue.AddBEvent(blockEvent);
                                        break;
                                    default:
                                        logger.Error($"Channel {channelName} peer {peer.Name} got event block with unknown type: {resp.TypeCase}");
                                        throwableList.Add(new TransactionException($"Channel  {channelName} peer {peer.Name} Status got unknown type {resp.TypeCase}"));
                                        break;
                                }

                                finishLatch.Signal();
                            }

                            logger.Debug($"DeliverResponse onCompleted channel {channelName} peer {peer.Name} setting done.");
                            finishLatch.Signal();
                        }
                        catch (Exception e)
                        {
                            Grpc.Core.Channel llmanagedChannel = managedChannel;
                            if (llmanagedChannel != null)
                            {
                                llmanagedChannel.ShutdownAsync().Wait();
                                managedChannel = null;
                            }

                            if (!shutdown)
                            {
                                long reconnectCount = peer.GetReconnectCount();
                                if (PEER_EVENT_RECONNECTION_WARNING_RATE > 1 && reconnectCount % PEER_EVENT_RECONNECTION_WARNING_RATE == 1)
                                {
                                    logger.Warn($"Received error on peer eventing service on channel {channelName}, peer {name}, url {url}, attempts {reconnectCount}. {e.Message}");
                                }
                                else
                                {
                                    logger.Trace($"Received error on peer eventing service on channel {channelName}, peer {name}, url {url}, attempts {reconnectCount}. {e.Message}");

                                }

                                peer.ReconnectPeerEventServiceClient(this, e);

                            }

                            finishLatch.Signal();
                        }
                    });
                    nso.RequestStream.WriteAsync(envelope);
                    if (!finishLatch.Wait((int) peerEventRegistrationWaitTimeMilliSecs))
                    {
                        TransactionException ex = new TransactionException($"Channel {channelName} connect time exceeded for peer eventing service {name}, timed out at {peerEventRegistrationWaitTimeMilliSecs} ms.");
                        throwableList.Insert(0, ex);

                    }

                    logger.Trace("Done waiting for reply!");

                    if (throwableList.Count > 0)
                    {
                        Grpc.Core.Channel llmanagedChannel = managedChannel;
                        if (llmanagedChannel != null)
                        {
                            llmanagedChannel.ShutdownAsync().Wait();
                            managedChannel = null;
                        }

                        Exception throwable = throwableList[0];
                        peer.ReconnectPeerEventServiceClient(this, throwable);

                    }
                }
            }
            catch (Exception e) {
                Grpc.Core.Channel llmanagedChannel = managedChannel;
                if (llmanagedChannel != null)
                {
                    llmanagedChannel.ShutdownAsync().Wait();
                    managedChannel = null;
                }
                logger.ErrorException(e.Message,e); // not likely

                peer.ReconnectPeerEventServiceClient(this, e);

            }
            /*
            finally {
                if (null != nso) {

                    try {
                        nso.onCompleted();
                    } catch (Exception e) {  //Best effort only report on debug
                        logger.debug(format("Exception completing connect with channel %s,  name %s, url %s %s",
                                channelName, name, url, e.getMessage()), e);
                    }

                }
            }*/
        }

        public bool IsChannelActive() {
            Grpc.Core.Channel lchannel = managedChannel;
            return lchannel != null && lchannel.State != ChannelState.Shutdown && lchannel.State != ChannelState.TransientFailure;
        }

        public void Connect(TransactionContext transactionContext)
        {

            this.transactionContext = transactionContext;
            PeerVent(transactionContext);

        }

        //=========================================================
        // Peer eventing
        public void PeerVent(TransactionContext transactionContext)
        {
            try {

                SeekPosition start=new SeekPosition();
                if (peerOptions.Newest!=null)
                {
                    start.Newest = new SeekNewest();
                } else if (peerOptions.StartEventsBlock!=null)
                {
                    start.Specified =new SeekSpecified { Number=(ulong)peerOptions.StartEventsBlock.Value};
                } else
                {
                    start.Newest = new SeekNewest();
                }

                //   properties.

                Envelope envelope = ProtoUtils.CreateSeekInfoEnvelope(transactionContext, start, new SeekPosition {Specified = new SeekSpecified {Number = (ulong) peerOptions.StopEventsBlock}},SeekInfo.Types.SeekBehavior.BlockUntilReady, clientTLSCertificateDigest); 
                ConnectEnvelope(envelope);
            } catch (CryptoException e) {
                throw new TransactionException(e);
            }

        }

    }
}