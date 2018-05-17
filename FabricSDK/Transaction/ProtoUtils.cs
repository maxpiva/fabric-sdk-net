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
using Hyperledger.Fabric.SDK.Logging;
using Hyperledger.Fabric.SDK.NetExtensions;
using Hyperledger.Fabric.SDK.Protos.Common;
using Hyperledger.Fabric.SDK.Protos.Msp;
using Hyperledger.Fabric.SDK.Protos.Orderer;
using Hyperledger.Fabric.SDK.Protos.Peer;
using Hyperledger.Fabric.SDK.Protos.Peer.FabricProposal;

using Hyperledger.Fabric.SDK.Security;
using ProtoBuf;

namespace Hyperledger.Fabric.SDK.Transaction
{/*
    package org.hyperledger.fabric.sdk.transaction;

import java.time.Instant;
import java.util.ArrayList;
import java.util.Date;
import java.util.List;

import javax.xml.bind.DatatypeConverter;

import com.google.protobuf.ByteString;
import com.google.protobuf.Timestamp;
import com.google.protobuf.util.Timestamps;
import org.apache.commons.logging.Log;
import org.apache.commons.logging.LogFactory;
import org.hyperledger.fabric.protos.common.Common;
import org.hyperledger.fabric.protos.common.Common.ChannelHeader;
import org.hyperledger.fabric.protos.common.Common.Envelope;
import org.hyperledger.fabric.protos.common.Common.HeaderType;
import org.hyperledger.fabric.protos.common.Common.Payload;
import org.hyperledger.fabric.protos.common.Common.SignatureHeader;
import org.hyperledger.fabric.protos.msp.Identities;
import org.hyperledger.fabric.protos.orderer.Ab.SeekInfo;
import org.hyperledger.fabric.protos.orderer.Ab.SeekInfo.SeekBehavior;
import org.hyperledger.fabric.protos.orderer.Ab.SeekPosition;
import org.hyperledger.fabric.protos.peer.Chaincode.ChaincodeDeploymentSpec;
import org.hyperledger.fabric.protos.peer.Chaincode.ChaincodeID;
import org.hyperledger.fabric.protos.peer.Chaincode.ChaincodeInput;
import org.hyperledger.fabric.protos.peer.Chaincode.ChaincodeSpec;
import org.hyperledger.fabric.protos.peer.Chaincode.ChaincodeSpec.Type;
import org.hyperledger.fabric.protos.peer.FabricProposal.ChaincodeHeaderExtension;
import org.hyperledger.fabric.sdk.User;
import org.hyperledger.fabric.sdk.exception.CryptoException;
import org.hyperledger.fabric.sdk.security.CryptoPrimitives;
import org.hyperledger.fabric.sdk.security.CryptoSuite;

import static java.lang.String.format;
import static java.nio.charset.StandardCharsets.UTF_8;
import static org.hyperledger.fabric.sdk.helper.Utils.logString;
import static org.hyperledger.fabric.sdk.helper.Utils.toHexString;
*/
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
        public static ChannelHeader CreateChannelHeader(HeaderType type, String txID, String channelID, long epoch,
                                                        DateTime? timeStamp, ChaincodeHeaderExtension chaincodeHeaderExtension,
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
                ret.Extension = chaincodeHeaderExtension.SerializeProtoBuf();
            if (tlsCertHash != null)
                ret.TlsCertHash = tlsCertHash.CloneBytes();
            return ret;

        }

        public static ChaincodeDeploymentSpec CreateDeploymentSpec(ChaincodeSpec.Type ccType, String name, String chaincodePath,
                                                                   String chaincodeVersion, List<String> args,
                                                                   byte[] codePackage) {

            Protos.Peer.ChaincodeID chaincodeID = new Protos.Peer.ChaincodeID
            {
                Name=name,
                Version = chaincodeVersion
            };
            if (chaincodePath != null)
                chaincodeID.Path=chaincodePath;

            // build chaincodeInput
            List<byte[]> argList = args.Select(a => Encoding.UTF8.GetBytes(a)).ToList();
            ChaincodeInput chaincodeInput = new ChaincodeInput();
            chaincodeInput.Args.AddRange(argList);
                
            // Construct the ChaincodeSpec
            ChaincodeSpec chaincodeSpec = new ChaincodeSpec { ChaincodeId = chaincodeID, Input = chaincodeInput, type=ccType};

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

                foreach (byte[] x in argList) {
                    sb.Append(sep).Append("\"").Append(Encoding.UTF8.GetString(x).LogString()).Append("\"");
                    sep = ", ";

                }
                sb.Append(")");
                logger.Debug(sb.ToString());

            }

