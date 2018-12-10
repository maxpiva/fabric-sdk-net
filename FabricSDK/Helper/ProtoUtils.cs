/*
 *  Copyright 2016 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
 *
 *  Licensed under the Apache License, Version 2.0 (the "License");
 *  you may not use this file except in compliance with the License.
 *  You may obtain a copy of the License at
 *    http://www.apache.org/licenses/LICENSE-2.0
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed under the License is distributed on an "AS IS" BASIS,
 *  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *  See the License for the specific language governing permissions and
 *  limitations under the License.
 */


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Hyperledger.Fabric.Protos.Common;
using Hyperledger.Fabric.Protos.Msp;
using Hyperledger.Fabric.Protos.Orderer;
using Hyperledger.Fabric.Protos.Peer;
using Hyperledger.Fabric.Protos.Peer.FabricProposal;
using Hyperledger.Fabric.SDK.Builders;
using Hyperledger.Fabric.SDK.Identity;
using Hyperledger.Fabric.SDK.Logging;
using Hyperledger.Fabric.SDK.Security;

namespace Hyperledger.Fabric.SDK.Helper
{
/**
 * Internal use only, not a public API.
 */
    public class ProtoUtils
    {

        private static readonly ILog logger = LogProvider.GetLogger(typeof(ProtoUtils));
        private static readonly bool isDebugLevel = logger.IsDebugEnabled();
        public static ICryptoSuite suite;

        /**
         * Private constructor to prevent instantiation.
         */


        // static ICryptoSuite suite = null;

        /*
         * createChannelHeader create chainHeader
         *
         * @param type                     header type. See {@link ChannelHeader.Builder#setType}.
         * @param txID                     transaction ID. See {@link ChannelHeader.Builder#setTxId}.
         * @param channelID                channel ID. See {@link ChannelHeader.Builder#setChannelId}.
         * @param epoch                    the epoch in which this header was generated. See {@link ChannelHeader.Builder#setEpoch}.
         * @param timeStamp                local time when the message was created. See {@link ChannelHeader.Builder#setTimestamp}.
         * @param chaincodeHeaderExtension extension to attach dependent on the header type. See {@link ChannelHeader.Builder#setExtension}.
         * @param tlsCertHash
         * @return a new chain header.
         */
        public static ChannelHeader CreateChannelHeader(HeaderType type, string txID, string channelID, long epoch,
                                                        Timestamp timeStamp, ChaincodeHeaderExtension chaincodeHeaderExtension,
                                                        byte[] tlsCertHash) {

            if (isDebugLevel)
            {
                string tlschs = string.Empty;
                if (tlsCertHash != null)
                    tlschs = tlsCertHash.ToHexString();
                logger.Debug($"ChannelHeader: type: {type}, version: 1, Txid: {txID}, channelId: {channelID}, epoch {epoch}, clientTLSCertificate digest: {tlschs}");
            }

            ChannelHeader ret=new ChannelHeader {Type = (int)type, Version = 1, TxId = txID, ChannelId = channelID, Timestamp = timeStamp, Epoch = (ulong)epoch};
            if (null != chaincodeHeaderExtension)
                ret.Extension = chaincodeHeaderExtension.ToByteString();
            if (tlsCertHash != null)
                ret.TlsCertHash = ByteString.CopyFrom(tlsCertHash);
            return ret;

        }

        public static ChaincodeDeploymentSpec CreateDeploymentSpec(ChaincodeSpec.Types.Type ccType, string name, string chaincodePath,
                                                                   string chaincodeVersion, List<string> args,
                                                                   byte[] codePackage) {

            Protos.Peer.ChaincodeID chaincodeID = new Protos.Peer.ChaincodeID
            {
                Name=name,
                Version = chaincodeVersion
            };
            if (chaincodePath != null)
                chaincodeID.Path=chaincodePath;
            if (args==null)
                args=new List<string>();
            // build chaincodeInput
            List<ByteString> argList = args.Select(ByteString.CopyFromUtf8).ToList();
            ChaincodeInput chaincodeInput = new ChaincodeInput();
            chaincodeInput.Args.AddRange(argList);
                
            // Construct the ChaincodeSpec
            ChaincodeSpec chaincodeSpec = new ChaincodeSpec { ChaincodeId = chaincodeID, Input = chaincodeInput, Type=ccType};

            if (isDebugLevel) {
                StringBuilder sb = new StringBuilder(1000);
                sb.Append("ChaincodeDeploymentSpec chaincode cctype: ")
                        .Append(ccType.ToString())
                        .Append(", name:")
                        .Append(chaincodeID.Name)
                        .Append(", path: ")
                        .Append(chaincodeID.Path)
                        .Append(", version: ")
                        .Append(chaincodeID.Version);

                string sep = "";
                sb.Append(" args(");

                foreach (ByteString x in argList) {
                    sb.Append(sep).Append("\"").Append(x.ToStringUtf8().LogString()).Append("\"");
                    sep = ", ";

                }
                sb.Append(")");
                logger.Debug(sb.ToString());

            }

            ChaincodeDeploymentSpec spec=new ChaincodeDeploymentSpec { ChaincodeSpec = chaincodeSpec,ExecEnv = ChaincodeDeploymentSpec.Types.ExecutionEnvironment.Docker};
            if (codePackage != null)
                spec.CodePackage = ByteString.CopyFrom(codePackage);
            return spec;

        }

