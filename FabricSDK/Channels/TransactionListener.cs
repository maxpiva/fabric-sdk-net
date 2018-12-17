using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Hyperledger.Fabric.SDK.Blocks;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Logging;
// ReSharper disable InconsistentlySynchronizedField

namespace Hyperledger.Fabric.SDK.Channels
{
    public class TransactionListener
    {
        private static readonly ILog intlogger = LogProvider.GetLogger(typeof(TransactionListener));
        private readonly Channel channel;
        private readonly long createTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        private readonly long DELTA_SWEEP = Config.Instance.GetTransactionListenerCleanUpTimeout();
        private readonly HashSet<EventHub> eventHubs;
        private readonly bool failFast;
        private readonly TaskCompletionSource<TransactionEvent> future;
        private readonly NOfEvents nOfEvents;
        private readonly HashSet<Peer> peers;
        private readonly string txID;
        private bool fired;
        private long sweepTime;

        public TransactionListener(Channel ch, string txID, TaskCompletionSource<TransactionEvent> future, NOfEvents nOfEvents, bool failFast)
        {
            sweepTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (long) (DELTA_SWEEP * 1.5);
            this.txID = txID;
            this.future = future;
            channel = ch;
            this.nOfEvents = new NOfEvents(nOfEvents);
            peers = new HashSet<Peer>(nOfEvents.UnSeenPeers());
            eventHubs = new HashSet<EventHub>(nOfEvents.UnSeenEventHubs());
            this.failFast = failFast;
            AddListener();
        }

        /**
         * Record transactions event.
         *
         * @param transactionEvent
         * @return True if transactions have been seen on all eventing peers and eventhubs.
         */
        public bool EventReceived(TransactionEvent transactionEvent)
        {
            sweepTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + DELTA_SWEEP; //seen activity keep it active.
            Peer peer = transactionEvent.Peer;
            EventHub eventHub = transactionEvent.EventHub;
            if (peer != null && !peers.Contains(peer))
                return false;
            if (eventHub != null && !eventHubs.Contains(eventHub))
                return false;
            if (failFast && !transactionEvent.IsValid)
                return true;
            if (peer != null)
            {
                nOfEvents.Seen(peer);
                intlogger.Debug($"Channel {channel.Name} seen transaction event {txID} for peer {peer}");
            }
            else if (null != eventHub)
            {
                nOfEvents.Seen(eventHub);
                intlogger.Debug($"Channel {channel.Name} seen transaction event {txID} for eventHub {eventHub}");
            }
            else
                intlogger.Error($"Channel {channel.Name} seen transaction event {txID} with no associated peer or eventhub");

            return nOfEvents.Ready;
        }

        private void AddListener()
        {
            channel.RunSweeper();
            lock (channel.txListeners)
            {
                LinkedList<TransactionListener> tl;
                if (channel.txListeners.ContainsKey(txID))
                    tl = channel.txListeners[txID];
                else
                {
                    tl = new LinkedList<TransactionListener>();
                    channel.txListeners.Add(txID, tl);
                }

                tl.AddLast(this);
            }
        }

        public bool sweepMe()
        {
            // Sweeps DO NOT fire future. user needs to put timeout on their futures for timeouts.

            bool ret = sweepTime < DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() || fired || future.Task.IsCompleted;
            if (intlogger.IsWarnEnabled() && ret)
            {
                StringBuilder sb = new StringBuilder(10000);
                sb.Append("Non reporting event hubs:");
                string sep = "";
                foreach (EventHub eh in nOfEvents.UnSeenEventHubs())
                {
                    sb.Append(sep).Append(eh).Append(" status: ").Append(eh.Status);
                    sep = ",";
                }

                if (sb.Length != 0)
                    sb.Append(". ");
                sep = "Non reporting peers: ";
                foreach (Peer peer in nOfEvents.UnSeenPeers())
                {
                    sb.Append(sep).Append(peer).Append(" status: ").Append(peer.EventingStatus);
                    sep = ",";
                }

                intlogger.Warn($"Force removing transaction listener after {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - createTime} ms for transaction {txID}. {sb}" + $". sweep timeout: {sweepTime < DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}, fired: {fired}, future done:{future.Task.IsCompleted}");
            }

            return ret;
        }

        public void Fire(TransactionEvent transactionEvent)
        {
            if (fired)
                return;
            lock (channel.txListeners)
            {
                if (channel.txListeners.ContainsKey(txID))
                {
                    LinkedList<TransactionListener> l = channel.txListeners[txID];
                    l.RemoveFirst();
                    if (l.Count == 0)
                        channel.txListeners.Remove(txID);
                }
            }

            if (future.Task.IsCompleted)
            {
                fired = true;
                return;
            }

            if (transactionEvent.IsValid)
            {
                intlogger.Debug($"Completing future for channel {channel.Name} and transaction id: {txID}");
                future.TrySetResult(transactionEvent);
            }
            else
            {
                intlogger.Debug($"Completing future as exception for channel {channel.Name} and transaction id: {txID}, validation code: {transactionEvent.ValidationCode:02X}");
                future.TrySetException(new TransactionEventException($"Received invalid transaction event. Transaction ID {transactionEvent.TransactionID} status {transactionEvent.ValidationCode}", transactionEvent));
            }
        }
    }
}