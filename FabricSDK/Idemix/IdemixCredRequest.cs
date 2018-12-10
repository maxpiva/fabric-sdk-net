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
using Newtonsoft.Json.Linq;
using ECP = Hyperledger.Fabric.SDK.AMCL.FP256BN.ECP;

namespace Hyperledger.Fabric.SDK.Idemix
{
    /**
     * IdemixCredRequest represents the first message of the idemix issuance protocol,
     * in which the user requests a credential from the issuer.
     */
    public class IdemixCredRequest
    {
        private static readonly string CREDREQUEST_LABEL = "credRequest";
        private readonly BIG issuerNonce;
        private readonly BIG proofC;
        private readonly BIG proofS;


        /**
         * Constructor
         *
         * @param sk          the secret key of the user
         * @param issuerNonce a nonce
         * @param ipk         the issuer public key
         */
        public IdemixCredRequest(BIG sk, BIG issuerNonce, IdemixIssuerPublicKey ipk)
        {
            if (sk == null)
                throw new ArgumentException("Cannot create idemix credrequest from null Secret Key input");

            if (issuerNonce == null)
                throw new ArgumentException("Cannot create idemix credrequest from null issuer nonce input");

            if (ipk == null)
                throw new ArgumentException("Cannot create idemix credrequest from null Issuer Public Key input");

            RAND rng = IdemixUtils.GetRand();
            Nym = ipk.Hsk.Mul(sk);
            this.issuerNonce = new BIG(issuerNonce);

            // Create Zero Knowledge Proof
            BIG rsk = rng.RandModOrder();
            ECP t = ipk.Hsk.Mul(rsk);

            // Make proofData: total 3 elements of G1, each 2*FIELD_BYTES+1 (ECP),
            // plus length of String array,
            // plus one BIG
            byte[] proofData = new byte[0];
            proofData = proofData.Append(CREDREQUEST_LABEL.ToBytes());
            proofData = proofData.Append(t.ToBytes());
            proofData = proofData.Append(ipk.Hsk.ToBytes());
            proofData = proofData.Append(Nym.ToBytes());
            proofData = proofData.Append(issuerNonce.ToBytes());
            proofData = proofData.Append(ipk.Hash);

            proofC = proofData.HashModOrder();

            // Compute proofS = ...
            proofS = BIG.ModMul(proofC, sk, IdemixUtils.GROUP_ORDER).Plus(rsk);
            proofS.Mod(IdemixUtils.GROUP_ORDER);
        }

        /**
         * Construct a IdemixCredRequest from a serialized credrequest
         *
         * @param proto a protobuf representation of a credential request
         */
        public IdemixCredRequest(CredRequest proto)
        {
            if (proto == null)
                throw new ArgumentException("Cannot create idemix credrequest from null input");

            Nym = proto.Nym.ToECP();
            proofC = BIG.FromBytes(proto.ProofC.ToByteArray());
            proofS = BIG.FromBytes(proto.ProofS.ToByteArray());
            issuerNonce = BIG.FromBytes(proto.IssuerNonce.ToByteArray());
        }

        /**
         * @return a pseudonym of the credential requester
         */
        public ECP Nym { get; }


        /**
         * @return a proto version of this IdemixCredRequest
         */
        public CredRequest ToProto()
        {
            return new CredRequest
            {
                Nym = Nym.ToProto(),
                ProofC = ByteString.CopyFrom(proofC.ToBytes()),
                ProofS = ByteString.CopyFrom(proofS.ToBytes()),
                IssuerNonce = ByteString.CopyFrom(issuerNonce.ToBytes())
            };
        }


        /**
         * Cryptographically verify the IdemixCredRequest
         *
         * @param ipk the issuer public key
         * @return true iff valid
         */
        public bool Check(IdemixIssuerPublicKey ipk)
        {
            if (Nym == null || issuerNonce == null || proofC == null || proofS == null || ipk == null)
            {
                return false;
            }

            ECP t = ipk.Hsk.Mul(proofS);
            t.Sub(Nym.Mul(proofC));

            byte[] proofData = new byte[0];
            proofData = proofData.Append(CREDREQUEST_LABEL.ToBytes());
            proofData = proofData.Append(t.ToBytes());
            proofData = proofData.Append(ipk.Hsk.ToBytes());
            proofData = proofData.Append(Nym.ToBytes());
            proofData = proofData.Append(issuerNonce.ToBytes());
            proofData = proofData.Append(ipk.Hash);


            // Hash proofData to hproofdata
            byte[] hproofdata = proofData.HashModOrder().ToBytes();

            return Enumerable.SequenceEqual(proofC.ToBytes(), hproofdata);
        }

        // Convert the enrollment request to a JSON string
        public string ToJson()
        {
            return ToJsonObject().ToString();
        }

        // Convert the enrollment request to a JSON object
        public JObject ToJsonObject()
        {
            JObject rt = new JObject();
            if (Nym != null)
            {
                JObject nym = new JObject();
                nym.Add("x", Convert.ToBase64String(Nym.X.ToBytes()));
                nym.Add("y", Convert.ToBase64String(Nym.Y.ToBytes()));
                rt.Add("nym", nym);
            }

            if (issuerNonce != null)
                rt.Add("issuer_nonce", Convert.ToBase64String(issuerNonce.ToBytes()));
            if (proofC != null)
                rt.Add("proof_c", Convert.ToBase64String(proofC.ToBytes()));
            if (proofS != null)
                rt.Add("proof_s", Convert.ToBase64String(proofS.ToBytes()));
            return rt;
        }
    }
}