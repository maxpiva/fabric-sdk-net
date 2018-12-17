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

using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Hyperledger.Fabric.Protos.Common;

namespace Hyperledger.Fabric.SDK
{
    /**
     * Collection of information on chaincode collection.
     */
    public class CollectionConfigPackage
    {
        private readonly ByteString collectionConfigBytes;
        private Protos.Common.CollectionConfigPackage cp;

        public CollectionConfigPackage(ByteString collectionConfig)
        {
            collectionConfigBytes = collectionConfig;
        }

        /**
         * The raw collection information returned from the peer.
         *
         * @return The raw collection information returned from the peer.
         * @throws InvalidProtocolBufferException
         */

        public Protos.Common.CollectionConfigPackage CollectionConfigPackageProto
        {
            get
            {
                if (null == cp)
                    cp = Protos.Common.CollectionConfigPackage.Parser.ParseFrom(collectionConfigBytes);
                return cp;
            }
        }


        /**
         * Collection of the chaincode collections.
         *
         * @return Collection of the chaincode collection
         * @throws InvalidProtocolBufferException
         */
        public List<CollectionConfig> CollectionConfigs => CollectionConfigPackageProto.Config.Select(a => new CollectionConfig(a)).ToList();
    }
    /**
     * Collection information.
     */

    public class CollectionConfig
    {
        private readonly Protos.Common.CollectionConfig collectionConfig;
        private readonly StaticCollectionConfig getStaticCollectionConfig;


        public CollectionConfig(Protos.Common.CollectionConfig cConfig)
        {
            collectionConfig = cConfig;
            getStaticCollectionConfig = collectionConfig.StaticCollectionConfig;
        }

        /**
         * Name of the collection.
         *
         * @return
         */
        public string Name => getStaticCollectionConfig.Name;

        /**
         * return required peer
         *
         * @return required peer count.
         */

        public int RequiredPeerCount => getStaticCollectionConfig.RequiredPeerCount;

        /**
         * Minimum peer count.
         *
         * @return minimum peer count.
         */
        public int MaximumPeerCount => getStaticCollectionConfig.MaximumPeerCount;

        /**
         * Block to live.
         *
         * @return block to live.
         */
        public long BlockToLive => (long) getStaticCollectionConfig.BlockToLive;

        /**
         * The collection information returned directly from the peer.
         *
         * @return The collection information returned directly from the peer.
         */
        public Protos.Common.CollectionConfig CollectionConfigProto
        {
            get
            {
                var _ = collectionConfig.StaticCollectionConfig;
                return collectionConfig;
            }
        }
    }
}