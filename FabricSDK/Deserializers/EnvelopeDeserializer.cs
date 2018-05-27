/*
 *
 *  Copyright 2016,2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
 *
 *  Licensed under the Apache License, Version 2.0 (the "License");
 *  you may not use this file except in compliance with the License.
 *  You may obtain a copy of the License at
 *     http://www.apache.org/licenses/LICENSE-2.0
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed under the License is distributed on an "AS IS" BASIS,
 *  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *  See the License for the specific language governing permissions and
 *  limitations under the License.
 *
 */

using Google.Protobuf;
using Hyperledger.Fabric.Protos.Common;
using Hyperledger.Fabric.Protos.Peer.FabricTransaction;
using Hyperledger.Fabric.SDK.Helper;

namespace Hyperledger.Fabric.SDK.Deserializers
{
    public class EnvelopeDeserializer : BaseDeserializer<Envelope>
    {
        private readonly WeakItem<PayloadDeserializer,Envelope> payload;            
        public EnvelopeDeserializer(ByteString byteString, byte validcode) : base(byteString)
        {
            ValidationCode = validcode;
            payload = new WeakItem<PayloadDeserializer, Envelope>((env) => new PayloadDeserializer(env.Payload), () => Reference);
        }

        /**
            * @return the validation code of this Transaction (enumeration TxValidationCode in Transaction.proto)
         */
        public byte ValidationCode { get; }

        public Envelope Envelope => Reference;
        public byte[] Signature => Envelope?.Signature.ToByteArray();
        public PayloadDeserializer Payload => payload.Reference;        
        public int Type => Payload?.Header?.ChannelHeader?.Type ?? 0;

        /**
         * @return whether this Transaction is marked as TxValidationCode.VALID
         */
        public bool IsValid => ValidationCode == (int) TxValidationCode.Valid;

        public static EnvelopeDeserializer Create(ByteString byteString, byte b)
        {
            EnvelopeDeserializer ret;
            int type = ChannelHeader.Parser.ParseFrom(Protos.Common.Payload.Parser.ParseFrom(Envelope.Parser.ParseFrom(byteString).Payload).Header.ChannelHeader).Type;

            /*
     
         MESSAGE = 0;                   // Used for messages which are signed but opaque
         CONFIG = 1;                    // Used for messages which express the channel config
         CONFIG_UPDATE = 2;             // Used for transactions which update the channel config
         ENDORSER_TRANSACTION = 3;      // Used by the SDK to submit endorser based transactions
         ORDERER_TRANSACTION = 4;       // Used internally by the orderer for management
         DELIVER_SEEK_INFO = 5;         // Used as the type for Envelope messages submitted to instruct the Deliver API to seek
         CHAINCODE_PACKAGE = 6;         // Used for packaging chaincode artifacts for install
     
          */

            switch (type)
            {
                case 3:
                    ret = new EndorserTransactionEnvDeserializer(byteString, b);
                    break;
                default: //just assume base properties.
                    ret = new EnvelopeDeserializer(byteString, b);
                    break;
            }

            return ret;
        }
    }
}