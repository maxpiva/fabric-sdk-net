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
//    package org.hyperledger.fabric.sdk;

/**
 * ChaincodeID identifies chaincode.
 */
    public class ChaincodeID
    {

        private readonly Protos.Peer.ChaincodeID fabricChaincodeID;

        public Protos.Peer.ChaincodeID FabricChaincodeID => fabricChaincodeID;

        public ChaincodeID(Protos.Peer.ChaincodeID chaincodeID)
        {
            this.fabricChaincodeID = chaincodeID;
        }
        /**
         * @param name of the Chaincode
         * @return Builder
         */

        public ChaincodeID Name(string name)
        {
            fabricChaincodeID.Name = name;
            return this;
        }
        /**
         * Set path of chaincode
         *
         * @param path of chaincode
         * @return Builder
         */
        public ChaincodeID Path(string path)
        {
            fabricChaincodeID.Path = path;
            return this;
        }
        /**
         * Set the version of the Chaincode
         *
         * @param version of the chaincode
         * @return Builder
         */
        public ChaincodeID Version(string version)
        {
            fabricChaincodeID.Version = version;
            return this;
        }
        public override string ToString()
        {
            return "ChaincodeID(" + fabricChaincodeID.Name + ":" + fabricChaincodeID.Path + ":" + fabricChaincodeID.Version + ")";
        }
    }
}
