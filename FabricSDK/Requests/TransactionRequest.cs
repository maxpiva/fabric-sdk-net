/*
 *  Copyright 2016, 2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
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
using Hyperledger.Fabric.SDK.Configuration;
using Hyperledger.Fabric.SDK.Helper;

namespace Hyperledger.Fabric.SDK.Requests
{
    /**
     * A base transaction request common for InstallProposalRequest,trRequest, and QueryRequest.
     */
    public class TransactionRequest
    {
        //Mirror Fabric try not expose any of its classes
        public enum Type
        {
            JAVA,
            GO_LANG,
            NODE
        }

        private ChaincodeID chaincodeID;

        protected string chaincodePath;


        /**
         * If this request has been submitted already.
         *
         * @return true if the already submitted.
         */
        private bool isSubmitted;

        /**
         * Transient data added to the proposal that is not added to the ledger.
         *
         * @return Map of strings to bytes that's added to the proposal
         */
        protected Dictionary<string, byte[]> transientMap;


        // The chaincode ID as provided by the 'submitted' event emitted by a TransactionContext


        // The local path containing the chaincode to deploy in network mode.


        protected TransactionRequest(IUser userContext)
        {
            UserContext = userContext;
            ProposalWaitTime = Config.Instance.GetProposalWaitTime();
        }


        /**
         * The user context to use on this request.
         *
         * @return User context that is used for signing
         */
        /**
         * Set the user context for this request. This context will override the user context set
         * on {@link HFClient#setUserContext(User)}
         *
         * @param userContext The user context for this request used for signing.
         */
        public IUser UserContext { get; set; }

        public virtual Dictionary<string, byte[]> TransientMap
        {
            get => transientMap?.ToDictionary(a => a.Key, a => a.Value);
            set => throw new ArgumentException("Transient map may not be set");
        }

        /**
         * Determines whether an empty channel ID should be set on proposals built
         * from this request. Some peer requests (e.g. queries to QSCC) require the
         * field to be blank. Subclasses should override this method as needed.
         * <p>
         * This implementation returns {@code false}.
         *
         * @return {@code true} if an empty channel ID should be used; otherwise
         * {@code false}.
         */
        public bool NoChannelID => false;

        /**
         * Some proposal responses from Fabric are not signed. We default to always verify a ProposalResponse.
         * Subclasses should override this method if you do not want the response signature to be verified
         *
         * @return true if proposal response is to be checked for a valid signature
         */
        public bool DoVerify => true;

        public string ChaincodePath
        {
            get => chaincodePath ?? string.Empty;
            set => chaincodePath = value;
        }

        public string ChaincodeName { get; set; }
        public string ChaincodeVersion { get; set; }

        public virtual ChaincodeID ChaincodeID
        {
            get => chaincodeID;
            set
            {
                if (ChaincodeName != null)
                    throw new ArgumentException("Chaincode name has already been set.");
                if (ChaincodeVersion != null)
                    throw new ArgumentException("Chaincode version has already been set.");
                if (chaincodePath != null)
                    throw new ArgumentException("Chaincode path has already been set.");
                chaincodeID = value;
                ChaincodeName = chaincodeID.Name;
                chaincodePath = chaincodeID.Path;
                ChaincodeVersion = chaincodeID.Version;
            }
        }

        public string Fcn { get; set; }
        public List<string> Args { get; set; }
        public List<byte[]> ArgsBytes { get; set; }

        /**
         * The chaincode language type: default type Type.GO_LANG
         *
         * @param chaincodeLanguage . Type.Java Type.GO_LANG Type.NODE
         */
        /**
         * sets the endorsementPolicy associated with the chaincode of this transaction
         *
         * @param policy a Policy object
         * @see ChaincodeEndorsementPolicy
         */
        public Type ChaincodeLanguage { get; set; } = Type.GO_LANG;

        /**
         * returns the Policy object associated with the chaincode of this transaction
         *
         * @return a Policy object
         * @see ChaincodeEndorsementPolicy
         */
        public ChaincodeEndorsementPolicy ChaincodeEndorsementPolicy { get; set; }

        /**
     * get collection configuration for this chaincode.
     *
     * @return collection configuration if set.
     */
        /**
     * Set collection configuration for this chaincode.
     *
     * @param chaincodeCollectionConfiguration
     */

        public ChaincodeCollectionConfiguration ChaincodeCollectionConfiguration { get; set; }

        /**
         * Gets the timeout for a single proposal request to endorser in milliseconds.
         *
         * @return the timeout for a single proposal request to endorser in milliseconds
         */
        /**
         * Sets the timeout for a single proposal request to endorser in milliseconds.
         *
         * @param proposalWaitTime the timeout for a single proposal request to endorser in milliseconds
         */
        public long ProposalWaitTime { get; set; }

        public bool IsSubmitted
        {
            get => isSubmitted;
            set
            {
                if (!value)
                    return;
                if (isSubmitted && value)
                {
                    // Has already been submitted.
                    throw new ArgumentException("Request has been already submitted and can not be reused.");
                }

                UserContext.UserContextCheck();
                isSubmitted = true;
            }
        }

        public static TransactionRequest Create(IUser userContext)
        {
            return new TransactionRequest(userContext);
        }
    }

    public static class TransactionRequestBuilder
    {
        public static T SetChaincodePath<T>(this T request, string chainCodePath) where T : TransactionRequest
        {
            request.ChaincodePath = chainCodePath;
            return request;
        }

        public static T SetChaincodeName<T>(this T request, string chaincodeName) where T : TransactionRequest
        {
            request.ChaincodeName = chaincodeName;
            return request;
        }

        public static T SetChaincodeCollectionConfiguration<T>(this T request, ChaincodeCollectionConfiguration chaincodeCollectionConfiguration) where T : TransactionRequest
        {
            request.ChaincodeCollectionConfiguration = chaincodeCollectionConfiguration;
            return request;
        }
        public static T SetChaincodeID<T>(this T request, ChaincodeID chaincodeid) where T : TransactionRequest
        {
            request.ChaincodeID = chaincodeid;
            return request;
        }

        public static T SetChaincodeVersion<T>(this T request, string chaincodeVersion) where T : TransactionRequest
        {
            request.ChaincodeVersion = chaincodeVersion;
            return request;
        }
        public static T SetChaincodeLanguage<T>(this T request, TransactionRequest.Type chaincodeLanguage) where T : TransactionRequest
        {
            request.ChaincodeLanguage = chaincodeLanguage;
            return request;
        }
        public static T SetChaincodeEndorsementPolicy<T>(this T request, ChaincodeEndorsementPolicy policy) where T : TransactionRequest
        {
            request.ChaincodeEndorsementPolicy = policy;
            return request;
        }

        public static T SetFcn<T>(this T request, string fcn) where T : TransactionRequest
        {
            request.Fcn = fcn;
            return request;
        }

        public static T SetArgs<T>(this T request, params string[] args) where T : TransactionRequest
        {
            request.Args = new List<string>(args);
            return request;
        }

        public static T SetArgBytes<T>(this T request, List<byte[]> args) where T : TransactionRequest
        {
            request.ArgsBytes = args;
            return request;
        }

        public static T SetArgBytes<T>(this T request, byte[][] args) where T : TransactionRequest
        {
            request.ArgsBytes = args.ToList();
            return request;
        }

        public static T SetArgs<T>(this T request, List<string> args) where T : TransactionRequest
        {
            request.Args = args;
            return request;
        }

        public static T SetArgs<T>(this T request, params byte[][] args) where T : TransactionRequest
        {
            request.ArgsBytes = args.ToList();
            return request;
        }

        public static T SetSubmitted<T>(this T request) where T : TransactionRequest
        {
            request.IsSubmitted = true;
            return request;
        }

        public static T SetProposalWaitTime<T>(this T request, long waitTime) where T : TransactionRequest
        {
            request.ProposalWaitTime = waitTime;
            return request;
        }

        public static T SetUserContext<T>(this T request, IUser user) where T : TransactionRequest
        {
            request.UserContext = user;
            return request;
        }
    }
}