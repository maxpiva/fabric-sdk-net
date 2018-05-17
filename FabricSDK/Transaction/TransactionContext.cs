/*
 *  Copyright 2016, 2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
 *
 *  Licensed under the Apache License, Version 2.0 (the "License");
 *  you may not use this file except in compliance with the License.
 *  You may obtain a copy of the License at
 *        http://www.apache.org/licenses/LICENSE-2.0
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed under the License is distributed on an "AS IS" BASIS,
 *  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *  See the License for the specific language governing permissions and
 *  limitations under the License.
 */

using System;
using System.Linq;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.NetExtensions;
using Hyperledger.Fabric.SDK.Protos.Msp;
using Hyperledger.Fabric.SDK.Security;

namespace Hyperledger.Fabric.SDK.Transaction
{
/**
 * Internal class, not an public API.
 * A transaction context emits events 'submitted', 'complete', and 'error'.
 * Each transaction context uses exactly one tcert.
 */
    public class TransactionContext
    {
        private static readonly Config config = Config.GetConfig();
        //    private static final Log logger = LogFactory.getLog(TransactionContext.class);
        //TODO right now the server does not care need to figure out
        private readonly byte[] nonce = Utils.GenerateNonce();
        private readonly ICryptoSuite cryptoPrimitives;
        private IUser user;
        private readonly Channel channel;
        private readonly string txID;
        private readonly SerializedIdentity identity;
        DateTime? currentTimeStamp=null;

        //private List<String> attrs;


        public TransactionContext(Channel channel, IUser user, ICryptoSuite cryptoPrimitives) {

            this.user = user;
            this.channel = channel;
            //TODO clean up when public classes are interfaces.
            this.Verify = !"".Equals(channel.Name);  //if name is not blank not system channel and need verify.
            //  this.txID = transactionID;
            this.cryptoPrimitives = cryptoPrimitives;
            identity = ProtoUtils.CreateSerializedIdentity(User);
            byte[] no = Nonce;
            byte[] comp = no.Concat(identity.SerializeProtoBuf()).ToArray();
            byte[] txh = cryptoPrimitives.Hash(comp);
            //    txID = Hex.encodeHexString(txh);
            txID = txh.ToHexString();

        }

        public ICryptoSuite CryptoPrimitives => cryptoPrimitives;

        public SerializedIdentity Identity => identity;

        public long Epoch => 0;

        /**
         * Get the user with which this transaction context is associated.
         *
         * @return The user
         */
        public IUser User => user;

        /**
         * Get the attribute names associated with this transaction context.
         *
         * @return the attributes.
         */
        //public List<String> getAttrs() {
        //    return this.attrs;
        //}

        /**
         * Set the attributes for this transaction context.
         *
         * @param attrs the attributes.
         */
        //public void setAttrs(List<String> attrs) {
        //    this.attrs = attrs;
        //}

        /**
         * Get the channel with which this transaction context is associated.
         *
         * @return The channel
         */
        public Channel Channel() => this.channel;

        /**
         * Gets/Sets the timeout for a single proposal request to endorser in milliseconds.
         *
         * @return the timeout for a single proposal request to endorser in milliseconds
         */
        public long ProposalWaitTime { get; set; } = config.GetProposalWaitTime();


        public DateTime FabricTimestamp
        {
            get
            {
                if (currentTimeStamp == null)
                    currentTimeStamp = DateTime.UtcNow;
                return currentTimeStamp.Value;
            }
        }

        public byte[] Nonce => nonce;

        public bool Verify { get; set; }



        public string ChannelID => Channel.Name;

        public string TxID => txID;

        byte[] Sign(byte[] b)
        {
            return cryptoPrimitives.Sign(User.Enrollment.Key, b);
        }



        public byte[] SignByteStrings(params byte[][] bs)
        {
            if (bs == null) {
                return null;
            }
            if (bs.Length == 0) {
                return null;
            }
            if (bs.Length == 1 && bs[0] == null)
                return null;
            byte[] f = bs[0];
            for (int i = 1; i < bs.Length; ++i) {
                f = f.Concat(bs[i]).ToArray();

            }
            return Sign(f);
        }

        public byte[][] SignByteStrings(IUser[] users, params byte[][]bs)
        {
            if (bs == null) {
                return null;
            }
            if (bs.Length == 0) {
                return null;
            }
            if (bs.Length == 1 && bs[0] == null) {
                return null;
            }

            byte[] signbytes = bs[0];
            for (int i = 1; i < bs.Length; ++i)
            {
                signbytes = signbytes.Concat(bs[i]).ToArray();
            }



            byte[][] ret = new byte[users.Length][];

            int ii = -1;
            foreach (IUser user in users) {
                ret[++ii] = cryptoPrimitives.Sign(user.Enrollment.Key, signbytes);
            }
            return ret;
        }

        public TransactionContext RetryTransactionSameContext() {

            return new TransactionContext(channel, user, cryptoPrimitives);

        }

    }  // end TransactionContext
}
