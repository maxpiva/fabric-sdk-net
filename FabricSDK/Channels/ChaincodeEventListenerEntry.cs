using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Hyperledger.Fabric.SDK.Blocks;
using Hyperledger.Fabric.SDK.Deserializers;
using Hyperledger.Fabric.SDK.Helper;

namespace Hyperledger.Fabric.SDK.Channels
{
    public class ChaincodeEventListenerEntry
    {
        public static readonly string CHAINCODE_EVENTS_TAG = "CHAINCODE_EVENTS_HANDLE";
        private readonly Regex chaincodeIdPattern;
        private readonly Regex eventNamePattern;
        private readonly Action<string, BlockEvent, ChaincodeEventDeserializer> listenerAction;

        public ChaincodeEventListenerEntry(Channel ch, Regex chaincodeIdPattern, Regex eventNamePattern, Action<string, BlockEvent, ChaincodeEventDeserializer> listenerAction)
        {
            Channel = ch;
            this.chaincodeIdPattern = chaincodeIdPattern;
            this.eventNamePattern = eventNamePattern;
            this.listenerAction = listenerAction;
            Handle = CHAINCODE_EVENTS_TAG + Utils.GenerateUUID() + CHAINCODE_EVENTS_TAG;
            lock (Channel.chainCodeListeners)
            {
                Channel.chainCodeListeners.Add(Handle, this);
            }
        }

        public Channel Channel { get; }

        public string Handle { get; }

        public bool IsMatch(ChaincodeEventDeserializer chaincodeEvent)
        {
            return chaincodeIdPattern.Match(chaincodeEvent.ChaincodeId).Success && eventNamePattern.Match(chaincodeEvent.EventName).Success;
        }

        public void Fire(BlockEvent blockEvent, ChaincodeEventDeserializer ce)
        {
            Task.Run(() => listenerAction(Handle, blockEvent, ce));
        }
    }
}