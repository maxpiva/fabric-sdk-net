/*
 *
 *  Copyright 2016,2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
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
using System.IO;
using System.Linq;
using System.Text;
using Google.Protobuf;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Logging;
using Hyperledger.Fabric.SDK.Responses;
using Hyperledger.Fabric.SDK.Security;
using Org.BouncyCastle.Asn1;

namespace Hyperledger.Fabric.SDK
{
    public class SDKUtils
    {
        private static readonly ILog logger = LogProvider.GetLogger(typeof(SDKUtils));

        private static readonly bool IS_DEBUG_LEVEL = logger.IsDebugEnabled();

        public static ICryptoSuite suite = null;

        private SDKUtils()
        {
        }

        /**
         * used asn1 and get hash
         *
         * @param blockNumber
         * @param previousHash
         * @param dataHash
         * @return byte[]
         * @throws IOException
         * @throws InvalidIllegalArgumentException
         */
        public static byte[] CalculateBlockHash(HFClient client, long blockNumber, byte[] previousHash, byte[] dataHash)
        {
            if (previousHash == null)
            {
                throw new ArgumentException("previousHash parameter is null.");
            }

            if (dataHash == null)
            {
                throw new ArgumentException("dataHash parameter is null.");
            }

            if (null == client)
            {
                throw new ArgumentException("client parameter is null.");
            }

            ICryptoSuite cryptoSuite = client.CryptoSuite;
            if (null == cryptoSuite)
            {
                throw new ArgumentException("Client crypto suite has not  been set.");
            }

            MemoryStream s = new MemoryStream();
            DerSequenceGenerator seq = new DerSequenceGenerator(s);
            seq.AddObject(new DerInteger((int) blockNumber));
            seq.AddObject(new DerOctetString(previousHash));
            seq.AddObject(new DerOctetString(dataHash));
            seq.Close();
            s.Flush();
            return cryptoSuite.Hash(s.ToArray());
        }

        /**
         * Check that the proposals all have consistent read write sets
         *
         * @param proposalResponses
         * @return A Collection of sets where each set has consistent proposals.
         * @throws InvalidIllegalArgumentException
         */

        public static List<HashSet<ProposalResponse>> GetProposalConsistencySets(List<ProposalResponse> proposalResponses)
        {
            return GetProposalConsistencySets(proposalResponses, new HashSet<ProposalResponse>());
        }

        /**
         * Check that the proposals all have consistent read write sets
         *
         * @param proposalResponses
         * @param invalid           proposals that were found to be invalid.
         * @return A Collection of sets where each set has consistent proposals.
         * @throws InvalidIllegalArgumentException
         */

        public static List<HashSet<ProposalResponse>> GetProposalConsistencySets(List<ProposalResponse> proposalResponses, HashSet<ProposalResponse> invalid)
        {
            if (proposalResponses == null)
            {
                throw new ArgumentException("proposalResponses collection is null");
            }

            if (proposalResponses.Count == 0)
            {
                throw new ArgumentException("proposalResponses collection is empty");
            }

            if (null == invalid)
            {
                throw new ArgumentException("invalid set is null.");
            }

            Dictionary<ByteString, HashSet<ProposalResponse>> ret = new Dictionary<ByteString, HashSet<ProposalResponse>>();

            foreach (ProposalResponse proposalResponse in proposalResponses)
            {
                if (proposalResponse.IsInvalid)
                {
                    invalid.Add(proposalResponse);
                }
                else
                {
                    // payload bytes is what's being signed over so it must be consistent.
                    ByteString payloadBytes = proposalResponse.PayloadBytes;

                    if (payloadBytes == null)
                    {
                        throw new ArgumentException($"proposalResponse.getPayloadBytes() was null from peer: {proposalResponse.Peer}.");
                    }
                    else if (payloadBytes.Length == 0)
                    {
                        throw new ArgumentException($"proposalResponse.getPayloadBytes() was empty from peer: {proposalResponse.Peer}.");
                    }

                    if (!ret.ContainsKey(payloadBytes))
                        ret.Add(payloadBytes, new HashSet<ProposalResponse>());

                    HashSet<ProposalResponse> set = ret[payloadBytes];
                    set.Add(proposalResponse);
                }
            }
            if (IS_DEBUG_LEVEL && ret.Count > 1)
            {

                StringBuilder sb = new StringBuilder(1000);

                int i = 0;
                string sep = "";

                foreach(ByteString bytes in ret.Keys)
                {
                    HashSet<ProposalResponse> presp = ret[bytes];

                    sb.Append(sep)
                        .Append("Consistency set: ").Append(i++).Append(" bytes size: ").Append(bytes.Length)
                        .Append(" bytes: ")
                        .Append(bytes.ToByteArray().ToHexString()).Append(" [");

                    string psep = "";

                    foreach (ProposalResponse proposalResponse in presp)
                    {
                        sb.Append(psep).Append(proposalResponse.Peer);
                        psep = ", ";
                    }
                    sb.Append("]");
                    sep = ", ";
                }

                logger.Debug(sb.ToString());

            }
            return ret.Values.ToList();
        }
    }
}