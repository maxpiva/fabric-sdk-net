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
/*
package org.hyperledger.fabric.sdk;

import java.io.Serializable;
import java.util.Properties;

import io.netty.util.internal.StringUtil;
import org.apache.commons.logging.Log;
import org.apache.commons.logging.LogFactory;
import org.hyperledger.fabric.protos.common.Common;
import org.hyperledger.fabric.protos.orderer.Ab;
import org.hyperledger.fabric.protos.orderer.Ab.DeliverResponse;
import org.hyperledger.fabric.sdk.exception.InvalidArgumentException;
import org.hyperledger.fabric.sdk.exception.TransactionException;

import static java.lang.String.format;
import static org.hyperledger.fabric.sdk.helper.Utils.checkGrpcUrl;*/

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Force.DeepCloner;
using Hyperledger.Fabric.Protos.Common;
using Hyperledger.Fabric.Protos.Orderer;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Logging;

namespace Hyperledger.Fabric.SDK
{
    /**
     * The Orderer class represents a orderer to which SDK sends deploy, invoke, or query requests.
     */
    [Serializable]
    public class Orderer
    {
        private static readonly ILog logger = LogProvider.GetLogger(typeof(Orderer));
        private readonly Dictionary<string, object> properties;
        private Channel channel;

        [NonSerialized] private byte[] clientTLSCertificateDigest;

        [NonSerialized] private OrdererClient ordererClient = null;

        [NonSerialized] private bool shutdown = false;

        public Orderer(string name, string url, Dictionary<string, object> properties)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new InvalidArgumentException("Invalid name for orderer");
            }

            Exception e = Utils.CheckGrpcUrl(url);
            if (e != null)
            {
                throw new InvalidArgumentException(e);
            }

            Name = name;
            Url = url;
            this.properties = properties?.DeepClone();
        }

        public byte[] ClientTLSCertificateDigest => clientTLSCertificateDigest ?? (clientTLSCertificateDigest = new Endpoint(Url, properties).GetClientTLSCertificateDigest());

        /**
         * Get Orderer properties.
         *
         * @return properties
         */

        public Dictionary<string, object> Properties => properties?.DeepClone();

        /**
         * Return Orderer's name
         *
         * @return orderer's name.
         */
        public string Name { get; }

        /**
         * getUrl - the Grpc url of the Orderer
         *
         * @return the Grpc url of the Orderer
         */
        public string Url { get; }

        /**
         * Get the channel of which this orderer is a member.
         *
         * @return {Channel} The channel of which this orderer is a member.
         */
        public Channel Channel
        {
            get => channel;
            set
            {
                if (value == null)
                    throw new InvalidArgumentException("Channel can not be null");

                if (null != channel && channel != value)
                    throw new InvalidArgumentException($"Can not add orderer {Name} to channel {value.Name} because it already belongs to channel {channel.Name}.");
                channel = value;
            }
        }

        public static Orderer Create(string name, string url, Dictionary<string, object> properties)
        {
            return new Orderer(name, url, properties);
        }


        public void UnsetChannel()
        {
            channel = null;
        }


        /**
         * Send transaction to Order
         *
         * @param transaction transaction to be sent
         */

        public BroadcastResponse SendTransaction(Envelope transaction)
        {
            if (shutdown)
                throw new TransactionException($"Orderer {Name} was shutdown.");

            logger.Debug($"Order.sendTransaction name: {Name}, url: {Url}");

            OrdererClient localOrdererClient = ordererClient;

            if (localOrdererClient == null || !localOrdererClient.IsChannelActive())
            {
                ordererClient = new OrdererClient(this, new Endpoint(Url, properties), properties);
                localOrdererClient = ordererClient;
            }

            try
            {
                return localOrdererClient.SendTransaction(transaction);
            }
            catch (Exception t)
            {
                ordererClient = null;
                throw t;
            }
        }

        public DeliverResponse[] SendDeliver(Envelope transaction)
        {
            if (shutdown)
                throw new TransactionException($"Orderer {Name} was shutdown.");
            OrdererClient localOrdererClient = ordererClient;

            logger.Debug($"Order.sendDeliver name: {Name}, url: {Url}");

            if (localOrdererClient == null || !localOrdererClient.IsChannelActive())
            {
                localOrdererClient = new OrdererClient(this, new Endpoint(Url, properties), properties);
                ordererClient = localOrdererClient;
            }

            try
            {
                return localOrdererClient.SendDeliver(transaction);
            }
            catch (Exception t)
            {
                ordererClient = null;
                throw t;
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Shutdown(bool force)
        {
            if (shutdown)
            {
                return;
            }

            shutdown = true;
            channel = null;

            if (ordererClient != null)
            {
                OrdererClient torderClientDeliver = ordererClient;
                ordererClient = null;
                torderClientDeliver.Shutdown(force);
            }
        }

        ~Orderer()
        {
            Shutdown(true);
        }


        public override string ToString() => "Orderer: " + Name + "(" + Url + ")";
    } // end Orderer
}