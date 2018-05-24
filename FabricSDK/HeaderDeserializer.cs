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
using Hyperledger.Fabric.Protos.Msp;
using Hyperledger.Fabric.SDK.Helper;

namespace Hyperledger.Fabric.SDK
{
    public class HeaderDeserializer
    {
        private readonly WeakItem<ChannelHeaderDeserializer, Header> channelHeader;

        public HeaderDeserializer(Header header)
        {
            Header = header;
            channelHeader = new WeakItem<ChannelHeaderDeserializer, Header>((h) => new ChannelHeaderDeserializer(h.ChannelHeader), () => Header);
        }

        public ChannelHeaderDeserializer ChannelHeader => channelHeader.Reference;
        public Header Header { get; }
        public SerializedIdentity Creator => SerializedIdentity.Parser.ParseFrom(SignatureHeader.Parser.ParseFrom(Header.SignatureHeader)?.Creator);
        public byte[] Nonce => SignatureHeader.Parser.ParseFrom(Header.SignatureHeader).Nonce.ToByteArray();
    }
}