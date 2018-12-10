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
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Hyperledger.Fabric.Protos.Peer.PeerEvents;
using Hyperledger.Fabric.SDK.Builders;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Logging;
using Newtonsoft.Json;
using Nito.AsyncEx;


namespace Hyperledger.Fabric.SDK
{
    
    public class EventHub : BaseClient
    {
        private static readonly ILog logger = LogProvider.GetLogger(typeof(EventHub));


        private static readonly bool IS_TRACE_LEVEL = logger.IsTraceEnabled();

        private static readonly long EVENTHUB_CONNECTION_WAIT_TIME = Config.Instance.GetEventHubConnectionWaitTime();
        private static readonly long EVENTHUB_RECONNECTION_WARNING_RATE = Config.Instance.GetEventHubReconnectionWarningRate();
        private byte[] clientTLSCertificateDigest;
        protected IEventHubDisconnected disconnectedHandler = new EventHubDisconnected();

        /**
         * Event queue for all events from eventhubs in the channel
         */
        private Channel.ChannelEventQue eventQue;

        // ReSharper disable once NotAccessedField.Local
        private BlockEvent lastBlockEvent;
        private long lastBlockNumber;
        private Grpc.Core.Channel managedChannel;
        private long reconnectCount;
        private string channelName;
        private readonly DaemonTask<SignedEvent, Event> dtask =new DaemonTask<SignedEvent, Event>();

        public EventHub(string name, string url,  Properties properties) : base(name, url, properties)
        {
            logger.Debug($"Created {ToString()}");
        }

        public string Status
        {
            get
            {
                StringBuilder sb = new StringBuilder(1000);
                sb.Append(ToString()).Append(", connected: ").Append(IsConnected);
                Grpc.Core.Channel lmanagedChannel = managedChannel;
                if (lmanagedChannel == null)
                {
                    sb.Append("managedChannel: null");
                }
                else
                {
                    sb.Append(", isShutdown: ").Append(lmanagedChannel.State==ChannelState.Shutdown);
                    sb.Append(", isTransientFailure: ").Append(lmanagedChannel.State == ChannelState.TransientFailure);
                    sb.Append(", state: ").Append("" + lmanagedChannel.State);
                }

                return sb.ToString();
            }

        }


        [JsonIgnore]
        public TransactionContext TransactionContext { get; private set; }

        /**
         * Get disconnected time.
         *
         * @return Time in milli seconds disconnect occurred. Zero if never disconnected
         */

        [JsonIgnore]
        public long DisconnectedTime { get; private set; }


        /**
         * Is event hub connected.
         *
         * @return boolean if true event hub is connected.
         */

        [JsonIgnore]
        public bool IsConnected { get; private set; }

        /**
         * Get last connect time.
         *
         * @return Time in milli seconds the event hub last connected. Zero if never connected.
         */


        [JsonIgnore]
        public long ConnectedTime { get; private set; }

        /**
         * Get last attempt time to connect the event hub.
         *
         * @return Last attempt time to connect the event hub in milli seconds. Zero when never attempted.
         */

        [JsonIgnore]
        public long LastConnectedAttempt { get; private set; }

       
        [JsonIgnore]
        public override Channel Channel
        {
            get => base.Channel;
            set
            {
                if (value == null)
                    throw new ArgumentException("setChannel Channel can not be null");
                if (null != channelName)
                    throw new ArgumentException($"Can not add event hub  {Name} to channel {value.Name} because it already belongs to channel {channelName}.");
                base.Channel = value;
                channelName = value.Name;
                logger.Debug($"{ToString()} set to channel: {value.Name}");
            }

        }

        /**
         * Create a new instance.
         *
         * @param name
         * @param url
         * @param properties
         * @return
         */

