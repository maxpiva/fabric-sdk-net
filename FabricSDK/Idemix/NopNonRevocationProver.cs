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

using Hyperledger.Fabric.Protos.Idemix;
using Hyperledger.Fabric.SDK.AMCL.FP256BN;

namespace Hyperledger.Fabric.SDK.Idemix
{
/**
 * NopNonRevocationProver is a concrete NonRevocationProver for RevocationAlgorithm "ALG_NO_REVOCATION"
 */
    public class NopNonRevocationProver : INonRevocationProver
    {
        private readonly byte[] empty = new byte[0];

        public byte[] GetFSContribution(BIG rh, BIG rRh, CredentialRevocationInformation cri)
        {
            return empty;
        }

        public NonRevocationProof GetNonRevocationProof(BIG challenge)
        {
            NonRevocationProof p = new NonRevocationProof();
            p.RevocationAlg = (int) RevocationAlgorithm.ALG_NO_REVOCATION;
            return p;
        }
    }
}