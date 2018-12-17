using System.Collections.Generic;
using System.Linq;

namespace Hyperledger.Fabric.SDK.Channels
{
    /**
       * Options for the peer.
       * These options are channel based.
       */


    public class PeerOptions
    {
        protected List<PeerRole> peerRoles;

        protected PeerOptions()
        {
        }

        /**
         * Get newest block on startup of peer eventing service.
         *
         * @return
         */

        public bool? Newest { get; private set; } = true;
        /**
         * The block number to start getting events from on start up of the peer eventing service..
         *
         * @return the start number
         */

        public long? StartEventsBlock { get; private set; }

        /**
         * The stopping block number when the peer eventing service will stop sending blocks.
         *
         * @return the stop block number.
         */


        public long StopEventsBlock { get; private set; } = long.MaxValue;

        /**
         * Clone.
         *
         * @return return a duplicate of this instance.
         */


        /**
         * Is the peer eventing service registered for filtered blocks
         *
         * @return true if filtered blocks will be returned by the peer eventing service.
         */

        public bool IsRegisterEventsForFilteredBlocks { get; protected set; }

        /**
         * Return the roles the peer has.
         *
         * @return the roles {@link PeerRole}
         */

        public List<PeerRole> PeerRoles
        {
            get => peerRoles ?? (peerRoles = PeerRoleExtensions.NoDiscovery());
            internal set => peerRoles = value;
        }

        public PeerOptions Clone()
        {
            PeerOptions p = new PeerOptions();
            p.peerRoles = peerRoles?.ToList();
            p.IsRegisterEventsForFilteredBlocks = IsRegisterEventsForFilteredBlocks;
            p.Newest = Newest;
            p.StartEventsBlock = StartEventsBlock;
            p.StopEventsBlock = StopEventsBlock;
            return p;
        }

        /**
         * Register the peer eventing services to return filtered blocks.
         *
         * @return the PeerOptions instance.
         */

        public PeerOptions RegisterEventsForFilteredBlocks()
        {
            IsRegisterEventsForFilteredBlocks = true;
            return this;
        }

        /**
         * Register the peer eventing services to return full event blocks.
         *
         * @return the PeerOptions instance.
         */

        public PeerOptions RegisterEventsForBlocks()
        {
            IsRegisterEventsForFilteredBlocks = false;
            return this;
        }

        /**
         * Create an instance of PeerOptions.
         *
         * @return the PeerOptions instance.
         */

        public static PeerOptions CreatePeerOptions()
        {
            return new PeerOptions();
        }

        public bool HasPeerRoles()
        {
            return peerRoles != null && peerRoles.Count > 0;
        }
        /**
         * Set the roles this peer will have on the chain it will added or joined.
         *
         * @param peerRoles {@link PeerRole}
         * @return This PeerOptions.
         */

        public PeerOptions SetPeerRoles(List<PeerRole> pRoles)
        {
            peerRoles = pRoles;
            return this;
        }

        public PeerOptions SetPeerRoles(params PeerRole[] pRoles)
        {
            peerRoles = pRoles.ToList();
            return this;
        }
        /**
         * Add to the roles this peer will have on the chain it will added or joined.
         *
         * @param peerRole see {@link PeerRole}
         * @return This PeerOptions.
         */

        public PeerOptions AddPeerRole(PeerRole peerRole)
        {
            if (peerRoles == null)
                peerRoles = new List<PeerRole>();
            if (!peerRoles.Contains(peerRole))
                peerRoles.Add(peerRole);
            return this;
        }

        /**
         * Set the block number the eventing peer will start relieving events.
         *
         * @param start The staring block number.
         * @return This PeerOptions.
         */
        public PeerOptions StartEvents(long start)
        {
            StartEventsBlock = start;
            Newest = null;
            return this;
        }


        /**
         * This is the default. It will start retrieving events with the newest. Note this is not the
         * next block that is added to the chain  but the current block on the chain.
         *
         * @return This PeerOptions.
         */

        public PeerOptions StartEventsNewest()
        {
            StartEventsBlock = null;
            Newest = true;
            return this;
        }

        /**
         * The block number to stop sending events.
         *
         * @param stop the number to stop sending events.
         * @return This PeerOptions.
         */
        public PeerOptions StopEvents(long stop)
        {
            StopEventsBlock = stop;
            return this;
        }
    }
}