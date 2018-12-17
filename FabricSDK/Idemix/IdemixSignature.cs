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
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Security;
using ECP = Hyperledger.Fabric.SDK.AMCL.FP256BN.ECP;
using ECP2 = Hyperledger.Fabric.Protos.Idemix.ECP2;

namespace Hyperledger.Fabric.SDK.Idemix
{
    /**
     * IdemixSignature represents an idemix signature, which is a zero knowledge proof
     * of knowledge of a BBS+ signature. The Camenisch-Drijvers-Lehmann ZKP (ia.cr/2016/663) is used
     */
    public class IdemixSignature
    {
        private static readonly string SIGN_LABEL = "sign";
        private readonly ECP aBar;

        private readonly ECP aPrime;
        private readonly ECP bPrime;
        private readonly BIG nonce;
        private readonly ECP nym;
        private readonly BIG proofC;
        private readonly BIG[] proofSAttrs;
        private readonly BIG proofSE;
        private readonly BIG proofSR2;
        private readonly BIG proofSR3;
        private readonly BIG proofSRNym;
        private readonly BIG proofSSk;
        private readonly BIG proofSSPrime;
        private readonly long epoch;
        private readonly NonRevocationProof nonRevocationProof;
        private readonly ECP2 revocationPk;
        private readonly byte[] revocationPKSig;

