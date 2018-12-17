using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Hyperledger.Fabric.SDK.Helper;

namespace Hyperledger.Fabric.SDK.Channels
{
    /**
     * NofEvents may be used with @see {@link TransactionOptions#nOfEvents(NOfEvents)}  to control how reporting Peer service events and Eventhubs will
     * complete the future acknowledging the transaction has been seen by those Peers.
     * <p/>
     * You can use the method @see {@link #nofNoEvents} to create an NOEvents that will result in the future being completed immediately
     * when the Orderer has accepted the transaction. Note in this case the transaction event will be set to null.
     * <p/>
     * NofEvents can add Peer Eventing services and Eventhubs that should complete the future. By default all will need to
     * see the transactions to complete the future.  The method @see {@link #setN(int)} can set how many in the group need to see the transaction
     * completion. Essentially setting it to 1 is any.
     * </p>
     * NofEvents may also contain other NofEvent grouping. They can be nested.
     */
    public class NOfEvents
    {
        private readonly HashSet<EventHub> eventHubs = new HashSet<EventHub>();
        private readonly HashSet<NOfEvents> nOfEvents = new HashSet<NOfEvents>();
        private readonly HashSet<Peer> peers = new HashSet<Peer>();
        private long n = long.MaxValue; //all
        private bool started;

        public NOfEvents(NOfEvents nof)
        {
            // Deep Copy.
            if (nof == NofNoEvents)
                throw new ArgumentException("nofNoEvents may not be copied.");
            Ready = false; // no use in one set to ready.
            started = false;
            n = nof.n;
            peers = new HashSet<Peer>(nof.peers);
            eventHubs = new HashSet<EventHub>(nof.eventHubs);
            foreach (NOfEvents nofc in nof.nOfEvents)
                nOfEvents.Add(new NOfEvents(nofc));
        }

        private NOfEvents()
        {
        }

        public bool Ready { get; private set; }

        public static NOfEvents NofNoEvents { get; } = new NoEvents();

        public virtual NOfEvents SetN(int num)
        {
            if (num < 1)
                throw new ArgumentException($"N was {num} but needs to be greater than 0.");
            n = num;
            return this;
        }
        /**
              * Peers that need to see the transaction event to complete.
              *
              * @param peers The peers that need to see the transaction event to complete.
              * @return This NofEvents.
              */
        public virtual NOfEvents AddPeers(params Peer[] pers)
        {
            if (pers == null || pers.Length == 0)
                throw new ArgumentException("Peers added must be not null or empty.");
            peers.AddRange(pers);
            return this;
        }
        /**
             * Peers that need to see the transaction event to complete.
             *
             * @param peers The peers that need to see the transaction event to complete.
             * @return This NofEvents.
             */
        public virtual NOfEvents AddPeers(IEnumerable<Peer> pers)
        {
            AddPeers(pers.ToArray());
            return this;
        }
        /**
             * EventHubs that need to see the transaction event to complete.
             * @param eventHubs The peers that need to see the transaction event to complete.
             * @return This NofEvents.
             */
        public virtual NOfEvents AddEventHubs(params EventHub[] evntHubs)
        {
            if (evntHubs == null || evntHubs.Length == 0)
                throw new ArgumentException("EventHubs added must be not null or empty.");
            eventHubs.AddRange(evntHubs);
            return this;
        }
        /**
             * EventHubs that need to see the transaction event to complete.
             * @param eventHubs The peers that need to see the transaction event to complete.
             * @return This NofEvents.
             */
        public virtual NOfEvents AddEventHubs(IEnumerable<EventHub> evntHubs)
        {
            AddEventHubs(evntHubs.ToArray());
            return this;
        }
        /**
             * NOfEvents that need to see the transaction event to complete.
             * @param nOfEvents  The nested event group that need to set the transacton event to complete.
             * @return This NofEvents.
             */
        public virtual NOfEvents AddNOfs(params NOfEvents[] nofEvents)
        {
            if (nofEvents == null || nofEvents.Length == 0)
                throw new ArgumentException("nofEvents added must be not null or empty.");
            foreach (NOfEvents num in nofEvents)
            {
                if (NofNoEvents == num)
                    throw new ArgumentException("nofNoEvents may not be added as an event.");
                if (InHayStack(num))
                    throw new ArgumentException("nofEvents already was added..");
                nOfEvents.Add(new NOfEvents(num));
            }

            return this;
        }

