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
     * WeakBB contains the functions to use Weak Boneh-Boyen signatures (https://ia.cr/2004/171)
     */
    public class WeakBB
    {
        private WeakBB()
        {
            // private constructor for util class
        }

        /**
         * Generate a new key-pair set
         *
         * @return a freshly generated key pair
         */
        public static KeyPair WeakBBKeyGen()
        {
            return new KeyPair();
        }

        /**
         * Produces a WBB signature for a give message
         *
         * @param sk Secret key
         * @param m  Message
         * @return Signature
         */
        public static ECP WeakBBSign(BIG sk, BIG m)
        {
            BIG exp = sk.ModAdd(m, IdemixUtils.GROUP_ORDER);
            exp.InvModp(IdemixUtils.GROUP_ORDER);

            return IdemixUtils.GenG1.Mul(exp);
        }

        /**
         * Verify a WBB signature for a certain message
         *
         * @param pk  Public key
         * @param sig Signature
         * @param m   Message
         * @return True iff valid
         */
        public static bool weakBBVerify(ECP2 pk, ECP sig, BIG m)
        {
            ECP2 p = new ECP2();
            p.Copy(pk);
            p.Add(IdemixUtils.GenG2.Mul(m));
            p.Affine();

            return PAIR.FExp(PAIR.Ate(p, sig)).Equals(IdemixUtils.GenGT);
        }

        /**
         * WeakBB.KeyPair represents a key pair for weak Boneh-Boyen signatures
         */
        public class KeyPair
        {
            public KeyPair()
            {
                RAND rng = IdemixUtils.GetRand();
                Sk = rng.RandModOrder();
                Pk = IdemixUtils.GenG2.Mul(Sk);
            }

            public BIG Sk { get; }
            public ECP2 Pk { get; }
        }
    }
}