        /**
         * Create a new IdemixSignature by proving knowledge of a credential
         *
         * @param c          the credential used to create an idemix signature
         * @param sk         the signer's secret key
         * @param pseudonym  a pseudonym of the signer
         * @param ipk        the issuer public key
         * @param disclosure a bool-array that steers the disclosure of attributes
         * @param msg        the message to be signed
         * @param rhIndex    the index of the attribute that represents the revocation handle
         * @param cri        the credential revocation information that allows the signer to prove non-revocation
         */
        public IdemixSignature(IdemixCredential c, BIG sk, IdemixPseudonym pseudonym, IdemixIssuerPublicKey ipk, bool[] disclosure, byte[] msg, int rhIndex, CredentialRevocationInformation cri)
        {
            if (c == null || sk == null || pseudonym == null || pseudonym.Nym == null || pseudonym.RandNym == null || ipk == null || disclosure == null || msg == null || cri == null)
                throw new ArgumentException("Cannot construct idemix signature from null input");

            if (disclosure.Length != c.Attrs.Length)
                throw new ArgumentException("Disclosure length must be the same as the number of attributes");

            if (cri.RevocationAlg >= Enum.GetValues(typeof(RevocationAlgorithm)).Length)
                throw new ArgumentException("CRI specifies unknown revocation algorithm");

            if (cri.RevocationAlg != (int) RevocationAlgorithm.ALG_NO_REVOCATION && disclosure[rhIndex])
                throw new ArgumentException("Attribute " + rhIndex + " is disclosed but also used a revocation handle attribute, which should remain hidden");

            RevocationAlgorithm revocationAlgorithm = (RevocationAlgorithm) cri.RevocationAlg;

            int[] hiddenIndices = HiddenIndices(disclosure);
            RAND rng = IdemixUtils.GetRand();
            // Start signature
            BIG r1 = rng.RandModOrder();
            BIG r2 = rng.RandModOrder();
            BIG r3 = new BIG(r1);
            r3.InvModp(IdemixUtils.GROUP_ORDER);

            nonce = rng.RandModOrder();

            aPrime = PAIR.G1Mul(c.A, r1);
            aBar = PAIR.G1Mul(c.B, r1);
            aBar.Sub(PAIR.G1Mul(aPrime, c.E));

            bPrime = PAIR.G1Mul(c.B, r1);
            bPrime.Sub(PAIR.G1Mul(ipk.HRand, r2));
            BIG sPrime = new BIG(c.S);
            sPrime.Add(BIG.ModNeg(BIG.ModMul(r2, r3, IdemixUtils.GROUP_ORDER), IdemixUtils.GROUP_ORDER));
            sPrime.Mod(IdemixUtils.GROUP_ORDER);

            //Construct Zero Knowledge Proof
            BIG rsk = rng.RandModOrder();
            BIG re = rng.RandModOrder();
            BIG rR2 = rng.RandModOrder();
            BIG rR3 = rng.RandModOrder();
            BIG rSPrime = rng.RandModOrder();
            BIG rRNym = rng.RandModOrder();
            BIG[] rAttrs = new BIG[hiddenIndices.Length];
            for (int i = 0; i < hiddenIndices.Length; i++)
            {
                rAttrs[i] = rng.RandModOrder();
            }

            // Compute non-revoked proof
            INonRevocationProver prover = NonRevocationProver.GetNonRevocationProver(revocationAlgorithm);
            int hiddenRHIndex = Array.IndexOf(hiddenIndices, rhIndex);
            if (hiddenRHIndex < 0)
            {
                // rhIndex is not present, set to last index position
                hiddenRHIndex = hiddenIndices.Length;
            }

            byte[] nonRevokedProofHashData = prover.GetFSContribution(BIG.FromBytes(c.Attrs[rhIndex]), rAttrs[hiddenRHIndex], cri);
            if (nonRevokedProofHashData == null)
                throw new Exception("Failed to compute non-revoked proof");

            ECP t1 = aPrime.Mul2(re, ipk.HRand, rR2);
            ECP t2 = PAIR.G1Mul(ipk.HRand, rSPrime);
            t2.Add(bPrime.Mul2(rR3, ipk.Hsk, rsk));

            for (int i = 0; i < hiddenIndices.Length / 2; i++)
                t2.Add(ipk.HAttrs[hiddenIndices[2 * i]].Mul2(rAttrs[2 * i], ipk.HAttrs[hiddenIndices[2 * i + 1]], rAttrs[2 * i + 1]));

            if (hiddenIndices.Length % 2 != 0)
                t2.Add(PAIR.G1Mul(ipk.HAttrs[hiddenIndices[hiddenIndices.Length - 1]], rAttrs[hiddenIndices.Length - 1]));

            ECP t3 = ipk.Hsk.Mul2(rsk, ipk.HRand, rRNym);

            // create proofData such that it can contain the sign label, 7 elements in G1 (each of size 2*FIELD_BYTES+1),
            // the ipk hash, the disclosure array, and the message
            byte[] proofData = new byte[0];
            proofData = proofData.Append(SIGN_LABEL.ToBytes());
            proofData = proofData.Append(t1.ToBytes());
            proofData = proofData.Append(t2.ToBytes());
            proofData = proofData.Append(t3.ToBytes());
            proofData = proofData.Append(aPrime.ToBytes());
            proofData = proofData.Append(aBar.ToBytes());
            proofData = proofData.Append(bPrime.ToBytes());
            proofData = proofData.Append(pseudonym.Nym.ToBytes());
            proofData = proofData.Append(ipk.Hash);
            proofData = proofData.Append(disclosure);
            proofData = proofData.Append(msg);

            BIG cvalue = proofData.HashModOrder();

            byte[] finalProofData = new byte[0];
            finalProofData = finalProofData.Append(cvalue.ToBytes());
            finalProofData = finalProofData.Append(nonce.ToBytes());

            proofC = finalProofData.HashModOrder();

            proofSSk = rsk.ModAdd(BIG.ModMul(proofC, sk, IdemixUtils.GROUP_ORDER), IdemixUtils.GROUP_ORDER);
            proofSE = re.ModSub(BIG.ModMul(proofC, c.E, IdemixUtils.GROUP_ORDER), IdemixUtils.GROUP_ORDER);
            proofSR2 = rR2.ModAdd(BIG.ModMul(proofC, r2, IdemixUtils.GROUP_ORDER), IdemixUtils.GROUP_ORDER);
            proofSR3 = rR3.ModSub(BIG.ModMul(proofC, r3, IdemixUtils.GROUP_ORDER), IdemixUtils.GROUP_ORDER);
            proofSSPrime = rSPrime.ModAdd(BIG.ModMul(proofC, sPrime, IdemixUtils.GROUP_ORDER), IdemixUtils.GROUP_ORDER);
            proofSRNym = rRNym.ModAdd(BIG.ModMul(proofC, pseudonym.RandNym, IdemixUtils.GROUP_ORDER), IdemixUtils.GROUP_ORDER);

            nym = new ECP();
            nym.Copy(pseudonym.Nym);

            proofSAttrs = new BIG[hiddenIndices.Length];
            for (int i = 0; i < hiddenIndices.Length; i++)
            {
                proofSAttrs[i] = new BIG(rAttrs[i]);
                proofSAttrs[i].Add(BIG.ModMul(proofC, BIG.FromBytes(c.Attrs[hiddenIndices[i]]), IdemixUtils.GROUP_ORDER));
                proofSAttrs[i].Mod(IdemixUtils.GROUP_ORDER);
            }

            // include non-revocation proof in signature
            revocationPk = cri.EpochPk;
            revocationPKSig = cri.EpochPkSig.ToByteArray();
            epoch = cri.Epoch;
            nonRevocationProof = prover.GetNonRevocationProof(proofC);
        }

