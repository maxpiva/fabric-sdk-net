/*
 Copyright IBM Corp. All Rights Reserved.

 SPDX-License-Identifier: Apache-2.0
*/
/*
package org.hyperledger.fabric.sdk;

import java.lang.ref.WeakReference;

import javax.xml.bind.DatatypeConverter;

import com.google.protobuf.ByteString;
import com.google.protobuf.InvalidProtocolBufferException;
import org.apache.commons.logging.Log;
import org.apache.commons.logging.LogFactory;
import org.hyperledger.fabric.protos.common.Common;
import org.hyperledger.fabric.protos.common.Common.Header;
import org.hyperledger.fabric.protos.ledger.rwset.Rwset.TxReadWriteSet;
import org.hyperledger.fabric.protos.msp.Identities;
import org.hyperledger.fabric.protos.peer.FabricProposal;
import org.hyperledger.fabric.protos.peer.FabricProposal.ChaincodeHeaderExtension;
import org.hyperledger.fabric.protos.peer.FabricProposalResponse;
import org.hyperledger.fabric.sdk.exception.CryptoException;
import org.hyperledger.fabric.sdk.exception.InvalidArgumentException;
import org.hyperledger.fabric.sdk.exception.ProposalException;
import org.hyperledger.fabric.sdk.helper.Config;
import org.hyperledger.fabric.sdk.helper.DiagnosticFileDumper;
import org.hyperledger.fabric.sdk.security.CryptoSuite;
*/

using System;
using System.Linq;
using System.Text;
using Google.Protobuf;
using Hyperledger.Fabric.Protos.Common;
using Hyperledger.Fabric.Protos.Ledger.Rwset;
using Hyperledger.Fabric.Protos.Msp;
using Hyperledger.Fabric.Protos.Peer.FabricProposal;
using Hyperledger.Fabric.Protos.Peer.FabricProposalResponse;
using Hyperledger.Fabric.SDK.Deserializers;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Logging;
using Hyperledger.Fabric.SDK.Security;
using Config = Hyperledger.Fabric.SDK.Helper.Config;

namespace Hyperledger.Fabric.SDK.Responses
{
    public class ProposalResponse : ChaincodeResponse
    {
        private static readonly ILog logger = LogProvider.GetLogger(typeof(ProposalResponse));

        private static readonly bool IS_TRACE_LEVEL = logger.IsTraceEnabled();

        private readonly DiagnosticFileDumper diagnosticFileDumper = IS_TRACE_LEVEL ? Config.Instance.GetDiagnosticFileDumper() : null;

        private ChaincodeID chaincodeID = null;

        private readonly WeakItem<ProposalResponsePayloadDeserializer, Protos.Peer.FabricProposalResponse.ProposalResponse> proposalResponsePayload;

        public ProposalResponse(string transactionID, string chaincodeID, int status, string message) : base(transactionID, chaincodeID, status, message)
        {
            proposalResponsePayload = new WeakItem<ProposalResponsePayloadDeserializer, Protos.Peer.FabricProposalResponse.ProposalResponse>((pr) => new ProposalResponsePayloadDeserializer(pr.Payload), () => ProtoProposalResponse);
        }

        private ProposalResponsePayloadDeserializer ProposalResponsePayloadDeserializer
        {
            get
            {
                if (IsInvalid)
                {
                    throw new InvalidArgumentException("Proposal response is invalid.");
                }

                return proposalResponsePayload.Reference;
            }
        }

        public ByteString PayloadBytes => ProtoProposalResponse?.Payload;

        public bool IsVerified { get; private set; } = false;

        public Proposal Proposal { get; private set; }

        /**
         * Get response to the proposal returned by the peer.
         *
         * @return peer response.
         */

        public Protos.Peer.FabricProposalResponse.ProposalResponse ProtoProposalResponse { get; set; }

        /**
         * The peer this proposal was created on.
         *
         * @return See {@link Peer}
         */

        public Peer Peer { get; set; } = null;

        //    public ByteString getPayload() {
        //        return proposalResponse.getPayload();
        //    }

        /**
         * Chaincode ID that was executed.
         *
         * @return See {@link ChaincodeID}
         * @throws InvalidArgumentException
         */

        public ChaincodeID ChaincodeID
        {
            get
            {
                try
                {
                    if (chaincodeID == null)
                    {
                        Header header = Header.Parser.ParseFrom(Proposal.Header);
                        ChannelHeader channelHeader = ChannelHeader.Parser.ParseFrom(header.ChannelHeader);
                        ChaincodeHeaderExtension chaincodeHeaderExtension = ChaincodeHeaderExtension.Parser.ParseFrom(channelHeader.Extension);
                        chaincodeID = new ChaincodeID(chaincodeHeaderExtension.ChaincodeId);
                    }

                    return chaincodeID;
                }
                catch (Exception e)
                {
                    throw new InvalidArgumentException(e);
                }
            }
        }

