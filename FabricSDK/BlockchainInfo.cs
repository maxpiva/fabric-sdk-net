/*
 *  Copyright 2016, 2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
 *
 *  Licensed under the Apache License, Version 2.0 (the "License");
 *  you may not use this file except in compliance with the License.
 *  You may obtain a copy of the License at
 *    http://www.apache.org/licenses/LICENSE-2.0
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed under the License is distributed on an "AS IS" BASIS,
 *  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *  See the License for the specific language governing permissions and
 *  limitations under the License.
 */



namespace Hyperledger.Fabric.SDK
{
	/**
	 * BlockchainInfo contains information about the blockchain ledger.
	 */
    public class BlockchainInfo
    {

        private Protos.Common.BlockchainInfo blockchainInfo;

        public BlockchainInfo(Protos.Common.BlockchainInfo blockchainInfo)
        {
            this.blockchainInfo = blockchainInfo;
        }

        /**
         * @return the current ledger blocks height
         */
        public long Height => blockchainInfo.Height;

        /**
         * @return the current bloch hash
         */
        public byte[] CurrentBlockHash => blockchainInfo.currentBlockHash;

        /**
         * @return the previous block hash
         */
        public byte[] PreviousBlockHash => blockchainInfo.previousBlockHash;

        /**
         * @return the protobuf BlockchainInfo struct this object is based on.
         */
        public Protos.Common.BlockchainInfo ProtoBlockchainInfo => blockchainInfo;
    }
}