        private bool InHayStack(NOfEvents needle)
        {
            if (this == needle)
                return true;
            foreach (NOfEvents straw in nOfEvents)
            {
                if (straw.InHayStack(needle))
                    return true;
            }

            return false;
        }
        /**
             * NOfEvents that need to see the transaction event to complete.
             * @param nofs  The nested event group that need to set the transacton event to complete.
             * @return This NofEvents.
             */
        public NOfEvents AddNOfs(IEnumerable<NOfEvents> nofs)
        {
            AddNOfs(nofs.ToArray());
            return this;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public List<Peer> UnSeenPeers()
        {
            HashSet<Peer> unseen = new HashSet<Peer>();
            unseen.AddRange(peers);
            foreach (NOfEvents noe in nOfEvents)
                unseen.AddRange(noe.UnSeenPeers());
            return unseen.ToList();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public List<EventHub> UnSeenEventHubs()
        {
            HashSet<EventHub> unseen = new HashSet<EventHub>();
            unseen.AddRange(eventHubs);
            foreach (NOfEvents noe in nOfEvents)
                unseen.AddRange(noe.UnSeenEventHubs());
            return unseen.ToList();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Seen(EventHub eventHub)
        {
            if (!started)
            {
                started = true;
                n = Math.Min(eventHubs.Count + peers.Count + nOfEvents.Count, n);
            }

            if (!Ready)
            {
                if (eventHubs.Remove(eventHub))
                {
                    if (--n == 0)
                    {
                        Ready = true;
                    }
                }

                if (!Ready)
                {
                    foreach (NOfEvents e in nOfEvents.ToList())
                    {
                        if (e.Seen(eventHub))
                        {
                            nOfEvents.Remove(e);
                            if (--n == 0)
                            {
                                Ready = true;
                                break;
                            }
                        }
                    }
                }
            }

            if (Ready)
            {
                eventHubs.Clear();
                peers.Clear();
                nOfEvents.Clear();
            }

            return Ready;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Seen(Peer peer)
        {
            if (!started)
            {
                started = true;
                n = Math.Min(eventHubs.Count + peers.Count + nOfEvents.Count, n);
            }

            if (!Ready)
            {
                if (peers.Remove(peer))
                {
                    if (--n == 0)
                        Ready = true;
                }

                if (!Ready)
                {
                    foreach (NOfEvents e in nOfEvents.ToList())
                    {
                        if (e.Seen(peer))
                        {
                            nOfEvents.Remove(e);
                            if (--n == 0)
                            {
                                Ready = true;
                                break;
                            }
                        }
                    }
                }
            }

            if (Ready)
            {
                eventHubs.Clear();
                peers.Clear();
                nOfEvents.Clear();
            }

            return Ready;
        }

        public static NOfEvents CreateNofEvents()
        {
            return new NOfEvents();
        }


        public static NOfEvents CreateNoEvents()
        {
            return NofNoEvents;
        }
        /**
            * Special NofEvents indicating that no transaction events are needed to complete the Future.
            * This will result in the Future being completed as soon has the Orderer has seen the transaction.
            */

        public class NoEvents : NOfEvents
        {
            public NoEvents() 
            {
                Ready = true;
            }

            public override NOfEvents AddNOfs(params NOfEvents[] nofEvents)
            {
                throw new ArgumentException("Can not add any events.");
            }

            public override NOfEvents AddEventHubs(params EventHub[] eventHub)
            {
                throw new ArgumentException("Can not add any events.");
            }

            public override NOfEvents AddPeers(params Peer[] pers)
            {
                throw new ArgumentException("Can not add any events.");
            }

            public override NOfEvents SetN(int num)
            {
                throw new ArgumentException("Can not set N");
            }

            public override NOfEvents AddEventHubs(IEnumerable<EventHub> evntHubs)
            {
                throw new ArgumentException("Can not add any events.");
            }

            public override NOfEvents AddPeers(IEnumerable<Peer> pers)
            {
                throw new ArgumentException("Can not add any events.");
            }
        }
    }
}