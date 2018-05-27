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

using System;
using Google.Protobuf;
using Hyperledger.Fabric.Protos.Ledger.Rwset;
using Hyperledger.Fabric.Protos.Peer.FabricProposal;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;

namespace Hyperledger.Fabric.SDK.Deserializers
{
    public class ChaincodeActionDeserializer : BaseDeserializer<ChaincodeAction>
    {
        public ChaincodeActionDeserializer(ByteString byteString) : base(byteString)
        {
        }
        public ChaincodeAction ChaincodeAction => Reference;
        
        public ChaincodeEventDeserializer Event => new ChaincodeEventDeserializer(ChaincodeAction.Events);
        public TxReadWriteSet Results
        {
            get
            {
                try
                {
                    return TxReadWriteSet.Parser.ParseFrom(ChaincodeAction?.Results);
                }
                catch (Exception e)
                {
                    throw new InvalidProtocolBufferRuntimeException(e);
                }

            }
        }

        public string ResponseMessage => ChaincodeAction?.Response?.Message;
        public int ResponseStatus => ChaincodeAction?.Response?.Status ?? 0;
        public ByteString ResponsePayload => ChaincodeAction?.Response?.Payload;
    }

}
