using System.Collections.Generic;
using Hyperledger.Fabric.Protos.Peer.PeerEvents;
using Hyperledger.Fabric.SDK.Deserializers;
using Hyperledger.Fabric.SDK.Helper;

namespace Hyperledger.Fabric.SDK.Blocks
{
    /**
     * Return and iterable EnvelopeInfo over each Envelope contained in the Block
     *
     * @return
     */

    public class TransactionEnvelopeInfo : EnvelopeInfo
    {
        protected readonly EndorserTransactionEnvDeserializer transactionDeserializer;

        public TransactionEnvelopeInfo(BlockInfo parent, FilteredTransaction filteredTx) : base(parent, filteredTx)
        {
            transactionDeserializer = null;
        }

        public TransactionEnvelopeInfo(BlockInfo parent, EndorserTransactionEnvDeserializer transactionDeserializer) : base(parent, transactionDeserializer)
        {
            this.transactionDeserializer = transactionDeserializer;
        }

        /**
         * Signature for the transaction.
         *
         * @return byte array that as the signature.
         */
        public byte[] Signature => transactionDeserializer.Signature;

        public EndorserTransactionEnvDeserializer TransactionDeserializer => transactionDeserializer;

        public int TransactionActionInfoCount => parent.IsFiltered ? filteredTx.TransactionActions.ChaincodeActions.Count : transactionDeserializer.Payload.Transaction.ActionsCount;

        public IEnumerable<TransactionActionInfo> TransactionActionInfos => new EnumerableBuilder<TransactionActionInfo>(() => TransactionActionInfoCount, GetTransactionActionInfo);

        public TransactionActionInfo GetTransactionActionInfo(int index)
        {
            return parent.IsFiltered ? new TransactionActionInfo(parent, filteredTx.TransactionActions.ChaincodeActions[index]) : new TransactionActionInfo(parent, transactionDeserializer.Payload.Transaction.GetTransactionAction(index));
        }

        
    }
}