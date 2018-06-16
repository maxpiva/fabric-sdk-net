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
using System.Runtime.Serialization;
using System.Security.Cryptography;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Security;

namespace Hyperledger.Fabric.SDK
{ 

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
        string Key { get; }

        /**
         * Get the user's signed certificate.
         *
         * @return a certificate.
         */
        string Cert { get; }

    }
    public class Enrollment : IEnrollment
    {
        public string Key { get; }
        public string Cert { get; }

        public Enrollment(string key, string cert)
        {
            Key = key;
            Cert = cert;
        }
    }

    public static class EnrollmentExtensions
    {
        private static WeakDictionary<string, KeyPair> cache= new WeakDictionary<string, KeyPair>(KeyPair.Create);

        public static KeyPair GetKeyPair(this IEnrollment enrolment)
        {
            if (string.IsNullOrEmpty(enrolment.Key))
                return null;
            return cache.Get(enrolment.Key);
        }
    }
}