        public static EventHub Create(string name, string url, Properties properties)
        {
            return new EventHub(name, url, properties);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public Task<bool> ConnectAsync(TransactionContext transactionContext, CancellationToken token=default(CancellationToken))
        {
            return ConnectAsync(transactionContext, false, token);
        }


        private async Task EventsAsync(Events.EventsClient events, TransactionContext context, CancellationToken token)
        {
            try
            {
                var senderLocal = events.Chat().ToADStreamingCall();
                Task connect = dtask.ConnectAsync(senderLocal, (evnt) =>
                {
                    logger.Debug($"EventHub {Name} got  event type: {evnt.EventCase.ToString()}");
                    if (evnt.EventCase == Event.EventOneofCase.Block)
                    {
                        try
                        {
                            BlockEvent blockEvent = new BlockEvent(this, evnt);
                            logger.Trace($"{ToString()} got block number: {blockEvent.BlockNumber}");
                            SetLastBlockSeen(blockEvent);
                            eventQue.AddBEvent(blockEvent); //add to channel queue
                        }
                        catch (InvalidProtocolBufferException e)
                        {
                            EventHubException eventHubException = new EventHubException($"{Name} onNext error {e}", e);
                            logger.Error(eventHubException.Message);
                        }
                    }
                    if (evnt.EventCase == Event.EventOneofCase.Register)
                    {
                        if (reconnectCount > 1)
                            logger.Info($"Eventhub {Name} has reconnecting after {reconnectCount} attempts");
                        IsConnected = true;
                        ConnectedTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        reconnectCount = 0L;
                        return ProcessResult.ConnectionComplete;
                    }
                    logger.Error($"{ToString()} got a unexpected block type: {evnt.EventCase}");
                    return ProcessResult.Ok;
                }, (ex) => ProcessException(ex), token);
                await BlockListenAsync(senderLocal, context).ConfigureAwait(false);
                await connect.TimeoutAsync(TimeSpan.FromMilliseconds(EVENTHUB_CONNECTION_WAIT_TIME),token).ConfigureAwait(false);
                logger.Debug($"Eventhub {Name} connect is done with connect status: {IsConnected} ");
            }
            catch (Exception e)
            {
                ProcessException(e,token);
            }
        }

        static readonly AsyncLock connectLock=new AsyncLock();

        public void ProcessException(Exception e, CancellationToken token=default(CancellationToken))
        {
            logger.Debug("Processing Exception "+e.Message+" Cancelation afterwards");
            IsConnected = false;        
            dtask.Cancel();
            DisconnectedTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (shutdown)
            {
                logger.Trace($"{Name} was shutdown.");
            }
            else
            {
                Grpc.Core.Channel lmanagedChannel = managedChannel;
                bool isTerminated = lmanagedChannel == null || lmanagedChannel.State == ChannelState.TransientFailure; //TODO TransientFailure!=Terminated
                bool isChannelShutdown = lmanagedChannel == null || lmanagedChannel.State == ChannelState.Shutdown;
                if (EVENTHUB_RECONNECTION_WARNING_RATE > 1 && reconnectCount % EVENTHUB_RECONNECTION_WARNING_RATE == 1)
                    logger.Warn($"{Name} terminated is {isTerminated} shutdown is {isChannelShutdown}, retry count {reconnectCount}  has error {e.Message}.");
                else
                    logger.Trace($"{Name} terminated is {isTerminated} shutdown is {isChannelShutdown}, retry count {reconnectCount}  has error {e.Message}.");
                if (e is TimeoutException)
                    logger.Warn($"EventHub {Name} failed to connect in {EVENTHUB_CONNECTION_WAIT_TIME} ms.");
                else if (e is RpcException sre)
                    logger.Error($"grpc status Code:{sre.StatusCode}, Description {sre.Status.Detail} {sre.Message}");
                else if (e is OperationCanceledException)
                    logger.Error($"(EventHub Connect {Name} canceled");
                else
                    logger.ErrorException(e.Message, e);
                Reconnect(token);
            }
        }
        public async Task<bool> ConnectAsync(TransactionContext transactionContext, bool reconnection, CancellationToken token=default(CancellationToken))
        {

            if (IsConnected)
            {
                logger.Warn($"{ToString()}%s already connected.");
                return false;
            }
            using (await connectLock.LockAsync(token).ConfigureAwait(false))
            {
                logger.Debug($"EventHub {Name} is connecting.");
                LastConnectedAttempt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                Endpoint endpoint = Endpoint.Create(Url, Properties);
                managedChannel = endpoint.BuildChannel();
                clientTLSCertificateDigest = endpoint.GetClientTLSCertificateDigest();
                await EventsAsync(new Events.EventsClient(managedChannel), transactionContext, token).ConfigureAwait(false);               
                return IsConnected;
            }            
        }

        public void Reconnect(CancellationToken token=default(CancellationToken))
        {
            Grpc.Core.Channel lmanagedChannel = managedChannel;
            if (lmanagedChannel != null)
            {
                managedChannel = null;
                lmanagedChannel.ShutdownAsync().Wait(token);
            }

            IEventHubDisconnected ldisconnectedHandler = disconnectedHandler;
            if (!shutdown && null != ldisconnectedHandler)
            {
                ++reconnectCount;
                ldisconnectedHandler.Disconnected(this, token);
            }
        }

        private async Task BlockListenAsync(ADStreamingCall<SignedEvent, Event> sendL, TransactionContext transactionContext)
        {
            TransactionContext = transactionContext;
            Register register = new Register();
            register.Events.Add(new Interest {EventType = EventType.Block});
            Event blockEvent = new Event {Register = register, Creator = transactionContext.Identity.ToByteString(), Timestamp = ProtoUtils.GetCurrentFabricTimestamp()};
            if (null != clientTLSCertificateDigest)
            {
                logger.Trace("Setting clientTLSCertificate digest for event registration to " + clientTLSCertificateDigest.ToHexString());
                blockEvent.TlsCertHash = ByteString.CopyFrom(clientTLSCertificateDigest);
            }
            ByteString blockEventByteString = blockEvent.ToByteString();
            SignedEvent signedBlockEvent = new SignedEvent {EventBytes = blockEventByteString, Signature = transactionContext.SignByteString(blockEventByteString.ToByteArray())};
            await sendL.Call.RequestStream.WriteAsync(signedBlockEvent).ConfigureAwait(false);
        }


        /**
         * Set the channel queue that will receive events
         *
         * @param eventQue
         */
        public void SetEventQue(Channel.ChannelEventQue eventQueue)
        {
            eventQue = eventQueue;
        }


        public override string ToString()
        {
            return $"EventHub{{id: {id}, name: {Name}, channelName: {channelName}, url: {Url}}}";
        }
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Shutdown()
        {
            if (shutdown)
                return;
            logger.Trace($"{ToString()} being shutdown.");
            dtask.Cancel();
            shutdown = true;
            lastBlockEvent = null;
            lastBlockNumber = 0;
            IsConnected = false;
            Grpc.Core.Channel lmanagedChannel = managedChannel;
            managedChannel = null;
            lmanagedChannel?.ShutdownAsync().Wait();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void SetLastBlockSeen(BlockEvent lastBlockSeen)
        {
            long newLastBlockNumber = lastBlockSeen.BlockNumber;
            // overkill but make sure.
            if (lastBlockNumber < newLastBlockNumber)
            {
                lastBlockNumber = newLastBlockNumber;
                lastBlockEvent = lastBlockSeen;
                if (IS_TRACE_LEVEL)
                {
                    logger.Trace($"{ToString()} last block seen: {lastBlockNumber}");
                }
            }
        }


        /**
         * Set class to handle Event hub disconnects
         *
         * @param newEventHubDisconnectedHandler New handler to replace.  If set to null no retry will take place.
         * @return the old handler.
         */

        public IEventHubDisconnected SetEventHubDisconnectedHandler(EventHubDisconnected newEventHubDisconnectedHandler)
        {
            IEventHubDisconnected ret = disconnectedHandler;
            disconnectedHandler = newEventHubDisconnectedHandler;
            return ret;
        }

        /**
         * Eventhub disconnection notification interface
         */
        public interface IEventHubDisconnected
        {
            /**
             * Called when a disconnect is detected.
             *
             * @param eventHub
             * @throws EventHubException
             */
            void Disconnected(EventHub eventHub, CancellationToken token);
        }

        public class EventHubDisconnected : IEventHubDisconnected
        {
            // ReSharper disable once MemberHidesStaticFromOuterClass
            private static readonly ILog logger = LogProvider.GetLogger(typeof(EventHubDisconnected));

            [MethodImpl(MethodImplOptions.Synchronized)]
            public void Disconnected(EventHub eventHub, CancellationToken token)
            {
                if (eventHub.reconnectCount == 1)
                    logger.Warn($"Channel {eventHub.Channel.Name} detected disconnect on event hub {eventHub} ({eventHub.Url})");
                Task.Run(async () =>
                {
                    await Task.Delay(500,token).ConfigureAwait(false);
                    try
                    {
                        if (eventHub.TransactionContext == null)
                            logger.Warn($"{ToString()} reconnect failed with no user context");
                        else
                            await eventHub.ConnectAsync(eventHub.TransactionContext, true, token).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        logger.Warn($"Failed {ToString()} to reconnect. {e.Message}");
                    }
                }, token);
            }

            /**
             * Default reconnect event hub implementation.  Applications are free to replace
             */
        }
    }
}