        /**
         * ChaincodeActionResponsePayload is the result of the executing chaincode.
         *
         * @return the result of the executing chaincode.
         * @throws InvalidArgumentException
         */

        public byte[] ChaincodeActionResponsePayload
        {
            get
            {
                if (IsInvalid)
                {
                    throw new InvalidArgumentException("Proposal response is invalid.");
                }

                try
                {
                    ProposalResponsePayloadDeserializer proposalResponsePayloadDeserializer = ProposalResponsePayloadDeserializer;
                    ByteString ret = proposalResponsePayloadDeserializer.Extension.ChaincodeAction.Response.Payload;
                    if (null == ret)
                    {
                        return null;
                    }

                    return ret.ToByteArray();
                }
                catch (InvalidArgumentException e)
                {
                    throw e;
                }
                catch (Exception e)
                {
                    throw new InvalidArgumentException(e);
                }
            }
        }


        /**
         * getChaincodeActionResponseStatus returns the what chaincode executions set as the return status.
         *
         * @return status code.
         * @throws InvalidArgumentException
         */

        public int ChaincodeActionResponseStatus
        {
            get
            {
                if (IsInvalid)
                {
                    throw new InvalidArgumentException("Proposal response is invalid.");
                }

                try
                {
                    ProposalResponsePayloadDeserializer proposalResponsePayloadDeserializer = ProposalResponsePayloadDeserializer;
                    return proposalResponsePayloadDeserializer.Extension.ResponseStatus;
                }
                catch (InvalidArgumentException e)
                {
                    throw e;
                }
                catch (Exception e)
                {
                    throw new InvalidArgumentException(e);
                }
            }
        }


        /**
         * getChaincodeActionResponseReadWriteSetInfo get this proposals read write set.
         *
         * @return The read write set. See {@link TxReadWriteSetInfo}
         * @throws InvalidArgumentException
         */

        public TxReadWriteSetInfo ChaincodeActionResponseReadWriteSetInfo
        {
            get
            {
                if (IsInvalid)
                {
                    throw new InvalidArgumentException("Proposal response is invalid.");
                }

                try
                {
                    ProposalResponsePayloadDeserializer proposalResponsePayloadDeserializer = ProposalResponsePayloadDeserializer;

                    TxReadWriteSet txReadWriteSet = proposalResponsePayloadDeserializer.Extension.Results;

                    if (txReadWriteSet == null)
                    {
                        return null;
                    }

                    return new TxReadWriteSetInfo(txReadWriteSet);
                }
                catch (Exception e)
                {
                    throw new InvalidArgumentException(e);
                }
            }
        }

        /*
         * Verifies that a Proposal response is properly signed. The payload is the
         * concatenation of the response payload byte string and the endorsement The
         * certificate (public key) is gotten from the Endorsement.Endorser.IdBytes
         * field
         *
         * @param crypto the CryptoPrimitives instance to be used for signing and
         * verification
         *
         * @return true/false depending on result of signature verification
         */
        public bool Verify(ICryptoSuite crypto)
        {
            if (IsVerified)
            {
                // check if this proposalResponse was already verified   by client code
                return IsVerified;
            }

            if (IsInvalid)
            {
                IsVerified = false;
            }

            Endorsement endorsement = ProtoProposalResponse.Endorsement;
            ByteString sig = endorsement.Signature;

            try
            {
                SerializedIdentity endorser = SerializedIdentity.Parser.ParseFrom(endorsement.Endorser);
                ByteString plainText = ByteString.CopyFrom(ProtoProposalResponse.Payload.Concat(endorsement.Endorser).ToArray());

                if (Config.Instance.ExtraLogLevel(10))
                {
                    if (null != diagnosticFileDumper)
                    {
                        StringBuilder sb = new StringBuilder(10000);
                        sb.AppendLine("payload TransactionBuilderbytes in hex: " + ProtoProposalResponse.Payload.ToByteArray().ToHexString());
                        sb.AppendLine("endorser bytes in hex: " + endorsement.ToByteArray().ToHexString());
                        sb.Append("plainText bytes in hex: " + plainText.ToByteArray().ToHexString());

                        logger.Trace("payload TransactionBuilderbytes:  " + diagnosticFileDumper.CreateDiagnosticFile(sb.ToString()));
                    }
                }

                IsVerified = crypto.Verify(endorser.IdBytes.ToByteArray(), Config.Instance.GetSignatureAlgorithm(), sig.ToByteArray(), plainText.ToByteArray());
            }
            catch (Exception e)
            {
                logger.ErrorException("verify: Cannot retrieve peer identity from ProposalResponse. Error is: " + e.Message, e);
                IsVerified = false;
            }

            return IsVerified;
        } // verify

        public void SetProposal(SignedProposal signedProposal)
        {
            try
            {
                Proposal = Proposal.Parser.ParseFrom(signedProposal.ProposalBytes);
            }
            catch (InvalidProtocolBufferException e)
            {
                throw new ProposalException("Proposal exception", e);
            }
        }
    }
}