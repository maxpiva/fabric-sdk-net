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
using System.Security.Cryptography;

namespace Hyperledger.Fabric.SDK
{ /*
    package org.hyperledger.fabric.sdk;

import java.util.Set;

import org.hyperledger.fabric.sdk.exception.InvalidIllegalArgumentException;
import org.hyperledger.fabric.sdk.helper.Utils;

import static java.lang.String.format;
    */

/**
 * Interface which is the contract between the Certificate Authority provider and the SDK.
 */
    public interface IEnrollment
    {

        /**
         * Get the user's private key
         *
         * @return private key.
         */
        AsymmetricAlgorithm Key { get; }

        /**
         * Get the user's signed certificate.
         *
         * @return a certificate.
         */
        string Cert { get; }

    }

    public class Enrollment : IEnrollment
    {
        public AsymmetricAlgorithm Key { get; }
        public string Cert { get; }

        public Enrollment(AsymmetricAlgorithm key, string cert)
        {
            Key = key;
            Cert = cert;
        }
    }
}
