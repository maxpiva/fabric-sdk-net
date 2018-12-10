/*
 Copyright IBM Corp. All Rights Reserved.

 SPDX-License-Identifier: Apache-2.0
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
using Hyperledger.Fabric.SDK.Builders;
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

        private readonly WeakItem<ProposalResponsePayloadDeserializer, Protos.Peer.FabricProposalResponse.ProposalResponse> proposalResponsePayload;

        private ChaincodeID chaincodeID;
        public TransactionContext TransactionContext { get; }

        public ProposalResponse(TransactionContext transactionContext, int status, string message) : base(transactionContext.TxID, transactionContext.ChannelID, status, message)
        {
            TransactionContext = transactionContext;
            proposalResponsePayload = new WeakItem<ProposalResponsePayloadDeserializer, Protos.Peer.FabricProposalResponse.ProposalResponse>((pr) => new ProposalResponsePayloadDeserializer(pr.Payload), () => ProtoProposalResponse);
        }

        private ProposalResponsePayloadDeserializer ProposalResponsePayloadDeserializer
        {
            get
            {
                if (IsInvalid)
                {
                    throw new ArgumentException("Proposal response is invalid.");
                }

                return proposalResponsePayload.Reference;
            }
        }

        public ByteString PayloadBytes => ProtoProposalResponse?.Payload;

        public bool IsVerified { get; private set; }

        public bool HasBeenVerified { get; private set; }

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


        /**
         * Chaincode ID that was executed.
         *
         * @return See {@link ChaincodeID}
         * @throws InvalidIllegalArgumentException
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
                    throw new ArgumentException(e.Message, e);
                }
            }
        }

        /**
         * ChaincodeActionResponsePayload is the result of the executing chaincode.
         *
         * @return the result of the executing chaincode.
         * @throws InvalidIllegalArgumentException
         */

        public byte[] ChaincodeActionResponsePayload
        {
            get
            {
                if (IsInvalid)
                {
                    throw new ArgumentException("Proposal response is invalid.");
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
                catch (ArgumentException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    throw new ArgumentException(e.Message, e);
                }
            }
        }


        /**
         * getChaincodeActionResponseStatus returns the what chaincode executions set as the return status.
         *
         * @return status code.
         * @throws InvalidIllegalArgumentException
         */

        public int ChaincodeActionResponseStatus
        {
            get
            {
                if (statusReturnCode != -1)
                    return statusReturnCode;

                try
                {
                    ProposalResponsePayloadDeserializer proposalResponsePayloadDeserializer = ProposalResponsePayloadDeserializer;
                    statusReturnCode=proposalResponsePayloadDeserializer.Extension.ResponseStatus;
                    return statusReturnCode;
                }
                catch (ArgumentException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    throw new ArgumentException(e.Message, e);
                }
            }
        }


        /**
         * getChaincodeActionResponseReadWriteSetInfo get this proposals read write set.
         *
         * @return The read write set. See {@link TxReadWriteSetInfo}
         * @throws InvalidIllegalArgumentException
         */

        public TxReadWriteSetInfo ChaincodeActionResponseReadWriteSetInfo
        {
            get
            {
                if (IsInvalid)
                {
                    throw new ArgumentException("Proposal response is invalid.");
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
                    throw new ArgumentException(e.Message, e);
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
            logger.Trace($"{Peer} verifying transaction: {TransactionID} endorsement.");
            if (HasBeenVerified)
            {
                // check if this proposalResponse was already verified   by client code
                logger.Trace($"{Peer} transaction: {TransactionID} was already verified returned {IsVerified}");
                return IsVerified;
            }

            try
            {
                if (IsInvalid)
                {
                    IsVerified = false;
                    logger.Debug($"{Peer} for transaction {TransactionID} returned invalid. Setting verify to false");
                    return false;
                }

                Endorsement endorsement = ProtoProposalResponse.Endorsement;
                ByteString sig = endorsement.Signature;
                byte[] endorserCertifcate = null;
                byte[] signature = null;
                byte[] data = null;

                try
                {
                    SerializedIdentity endorser = SerializedIdentity.Parser.ParseFrom(endorsement.Endorser);
                    ByteString plainText = ByteString.CopyFrom(ProtoProposalResponse.Payload.Concat(endorsement.Endorser).ToArray());

                    if (Config.Instance.ExtraLogLevel(10))
                    {
                        if (null != diagnosticFileDumper)
                        {
                            StringBuilder sb = new StringBuilder(10000);
                            sb.AppendLine("payload TransactionBuilder bytes in hex: " + ProtoProposalResponse.Payload.ToByteArray().ToHexString());
                            sb.AppendLine("endorser bytes in hex: " + endorsement.ToByteArray().ToHexString());
                            sb.Append("plainText bytes in hex: " + plainText.ToByteArray().ToHexString());
                            logger.Trace("payload TransactionBuilder bytes:  " + diagnosticFileDumper.CreateDiagnosticFile(sb.ToString()));
                        }
                    }

                    if (sig == null || sig.IsEmpty)
                    {
                        // we shouldn't get here ...
                        logger.Warn($"{Peer} {TransactionID} returned signature is empty verify set to false.");
                        IsVerified = false;
                    }
                    else
                    {
                        endorserCertifcate = endorser.IdBytes.ToByteArray();
                        signature = sig.ToByteArray();
                        data = plainText.ToByteArray();

                        IsVerified = crypto.Verify(endorserCertifcate, Config.Instance.GetSignatureAlgorithm(), signature, data);
                        if (!IsVerified)
                        {
                            logger.Warn($"{Peer} transaction: {TransactionID} verify: Failed to verify. Endorsers certificate: {endorserCertifcate.ToHexString()}, signature: {signature.ToHexString()}, signing algorithm: {Config.Instance.GetSignatureAlgorithm()}, signed data: {data.ToHexString()}.");
                        }
                    }
                }
                catch (Exception e)
                {
                    logger.ErrorException($"{Peer} transaction: {TransactionID} verify: Failed to verify. Endorsers certificate: {endorserCertifcate.ToHexString()}, signature: {signature.ToHexString()}, signing algorithm: {Config.Instance.GetSignatureAlgorithm()}, signed data: {data.ToHexString()}.", e);
                    logger.Error($"{Peer} transaction: {TransactionID} verify: Cannot retrieve peer identity from ProposalResponse. Error is: {e.Message}");


                    logger.ErrorException("verify: Cannot retrieve peer identity from ProposalResponse. Error is: " + e.Message, e);
                    IsVerified = false;
                }

                logger.Debug($"{Peer} finished verify for transaction {TransactionID} returning {IsVerified}");
                return IsVerified;
            }
            finally
            {
                HasBeenVerified = true;
            }
        } // verify

        public void SetProposal(SignedProposal signedProposal)
        {
            try
            {
                Proposal = Proposal.Parser.ParseFrom(signedProposal.ProposalBytes);
            }
            catch (InvalidProtocolBufferException e)
            {
                throw new ProposalException($"{Peer} transaction: {TransactionID} Proposal exception", e);
            }
        }
    }
}