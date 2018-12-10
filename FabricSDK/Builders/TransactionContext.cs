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
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Hyperledger.Fabric.Protos.Msp;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Identity;
using Hyperledger.Fabric.SDK.Security;

namespace Hyperledger.Fabric.SDK.Builders
{
/**
 * Internal class, not an public API.
 * A transaction context emits events 'submitted', 'complete', and 'error'.
 * Each transaction context uses exactly one tcert.
 */
    public class TransactionContext
    {
        private readonly string toString;
        private Timestamp currentTimeStamp;
        private readonly ISigningIdentity signingIdentity;

        //private List<String> attrs;


        public TransactionContext(Channel channel, IUser user, ICryptoSuite cryptoPrimitives)
        {
            User = user;
            Channel = channel;
            //TODO clean up when public classes are interfaces.
            Verify = !"".Equals(channel.Name); //if name is not blank not system channel and need verify.
            //  this.txID = transactionID;
            CryptoPrimitives = cryptoPrimitives;

            // Get the signing identity from the user
            signingIdentity = IdentityFactory.GetSigningIdentity(cryptoPrimitives, user);

            // Serialize signingIdentity
            Identity = signingIdentity.CreateSerializedIdentity();

            ByteString no = Nonce;
            byte[] comp = no.Concat(Identity.ToByteArray()).ToArray();
            byte[] txh = cryptoPrimitives.Hash(comp);
            //    txID = Hex.encodeHexString(txh);
            TxID = txh.ToHexString();
            toString = $"TransactionContext {{ txID: {TxID} mspid: {user.MspId}, user: {user.Name} }}";
        }

        public ICryptoSuite CryptoPrimitives { get; }
        public SerializedIdentity Identity { get; }

        public long Epoch => 0;

        /**
         * Get the user with which this transaction context is associated.
         *
         * @return The user
         */
        public IUser User { get; }

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
        public Channel Channel { get; }

        /**
         * Gets/Sets the timeout for a single proposal request to endorser in milliseconds.
         *
         * @return the timeout for a single proposal request to endorser in milliseconds
         */
        public long ProposalWaitTime { get; set; } = Config.Instance.GetProposalWaitTime();


        public Timestamp FabricTimestamp => currentTimeStamp ?? (currentTimeStamp = ProtoUtils.GetCurrentFabricTimestamp());

        public ByteString Nonce { get; } = ByteString.CopyFrom(Utils.GenerateNonce());

        public bool Verify { get; set; }


        public string ChannelID => Channel.Name;

        public string TxID { get; }

        public byte[] Sign(byte[] b)
        {
            return signingIdentity.Sign(b);
        }

        public ByteString SignByteString(byte[] b)
        {
            return ByteString.CopyFrom(Sign(b));
        }

        public ByteString SignByteStrings(params ByteString[] bs)
        {
            if (bs == null)
            {
                return null;
            }

            if (bs.Length == 0)
            {
                return null;
            }

            if (bs.Length == 1 && bs[0] == null)
                return null;
            byte[] total = new byte[bs.Sum(a => a.Length)];
            int start = 0;
            foreach (ByteString b in bs)
            {
                Array.Copy(b.ToByteArray(), 0, total, start, b.Length);
                start += b.Length;
            }

            return ByteString.CopyFrom(Sign(total));
        }

        public ByteString[] SignByteStrings(IUser[] users, params ByteString[] bs)
        {
            if (bs == null)
            {
                return null;
            }

            if (bs.Length == 0)
            {
                return null;
            }

            if (bs.Length == 1 && bs[0] == null)
            {
                return null;
            }

            byte[] signbytes = new byte[bs.Sum(a => a.Length)];
            int start = 0;
            foreach (ByteString b in bs)
            {
                Array.Copy(b.ToByteArray(), 0, signbytes, start, b.Length);
                start += b.Length;
            }


            ByteString[] ret = new ByteString[users.Length];
            int ii = -1;
            foreach (IUser user in users)
            {
                // Get the signing identity from the user
                ISigningIdentity si = IdentityFactory.GetSigningIdentity(CryptoPrimitives, user);

                // generate signature
                ret[++ii] = ByteString.CopyFrom(si.Sign(signbytes));
            }

            return ret;
        }

        public TransactionContext RetryTransactionSameContext()
        {
            return new TransactionContext(Channel, User, CryptoPrimitives);
        }

        public override string ToString()
        {
            return toString;
        }
    } // end TransactionContext
}