        public static ByteString GetSignatureHeaderAsByteString(TransactionContext transactionContext) {

            return GetSignatureHeaderAsByteString(transactionContext.User, transactionContext);
        }

        public static ByteString GetSignatureHeaderAsByteString(IUser user, TransactionContext transactionContext)
        {

            SerializedIdentity identity = transactionContext.Identity;


            if (isDebugLevel)
            {
                IEnrollment enrollment = user.Enrollment;
                string cert = enrollment.Cert;
                string hexcert = cert == null ? "null" : cert.ToBytes().ToHexString();
                logger.Debug($" User: {user.Name} Certificate: {hexcert}");

                if (enrollment is X509Enrollment)
                {


                    cert = transactionContext.CryptoPrimitives.Hash(Certificate.Create(cert).ExtractDER()).ToHexString();

                    // logger.debug(format(" User: %s Certificate:\n%s", user.getName(), cert));

                    logger.Debug($"SignatureHeader: nonce: {transactionContext.Nonce.ToHexString()}, User:{user.Name}, MSPID: {user.MspId}, idBytes: {cert}");

                }
            }

            return (new SignatureHeader {Creator = identity.ToByteString(), Nonce = transactionContext.Nonce}).ToByteString();
        }

        public static SerializedIdentity CreateSerializedIdentity(IUser user)
        {
            return new SerializedIdentity {IdBytes = ByteString.CopyFromUtf8(user.Enrollment.Cert), Mspid = user.MspId};
        }

        public static Timestamp GetCurrentFabricTimestamp()
        {
            return Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow);
        }


        public static Envelope CreateSeekInfoEnvelope(TransactionContext transactionContext, SeekInfo seekInfo, byte[] tlsCertHash)
        {

            ChannelHeader seekInfoHeader = CreateChannelHeader(HeaderType.DeliverSeekInfo,
                    transactionContext.TxID, transactionContext.ChannelID, transactionContext.Epoch,
                    transactionContext.FabricTimestamp, null, tlsCertHash);

            SignatureHeader signatureHeader = new SignatureHeader { Creator = transactionContext.Identity.ToByteString(), Nonce = transactionContext.Nonce };
            Header seekHeader = new Header {SignatureHeader = signatureHeader.ToByteString(), ChannelHeader = seekInfoHeader.ToByteString()};
            Payload seekPayload = new Payload { Header = seekHeader, Data=seekInfo.ToByteString()};
            return new Envelope {Signature = transactionContext.SignByteStrings(seekPayload.ToByteString()), Payload = seekPayload.ToByteString()};

        }

        public static Envelope CreateSeekInfoEnvelope(TransactionContext transactionContext, SeekPosition startPosition,
                                                      SeekPosition stopPosition, SeekInfo.Types.SeekBehavior seekBehavior, byte[] tlsCertHash)
        {
            return CreateSeekInfoEnvelope(transactionContext, new SeekInfo {Start = startPosition, Behavior = seekBehavior, Stop = stopPosition},tlsCertHash);
        }

        // not an api

        public static bool ComputeUpdate(string channelId, Protos.Common.Config original, Protos.Common.Config update, ConfigUpdate cupd)
        {

            ConfigGroup readSet=new ConfigGroup();
            ConfigGroup writeSet=new ConfigGroup();
            if (ComputeGroupUpdate(original.ChannelGroup, update.ChannelGroup, readSet, writeSet))
            {
                cupd.ReadSet = readSet;
                cupd.WriteSet = writeSet;
                cupd.ChannelId = channelId;
                return true;
            }
            return false;

        }

