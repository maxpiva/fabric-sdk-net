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
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Hyperledger.Fabric.Protos.Idemix;
using Hyperledger.Fabric.SDK.AMCL;
using Hyperledger.Fabric.SDK.AMCL.FP256BN;
using ECP = Hyperledger.Fabric.SDK.AMCL.FP256BN.ECP;
using ECP2 = Hyperledger.Fabric.SDK.AMCL.FP256BN.ECP2;

namespace Hyperledger.Fabric.SDK.Idemix
{
    /**
     * IdemixIssuerPublicKey represents the idemix public key of an issuer (Certificate Authority).
     */
    public class IdemixIssuerPublicKey
    {
        private readonly ECP BarG1;
        private readonly ECP BarG2;
        private readonly BIG ProofC;
        private readonly BIG ProofS;

        /**
         * Constructor
         *
         * @param attributeNames the names of attributes as String array (must not contain duplicates)
         * @param isk            the issuer secret key
         */
        public IdemixIssuerPublicKey(string[] attributeNames, BIG isk)
        {
            // check null input
            if (attributeNames == null || isk == null)
            {
                throw new ArgumentException("Cannot create IdemixIssuerPublicKey from null input");
            }

            // Checking if attribute names are unique
            HashSet<string> map = new HashSet<string>();
            foreach (string item in attributeNames)
            {
                if (map.Contains(item))
                    throw new ArgumentException("Attribute " + item + " appears multiple times in attributeNames");
                map.Add(item);
            }

            RAND rng = IdemixUtils.GetRand();
            // Attaching Attribute Names array correctly
            AttributeNames = attributeNames;

            // Computing W value
            W = IdemixUtils.GenG2.Mul(isk);

            // Filling up HAttributes correctly in Issuer Public Key, length
            // preserving
            HAttrs = new ECP[attributeNames.Length];

            for (int i = 0; i < attributeNames.Length; i++)
            {
                HAttrs[i] = IdemixUtils.GenG1.Mul(rng.RandModOrder());
            }

            // Generating Hsk value
            Hsk = IdemixUtils.GenG1.Mul(rng.RandModOrder());

            // Generating HRand value
            HRand = IdemixUtils.GenG1.Mul(rng.RandModOrder());

            // Generating BarG1 value
            BarG1 = IdemixUtils.GenG1.Mul(rng.RandModOrder());

            // Generating BarG2 value
            BarG2 = BarG1.Mul(isk);

            // Zero Knowledge Proofs

            // Computing t1 and t2 values with random local variable r for later use
            BIG r = rng.RandModOrder();
            ECP2 t1 = IdemixUtils.GenG2.Mul(r);
            ECP t2 = BarG1.Mul(r);

            // Generating proofData that will contain 3 elements in G1 (of size 2*FIELD_BYTES+1)and 3 elements in G2 (of size 4 * FIELD_BYTES)
            byte[] proofData = new byte[0];
            proofData = proofData.Append(t1.ToBytes());
            proofData = proofData.Append(t2.ToBytes());
            proofData = proofData.Append(IdemixUtils.GenG2.ToBytes());
            proofData = proofData.Append(BarG1.ToBytes());
            proofData = proofData.Append(W.ToBytes());
            proofData = proofData.Append(BarG2.ToBytes());

            // Hashing proofData to proofC
            ProofC = proofData.HashModOrder();

            // Computing ProofS = (ProofC*isk) + r mod GROUP_ORDER
            ProofS = BIG.ModMul(ProofC, isk, IdemixUtils.GROUP_ORDER).Plus(r);
            ProofS.Mod(IdemixUtils.GROUP_ORDER);

            // Compute Hash of IdemixIssuerPublicKey
            byte[] serializedIpk = ToProto().ToByteArray();
            Hash = serializedIpk.HashModOrder().ToBytes();
        }