        /**
     * Construct a new signature from a serialized IdemixSignature
     *
     * @param proto a protobuf object representing an IdemixSignature
     */
        public IdemixSignature(Signature proto)
        {
            if (proto == null)
                throw new ArgumentException("Cannot construct idemix signature from null input");

            aBar = proto.ABar.ToECP();
            aPrime = proto.APrime.ToECP();
            bPrime = proto.BPrime.ToECP();
            nym = proto.Nym.ToECP();
            proofC = BIG.FromBytes(proto.ProofC.ToByteArray());
            proofSSk = BIG.FromBytes(proto.ProofSSk.ToByteArray());
            proofSE = BIG.FromBytes(proto.ProofSE.ToByteArray());
            proofSR2 = BIG.FromBytes(proto.ProofSR2.ToByteArray());
            proofSR3 = BIG.FromBytes(proto.ProofSR3.ToByteArray());
            proofSSPrime = BIG.FromBytes(proto.ProofSSPrime.ToByteArray());
            proofSRNym = BIG.FromBytes(proto.ProofSRNym.ToByteArray());
            nonce = BIG.FromBytes(proto.Nonce.ToByteArray());
            proofSAttrs = new BIG[proto.ProofSAttrs.Count];
            for (int i = 0; i < proto.ProofSAttrs.Count; i++)
                proofSAttrs[i] = BIG.FromBytes(proto.ProofSAttrs[i].ToByteArray());
            revocationPk = proto.RevocationEpochPk;
            revocationPKSig = proto.RevocationPkSig.ToByteArray();
            epoch = proto.Epoch;
            nonRevocationProof = proto.NonRevocationProof;
        }

        /**
         * Verify this signature
         *
         * @param disclosure      an array indicating which attributes it expects to be disclosed
         * @param ipk             the issuer public key
         * @param msg             the message that should be signed in this signature
         * @param attributeValues BIG array where attributeValues[i] contains the desired attribute value for the i-th attribute if its disclosed
         * @param rhIndex         index of the attribute that represents the revocation-handle
         * @param revPk           the long term public key used to authenticate CRIs
         * @param epoch           monotonically increasing counter representing a time window
         * @return true iff valid
         */
        // ReSharper disable once ParameterHidesMember
        public bool Verify(bool[] disclosure, IdemixIssuerPublicKey ipk, byte[] msg, BIG[] attributeValues, int rhIndex, KeyPair revPk, int epoch)
        {
            if (disclosure == null || ipk == null || msg == null || attributeValues == null || attributeValues.Length != ipk.AttributeNames.Length || disclosure.Length != ipk.AttributeNames.Length)
                return false;

            for (int i = 0; i < ipk.AttributeNames.Length; i++)
            {
                if (disclosure[i] && attributeValues[i] == null)
                    return false;
            }

            int[] hiddenIndices = HiddenIndices(disclosure);
            if (proofSAttrs.Length != hiddenIndices.Length)
                return false;

            if (aPrime.IsInfinity())
                return false;

            if (nonRevocationProof.RevocationAlg >= Enum.GetValues(typeof(RevocationAlgorithm)).Length)
                throw new ArgumentException("CRI specifies unknown revocation algorithm");

            RevocationAlgorithm revocationAlgorithm = (RevocationAlgorithm) nonRevocationProof.RevocationAlg;

            if (disclosure[rhIndex])
                throw new ArgumentException("Attribute " + rhIndex + " is disclosed but also used a revocation handle attribute, which should remain hidden");


            // Verify EpochPK
            if (!RevocationAuthority.VerifyEpochPK(revPk, revocationPk, revocationPKSig, epoch, revocationAlgorithm))
            {
                // Signature is based on an invalid revocation epoch public key
                return false;
            }

            FP12 temp1 = PAIR.Ate(ipk.W, aPrime);
            FP12 temp2 = PAIR.Ate(IdemixUtils.GenG2, aBar);
            temp2.Inverse();
            temp1.mul(temp2);
            if (!PAIR.FExp(temp1).IsUnity())
                return false;


            ECP t1 = aPrime.Mul2(proofSE, ipk.HRand, proofSR2);
            ECP temp = new ECP();
            temp.Copy(aBar);
            temp.Sub(bPrime);
            t1.Sub(PAIR.G1Mul(temp, proofC));

            ECP t2 = PAIR.G1Mul(ipk.HRand, proofSSPrime);
            t2.Add(bPrime.Mul2(proofSR3, ipk.Hsk, proofSSk));

            for (int i = 0; i < hiddenIndices.Length / 2; i++)
            {
                t2.Add(ipk.HAttrs[hiddenIndices[2 * i]].Mul2(proofSAttrs[2 * i], ipk.HAttrs[hiddenIndices[2 * i + 1]], proofSAttrs[2 * i + 1]));
            }

            if (hiddenIndices.Length % 2 != 0)
            {
                t2.Add(PAIR.G1Mul(ipk.HAttrs[hiddenIndices[hiddenIndices.Length - 1]], proofSAttrs[hiddenIndices.Length - 1]));
            }

            temp = new ECP();
            temp.Copy(IdemixUtils.GenG1);

            for (int i = 0; i < disclosure.Length; i++)
            {
                if (disclosure[i])
                {
                    temp.Add(PAIR.G1Mul(ipk.HAttrs[i], attributeValues[i]));
                }
            }

            t2.Add(PAIR.G1Mul(temp, proofC));

            ECP t3 = ipk.Hsk.Mul2(proofSSk, ipk.HRand, proofSRNym);
            t3.Sub(nym.Mul(proofC));

            // Check with non-revoked-verifier
            INonRevocationVerifier nonRevokedVerifier = NonRevocationVerifier.GetNonRevocationVerifier(revocationAlgorithm);
            int hiddenRHIndex = Array.IndexOf(hiddenIndices, rhIndex);
            if (hiddenRHIndex < 0)
            {
                // rhIndex is not present, set to last index position
                hiddenRHIndex = hiddenIndices.Length;
            }

            BIG proofSRh = proofSAttrs[hiddenRHIndex];
            byte[] nonRevokedProofBytes = nonRevokedVerifier.RecomputeFSContribution(nonRevocationProof, proofC, revocationPk.ToECP2(), proofSRh);
            if (nonRevokedProofBytes == null)
                return false;


            // create proofData such that it can contain the sign label, 7 elements in G1 (each of size 2*FIELD_BYTES+1),
            // the ipk hash, the disclosure array, and the message
            byte[] proofData = new byte[0];
            proofData = proofData.Append(SIGN_LABEL.ToBytes());
            proofData = proofData.Append(t1.ToBytes());
            proofData = proofData.Append(t2.ToBytes());
            proofData = proofData.Append(t3.ToBytes());
            proofData = proofData.Append(aPrime.ToBytes());
            proofData = proofData.Append(aBar.ToBytes());
            proofData = proofData.Append(bPrime.ToBytes());
            proofData = proofData.Append(nym.ToBytes());
            proofData = proofData.Append(ipk.Hash);
            proofData = proofData.Append(disclosure);
            proofData = proofData.Append(msg);

            BIG cvalue = proofData.HashModOrder();

            byte[] finalProofData = new byte[0];
            finalProofData = finalProofData.Append(cvalue.ToBytes());
            finalProofData = finalProofData.Append(nonce.ToBytes());

            byte[] hashedProofData = finalProofData.HashModOrder().ToBytes();
            return Enumerable.SequenceEqual(proofC.ToBytes(), hashedProofData);
        }