        private static bool ComputeGroupUpdate(ConfigGroup original, ConfigGroup updated, ConfigGroup readSet, ConfigGroup writeSet)
        {

            Dictionary<string, ConfigPolicy> readSetPolicies = new Dictionary<string, ConfigPolicy>();
            Dictionary<string, ConfigPolicy> writeSetPolicies = new Dictionary<string, ConfigPolicy>();
            Dictionary<string, ConfigPolicy> sameSetPolicies = new Dictionary<string, ConfigPolicy>();

            bool policiesMembersUpdated = ComputePoliciesMapUpdate(original.Policies, updated.Policies, writeSetPolicies, sameSetPolicies);

            Dictionary<string, ConfigValue> readSetValues = new Dictionary<string, ConfigValue>();
            Dictionary<string, ConfigValue> writeSetValues = new Dictionary<string, ConfigValue>();
            Dictionary<string, ConfigValue> sameSetValues =  new Dictionary<string, ConfigValue>();

            bool valuesMembersUpdated = ComputeValuesMapUpdate(original.Values, updated.Values, writeSetValues, sameSetValues);

            Dictionary<string, ConfigGroup> readSetGroups = new Dictionary<string, ConfigGroup>();
            Dictionary<string, ConfigGroup> writeSetGroups = new Dictionary<string, ConfigGroup>();
            Dictionary<string, ConfigGroup> sameSetGroups = new Dictionary<string, ConfigGroup>();

            bool groupsMembersUpdated = ComputeGroupsMapUpdate(original.Groups, updated.Groups, readSetGroups, writeSetGroups, sameSetGroups);

            if (!policiesMembersUpdated && !valuesMembersUpdated && !groupsMembersUpdated && original.ModPolicy.Equals(updated.ModPolicy))
            {
                // nothing changed.

                if (writeSetValues.Count==0 && writeSetPolicies.Count==0 && writeSetGroups.Count==0 && readSetGroups.Count==0)
                {
                    readSet.Version=original.Version;
                    writeSet.Version = original.Version;                    
                    return false;
                }
                readSet.Version = original.Version;
                readSet.Groups.Add(readSetGroups);
                writeSet.Version = original.Version;
                writeSet.Policies.Add(writeSetPolicies);
                writeSet.Values.Add(writeSetValues);
                writeSet.Groups.Add(writeSetGroups);
                return true;
            }
            foreach (string name in sameSetPolicies.Keys)
            {
                readSetPolicies[name] = sameSetPolicies[name];
                writeSetPolicies[name] = sameSetPolicies[name];
            }
            foreach (string name in sameSetValues.Keys)
            {
                readSetValues[name] = sameSetValues[name];
                writeSetValues[name] = sameSetValues[name];
            }
            foreach(string name in sameSetGroups.Keys)
            {
                readSetGroups[name] = sameSetGroups[name];
                writeSetGroups[name] = sameSetGroups[name];
            }

            readSet.Version = original.Version;
            readSet.Policies.Add(readSetPolicies);
            readSet.Values.Add(readSetValues);
            readSet.Groups.Add(readSetGroups);
            writeSet.Version = original.Version + 1;
            writeSet.Policies.Add(writeSetPolicies);
            writeSet.Values.Add(writeSetValues);
            writeSet.ModPolicy = updated.ModPolicy;
            writeSet.Groups.Add(writeSetGroups);
            return true;
        }

