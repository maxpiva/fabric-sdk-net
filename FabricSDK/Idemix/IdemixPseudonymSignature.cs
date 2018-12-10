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
using System.Linq;
using Google.Protobuf;
using Hyperledger.Fabric.Protos.Idemix;
using Hyperledger.Fabric.SDK.AMCL;
using Hyperledger.Fabric.SDK.AMCL.FP256BN;
using Hyperledger.Fabric.SDK.Helper;
using ECP = Hyperledger.Fabric.SDK.AMCL.FP256BN.ECP;

namespace Hyperledger.Fabric.SDK.Idemix
{
    /**
     * IdemixPseudonymSignature is a signature on a message which can be verified with respect to a pseudonym
     */
    public class IdemixPseudonymSignature
    {
        private static readonly string NYM_SIGN_LABEL = "sign";
        private readonly BIG nonce;
        private readonly BIG proofC;
        private readonly BIG proofSRNym;
        private readonly BIG proofSSk;

        /**
         * Constructor
         *
         * @param sk        the secret key
         * @param pseudonym the pseudonym with respect to which this signature can be verified
         * @param ipk       the issuer public key
         * @param msg       the message to be signed
         */
        public IdemixPseudonymSignature(BIG sk, IdemixPseudonym pseudonym, IdemixIssuerPublicKey ipk, byte[] msg)
        {
            if (sk == null || pseudonym == null || pseudonym.Nym == null || pseudonym.RandNym == null || ipk == null || msg == null)
            {
                throw new ArgumentException("Cannot create IdemixPseudonymSignature from null input");
            }

            RAND rng = IdemixUtils.GetRand();
            nonce = rng.RandModOrder();

            //Construct Zero Knowledge Proof
            BIG rsk = rng.RandModOrder();
            BIG rRNym = rng.RandModOrder();
            ECP t = ipk.Hsk.Mul2(rsk, ipk.HRand, rRNym);

            // create array for proof data that will contain the sign label, 2 ECPs (each of length 2* FIELD_BYTES + 1), the ipk hash and the message
            byte[] proofData = new byte[0];
            proofData = proofData.Append(NYM_SIGN_LABEL.ToBytes());
            proofData = proofData.Append(t.ToBytes());
            proofData = proofData.Append(pseudonym.Nym.ToBytes());
            proofData = proofData.Append(ipk.Hash);
            proofData = proofData.Append(msg);

            BIG cvalue = proofData.HashModOrder();

            byte[] finalProofData = new byte[0];
            finalProofData = finalProofData.Append(cvalue.ToBytes());
            finalProofData = finalProofData.Append(nonce.ToBytes());
            proofC = finalProofData.HashModOrder();

            proofSSk = new BIG(rsk);
            proofSSk.Add(BIG.ModMul(proofC, sk, IdemixUtils.GROUP_ORDER));
            proofSSk.Mod(IdemixUtils.GROUP_ORDER);

            proofSRNym = new BIG(rRNym);
            proofSRNym.Add(BIG.ModMul(proofC, pseudonym.RandNym, IdemixUtils.GROUP_ORDER));
            proofSRNym.Mod(IdemixUtils.GROUP_ORDER);
        }

        /**
         * Construct a new signature from a serialized IdemixPseudonymSignature
         *
         * @param proto a protobuf object representing an IdemixPseudonymSignature
         */
        public IdemixPseudonymSignature(NymSignature proto)
        {
            if (proto == null)
                throw new ArgumentException("Cannot create idemix nym signature from null input");

            proofC = BIG.FromBytes(proto.ProofC.ToByteArray());
            proofSSk = BIG.FromBytes(proto.ProofSSk.ToByteArray());
            proofSRNym = BIG.FromBytes(proto.ProofSRNym.ToByteArray());
            nonce = BIG.FromBytes(proto.Nonce.ToByteArray());
        }

        /**
         * Verify this IdemixPseudonymSignature
         *
         * @param nym the pseudonym with respect to which the signature is verified
         * @param ipk the issuer public key
         * @param msg the message that should be signed in this signature
         * @return true iff valid
         */
        public bool Verify(ECP nym, IdemixIssuerPublicKey ipk, byte[] msg)
        {
            if (nym == null || ipk == null || msg == null)
                return false;

            ECP t = ipk.Hsk.Mul2(proofSSk, ipk.HRand, proofSRNym);
            t.Sub(nym.Mul(proofC));

            // create array for proof data that will contain the sign label, 2 ECPs (each of length 2* FIELD_BYTES + 1), the ipk hash and the message
            byte[] proofData = new byte[0];
            proofData = proofData.Append(NYM_SIGN_LABEL.ToBytes());
            proofData = proofData.Append(t.ToBytes());
            proofData = proofData.Append(nym.ToBytes());
            proofData = proofData.Append(ipk.Hash);
            proofData = proofData.Append(msg);

            BIG cvalue = proofData.HashModOrder();

            byte[] finalProofData = new byte[0];
            finalProofData = finalProofData.Append(cvalue.ToBytes());
            finalProofData = finalProofData.Append(nonce.ToBytes());

            byte[] hashedProofData = finalProofData.HashModOrder().ToBytes();
            return Enumerable.SequenceEqual(proofC.ToBytes(), hashedProofData);
        }

        /**
         * @return A proto object representing this IdemixPseudonymSignature
         */
        public NymSignature ToProto()
        {
            return new NymSignature
            {
                ProofC = ByteString.CopyFrom(proofC.ToBytes()),
                ProofSSk = ByteString.CopyFrom(proofSSk.ToBytes()),
                ProofSRNym = ByteString.CopyFrom(proofSRNym.ToBytes()),
                Nonce = ByteString.CopyFrom(nonce.ToBytes())
            };
        }
    }
}