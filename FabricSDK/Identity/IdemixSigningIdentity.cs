/*
 *
 *  Copyright IBM Corp. All Rights Reserved.
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
using Hyperledger.Fabric.Protos.Msp;
using Hyperledger.Fabric.SDK.AMCL.FP256BN;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Idemix;
using Hyperledger.Fabric.SDK.Logging;
using Hyperledger.Fabric.SDK.Security;
using Org.BouncyCastle.Crypto;

namespace Hyperledger.Fabric.SDK.Identity
{
    /**
     * IdemixSigningIdentity is an Idemix implementation of the SigningIdentity It
     * contains IdemixIdentity (a public part) and a corresponding secret part that
     * contains the user secret key and the commitment opening (randomness) to the
     * pseudonym value (a commitment to the user secret)
     * <p>
     * We note that since the attributes and their disclosure is fixed we are not
     * adding them as fields here.
     */
    public class IdemixSigningIdentity : ISigningIdentity
    {
        // discloseFlags will be passed to the idemix signing and verification
        // routines.
        // It informs idemix to disclose both attributes (OU and Role) when signing.
        private static readonly bool[] disclosedFlags = new bool[] {true, true, false, false};

        // empty message to sign in the validate identity proof
        private static readonly byte[] msgEmpty = { };

        // the revocation handle is always the third attribute
        private static readonly int rhIndex = 3;

        private static readonly ILog logger = LogProvider.GetLogger(typeof(IdemixSigningIdentity));

        // credental revocation information
        private readonly CredentialRevocationInformation cri;

        // public part of the signing identity (passed with the signature)
        private readonly IdemixIdentity idemixIdentity;

        // public key of the Idemix CA (issuer)
        private readonly IdemixIssuerPublicKey ipk;

        // user's secret
        private readonly BIG sk;


        public IdemixSigningIdentity(IdemixEnrollment enrollment) : this(enrollment.Ipk, enrollment.RevocationPk, enrollment.MspId, enrollment.Sk, enrollment.Cred, enrollment.Cri, enrollment.Ou, enrollment.RoleMask)
        {
        }

        /**
         * Create new Idemix Signing Identity with a fresh pseudonym
         *
         * @param ipk          issuer public key
         * @param revocationPk the issuer's long term revocation public key
         * @param mspId        MSP identifier
         * @param sk           user's secret
         * @param cred         idemix credential
         * @param cri          the credential revocation information
         * @param ou           is OU attribute
         * @param role         is role attribute
         * @throws CryptoException
         * @throws InvalidArgumentException
         */
        public IdemixSigningIdentity(IdemixIssuerPublicKey ipk, KeyPair revocationPk, string mspId, BIG sk, IdemixCredential cred, CredentialRevocationInformation cri, string ou, IdemixRoles role)
        {
            // input checks
            if (ipk == null)
                throw new ArgumentException("Issuer Public Key (IPK) must not be null");
            if (revocationPk == null)
                throw new ArgumentException("Revocation PK must not be null");
            if (mspId == null)
                throw new ArgumentException("MSP ID must not be null");
            if (string.IsNullOrEmpty(mspId))
                throw new ArgumentException("MSP ID must not be empty");
            if (ou == null)
                throw new ArgumentException("OU must not be null");
            if (string.IsNullOrEmpty(ou))
                throw new ArgumentException("OU must not be empty");
            if (sk == null)
                throw new ArgumentException("SK must not be null");
            if (cred == null)
                throw new ArgumentException("Credential must not be null");
            if (cri == null)
                throw new ArgumentException("Credential revocation information must not be null");

            logger.Trace($"Verifying public key with hash: [{BitConverter.ToString(ipk.Hash).Replace("-", "")}] \nAttributes: [{string.Join(",", ipk.AttributeNames)}]");

            if (!ipk.Check())
            {
                CryptoException e = new CryptoException("Issuer public key is not valid");
                logger.Error("", e);
                throw e;
            }

            this.ipk = ipk;
            this.sk = sk;
            this.cri = cri;

            logger.Trace("Verifying the credential");

            // cryptographically verify credential
            // (check if the issuer's signature is valid)
            if (!cred.Verify(sk, ipk))
            {
                CryptoException e = new CryptoException("Credential is not cryptographically valid");
                logger.Error("", e);
                throw e;
            }

            logger.Trace("Checking attributes");

            // attribute checks
            // 4 attributes are expected:
            // - organization unit (disclosed)
            // - role: admin or member (disclosed)
            // - enrollment id (hidden, for future auditing feature and authorization with CA)
            // - revocation handle (hidden, for future revocation support)
            if (cred.Attrs.Length != 4)
                throw new CryptoException($"Error: There are {cred.Attrs.Length} attributes and the expected are 4");

            byte[] ouBytes = cred.Attrs[0];
            byte[] roleBytes = cred.Attrs[1];
            byte[] eIdBytes = cred.Attrs[2];
            byte[] rHBytes = cred.Attrs[3];

            BIG[] attributes = new BIG[4];
            attributes[0] = BIG.FromBytes(ouBytes);
            attributes[1] = BIG.FromBytes(roleBytes);
            attributes[2] = BIG.FromBytes(eIdBytes);
            attributes[3] = BIG.FromBytes(rHBytes);

            // check that the OU string matches the credential's attribute value
            if (!ou.ToBytes().HashModOrder().ToBytes().SequenceEqual(ouBytes))
                throw new ArgumentException("the OU string does not match the credential");

            // check that the role matches the credential's attribute value
            if (!new BIG((int) role).ToBytes().SequenceEqual(roleBytes))
                throw new ArgumentException("the role does not match the credential");

            logger.Trace("Generating fresh pseudonym and proof");
            // generate a fresh pseudonym
            Pseudonym = new IdemixPseudonym(this.sk, this.ipk);

            // generate a fresh proof of possession of a credential
            // with respect to a freshly generated pseudonym
            Proof = new IdemixSignature(cred, this.sk, Pseudonym, this.ipk, disclosedFlags, msgEmpty, rhIndex, cri);
            logger.Trace("Verifying the proof");
            // verify the proof
            if (!Proof.Verify(disclosedFlags, this.ipk, msgEmpty, attributes, rhIndex, revocationPk, (int) cri.Epoch))
                throw new CryptoException("Generated proof of identity is not valid");

            logger.Trace("Generating the Identity Object");
            // generate a fresh identity with new pseudonym
            idemixIdentity = new IdemixIdentity(mspId, this.ipk, Pseudonym.Nym, ou, role, Proof);
            logger.Trace(idemixIdentity.ToString());
        }

        // idemix pseudonym (represents Idemix identity)
        public IdemixPseudonym Pseudonym { get; }

        // proof that the identity is valid (proof of possession of a credential
        // with respect to a pseudonym.
        public IdemixSignature Proof { get; }


        public byte[] Sign(byte[] msg)
        {
            if (msg == null)
                throw new ArgumentException("Input must not be null");

            return new IdemixPseudonymSignature(sk, Pseudonym, ipk, msg).ToProto().ToByteArray();
        }


        public SerializedIdentity CreateSerializedIdentity()
        {
            return idemixIdentity.CreateSerializedIdentity();
        }

        public bool VerifySignature(byte[] msg, byte[] sig)
        {
            if (msg == null)
                throw new ArgumentException("Message must not be null");

            if (sig == null)
                throw new ArgumentException("Signature must not be null");

            NymSignature nymSigProto;
            try
            {
                nymSigProto = NymSignature.Parser.ParseFrom(sig);
            }
            catch (InvalidProtocolBufferException e)
            {
                logger.Error($"Idemix Nym Signature parsing error, dumping \nSignature: {BitConverter.ToString(sig).Replace("-", "")} \nMessage: {msg.ToUTF8String()}");
                throw new CryptoException("Could not parse Idemix Nym Signature", e);
            }

            IdemixPseudonymSignature nymSig = new IdemixPseudonymSignature(nymSigProto);
            if (!nymSig.Verify(Pseudonym.Nym, ipk, msg))
            {
                logger.Error($"Idemix Nym Signature verification error, dumping \nSignature: {BitConverter.ToString(sig).Replace("-", "")} \nMessage: {msg.ToUTF8String()}");
                return false;
            }

            return true;
        }
    }
}