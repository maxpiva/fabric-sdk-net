using System;
using System.Collections.Generic;
using System.Text;
using Hyperledger.Fabric.Protos.Discovery;

namespace Hyperledger.Fabric.SDK.Channels
{
    /**
     * Additional metadata used by service discovery to find the endorsements needed.
     * Specify which chaincode is invoked and what collections are used.
     */
    public class ServiceDiscoveryChaincodeCalls
    {
        private readonly ChaincodeCall ret = null;

        public ServiceDiscoveryChaincodeCalls(string chaincodeName)
        {
            Name = chaincodeName;
        }

        public string Name { get; set; }
        public List<string> Collections { get; set; } = new List<string>();

        /**
         * The collections used by this chaincode.
         *
         * @param collectionName name of collection.
         * @return
         */

        public ServiceDiscoveryChaincodeCalls AddCollections(params string[] collectionName)
        {
            if (Collections == null)
                Collections = new List<string>();
            Collections.AddRange(collectionName);
            return this;
        }

        public string Write(List<ServiceDiscoveryChaincodeCalls> dep)
        {
            StringBuilder cns = new StringBuilder(1000);
            cns.Append("ServiceDiscoveryChaincodeCalls(name: ").Append(Name);
            List<string> collections = Collections;
            if (collections.Count > 0)
            {
                cns.Append(", collections:[");
                string sep2 = "";
                foreach (string collection in collections)
                {
                    cns.Append(sep2).Append(collection);
                    sep2 = ", ";
                }

                cns.Append("]");
            }

            if (dep != null && dep.Count > 0)
            {
                cns.Append(" ,dependents:[");
                string sep2 = "";

                foreach (ServiceDiscoveryChaincodeCalls chaincodeCalls in dep)
                {
                    cns.Append(sep2).Append(chaincodeCalls.Write(null));
                    sep2 = ", ";
                }

                cns.Append("]");
            }

            cns.Append(")");
            return cns.ToString();
        }

        /**
         * Create ch
         *
         * @param name
         * @return
         * @throws InvalidArgumentException
         */

        public static ServiceDiscoveryChaincodeCalls CreateServiceDiscoveryChaincodeCalls(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("The name parameter must be non null nor an empty string.");
            return new ServiceDiscoveryChaincodeCalls(name);
        }

        public ChaincodeCall Build()
        {
            if (ret == null)
            {
                ChaincodeCall rt = new ChaincodeCall();
                rt.Name = Name;
                if (Collections != null && Collections.Count > 0)
                {
                    rt.CollectionNames.AddRange(Collections);
                }
            }

            return ret;
        }
    }
}