        /**
     * Convert this signature to a proto
     *
     * @return a protobuf object representing this IdemixSignature
     */
        public Signature ToProto()
        {
            Signature s = new Signature
            {
                APrime = aPrime.ToProto(),
                ABar = aBar.ToProto(),
                BPrime = bPrime.ToProto(),
                Nym = nym.ToProto(),
                ProofC = ByteString.CopyFrom(proofC.ToBytes()),
                ProofSSk = ByteString.CopyFrom(proofSSk.ToBytes()),
                ProofSE = ByteString.CopyFrom(proofSE.ToBytes()),
                ProofSR2 = ByteString.CopyFrom(proofSR2.ToBytes()),
                ProofSR3 = ByteString.CopyFrom(proofSR3.ToBytes()),
                ProofSRNym = ByteString.CopyFrom(proofSRNym.ToBytes()),
                ProofSSPrime = ByteString.CopyFrom(proofSSPrime.ToBytes()),
                Nonce = ByteString.CopyFrom(nonce.ToBytes()),
                RevocationEpochPk = revocationPk,
                RevocationPkSig = ByteString.CopyFrom(revocationPKSig),
                Epoch = epoch,
                NonRevocationProof = nonRevocationProof
            };

            foreach (BIG attr in proofSAttrs)
                s.ProofSAttrs.Add(ByteString.CopyFrom(attr.ToBytes()));
            return s;
        }

        /**
     * Some attributes may be hidden, some disclosed. The indices of the hidden attributes will be passed.
     *
     * @param disclosure an array where the i-th value indicates whether or not the i-th attribute should be disclosed
     * @return an integer array of the hidden indices
     */
        private int[] HiddenIndices(bool[] disclosure)
        {
            if (disclosure == null)
                throw new ArgumentException("cannot compute hidden indices of null disclosure");
            List<int> hiddenIndicesList = new List<int>();
            for (int i = 0; i < disclosure.Length; i++)
            {
                if (!disclosure[i])
                    hiddenIndicesList.Add(i);
            }

            return hiddenIndicesList.ToArray();
        }
    }
}