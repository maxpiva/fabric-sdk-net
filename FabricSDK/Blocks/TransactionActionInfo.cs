using Google.Protobuf;
using Hyperledger.Fabric.Protos.Ledger.Rwset;
using Hyperledger.Fabric.Protos.Peer.PeerEvents;
using Hyperledger.Fabric.SDK.Deserializers;
using Hyperledger.Fabric.SDK.Helper;

namespace Hyperledger.Fabric.SDK.Blocks
{
    public class TransactionActionInfo
    {
        private readonly FilteredChaincodeAction filteredAction;
        private readonly WeakDictionary<int, EndorserInfo> infos;
        private readonly BlockInfo parent;
        private readonly TransactionActionDeserializer transactionAction;

        public TransactionActionInfo(BlockInfo parent, TransactionActionDeserializer transactionAction)
        {
            this.parent = parent;
            this.transactionAction = transactionAction;
            filteredAction = null;
            infos = new WeakDictionary<int, EndorserInfo>((index) => new EndorserInfo(transactionAction.Payload.Action.ChaincodeEndorsedAction.Endorsements[index]));
        }

        public TransactionActionInfo(BlockInfo parent, FilteredChaincodeAction filteredAction)
        {
            this.parent = parent;
            this.filteredAction = filteredAction;
            transactionAction = null;
            infos = new WeakDictionary<int, EndorserInfo>((index) => new EndorserInfo(transactionAction.Payload.Action.ChaincodeEndorsedAction.Endorsements[index]));
        }

        public bool IsFiltered => filteredAction != null;

        public byte[] ResponseMessageBytes => IsFiltered ? null : transactionAction.Payload.Action.ProposalResponsePayload.Extension.ResponseMessage.ToBytes();

        public string ResponseMessage => IsFiltered ? null : transactionAction.Payload.Action.ProposalResponsePayload.Extension.ResponseMessage;

        public int ResponseStatus => IsFiltered ? -1 : transactionAction.Payload.Action.ProposalResponsePayload.Extension.ResponseStatus;
        public int ChaincodeInputArgsCount => IsFiltered ? 0 : transactionAction.Payload.ChaincodeProposalPayload.ChaincodeInvocationSpec.ChaincodeInput.ChaincodeInput.Args.Count;
        public int EndorsementsCount => IsFiltered ? 0 : transactionAction.Payload.Action.EndorsementsCount;

        public byte[] ProposalResponseMessageBytes => IsFiltered ? null : transactionAction.Payload.Action.ProposalResponsePayload.Extension.ResponseMessage.ToBytes();

        public byte[] ProposalResponsePayload => IsFiltered ? null : transactionAction.Payload.Action.ProposalResponsePayload.Extension.ResponsePayload?.ToByteArray();

        public int ProposalResponseStatus => IsFiltered ? -1 : transactionAction.Payload.Action.ProposalResponsePayload.Extension.ResponseStatus;

        /**
         * get name of chaincode with this transaction action
         *
         * @return name of chaincode.  Maybe null if no chaincode or if block is filtered.
         */
        public string ChaincodeIDName => IsFiltered ? null : transactionAction.Payload.Action.ProposalResponsePayload.Extension.ChaincodeID?.Name;

        /**
         * get path of chaincode with this transaction action
         *
         * @return path of chaincode.  Maybe null if no chaincode or if block is filtered.
         */
        public string ChaincodeIDPath => IsFiltered ? null : transactionAction.Payload.Action.ProposalResponsePayload.Extension.ChaincodeID?.Path;


        /**
         * get version of chaincode with this transaction action
         *
         * @return version of chaincode.  Maybe null if no chaincode or if block is filtered.
         */

        public string ChaincodeIDVersion => IsFiltered ? null : transactionAction.Payload.Action.ProposalResponsePayload.Extension.ChaincodeID?.Version;


        /**


            /**
             * Get read write set for this transaction. Will return null on for Eventhub events.
             * For eventhub events find the block by block number to get read write set if needed.
             *
             * @return Read write set.
             */

        public TxReadWriteSetInfo TxReadWriteSet
        {
            get
            {
                if (parent.IsFiltered)
                {
                    return null;
                }

                TxReadWriteSet txReadWriteSet = transactionAction.Payload.Action.ProposalResponsePayload.Extension.Results;
                if (txReadWriteSet == null)
                {
                    return null;
                }

                return new TxReadWriteSetInfo(txReadWriteSet);
            }
        }

        /**
         * Get chaincode events for this transaction.
         *
         * @return A chaincode event if the chaincode set an event otherwise null.
         */

        public ChaincodeEventDeserializer Event
        {
            get
            {
                if (IsFiltered)
                    return new ChaincodeEventDeserializer(filteredAction.ChaincodeEvent.ToByteString());
                return transactionAction.Payload.Action.ProposalResponsePayload.Extension.Event;
            }
        }

        public byte[] GetChaincodeInputArgs(int index) => IsFiltered ? null : transactionAction.Payload.ChaincodeProposalPayload.ChaincodeInvocationSpec.ChaincodeInput.ChaincodeInput.Args[index].ToByteArray();

        public EndorserInfo GetEndorsementInfo(int index) => IsFiltered ? null : infos.Get(index);
    }
}