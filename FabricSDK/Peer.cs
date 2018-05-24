/*
 *  Copyright 2016, 2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
 *
 *  Licensed under the Apache License, Version 2.0 (the "License");
 *  you may not use this file except in compliance with the License.
 *  You may obtain a copy of the License at`
 *    http://www.apache.org/licenses/LICENSE-2.0
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed under the License is distributed on an "AS IS" BASIS,
 *  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *  See the License for the specific language governing permissions and
 *  limitations under the License.
 */
/*
package org.hyperledger.fabric.sdk;

import java.io.IOException;
import java.io.ObjectInputStream;
import java.io.Serializable;
import java.util.EnumSet;
import java.util.Objects;
import java.util.Properties;
import java.util.concurrent.ExecutorService;

import com.google.common.util.concurrent.ListenableFuture;
import io.netty.util.internal.StringUtil;
import org.apache.commons.logging.Log;
import org.apache.commons.logging.LogFactory;
import org.hyperledger.fabric.protos.peer.FabricProposal;
import org.hyperledger.fabric.protos.peer.FabricProposalResponse;
import org.hyperledger.fabric.sdk.Channel.PeerOptions;
import org.hyperledger.fabric.sdk.exception.InvalidArgumentException;
import org.hyperledger.fabric.sdk.exception.PeerException;
import org.hyperledger.fabric.sdk.exception.TransactionException;
import org.hyperledger.fabric.sdk.helper.Config;
import org.hyperledger.fabric.sdk.transaction.TransactionContext;

import static java.lang.String.format;
import static org.hyperledger.fabric.sdk.helper.Utils.checkGrpcUrl;
*/
/**
 * The Peer class represents a peer to which SDK sends deploy, or query proposals requests.
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Force.DeepCloner;
using Hyperledger.Fabric.Protos.Peer.FabricProposal;
using Hyperledger.Fabric.SDK;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Logging;
using Hyperledger.Fabric.SDK.Transaction;


namespace Hyperledger.Fabric.SDK
{

    /**
* Endorsing peer installs and runs chaincode.
*/

    public enum PeerRole
    {
        ENDORSING_PEER,
        CHAINCODE_QUERY,
        LEDGER_QUERY,
        EVENT_SOURCE
    }

    public static class PeerRoleExtensions
    {

        public static string ToValue(this PeerRole role)
        {
            switch (role)
            {
                case PeerRole.ENDORSING_PEER:
                    return "endorsingPeer";
                case PeerRole.CHAINCODE_QUERY:
                    return "chaincodeQuery";
                case PeerRole.LEDGER_QUERY:
                    return "ledgerQuery";
                case PeerRole.EVENT_SOURCE:
                    return "eventSource";
            }

            return string.Empty;
        }

        public static List<PeerRole> All() => Enum.GetValues(typeof(PeerRole)).Cast<PeerRole>().ToList();
        public static List<PeerRole> NoEventSource() => Enum.GetValues(typeof(PeerRole)).Cast<PeerRole>().Except(new [] { PeerRole.EVENT_SOURCE}).ToList();

        public static PeerRole FromValue(this string value)
        {
            switch (value)
            {
                case "endorsingPeer":
                    return PeerRole.ENDORSING_PEER;
                case "chaincodeQuery":
                    return PeerRole.CHAINCODE_QUERY;
                case "ledgerQuery":
                    return PeerRole.LEDGER_QUERY;
                case "eventSource":
                    return PeerRole.EVENT_SOURCE;
            }

            return PeerRole.CHAINCODE_QUERY;
        }
    }
    /**
* Possible roles a peer can perform.
*/
    public class Peer : IEquatable<Peer>
    {

        private static readonly ILog logger = LogProvider.GetLogger(typeof(Peer));
        private static readonly Config config = Config.GetConfig();
        private static readonly long PEER_EVENT_RETRY_WAIT_TIME = config.GetPeerRetryWaitTime();
        private readonly Dictionary<string, object> properties;

        [NonSerialized]
        private EndorserClient endorserClent;
        [NonSerialized]
        private PeerEventServiceClient peerEventingClient;
        [NonSerialized]
        private bool shutdown = false;
        private Channel channel;
        [NonSerialized]
        private TransactionContext transactionContext;
        [NonSerialized]
        private long lastConnectTime;
        [NonSerialized]
        private long reconnectCount;
        [NonSerialized]
        private BlockEvent lastBlockEvent;
        [NonSerialized]
        private long lastBlockNumber;

        public Peer(string name, string grpcURL, Dictionary<string, object> properties)
        {

            Exception e = Utils.CheckGrpcUrl(grpcURL);
            if (e != null) {
                throw new InvalidArgumentException("Bad peer url.", e);

            }

            if (string.IsNullOrEmpty(name)) {
                throw new InvalidArgumentException("Invalid name for peer");
            }

            this.Url = grpcURL;
            this.Name = name;
            this.properties = properties?.DeepClone();
            reconnectCount = 0L;

        }

        public string Url { get; }
        public static Peer Create(string name, string grpcURL, Dictionary<string, object> properties)
        {
            return new Peer(name, grpcURL, properties);
        }

        /**
         * Peer's name
         *
         * @return return the peer's name.
         */
        public string Name { get; }

        public Dictionary<string, object> GetProperties()
        {
            return properties?.DeepClone();
        }

        public void UnsetChannel() {
            channel = null;
        }

        public BlockEvent getLastBlockEvent() {
            return lastBlockEvent;
        }



        TaskScheduler GetExecutorService() {
            return channel.ExecutorService;
        }

        public void InitiateEventing(TransactionContext transactionContext, Channel.PeerOptions peersOptions)
        {

            this.transactionContext = transactionContext.RetryTransactionSameContext();
            
            if (peerEventingClient == null) {

                //PeerEventServiceClient(Peer peer, ManagedChannelBuilder<?> channelBuilder, Properties properties)
                //   peerEventingClient = new PeerEventServiceClient(this, new HashSet<Channel>(Arrays.asList(new Channel[] {channel})));

                peerEventingClient = new PeerEventServiceClient(this, new Endpoint(Url, properties), properties, peersOptions);

                peerEventingClient.Connect(transactionContext);

            }

        }

        /**
         * The channel the peer is set on.
         *
         * @return
         */

        public Channel GetChannel() {

            return channel;

        }

        /**
         * Set the channel the peer is on.
         *
         * @param channel
         */

        public void SetChannel(Channel channel)
        {

            if (null != this.channel) {
                throw new InvalidArgumentException($"Can not add peer {Name} to channel {channel.Name} because it already belongs to channel {this.channel.Name}.");
            }

            this.channel = channel;

        }

        /**
         * Get the URL of the peer.
         *
         * @return {string} Get the URL associated with the peer.
         */


        /**
         * for use in list of peers comparisons , e.g. list.contains() calls
         *
         * @param otherPeer the peer instance to compare against
         * @return true if both peer instances have the same name and url
         */

        public bool Equals(Peer other)
        {
            if (this == other)
            {
                return true;
            }
            if (other == null)
            {
                return false;
            }

            return this.Name.Equals(other.Name) && this.Url.Equals(other.Url);
        }



        public TaskCompletionSource<Protos.Peer.FabricProposalResponse.ProposalResponse> SendProposalAsync(SignedProposal proposal)
        {
            TaskCompletionSource<Protos.Peer.FabricProposalResponse.ProposalResponse> src=new TaskCompletionSource<Protos.Peer.FabricProposalResponse.ProposalResponse>();

            CheckSendProposal(proposal);

            logger.Debug($"peer.sendProposalAsync name: {Name}, url: {Url}");
                
            EndorserClient localEndorserClient = endorserClent; //work off thread local copy.

            if (null == localEndorserClient || !localEndorserClient.IsChannelActive()) {
                endorserClent = new EndorserClient(new Endpoint(Url, properties));
                localEndorserClient = endorserClent;
            }

            try
            {
                Task.Factory.StartNew(async () =>
                {
                    Protos.Peer.FabricProposalResponse.ProposalResponse resp = await localEndorserClient.SendProposalAsync(proposal);
                    src.SetResult(resp);
                }, default(CancellationToken), TaskCreationOptions.None, GetExecutorService());
            } catch (Exception t) {
                endorserClent = null;
                throw t;
            }
            return src;
        }

        private void CheckSendProposal(SignedProposal proposal) {

            if (shutdown) {
                throw new PeerException($"Peer {Name} was shutdown.");
            }
            if (proposal == null) {
                throw new PeerException("Proposal is null");
            }
            Exception e = Utils.CheckGrpcUrl(Url);
            if (e != null) {
                throw new InvalidArgumentException("Bad peer url.", e);

            }
        }
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Shutdown(bool force) {
            if (shutdown) {
                return;
            }
            shutdown = true;
            channel = null;
            lastBlockEvent = null;
            lastBlockNumber = 0;

            EndorserClient lendorserClent = endorserClent;

            //allow resources to finalize

            endorserClent = null;

            if (lendorserClent != null) {

                lendorserClent.Shutdown(force);
            }

            PeerEventServiceClient lpeerEventingClient = peerEventingClient;
            peerEventingClient = null;

            if (null != lpeerEventingClient) {
                // PeerEventServiceClient peerEventingClient1 = peerEventingClient;

                lpeerEventingClient.Shutdown(force);
            }
        }

        ~Peer()
        {
            Shutdown(true);
        }

        public void ReconnectPeerEventServiceClient(PeerEventServiceClient failedPeerEventServiceClient, Exception throwable) {
            if (shutdown) {
                logger.Debug("Not reconnecting PeerEventServiceClient shutdown ");
                return;

            }
            IPeerEventingServiceDisconnected ldisconnectedHandler = _disconnectedHandler;
            if (null == ldisconnectedHandler) {

                return; // just wont reconnect.

            }
            TransactionContext ltransactionContext = transactionContext;
            if (ltransactionContext == null) {

                logger.Warn("Not reconnecting PeerEventServiceClient no transaction available ");
                return;
            }

            TransactionContext fltransactionContext = ltransactionContext.RetryTransactionSameContext();

            TaskScheduler executorService = GetExecutorService();
            Channel.PeerOptions peerOptions = null != failedPeerEventServiceClient.GetPeerOptions() ? failedPeerEventServiceClient.GetPeerOptions() :
                    Channel.PeerOptions.CreatePeerOptions();
            if (executorService != null)
            {
                Task.Factory.StartNew(() =>
                    {
                        ldisconnectedHandler.Disconnected(new PeerEventingServiceDisconnectEvent(this,throwable,peerOptions,fltransactionContext));

                    },default(CancellationToken), TaskCreationOptions.None, executorService);
            }

        }

        public void SetLastConnectTime(long lastConnectTime) {
            this.lastConnectTime = lastConnectTime;
        }

        public long GetLastConnectTime() => lastConnectTime;
        public void ResetReconnectCount() {
            reconnectCount = 0L;
        }

        public long GetReconnectCount() {
            return reconnectCount;
        }

    

        public void IncrementReconnectCount()
        {
            reconnectCount++;
        }
        public interface IPeerEventingServiceDisconnected {

            /**
             * Called when a disconnect is detected in peer eventing service.
             *
             * @param event
             */
            void Disconnected(IPeerEventingServiceDisconnectEvent evnt);

        }

        public interface IPeerEventingServiceDisconnectEvent {

            /**
             * The latest BlockEvent received by peer eventing service.
             *
             * @return The latest BlockEvent.
             */

            BlockEvent GetLatestBlockReceived();

            /**
             * Last connect time
             *
             * @return Last connect time as reported by System.currentTimeMillis()
             */
            long GetLastConnectTime();

            /**
             * Number reconnection attempts since last disconnection.
             *
             * @return reconnect attempts.
             */

            long GetReconnectCount();

            /**
             * Last exception throw for failing connection
             *
             * @return
             */

            Exception GetExceptionThrown();

            void Reconnect(long? startEvent);

        }


        public class PeerEventingServiceDisconnectEvent : IPeerEventingServiceDisconnectEvent
        {
            private static readonly ILog logger = LogProvider.GetLogger(typeof(PeerEventingServiceDisconnectEvent));
            private Exception throwable;
            private Peer peer;
            private Channel.PeerOptions peerOptions;

            private TransactionContext filteredTransactionContext;
  

            public BlockEvent GetLatestBlockReceived()
            {
                return peer.getLastBlockEvent();
            }

            public long GetLastConnectTime()
            {
                return peer.GetLastConnectTime();
            }
            
           
            public long GetReconnectCount()
            {
                return peer.GetReconnectCount();
            }

            public Exception GetExceptionThrown()
            {
                return throwable;
            }

            public PeerEventingServiceDisconnectEvent(Peer peer, Exception throwable, Channel.PeerOptions options, TransactionContext context)
            {
                this.peer = peer;
                this.throwable = throwable;
                this.peerOptions = options;
                this.filteredTransactionContext = context;
            }
            public void Reconnect(long? startBlockNumber)
            {
                logger.Trace("reconnecting startBLockNumber" + startBlockNumber);
                peer.IncrementReconnectCount();
                if (startBlockNumber == null) {
                    peerOptions.StartEventsNewest();
                } else {
                    peerOptions.StartEvents(startBlockNumber.Value);
                }



                PeerEventServiceClient lpeerEventingClient = new PeerEventServiceClient(peer,
                    new Endpoint(peer.Url, peer.properties), peer.properties, peerOptions);
                lpeerEventingClient.Connect(filteredTransactionContext);
                peer.peerEventingClient=lpeerEventingClient;
            }
        }


        public class PeerEventingServiceDisconnect : IPeerEventingServiceDisconnected
        {
            private static readonly ILog logger = LogProvider.GetLogger(typeof(PeerEventingServiceDisconnect));

            public void Disconnected(IPeerEventingServiceDisconnectEvent evnt) {

                BlockEvent lastBlockEvent = evnt.GetLatestBlockReceived();

                long? startBlockNumber = null;

                if (null != lastBlockEvent)
                {
                    startBlockNumber = lastBlockEvent.BlockNumber;
                }

                if (0 != evnt.GetReconnectCount()) {
                    try
                    {
                        Thread.Sleep((int)PEER_EVENT_RETRY_WAIT_TIME);
                    }
                    catch (Exception e)
                    {

                    }
                }

                try {
                    evnt.Reconnect(startBlockNumber);
                } catch (TransactionException e) {
                    logger.ErrorException(e.Message,e);
                }

            }
        }
        private static IPeerEventingServiceDisconnected _disconnectedHandler;
        public static IPeerEventingServiceDisconnected DefaultDisconnectHandler => _disconnectedHandler ?? (_disconnectedHandler = new PeerEventingServiceDisconnect());

   

        /**
         * Set class to handle Event hub disconnects
         *
         * @param newPeerEventingServiceDisconnectedHandler New handler to replace.  If set to null no retry will take place.
         * @return the old handler.
         */

        public IPeerEventingServiceDisconnected SetPeerEventingServiceDisconnected(IPeerEventingServiceDisconnected newPeerEventingServiceDisconnectedHandler) {
            IPeerEventingServiceDisconnected ret = _disconnectedHandler;
            _disconnectedHandler = newPeerEventingServiceDisconnectedHandler;
            return ret;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void SetLastBlockSeen(BlockEvent lastBlockSeen)
        {
            long newLastBlockNumber = lastBlockSeen.BlockNumber;
            // overkill but make sure.
            if (lastBlockNumber < newLastBlockNumber) {
                lastBlockNumber = newLastBlockNumber;
                this.lastBlockEvent = lastBlockSeen;
            }
        }

       

        public override string ToString() {
            return "Peer " + Name + " url: " + Url;

        }


    } // end Peer
}