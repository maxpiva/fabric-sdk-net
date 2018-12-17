/*
 *
 *  Copyright 2017, 2018 IBM Corp. All Rights Reserved.
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


using Hyperledger.Fabric.SDK.AMCL;
using Hyperledger.Fabric.SDK.AMCL.FP256BN;

namespace Hyperledger.Fabric.SDK.Idemix
{
    /**
     * IdemixIssuerKey represents an idemix issuer key pair
     */
    public class IdemixIssuerKey
    {
        /**
         * Constructor
         *
         * @param attributeNames the names of attributes as String array (must not contain duplicates)
         */
        public IdemixIssuerKey(string[] attributeNames)
        {
            RAND rng = IdemixUtils.GetRand();
            // generate the secret key
            Isk = rng.RandModOrder();

            // construct the corresponding public key
            Ipk = new IdemixIssuerPublicKey(attributeNames, Isk);
        }

        /**
         * @return The secret part of the issuer key pair
         */
        public BIG Isk { get; }

        /**
         * @return The public part of the issuer key pair
         */
        public IdemixIssuerPublicKey Ipk { get; }
    }
}