        public static bool ComputeGroupsMapUpdate(IDictionary<string, ConfigGroup> original, IDictionary<string, ConfigGroup> updated, Dictionary<string, ConfigGroup> readSet, 
            Dictionary<string, ConfigGroup> writeSet, Dictionary<string, ConfigGroup> sameSet)
        {
            bool updatedMembers = false;
            foreach (string groupName in original.Keys)
            {
                ConfigGroup originalGroup = original[groupName];
                
                if (!updated.ContainsKey(groupName) || null == updated[groupName])
                {
                    updatedMembers = true; //missing from updated ie deleted.

                }
                else
                {
                    ConfigGroup updatedGroup = updated[groupName];

                    ConfigGroup readSetB = new ConfigGroup();
                    ConfigGroup writeSetB = new ConfigGroup();

                    if (!ComputeGroupUpdate(originalGroup, updatedGroup, readSetB, writeSetB))
                    {
                        sameSet[groupName] = readSetB;

                    }
                    else
                    {
                        readSet[groupName] = readSetB;
                        writeSet[groupName] = writeSetB;
                    }

                }

            }

            foreach (string groupName in updated.Keys)
            {
                ConfigGroup updatedConfigGroup = updated[groupName];
                
                if (!original.ContainsKey(groupName) || null == original[groupName])
                {
                    updatedMembers = true;
                    // final Configtx.ConfigGroup originalConfigGroup = original.get(groupName);
                    ConfigGroup readSetB = new ConfigGroup();
                    ConfigGroup writeSetB = new ConfigGroup();
                    ComputeGroupUpdate(new ConfigGroup(), updatedConfigGroup, readSetB, writeSetB);
                    ConfigGroup cfg = new ConfigGroup {Version = 0, ModPolicy = updatedConfigGroup.ModPolicy};
                    cfg.Policies.Add(writeSetB.Policies);
                    cfg.Values.Add(writeSetB.Values);
                    cfg.Groups.Add(writeSetB.Groups);
                    writeSet[groupName]=cfg;
                }

            }

            return updatedMembers;
        }

        private static bool ComputeValuesMapUpdate(IDictionary<string, ConfigValue> original, IDictionary<string, ConfigValue> updated,
            Dictionary<string, ConfigValue> writeSet, Dictionary<string, ConfigValue> sameSet)
        {

            bool updatedMembers = false;

            foreach (string valueName in original.Keys)
            {
                ConfigValue originalValue = original[valueName];
                if (!updated.ContainsKey(valueName) || null == updated[valueName])
                {
                    updatedMembers = true; //missing from updated ie deleted.

                }
                else
                { // is in both...

                    ConfigValue updatedValue = updated[valueName];
                    if (originalValue.ModPolicy.Equals(updatedValue.ModPolicy) &&
                            originalValue.Value.Equals(updatedValue.Value))
                    { //same value

                        sameSet[valueName]=new ConfigValue { Version = originalValue.Version };

                    }
                    else
                    { // new value put in writeset.

                        writeSet[valueName]=new ConfigValue { Version = originalValue.Version+1, ModPolicy = updatedValue.ModPolicy, Value = updatedValue.Value};
                    }

                }

            }

            foreach (string valueName in updated.Keys)
            {
                ConfigValue updatedValue = updated[valueName];
                
                if (!original.ContainsKey(valueName) || null == original[valueName])
                {

                    updatedMembers = true;
                    writeSet[valueName]=new ConfigValue { Version = 0, ModPolicy = updatedValue.ModPolicy, Value=updatedValue.Value};

                }
            }

            return updatedMembers;

        }

        private static bool ComputePoliciesMapUpdate(IDictionary<string, ConfigPolicy> original, IDictionary<string, ConfigPolicy> updated,
            Dictionary<string, ConfigPolicy> writeSet, Dictionary<string, ConfigPolicy> sameSet)
        {

            bool updatedMembers = false;

            foreach (string policyName in original.Keys)
            {
                ConfigPolicy originalPolicy = original[policyName];
                if (!updated.ContainsKey(policyName) || null == updated[policyName])
                {
                    updatedMembers = true; //missing from updated ie deleted.

                }
                else
                { // is in both...

                    ConfigPolicy updatedPolicy = updated[policyName];
                    if (originalPolicy.ModPolicy.Equals(updatedPolicy.ModPolicy) &&
                            originalPolicy.ToByteString().Equals(updatedPolicy.ToByteString()))
                    { //same policy

                        sameSet[policyName]=new ConfigPolicy { Version = originalPolicy.Version};
                    }
                    else
                    { // new policy put in writeset.

                        writeSet[policyName] = new ConfigPolicy { Version = originalPolicy.Version+1,ModPolicy = updatedPolicy.ModPolicy, Policy = new Policy()};
                    }

                }

            }

            foreach (string policyName in updated.Keys)
            {
                ConfigPolicy updatedPolicy = updated[policyName];
                
                if (!original.ContainsKey(policyName) || null == original[policyName])
                {

                    updatedMembers = true;
                    writeSet[policyName]=new ConfigPolicy { Version = 0, ModPolicy = updatedPolicy.ModPolicy, Policy = new Policy()};
                }
            }

            return updatedMembers;
        }

    }
}
