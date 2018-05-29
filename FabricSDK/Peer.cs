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
/**
 * The Peer class represents a peer to which SDK sends deploy, or query proposals requests.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Hyperledger.Fabric.Protos.Peer.FabricProposal;
using Hyperledger.Fabric.SDK.Builders;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Logging;

namespace Hyperledger.Fabric.SDK
{
    /**
* Endorsing peer installs and runs chaincode.
*/


    [DataContract]
    public class Peer : BaseClient, IEquatable<Peer>
    {
        private static readonly ILog logger = LogProvider.GetLogger(typeof(Peer));
        private static IPeerEventingServiceDisconnected _disconnectedHandler;
        internal EndorserClient endorserClent;
        private long lastBlockNumber;
        private PeerEventServiceClient peerEventingClient;
        private TransactionContext transactionContext;

        public Peer(string name, string grpcURL, Properties properties) : base(name, grpcURL, properties)
        {
            ReconnectCount = 0L;
        }


        [IgnoreDataMember]
        public BlockEvent LastBlockEvent { get; private set; }

        [IgnoreDataMember]
        private TaskScheduler ExecutorService => Channel.ExecutorService;

        /**
         * Set the channel the peer is on.
         *
         * @param channel
         */
        [DataMember]
        public override Channel Channel
        {
            get => base.Channel;
            set
            {
                if (null != base.Channel)
                    throw new InvalidArgumentException($"Can not add peer {Name} to channel {value.Name} because it already belongs to channel {base.Channel.Name}.");
                base.Channel = value;
            }
        }

        internal Channel intChannel
        {
            set => base.Channel = value;
        }
        [IgnoreDataMember]
        public long LastConnectTime { get; set; }

        [IgnoreDataMember]
        public long ReconnectCount { get; private set; }
        public static IPeerEventingServiceDisconnected DefaultDisconnectHandler => _disconnectedHandler ?? (_disconnectedHandler = new PeerEventingServiceDisconnect());


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

            return Name.Equals(other.Name) && Url.Equals(other.Url);
        }

        public static Peer Create(string name, string grpcURL, Properties properties)
        {
            return new Peer(name, grpcURL, properties);
        }

        public Peer()
        {
            _disconnectedHandler = DefaultDisconnectHandler;
        }
        public void InitiateEventing(TransactionContext transContext, Channel.PeerOptions peersOptions)
        {
            transactionContext = transContext.RetryTransactionSameContext();
            if (peerEventingClient == null)
            {
                //PeerEventServiceClient(Peer peer, ManagedChannelBuilder<?> channelBuilder, Properties properties)
                //   peerEventingClient = new PeerEventServiceClient(this, new HashSet<Channel>(Arrays.asList(new Channel[] {channel})));

                peerEventingClient = new PeerEventServiceClient(this, new Endpoint(Url, Properties), Properties, peersOptions);

                peerEventingClient.Connect(transContext);
            }
        }

        public void UnsetChannel()
        {
            base.Channel = null;
        }


        public async Task<Protos.Peer.FabricProposalResponse.ProposalResponse> SendProposalAsync(SignedProposal proposal, CancellationToken token=default(CancellationToken))
        {
            CheckSendProposal(proposal);

            logger.Debug($"peer.sendProposalAsync name: {Name}, url: {Url}");

            EndorserClient localEndorserClient = endorserClent; //work off thread local copy.

            if (null == localEndorserClient || !localEndorserClient.IsChannelActive)
            {
                endorserClent = new EndorserClient(new Endpoint(Url, Properties));
                localEndorserClient = endorserClent;
            }

            try
            {
                return await localEndorserClient.SendProposalAsync(proposal,token);
            }
            catch (Exception)
            {
                endorserClent = null;
                throw;
            }
        }

        private void CheckSendProposal(SignedProposal proposal)
        {
            if (proposal == null)
                throw new PeerException("Proposal is null");
            if (shutdown)
                throw new PeerException($"Peer {Name} was shutdown.");
            Exception e = Utils.CheckGrpcUrl(Url);
            if (e != null)
                throw new InvalidArgumentException("Bad peer url.", e);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Shutdown(bool force)
        {
            if (shutdown)
            {
                return;
            }

            shutdown = true;
            Channel = null;
            LastBlockEvent = null;
            lastBlockNumber = 0;

            EndorserClient lendorserClent = endorserClent;

            //allow resources to finalize

            endorserClent = null;

            if (lendorserClent != null)
            {
                lendorserClent.Shutdown(force);
            }

            PeerEventServiceClient lpeerEventingClient = peerEventingClient;
            peerEventingClient = null;

            if (null != lpeerEventingClient)
            {
                // PeerEventServiceClient peerEventingClient1 = peerEventingClient;

                lpeerEventingClient.Shutdown(force);
            }
        }

        ~Peer()
        {
            Shutdown(true);
        }

        public void ReconnectPeerEventServiceClient(PeerEventServiceClient failedPeerEventServiceClient, Exception throwable)
        {
            if (shutdown)
            {
                logger.Debug("Not reconnecting PeerEventServiceClient shutdown ");
                return;
            }

            IPeerEventingServiceDisconnected ldisconnectedHandler = _disconnectedHandler;
            if (null == ldisconnectedHandler)
            {
                return; // just wont reconnect.
            }

            TransactionContext ltransactionContext = transactionContext;
            if (ltransactionContext == null)
            {
                logger.Warn("Not reconnecting PeerEventServiceClient no transaction available ");
                return;
            }

            TransactionContext fltransactionContext = ltransactionContext.RetryTransactionSameContext();

            TaskScheduler executorService = ExecutorService;
            Channel.PeerOptions peerOptions = null != failedPeerEventServiceClient.GetPeerOptions() ? failedPeerEventServiceClient.GetPeerOptions() : Channel.PeerOptions.CreatePeerOptions();
            if (executorService != null)
            {
                Task.Factory.StartNew(() => { ldisconnectedHandler.Disconnected(new PeerEventingServiceDisconnectEvent(this, throwable, peerOptions, fltransactionContext)); }, default(CancellationToken), TaskCreationOptions.None, executorService);
            }
        }

        public void ResetReconnectCount()
        {
            ReconnectCount = 0L;
        }

        public void IncrementReconnectCount()
        {
            ReconnectCount++;
        }


        /**
         * Set class to handle Event hub disconnects
         *
         * @param newPeerEventingServiceDisconnectedHandler New handler to replace.  If set to null no retry will take place.
         * @return the old handler.
         */

        public IPeerEventingServiceDisconnected SetPeerEventingServiceDisconnected(IPeerEventingServiceDisconnected newPeerEventingServiceDisconnectedHandler)
        {
            IPeerEventingServiceDisconnected ret = _disconnectedHandler;
            _disconnectedHandler = newPeerEventingServiceDisconnectedHandler;
            return ret;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void SetLastBlockSeen(BlockEvent lastBlockSeen)
        {
            long newLastBlockNumber = lastBlockSeen.BlockNumber;
            // overkill but make sure.
            if (lastBlockNumber < newLastBlockNumber)
            {
                lastBlockNumber = newLastBlockNumber;
                LastBlockEvent = lastBlockSeen;
            }
        }


        public override string ToString()
        {
            return "Peer " + Name + " url: " + Url;
        }

        public interface IPeerEventingServiceDisconnected
        {
            /**
             * Called when a disconnect is detected in peer eventing service.
             *
             * @param event
             */
            void Disconnected(IPeerEventingServiceDisconnectEvent evnt);
        }

        public interface IPeerEventingServiceDisconnectEvent
        {
            /**
             * The latest BlockEvent received by peer eventing service.
             *
             * @return The latest BlockEvent.
             */

            BlockEvent LatestBlockReceived { get; }

            /**
             * Last connect time
             *
             * @return Last connect time as reported by System.currentTimeMillis()
             */
            long LastConnectTime { get; }

            /**
             * Number reconnection attempts since last disconnection.
             *
             * @return reconnect attempts.
             */

            long ReconnectCount { get; }

            /**
             * Last exception throw for failing connection
             *
             * @return
             */

            Exception ExceptionThrown { get; }

            void Reconnect(long? startEvent);
        }


        public class PeerEventingServiceDisconnectEvent : IPeerEventingServiceDisconnectEvent
        {
            // ReSharper disable once MemberHidesStaticFromOuterClass
            private static readonly ILog logger = LogProvider.GetLogger(typeof(PeerEventingServiceDisconnectEvent));
            private readonly TransactionContext filteredTransactionContext;
            private readonly Peer peer;
            private readonly Channel.PeerOptions peerOptions;

            public PeerEventingServiceDisconnectEvent(Peer peer, Exception throwable, Channel.PeerOptions options, TransactionContext context)
            {
                this.peer = peer;
                ExceptionThrown = throwable;
                peerOptions = options;
                filteredTransactionContext = context;
            }


            public BlockEvent LatestBlockReceived => peer.LastBlockEvent;
            public long LastConnectTime => peer.LastConnectTime;
            public long ReconnectCount => peer.ReconnectCount;
            public Exception ExceptionThrown { get; }

            public void Reconnect(long? startBlockNumber)
            {
                logger.Trace("reconnecting startBLockNumber" + startBlockNumber);
                peer.IncrementReconnectCount();
                if (startBlockNumber == null)
                {
                    peerOptions.StartEventsNewest();
                }
                else
                {
                    peerOptions.StartEvents(startBlockNumber.Value);
                }


                PeerEventServiceClient lpeerEventingClient = new PeerEventServiceClient(peer, new Endpoint(peer.Url, peer.Properties), peer.Properties, peerOptions);
                lpeerEventingClient.Connect(filteredTransactionContext);
                peer.peerEventingClient = lpeerEventingClient;
            }
        }


        public class PeerEventingServiceDisconnect : IPeerEventingServiceDisconnected
        {
            // ReSharper disable once MemberHidesStaticFromOuterClass
            private static readonly ILog logger = LogProvider.GetLogger(typeof(PeerEventingServiceDisconnect));
            private readonly long PEER_EVENT_RETRY_WAIT_TIME = Config.Instance.GetPeerRetryWaitTime();

            public void Disconnected(IPeerEventingServiceDisconnectEvent evnt)
            {
                BlockEvent lastBlockEvent = evnt.LatestBlockReceived;

                long? startBlockNumber = null;

                if (null != lastBlockEvent)
                {
                    startBlockNumber = lastBlockEvent.BlockNumber;
                }

                if (0 != evnt.ReconnectCount)
                {
                    try
                    {
                        Thread.Sleep((int) PEER_EVENT_RETRY_WAIT_TIME);
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }

                try
                {
                    evnt.Reconnect(startBlockNumber);
                }
                catch (TransactionException e)
                {
                    logger.ErrorException(e.Message, e);
                }
            }
        }
    } // end Peer

    /**
* Possible roles a peer can perform.
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
        public static List<PeerRole> NoEventSource() => Enum.GetValues(typeof(PeerRole)).Cast<PeerRole>().Except(new[] {PeerRole.EVENT_SOURCE}).ToList();

        public static PeerRole PeerRoleFromValue(this string value)
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
}