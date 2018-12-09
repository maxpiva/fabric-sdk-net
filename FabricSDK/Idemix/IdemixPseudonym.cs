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

using System;
using Hyperledger.Fabric.SDK.AMCL;
using Hyperledger.Fabric.SDK.AMCL.FP256BN;

namespace Hyperledger.Fabric.SDK.Idemix
{
    /**
     * The class represents a pseudonym of a user,
     * unlinkable to other pseudonyms of the user.
     */
    public class IdemixPseudonym
    {
        /**
         * Constructor
         *
         * @param sk  the secret key of the user
         * @param ipk the public key of the issuer
         */
        public IdemixPseudonym(BIG sk, IdemixIssuerPublicKey ipk)
        {
            if (sk == null || ipk == null)
                throw new ArgumentException("Cannot construct idemix pseudonym from null input");
            RAND rng = IdemixUtils.GetRand();
            RandNym = rng.RandModOrder();
            Nym = ipk.Hsk.Mul2(sk, ipk.HRand, RandNym);
        }

        /**
         * @return the value of the pseudonym as an ECP
         */
        public ECP Nym { get; }

        /**
         * @return the secret randomness used to construct this pseudonym
         */
        public BIG RandNym { get; }
    }
}