            ChaincodeDeploymentSpec spec=new ChaincodeDeploymentSpec { ChaincodeSpec = chaincodeSpec,ExecEnv = ChaincodeDeploymentSpec.ExecutionEnvironment.Docker};
            if (codePackage != null)
                spec.CodePackage = codePackage.CloneBytes();
            return spec;

        }

        public static byte[] GetSignatureHeaderAsByteString(TransactionContext transactionContext) {

            return GetSignatureHeaderAsByteString(transactionContext.User, transactionContext);
        }

        public static byte[] GetSignatureHeaderAsByteString(IUser user, TransactionContext transactionContext)
        {

            SerializedIdentity identity = CreateSerializedIdentity(user);

            if (isDebugLevel)
            {
                string cert = user.Enrollment.Cert;
                // logger.debug(format(" User: %s Certificate:\n%s", user.getName(), cert));

                if (null == suite) {

                    try
                    {
                        suite = HLSDKJCryptoSuiteFactory.Instance.GetCryptoSuite();
                    } catch (Exception e) {
                        //best try.
                    }

                }
                if (null != suite && suite is CryptoPrimitives) {

                    CryptoPrimitives cp = (CryptoPrimitives) suite;
                    byte[] der = cp.CertificateToDER(cert);
                    if (null != der && der.Length > 0)
                    {
                        cert = suite.Hash(der).ToHexString();
                    }

                }

                logger.Debug($"SignatureHeader: nonce: {transactionContext.Nonce.ToHexString()}, User:{user.Name}, MSPID: {user.MspId}, idBytes: {cert}");

            }

            ;
            return (new SignatureHeader {Creator = identity.SerializeProtoBuf(), Nonce = transactionContext.Nonce}).SerializeProtoBuf();
        }

        public static SerializedIdentity CreateSerializedIdentity(IUser user)
        {
            return new SerializedIdentity {IdBytes = Encoding.UTF8.GetBytes(user.Enrollment.Cert), Mspid = user.MspId};
        }
        /*
        public static ProtoBuf.Protos.Rwset.TITimestamp getCurrentFabricTimestamp()
        {
            Instant time = Instant.now();
            return Timestamp.newBuilder().setSeconds(time.getEpochSecond())
                    .setNanos(time.getNano()).build();
        }

        public static Date getDateFromTimestamp(Timestamp timestamp) {
            return new Date(Timestamps.toMillis(timestamp));
        }

        static Timestamp getTimestampFromDate(Date date) {

            long millis = date.getTime();
            return Timestamp.newBuilder().setSeconds(millis / 1000)
                    .setNanos((int) ((millis % 1000) * 1000000)).build();
        }
        */
        public static Envelope CreateSeekInfoEnvelope(TransactionContext transactionContext, SeekInfo seekInfo, byte[] tlsCertHash)
        {

            ChannelHeader seekInfoHeader = CreateChannelHeader(HeaderType.DeliverSeekInfo,
                    transactionContext.TxID, transactionContext.ChannelID, transactionContext.Epoch,
                    transactionContext.FabricTimestamp, null, tlsCertHash);

            SignatureHeader signatureHeader = new SignatureHeader { Creator = transactionContext.Identity.SerializeProtoBuf(), Nonce = transactionContext.Nonce };
            Header seekHeader = new Header {SignatureHeader = signatureHeader.SerializeProtoBuf(), ChannelHeader = seekInfoHeader.SerializeProtoBuf()};
            Payload seekPayload = new Payload { Header = seekHeader, Data=seekInfo.SerializeProtoBuf()};
            return new Envelope {Signature = transactionContext.SignByteStrings(seekPayload.SerializeProtoBuf()), Payload = seekPayload.SerializeProtoBuf()};

        }

        public static Envelope CreateSeekInfoEnvelope(TransactionContext transactionContext, SeekPosition startPosition,
                                                      SeekPosition stopPosition, SeekInfo.SeekBehavior seekBehavior, byte[] tlsCertHash)
        {
            return CreateSeekInfoEnvelope(transactionContext, new SeekInfo {Start = startPosition, Behavior = seekBehavior, Stop = stopPosition},tlsCertHash);
        }
    }
}
