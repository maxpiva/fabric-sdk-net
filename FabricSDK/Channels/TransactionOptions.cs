using System.Collections.Generic;
using System.Linq;

namespace Hyperledger.Fabric.SDK.Channels
{
    /**
     * TransactionOptions class can be used to change how the SDK processes the Transaction.
     */
    public class TransactionOptions
    {
        private TransactionOptions()
        {
        }

        public List<Orderer> Orderers { get; set; }
        public bool ShuffleOrders { get; set; } = true;
        public NOfEvents NOfEvents { get; set; }
        public IUser UserContext { get; set; }
        public bool FailFast { get; set; } = true;

        /**
         * Fail fast when there is an invalid transaction received on the eventhub or eventing peer being observed.
         * The default value is true.
         *
         * @param failFast fail fast.
         * @return This TransactionOptions
         */
        public TransactionOptions SetFailFast(bool failFast)
        {
            FailFast = failFast;
            return this;
        }

        /**
         * The user context that is to be used. The default is the user context on the client.
         *
         * @param userContext
         * @return This TransactionOptions
         */
        public TransactionOptions SetUserContext(IUser userContext)
        {
            UserContext = userContext;
            return this;
        }

        /**
         * The orders to try on this transaction. Each order is tried in turn for a successful submission.
         * The default is try all orderers on the chain.
         *
         * @param orderers the orderers to try.
         * @return This TransactionOptions
         */
        public TransactionOptions SetOrderers(params Orderer[] orderers)
        {
            Orderers = orderers.ToList();
            return this;
        }

        /**
         * Shuffle the order the Orderers are tried. The default is true.
         *
         * @param shuffleOrders
         * @return This TransactionOptions
         */
        public TransactionOptions SetShuffleOrders(bool shuffleOrders)
        {
            ShuffleOrders = shuffleOrders;
            return this;
        }

        /**
         * Events reporting Eventing Peers and EventHubs to complete the transaction.
         * This maybe set to NOfEvents.nofNoEvents that will complete the future as soon as a successful submission
         * to an Orderer, but the completed Transaction event in that case will be null.
         *
         * @param nOfEvents @see {@link NOfEvents}
         * @return This TransactionOptions
         */
        public TransactionOptions SetNOfEvents(NOfEvents nOfEvents)
        {
            NOfEvents = nOfEvents == NOfEvents.NofNoEvents ? nOfEvents : new NOfEvents(nOfEvents);
            return this;
        }

        /**
         * Create transaction options.
         *
         * @return return transaction options.
         */
        public static TransactionOptions Create()
        {
            return new TransactionOptions();
        }

        /**
         * The orders to try on this transaction. Each order is tried in turn for a successful submission.
         * The default is try all orderers on the chain.
         *
         * @param orderers the orderers to try.
         * @return This TransactionOptions
         */
        public TransactionOptions SetOrderers(IEnumerable<Orderer> orderers)
        {
            return SetOrderers(orderers.ToArray());
        }
    }
}