        /**
         * Construct an IdemixIssuerPublicKey from a serialized issuer public key
         *
         * @param proto a protobuf representation of an issuer public key
         */
        public IdemixIssuerPublicKey(IssuerPublicKey proto)
        {
            // check for bad input
            if (proto == null)
            {
                throw new ArgumentException("Cannot create IdemixIssuerPublicKey from null input");
            }

            if (proto.HAttrs.Count < proto.AttributeNames.Count)
            {
                throw new ArgumentException("Serialized IPk does not contain enough HAttr values");
            }

            AttributeNames = new string[proto.AttributeNames.Count];
            for (int i = 0; i < proto.AttributeNames.Count; i++)
            {
                AttributeNames[i] = proto.AttributeNames[i];
            }

            HAttrs = new ECP[proto.HAttrs.Count];
            for (int i = 0; i < proto.HAttrs.Count; i++)
            {
                HAttrs[i] = proto.HAttrs[i].ToECP();
            }

            BarG1 = proto.BarG1.ToECP();
            BarG2 = proto.BarG2.ToECP();
            HRand = proto.HRand.ToECP();
            Hsk = proto.HSk.ToECP();
            ProofC = BIG.FromBytes(proto.ProofC.ToByteArray());
            ProofS = BIG.FromBytes(proto.ProofS.ToByteArray());
            W = proto.W.ToECP2();

            // Compute Hash of IdemixIssuerPublicKey
            byte[] serializedIpk = ToProto().ToByteArray();
            Hash = serializedIpk.HashModOrder().ToBytes();
        }


        /**
         * @return The names of the attributes certified with this issuer public key
         */
        public string[] AttributeNames { get; }
        public ECP Hsk { get; }
        public ECP HRand { get; }
        public ECP[] HAttrs { get; }
        public ECP2 W { get; }

        /**
         * @return A digest of this issuer public key
         */
        // ReSharper disable once MemberInitializerValueIgnored
        public byte[] Hash { get; }=new byte[0];


        /**
         * check whether the issuer public key is correct
         *
         * @return true iff valid
         */
        public bool Check()
        {
            // check formalities of IdemixIssuerPublicKey
            if (AttributeNames == null || Hsk == null || HRand == null || HAttrs == null || BarG1 == null || BarG1.IsInfinity() || BarG2 == null || HAttrs.Length < AttributeNames.Length)
            {
                return false;
            }

            for (int i = 0; i < AttributeNames.Length; i++)
            {
                if (HAttrs[i] == null)
                {
                    return false;
                }
            }

            // check proofs
            ECP2 t1 = IdemixUtils.GenG2.Mul(ProofS);
            ECP t2 = BarG1.Mul(ProofS);

            t1.Add(W.Mul(BIG.ModNeg(ProofC, IdemixUtils.GROUP_ORDER)));
            t2.Add(BarG2.Mul(BIG.ModNeg(ProofC, IdemixUtils.GROUP_ORDER)));

            // Generating proofData that will contain 3 elements in G1 (of size 2*FIELD_BYTES+1)and 3 elements in G2 (of size 4 * FIELD_BYTES)
            byte[] proofData = new byte[0];
            proofData = proofData.Append(t1.ToBytes());
            proofData = proofData.Append(t2.ToBytes());
            proofData = proofData.Append(IdemixUtils.GenG2.ToBytes());
            proofData = proofData.Append(BarG1.ToBytes());
            proofData = proofData.Append(W.ToBytes());
            proofData = proofData.Append(BarG2.ToBytes());

            // Hash proofData to hproofdata and compare with proofC
            return Enumerable.SequenceEqual(proofData.HashModOrder().ToBytes(), ProofC.ToBytes());
        }

        /**
         * @return A proto version of this issuer public key
         */
        public IssuerPublicKey ToProto()
        {
            IssuerPublicKey ipc = new IssuerPublicKey
            {
                ProofC = ByteString.CopyFrom(ProofC.ToBytes()),
                ProofS = ByteString.CopyFrom(ProofS.ToBytes()),
                W = W.ToProto(),
                HSk = Hsk.ToProto(),
                HRand = HRand.ToProto(),
                Hash = ByteString.CopyFrom(Hash),
                BarG1 = BarG1.ToProto(),
                BarG2 = BarG2.ToProto()
            };
            foreach (string attributeName in AttributeNames)
                ipc.AttributeNames.Add(attributeName);
            foreach (ECP ecp in HAttrs)
                ipc.HAttrs.Add(ecp.ToProto());
            return ipc;
        }
    }
}