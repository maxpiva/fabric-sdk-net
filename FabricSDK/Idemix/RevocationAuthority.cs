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
using Google.Protobuf;
using Hyperledger.Fabric.Protos.Idemix;
using Hyperledger.Fabric.SDK.AMCL.FP256BN;
using Hyperledger.Fabric.SDK.Security;
using Org.BouncyCastle.Crypto;
using ECP2 = Hyperledger.Fabric.Protos.Idemix.ECP2;

namespace Hyperledger.Fabric.SDK.Idemix
{
    public class RevocationAuthority
    {
        private RevocationAuthority()
        {
            // private constructor for utility class
        }

        /**
     * Depending on the selected revocation algorithm, the proof data length will be different.
     * This method will give the proof length for any supported revocation algorithm.
     *
     * @param alg The revocation algorithm
     * @return The proof data length for the given revocation algorithm
     */
        public static int GetProofBytes(RevocationAlgorithm alg)
        {
            switch (alg)
            {
                case RevocationAlgorithm.ALG_NO_REVOCATION:
                    return 0;
                default:
                    throw new ArgumentException("Unsupported RevocationAlgorithm: " + alg.ToString());
            }
        }

        /**
     * Generate a long term ECDSA key pair used for revocation
     *
     * @return Freshly generated ECDSA key pair
     */
        public static KeyPair GenerateLongTermRevocationKey()
        {
            try
            {
                return KeyPair.GenerateECDSA("secp384r1");
            }
            catch (Exception e)
            {
                throw new Exception("Error during the LTRevocation key. Invalid algorithm", e);
            }
        }

        /**
     * Creates a Credential Revocation Information object
     *
     * @param key              Private key
     * @param unrevokedHandles Array of unrevoked revocation handles
     * @param epoch            The counter (representing a time window) in which this CRI is valid
     * @param alg              Revocation algorithm
     * @return CredentialRevocationInformation object
     */
        public static CredentialRevocationInformation CreateCRI(KeyPair key, BIG[] unrevokedHandles, int epoch, RevocationAlgorithm alg)
        {
            CredentialRevocationInformation cr = new CredentialRevocationInformation();
            cr.RevocationAlg = (int) alg;
            cr.Epoch = epoch;

            // Create epoch key
            WeakBB.KeyPair keyPair = WeakBB.WeakBBKeyGen();
            if (alg == RevocationAlgorithm.ALG_NO_REVOCATION)
            {
                // Dummy PK in the proto
                cr.EpochPk = IdemixUtils.GenG2.ToProto();
            }
            else
            {
                // Real PK only if we are going to use it
                cr.EpochPk = keyPair.Pk.ToProto();
            }

            // Sign epoch + epoch key with the long term key
            byte[] signed;
            try
            {
                signed = key.Sign(cr.ToByteArray(), "SHA256withECDSA");
                cr.EpochPkSig = ByteString.CopyFrom(signed);
            }
            catch (Exception e)
            {
                throw new CryptoException("Error processing the signature: ", e);
            }

            if (alg == RevocationAlgorithm.ALG_NO_REVOCATION)
            {
                // build and return the credential information object
                return cr;
            }
            // If alg not supported, return null
            throw new ArgumentException("Algorithm " + alg.ToString() + " not supported");
        }

        /**
     * Verifies that the revocation PK for a certain epoch is valid,
     * by checking that it was signed with the long term revocation key
     *
     * @param pk         Public Key
     * @param epochPK    Epoch PK
     * @param epochPkSig Epoch PK Signature
     * @param epoch      Epoch
     * @param alg        Revocation algorithm
     * @return True if valid
     */
        public static bool VerifyEpochPK(KeyPair pk, ECP2 epochPK, byte[] epochPkSig, long epoch, RevocationAlgorithm alg)
        {
            CredentialRevocationInformation cr = new CredentialRevocationInformation();
            cr.RevocationAlg = (int) alg;
            cr.EpochPk = epochPK;
            cr.Epoch = epoch;

            byte[] bytesTosign = cr.ToByteArray();
            try
            {
                return pk.Verify(bytesTosign, epochPkSig, "SHA256withECDSA");
            }
            catch (Exception e)
            {
                throw new CryptoException("Error during the EpochPK verification", e);
            }
        }
    }
}