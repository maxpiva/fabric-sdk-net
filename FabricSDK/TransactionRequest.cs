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
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.NetExtensions;
using Org.BouncyCastle.Utilities;

namespace Hyperledger.Fabric.SDK
{

    /**
     * A base transaction request common for InstallProposalRequest,trRequest, and QueryRequest.
     */
    public class TransactionRequest
    {


        bool submitted = false;

        private Config config => Config.GetConfig();
        // The local path containing the chaincode to deploy in network mode.
        protected string chaincodePath;



        // The chaincode ID as provided by the 'submitted' event emitted by a TransactionContext
        private ChaincodeID chaincodeID;





        
        protected Dictionary<string, byte[]> transientMap;

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

        /**
         * Transient data added to the proposal that is not added to the ledger.
         *
         * @return Map of strings to bytes that's added to the proposal
         */

        public Dictionary<String, byte[]> TransientMap => transientMap;

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

        public TransactionRequest SetChaincodePath(string chaincodePath) {

            this.chaincodePath = chaincodePath;
            return this;
        }
        public string ChaincodeName { get; set; }

        public TransactionRequest SetChaincodeName(string chaincodeName)
        {
            ChaincodeName = chaincodeName;
            return this;
        }
        public string ChaincodeVersion { get; set; }
        public TransactionRequest SetChaincodeVersion(string chaincodeVersion) {
            ChaincodeVersion = chaincodeVersion;
            return this;
        }

        public ChaincodeID ChaincodeID
        {
            get => chaincodeID;
            set
            {
                if (ChaincodeName != null)
                {

                    throw new InvalidArgumentException("Chaincode name has already been set.");
                }
                if (ChaincodeVersion != null)
                {

                    throw new InvalidArgumentException("Chaincode version has already been set.");
                }

                if (chaincodePath != null)
                {

                    throw new InvalidArgumentException("Chaincode path has already been set.");
                }

                this.chaincodeID = value;
                ChaincodeName = chaincodeID.Name;
                chaincodePath = chaincodeID.Path;
                ChaincodeVersion = chaincodeID.Version;
            }
        }
        public string Fcn { get; set; }
      

        public TransactionRequest setFcn(string fcn)
        {
            this.Fcn = fcn;
            return this;
        }
        public List<string> Args { get; set; }


        public TransactionRequest SetArgs(params string[] args)
        {
            this.Args = new List<string>(args);
            return this;
        }
        public List<byte[]> ArgsBytes { get; set; }
        public TransactionRequest SetArgBytes(List<byte[]> args)
        {
            ArgsBytes = args;
            return this;
        }
        public TransactionRequest SetArgBytes(byte[][] args)
        {

            this.ArgsBytes = args.ToList();
            return this;
        }

        public TransactionRequest SetArgs(List<String> args)
        {
            this.Args = args;
            return this;
        }

        public TransactionRequest SetArgs(params byte[][] args)
        {
            this.ArgsBytes = args.ToList();
            return this;
        }

        //Mirror Fabric try not expose any of its classes
        public enum Type {
            JAVA,
            GO_LANG,
            NODE
        }

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
        public ChaincodeEndorsementPolicy EndorsementPolicy { get; set; }


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
        public long ProposalWaitTime { get; set; } = config.GetProposalWaitTime();




        /**
         * If this request has been submitted already.
         *
         * @return true if the already submitted.
         */

        public bool IsSubmitted => submitted;

        public void SetSubmitted()
        {
            if (submitted)
            {
                // Has already been submitted.
                throw new InvalidArgumentException("Request has been already submitted and can not be reused.");
            }
            UserContext.UserContextCheck();
            submitted = true;
        }

        protected TransactionRequest(IUser userContext) {
            this.UserContext = userContext;
        }
    }
}
