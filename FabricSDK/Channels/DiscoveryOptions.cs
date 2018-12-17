using System;
using System.Collections.Generic;
using System.Linq;
using Hyperledger.Fabric.SDK.Discovery;

namespace Hyperledger.Fabric.SDK.Channels
{
    /**
     * Options for doing service discovery.
     */
    public class DiscoveryOptions
    {
        private readonly HashSet<string> ignoreList = new HashSet<string>();

        public Func<SDChaindcode, SDEndorserState> EndorsementSelector { get; private set; }

        public List<ServiceDiscoveryChaincodeCalls> ServiceDiscoveryChaincodeInterests { get; private set; }

        public bool ForceDiscovery { get; private set; }

        public bool IsInspectResults { get; set; }

        public List<string> IgnoreList => ignoreList.ToList();

        /**
         * Create transaction options.
         *
         * @return return transaction options.
         */
        public static DiscoveryOptions CreateDiscoveryOptions()
        {
            return new DiscoveryOptions();
        }

        /**
         * Set to true to inspect proposals results on error.
         *
         * @param inspectResults
         * @return
         */
        public DiscoveryOptions SetInspectResults(bool inspectResults)
        {
            IsInspectResults = inspectResults;
            return this;
        }

        /**
         * Set the handler which selects the endorser endpoints from the alternatives provided by service discovery.
         *
         * @param endorsementSelector
         * @return
         * @throws InvalidArgumentException
         */
        public DiscoveryOptions SetEndorsementSelector(Func<SDChaindcode, SDEndorserState> endorsementSelector)
        {
            EndorsementSelector = endorsementSelector ?? throw new ArgumentException("endorsementSelector parameter is null.");
            return this;
        }

        /**
         * Set which other chaincode calls are made by this chaincode and they're collections.
         *
         * @param serviceDiscoveryChaincodeInterests
         * @return DiscoveryOptions
         */

        public DiscoveryOptions SetServiceDiscoveryChaincodeInterests(params ServiceDiscoveryChaincodeCalls[] serviceDiscoveryChaincodeInterests)
        {
            if (ServiceDiscoveryChaincodeInterests == null)
            {
                ServiceDiscoveryChaincodeInterests = new List<ServiceDiscoveryChaincodeCalls>();
            }

            ServiceDiscoveryChaincodeInterests.AddRange(ServiceDiscoveryChaincodeInterests);
            return this;
        }

        /**
     * Force new service discovery
     *
     * @param forceDiscovery
     * @return
     */

        public DiscoveryOptions SetForceDiscovery(bool forceDiscovery)
        {
            ForceDiscovery = forceDiscovery;
            return this;
        }

        public DiscoveryOptions IgnoreEndpoints(params string[] endpoints)
        {
            if (endpoints == null)
            {
                throw new ArgumentException("endpoints parameter is null.");
            }

            foreach (string endpoint in endpoints)
            {
                if (endpoint == null)
                {
                    throw new ArgumentException("endpoints parameter is null.");
                }

                ignoreList.Add(endpoint);
            }

            return this;
        }
    }
}