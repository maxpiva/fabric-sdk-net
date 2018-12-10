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
using Hyperledger.Fabric.Protos.Common;
using Hyperledger.Fabric.Protos.Idemix;
using Hyperledger.Fabric.Protos.Msp;
using Hyperledger.Fabric.SDK.AMCL.FP256BN;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Idemix;
using Hyperledger.Fabric.SDK.Logging;
using ECP = Hyperledger.Fabric.SDK.AMCL.FP256BN.ECP;

namespace Hyperledger.Fabric.SDK.Identity
{
    /**
     * IdemixIdentity is a public serializable part of the IdemixSigningIdentity.
     * It contains an (un)linkable pseudonym, revealed attribute values, and a
     * corresponding proof of possession of an Idemix credential
     */
    public class IdemixIdentity : IIdentity
    {
        private static readonly ILog logger = LogProvider.GetLogger(typeof(IdemixIdentity));

        // Proof of possession of Idemix credential
        // with respect to the pseudonym (nym)
        // and the corresponding attributes (ou, roleMask)
        private readonly IdemixSignature associationProof;

        // Issuer public key hash
        private readonly byte[] ipkHash;

        // MSP identifier
        private readonly string mspId;

        // Idemix Pseudonym
        private readonly ECP pseudonym;

        /**
         * Create Idemix Identity from a Serialized Identity
         *
         * @param proto
         */
        public IdemixIdentity(SerializedIdentity proto)
        {
            if (proto == null)
                throw new ArgumentException("Input must not be null");

            mspId = proto.Mspid;

            try
            {
                logger.Trace("Fetching Idemix Proto");
                SerializedIdemixIdentity idemixProto = SerializedIdemixIdentity.Parser.ParseFrom(proto.IdBytes);
                if (idemixProto == null)
                    throw new ArgumentException("The identity does not contain a serialized idemix identity");
                logger.Trace("Deserializing Nym and attribute values");
                pseudonym = new ECP(BIG.FromBytes(idemixProto.NymX.ToByteArray()), BIG.FromBytes(idemixProto.NymY.ToByteArray()));
                OrganizationUnit ou = OrganizationUnit.Parser.ParseFrom(idemixProto.Ou);
                MSPRole role = MSPRole.Parser.ParseFrom(idemixProto.Role);

                Ou = ou.OrganizationalUnitIdentifier;
                RoleMask = role.Role.ToIdemixRole();
                ipkHash = ou.CertifiersIdentifier.ToByteArray();
                logger.Trace("Deserializing Proof");
                associationProof = new IdemixSignature(Signature.Parser.ParseFrom(idemixProto.Proof.ToByteArray()));
            }
            catch (InvalidProtocolBufferException e)
            {
                throw new CryptoException("Cannot deserialize MSP ID", e);
            }
        }

        /**
         * Create Idemix Identity from the following inputs:
         *
         * @param mspId is MSP ID sting
         * @param nym   is Identity Mixer Pseudonym
         * @param ou    is OU attribute
         * @param roleMask  is a bitmask that represent all the roles attached to this identity
         * @param proof is Proof
         */
        public IdemixIdentity(string mspId, IdemixIssuerPublicKey ipk, ECP nym, string ou, IdemixRoles roleMask, IdemixSignature proof)
        {
            if (mspId == null)
                throw new ArgumentException("MSP ID must not be null");

            if (string.IsNullOrEmpty(mspId))
                throw new ArgumentException("MSP ID must not be empty");

            if (ipk == null)
                throw new ArgumentException("Issuer Public Key must not be empty");

            if (nym == null)
                throw new ArgumentException("Identity Mixer Pseudonym (nym) must not be null");

            if (ou == null)
                throw new ArgumentException("OU attribute must not be null");

            if (string.IsNullOrEmpty(ou))
                throw new ArgumentException("OU attribute must not be empty");

            if (proof == null)
                throw new ArgumentException("Proof must not be null");


            this.mspId = mspId;
            ipkHash = ipk.Hash;
            pseudonym = nym;
            Ou = ou;
            RoleMask = roleMask;
            associationProof = proof;
        }

        // Organization Unit attribute
        public string Ou { get; }

        // Role mask its a bitmask that represent all the roles attached to this identity
        public IdemixRoles RoleMask { get; }

        /**
         * Serialize Idemix Identity
         */

        public SerializedIdentity CreateSerializedIdentity()
        {
            OrganizationUnit ou = new OrganizationUnit();
            ou.CertifiersIdentifier = ByteString.CopyFrom(ipkHash);
            ou.MspIdentifier = mspId;
            ou.OrganizationalUnitIdentifier = Ou;

            //Warning, this does not support multi-roleMask.
            //Serialize the bitmask is the correct way to support multi-roleMask in the future
            MSPRole role = new MSPRole();
            role.Role = RoleMask.ToMSPRoleTypes().First();
            role.MspIdentifier = mspId;
            SerializedIdemixIdentity serializedIdemixIdentity = new SerializedIdemixIdentity();
            serializedIdemixIdentity.Proof = ByteString.CopyFrom(associationProof.ToProto().ToByteArray());
            serializedIdemixIdentity.Ou = ByteString.CopyFrom(ou.ToByteArray());
            serializedIdemixIdentity.Role = ByteString.CopyFrom(role.ToByteArray());
            serializedIdemixIdentity.NymY = ByteString.CopyFrom(pseudonym.Y.ToBytes());
            serializedIdemixIdentity.NymX = ByteString.CopyFrom(pseudonym.X.ToBytes());
            SerializedIdentity serializedIdentity = new SerializedIdentity();
            serializedIdentity.IdBytes = ByteString.CopyFrom(serializedIdemixIdentity.ToByteArray());
            serializedIdentity.Mspid = mspId;
            return serializedIdentity;
        }

        public override string ToString()
        {
            return $"IdemixIdentity [ MSP ID: {mspId} Issuer Public Key Hash: [{string.Join(",", ipkHash)}] Pseudonym: {pseudonym.ToRawString()} OU: {Ou} Role mask: {RoleMask} Association Proof: {associationProof.ToProto()} ]";
        }
    }
}