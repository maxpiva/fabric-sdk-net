/*
 *  Copyright 2016 IBM, DTCC, Fujitsu Australia Software Technology - All Rights Reserved.
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
/**
 * Class to manage fabric events.
 * <p>
 * Feeds Channel event queues with events
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Google.Protobuf;
using Grpc.Core;
using Hyperledger.Fabric.Protos.Peer.PeerEvents;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Logging;

using Hyperledger.Fabric.SDK.Transaction;
using Utils = Hyperledger.Fabric.SDK.Helper.Utils;

namespace Hyperledger.Fabric.SDK
{
    [Serializable]
    public class EventHub
    {
        private static readonly ILog logger = LogProvider.GetLogger(typeof(EventHub));

        private static long EVENTHUB_CONNECTION_WAIT_TIME = Config.Instance.GetEventHubConnectionWaitTime();
        private static long EVENTHUB_RECONNECTION_WARNING_RATE = Config.Instance.GetEventHubReconnectionWarningRate();

        private readonly string url;
        private readonly string name;
        private readonly Properties properties;
        [NonSerialized]
        private Grpc.Core.Channel managedChannel;
        [NonSerialized]
        private bool connected = false;
        [NonSerialized]
        private AsyncDuplexStreamingCall<SignedEvent, Event> sender;
        /**
         * Event queue for all events from eventhubs in the channel
         */
        [NonSerialized]
        private Channel.ChannelEventQue eventQue;

    [NonSerialized]
        private bool shutdown = false;
        private Channel channel;
    [NonSerialized]
        private TransactionContext transactionContext;
    [NonSerialized]
        private byte[] clientTLSCertificateDigest;
    [NonSerialized]
        private long reconnectCount;
    [NonSerialized]
        private long lastBlockNumber;
    [NonSerialized]
        private BlockEvent lastBlockEvent;

        [NonSerialized] private TaskScheduler scheduler;

        public TaskScheduler Scheduler => scheduler;
        public TransactionContext TransactionContext => transactionContext;

        /**
         * Get disconnected time.
         *
         * @return Time in milli seconds disconnect occurred. Zero if never disconnected
         */
        public long DisconnectedTime { get; private set; }
        

        /**
         * Is event hub connected.
         *
         * @return boolean if true event hub is connected.
         */
        public bool IsConnected => connected;

        /**
         * Get last connect time.
         *
         * @return Time in milli seconds the event hub last connected. Zero if never connected.
         */
        public long ConnectedTime { get; private set; } = 0;

        /**
         * Get last attempt time to connect the event hub.
         *
         * @return Last attempt time to connect the event hub in milli seconds. Zero when never attempted.
         */

        public long LastConnectedAttempt { get; private set; }


        public EventHub(string name, string grpcURL, TaskScheduler scheduler, Properties properties)  {

            Exception e = Utils.CheckGrpcUrl(grpcURL);
            if (e != null) {
                throw new InvalidArgumentException("Bad event hub url.", e);

            }

            if (string.IsNullOrEmpty(name)) {
                throw new InvalidArgumentException("Invalid name for eventHub");
            }

            this.url = grpcURL;
            this.name = name;
            this.scheduler = scheduler;
            this.properties = this.properties.Clone();

        }

        /**
         * Create a new instance.
         *
         * @param name
         * @param url
         * @param properties
         * @return
         */

        public static EventHub Create(string name, string url, TaskScheduler executorService, Properties properties)
        {
            return new EventHub(name, url, executorService, properties);
        }

        /**
         * Event hub name
         *
         * @return event hub name
         */

        public string Name =>name;

        /**
         * Event hub properties
         *
         * @return Event hub properties
         * @see HFClient#newEventHub(String, String, Properties)
         */
        public Properties Properties => properties.Clone();
            
        //private readonly StreamObserver<PeerEvents.Event> eventStream = null; // Saved here to avoid potential garbage collection

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Connect(TransactionContext transactionContext)
        {
            return Connect(transactionContext, false);
        }
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Connect(TransactionContext transactionContext, bool reconnection)
        {
            if (connected) {
                logger.Warn($"{ToString()}%s already connected.");
                return true;
            }
            //eventStream = null;

            CountDownLatch finishLatch = new CountDownLatch(1);

            logger.Debug($"EventHub {name} is connecting.");

            LastConnectedAttempt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            
            Endpoint endpoint = new Endpoint(url, properties);
            managedChannel = endpoint.BuildChannel();
            clientTLSCertificateDigest = endpoint.GetClientTLSCertificateDigest();
            Events.EventsClient events = new Events.EventsClient(managedChannel);
            var senderLocal = events.Chat();
            Task.Factory.StartNew(async () =>
                {
                    List<Exception> threw=new List<Exception>();
                    try
                    {
                        while (await sender.ResponseStream.MoveNext())
                        {
                            Event evnt = senderLocal.ResponseStream.Current;
                            logger.Debug($"EventHub {Name} got  event type: {evnt.EventCase.ToString()}");
                            if (evnt.EventCase == Event.EventOneofCase.Block)
                            {
                                try
                                {
                                    BlockEvent blockEvent = new BlockEvent(this, evnt);
                                    SetLastBlockSeen(blockEvent);
                                    eventQue.AddBEvent(blockEvent);  //add to channel queue
                                }
                                catch (InvalidProtocolBufferException e)
                                {
                                    EventHubException eventHubException = new EventHubException($"{this.Name} onNext error {e}", e);
                                    logger.Error(eventHubException.Message);
                                    threw.Add(eventHubException);
                                }
                            }
                            else if (evnt.EventCase == Event.EventOneofCase.Register)
                            {

                                if (reconnectCount > 1)
                                {
                                    logger.Info($"Eventhub {name} has reconnecting after {reconnectCount} attempts");
                                }

                                connected = true;
                                ConnectedTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                                reconnectCount = 0L;
                                finishLatch.Signal();
                            }
                        }
                        logger.Debug($"Stream completed %s", this.ToString());
                        finishLatch.Signal();
                    }
                    catch (Exception e)
                    {
                        connected = false;
                        if (sender!=null)
                            sender.Dispose();
                        sender = null;
                        DisconnectedTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        if (shutdown)
                        { //IF we're shutdown don't try anything more.
                            logger.Trace("${Name} was shutdown.");
                            finishLatch.Signal();
                        }
                        else
                        {
                            Grpc.Core.Channel lmanagedChannel = managedChannel;
                            bool isTerminated = lmanagedChannel == null ? true : lmanagedChannel.State == ChannelState.TransientFailure; //TODO TransientFailure!=Terminated
                            bool isChannelShutdown = lmanagedChannel == null || lmanagedChannel.State == ChannelState.Shutdown;

                            if (EVENTHUB_RECONNECTION_WARNING_RATE > 1 && reconnectCount % EVENTHUB_RECONNECTION_WARNING_RATE == 1)
                            {
                                logger.Warn($"{Name} terminated is {isTerminated} shutdown is {isChannelShutdown}, retry count {reconnectCount}  has error {e.Message}.");
                            }
                            else
                            {
                                logger.Trace($"{Name} terminated is {isTerminated} shutdown is {isChannelShutdown}, retry count {reconnectCount}  has error {e.Message}.");
                            }

                            finishLatch.Signal();
                        }
                    }                    
                });
            try
            {
                BlockListen(transactionContext);
            }
            catch (CryptoException e)
            {
                throw new EventHubException(e);
            }
            try
            {

                //On reconnection don't wait here.

                if (!reconnection && !finishLatch.Wait((int)EVENTHUB_CONNECTION_WAIT_TIME))
                {

                    logger.Warn($"EventHub {name} failed to connect in {EVENTHUB_CONNECTION_WAIT_TIME} ms.");

                }
                else
                {
                    logger.Trace("Eventhub {name} Done waiting for reply!");
                }

            }
            catch (Exception e)
            {
                logger.ErrorException(e.Message,e);
            }

            logger.Debug($"Eventhub {name} connect is done with connect status: {connected} ");

            if (connected)
            {
                sender = senderLocal;
            }

            return connected;
        }

        private void Reconnect()
        {

            Grpc.Core.Channel lmanagedChannel = managedChannel;

            if (lmanagedChannel != null) {
                managedChannel = null;
                lmanagedChannel.ShutdownAsync().Wait();
            }

            IEventHubDisconnected ldisconnectedHandler = disconnectedHandler;
            if (!shutdown && null != ldisconnectedHandler) {
                ++reconnectCount;
                ldisconnectedHandler.Disconnected(this);

            }

        }

        private void BlockListen(TransactionContext transactionContext)
        {

            this.transactionContext = transactionContext;

            Register register = new Register();
            register.Events.Add(new Interest { EventType = EventType.Block});
            Event blockEvent=new Event { Register = register, Creator = transactionContext.Identity.ToByteString(), Timestamp = ProtoUtils.GetCurrentFabricTimestamp()};

            if (null != clientTLSCertificateDigest) {
                logger.Trace("Setting clientTLSCertificate digest for event registration to " + clientTLSCertificateDigest.ToHexString());
                blockEvent.TlsCertHash=ByteString.CopyFrom(clientTLSCertificateDigest);
            }

            ByteString blockEventByteString = blockEvent.ToByteString();

            SignedEvent signedBlockEvent = new SignedEvent { EventBytes = blockEventByteString, Signature = transactionContext.SignByteString(blockEventByteString.ToByteArray())};
            sender.RequestStream.WriteAsync(signedBlockEvent).Wait();
        }

        /**
         * Get the GRPC URL used to connect.
         *
         * @return GRPC URL.
         */
        public string GetUrl() {
            return url;
        }

        /**
         * Set the channel queue that will receive events
         *
         * @param eventQue
         */
        public void SetEventQue(Channel.ChannelEventQue eventQue) {
            this.eventQue = eventQue;
        }


        public override string ToString()
        {
            return "EventHub:" + Name;
        }

        public void Shutdown() {
            shutdown = true;
            lastBlockEvent = null;
            lastBlockNumber = 0;
            connected = false;

            channel = null;
            if (sender!=null)
                sender.Dispose();
            sender = null;
            Grpc.Core.Channel lmanagedChannel = managedChannel;
            managedChannel = null;
            if (lmanagedChannel != null)
            {
                lmanagedChannel.ShutdownAsync().Wait();
            }
        }

        public void SetChannel(Channel channel)
        {
            if (channel == null) {
                throw new InvalidArgumentException("setChannel Channel can not be null");
            }

            if (null != this.channel)
            {
                throw new InvalidArgumentException($"Can not add event hub  {name} to channel {channel.Name}%s because it already belongs to channel {this.channel.Name}.");
            }

            this.channel = channel;
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

        /**
         * Eventhub disconnection notification interface
         */
        public interface IEventHubDisconnected {

            /**
             * Called when a disconnect is detected.
             *
             * @param eventHub
             * @throws EventHubException
             */
            void Disconnected(EventHub eventHub);

        }

        public class EventHubDisconnected : IEventHubDisconnected
        {
            private static readonly ILog logger = LogProvider.GetLogger(typeof(EventHubDisconnected));

            [MethodImpl(MethodImplOptions.Synchronized)]
            public void Disconnected(EventHub eventHub)
            {
                if (eventHub.reconnectCount == 1)
                {
                    logger.Warn($"Channel {eventHub.channel.Name} detected disconnect on event hub {eventHub.ToString()} ({eventHub.url})");
                }

                Task.Factory.StartNew(async () =>
                {
                    await Task.Delay(500);
                    try
                    {
                        if (eventHub.TransactionContext == null)
                        {
                            logger.Warn("Eventhub reconnect failed with no user context");
                        }
                        else
                        {
                            eventHub.Connect(eventHub.TransactionContext, true);
                        }


                    }
                    catch (Exception e)
                    {

                        logger.Warn($"Failed {eventHub.ToString()} to reconnect. {e.Message}");

                    }
                }, default(CancellationToken), TaskCreationOptions.LongRunning, eventHub.Scheduler);


            }

            /**
             * Default reconnect event hub implementation.  Applications are free to replace
             */
        }

        protected IEventHubDisconnected disconnectedHandler = new EventHubDisconnected();


        /**
         * Set class to handle Event hub disconnects
         *
         * @param newEventHubDisconnectedHandler New handler to replace.  If set to null no retry will take place.
         * @return the old handler.
         */

        public IEventHubDisconnected SetEventHubDisconnectedHandler(EventHubDisconnected newEventHubDisconnectedHandler) {
            IEventHubDisconnected ret = disconnectedHandler;
            disconnectedHandler = newEventHubDisconnectedHandler;
            return ret;
        }

    }
}
