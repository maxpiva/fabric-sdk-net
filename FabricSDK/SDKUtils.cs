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

/*    package org.hyperledger.fabric.sdk;

import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.util.Collection;
import java.util.HashMap;
import java.util.HashSet;
import java.util.Set;

import com.google.protobuf.ByteString;
import org.bouncycastle.asn1.ASN1Integer;
import org.bouncycastle.asn1.DEROctetString;
import org.bouncycastle.asn1.DERSequenceGenerator;
import org.hyperledger.fabric.sdk.exception.InvalidArgumentException;
import org.hyperledger.fabric.sdk.security.CryptoSuite;

import static java.lang.String.format;*/

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Google.Protobuf;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Responses;
using Hyperledger.Fabric.SDK.Security;
using Org.BouncyCastle.Asn1;

namespace Hyperledger.Fabric.SDK
{

    public class SDKUtils {
        private SDKUtils() {

        }

        public static ICryptoSuite suite = null;

        /**
         * used asn1 and get hash
         *
         * @param blockNumber
         * @param previousHash
         * @param dataHash
         * @return byte[]
         * @throws IOException
         * @throws InvalidArgumentException
         */
        public static byte[] CalculateBlockHash(HFClient client, long blockNumber, byte[] previousHash, byte[] dataHash)
        {

            if (previousHash == null) {
                throw new InvalidArgumentException("previousHash parameter is null.");
            }
            if (dataHash == null) {
                throw new InvalidArgumentException("dataHash parameter is null.");
            }
            if (null == client) {
                throw new InvalidArgumentException("client parameter is null.");
            }

            ICryptoSuite cryptoSuite = client.CryptoSuite;
            if (null == client) {
                throw new InvalidArgumentException("Client crypto suite has not  been set.");
            }

            MemoryStream s = new MemoryStream();
            DerSequenceGenerator seq = new DerSequenceGenerator(s);
            seq.AddObject(new DerInteger((int)blockNumber));            
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
         * @throws InvalidArgumentException
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
         * @throws InvalidArgumentException
         */

        public static List<HashSet<ProposalResponse>> GetProposalConsistencySets(List<ProposalResponse> proposalResponses, HashSet<ProposalResponse> invalid) {

            if (proposalResponses == null) {
                throw new InvalidArgumentException("proposalResponses collection is null");
            }

            if (proposalResponses.Count==0) {
                throw new InvalidArgumentException("proposalResponses collection is empty");
            }

            if (null == invalid) {
                throw new InvalidArgumentException("invalid set is null.");
            }

            Dictionary<ByteString, HashSet<ProposalResponse>> ret = new Dictionary<ByteString, HashSet<ProposalResponse>>();

            foreach (ProposalResponse proposalResponse in proposalResponses) {

                if (proposalResponse.IsInvalid) {
                    invalid.Add(proposalResponse);
                } else {
                    // payload bytes is what's being signed over so it must be consistent.
                    ByteString payloadBytes = proposalResponse.PayloadBytes;
                    
                    if (payloadBytes == null)
                    {
                        throw new InvalidArgumentException($"proposalResponse.getPayloadBytes() was null from peer: {proposalResponse.Peer}.");

                    } else if (payloadBytes.Length==0) {
                        throw new InvalidArgumentException($"proposalResponse.getPayloadBytes() was empty from peer: {proposalResponse.Peer}.");
                    }
                    if (!ret.ContainsKey(payloadBytes))
                        ret.Add(payloadBytes,new HashSet<ProposalResponse>());

                    HashSet<ProposalResponse> set = ret[payloadBytes];
                    set.Add(proposalResponse);
                }
            }

            return ret.Values.ToList();

        }
    }
}