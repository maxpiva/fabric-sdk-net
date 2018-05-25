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
/*
package org.hyperledger.fabric.sdk.testutils;

import java.io.ByteArrayInputStream;
import java.lang.reflect.Field;
import java.lang.reflect.InvocationTargetException;
import java.lang.reflect.Method;
import java.security.PrivateKey;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.Iterator;
import java.util.List;
import java.util.Properties;
import java.util.Set;
import java.util.zip.GZIPInputStream;

import org.apache.commons.compress.archivers.tar.TarArchiveEntry;
import org.apache.commons.compress.archivers.tar.TarArchiveInputStream;
import org.hamcrest.Description;
import org.hamcrest.Matcher;
import org.hamcrest.TypeSafeMatcher;
import org.hyperledger.fabric.sdk.Enrollment;
import org.hyperledger.fabric.sdk.User;
import org.hyperledger.fabric.sdk.helper.Config;
import org.junit.Assert;

import static java.lang.String.format;
*/
//import org.hyperledger.fabric.sdk.MockUser;
//import org.hyperledger.fabric.sdk.ClientTest.MockEnrollment;

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Hyperledger.Fabric.SDK;
using Hyperledger.Fabric.SDK.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Writers;

namespace Hyperledger.Fabric.Tests.SDK.TestUtils
{
    public class TestUtils
    {

    private TestUtils() {
    }

        //Reflection methods deleted, there is no need, stuff marked as internal
        private static readonly string MOCK_CERT = "-----BEGIN CERTIFICATE-----" +
        "MIICGjCCAcCgAwIBAgIRAPDmqtljAyXFJ06ZnQjXqbMwCgYIKoZIzj0EAwIwczEL" +
        "MAkGA1UEBhMCVVMxEzARBgNVBAgTCkNhbGlmb3JuaWExFjAUBgNVBAcTDVNhbiBG" +
        "cmFuY2lzY28xGTAXBgNVBAoTEG9yZzEuZXhhbXBsZS5jb20xHDAaBgNVBAMTE2Nh" +
        "Lm9yZzEuZXhhbXBsZS5jb20wHhcNMTcwNjIyMTIwODQyWhcNMjcwNjIwMTIwODQy" +
        "WjBbMQswCQYDVQQGEwJVUzETMBEGA1UECBMKQ2FsaWZvcm5pYTEWMBQGA1UEBxMN" +
        "U2FuIEZyYW5jaXNjbzEfMB0GA1UEAwwWQWRtaW5Ab3JnMS5leGFtcGxlLmNvbTBZ" +
        "MBMGByqGSM49AgEGCCqGSM49AwEHA0IABJve76Fj5T8Vm+FgM3p3TwcnW/npQlTL" +
        "P+fY0fImBODqQLTkBokx4YiKcQXQl4m1EM1VAbOhAlBiOfNRNL0W8aGjTTBLMA4G" +
        "A1UdDwEB/wQEAwIHgDAMBgNVHRMBAf8EAjAAMCsGA1UdIwQkMCKAIPz3drAqBWAE" +
        "CNC+nZdSr8WfZJULchyss2O1uVoP6mIWMAoGCCqGSM49BAMCA0gAMEUCIQDatF1P" +
        "L7SavLsmjbFxdeVvLnDPJuCFaAdr88oE2YuAvwIgDM4qXAcDw/AhyQblWR4F4kkU" +
        "NHvr441QC85U+V4UQWY=" +
        "-----END CERTIFICATE-----";
        public class MockEnrollment : IEnrollment
        {         
            public AsymmetricAlgorithm Key { get; }
            public string Cert { get; }

            public MockEnrollment(AsymmetricAlgorithm key, string cert)
            {
                Key = key;
                Cert = cert;
            }
        }

        public class MockUser : IUser
        {
            public MockUser(string name, string mspId)
            {
                Name = name;
                MspId = mspId;
                Enrollment = GetMockEnrollment(MOCK_CERT);
            }

            public string Name { get; }
            public HashSet<string> Roles { get; }
            public string Account { get; }
            public string Affiliation { get; }
            public IEnrollment Enrollment { get; }
            public string MspId { get; }
        }


    public static MockUser GetMockUser(string name, string mspId) {
        return new MockUser(name, mspId);
    }

    public static MockEnrollment GetMockEnrollment(string cert) {
        return new MockEnrollment(new RSACng(), cert);
    }

    public static MockEnrollment GetMockEnrollment(AsymmetricAlgorithm key, string cert) {
        return new MockEnrollment(key, cert);
    }

    public static List<string> TarBytesToEntryArrayList(byte[] bytes)
    {

        List<string> ret = new List<string>();
        using (MemoryStream bos = new MemoryStream(bytes))            
        using (var reader = ReaderFactory.Open(bos))
        {
            bool end = false;
            do
            {
                IEntry ta = reader.Entry;
                Assert.IsTrue(!ta.IsDirectory,$"Tar entry {ta.Key} is not a file.");
                ret.Add(ta.Key);
                end = !reader.MoveToNextEntry();
            } while (!end);


        return ret;

    }
        /*
    public static void AssertArrayListEquals(string failmsg, List<string> expect, List<string> actual) {
        ArrayList expectSort = new ArrayList(expect);
        Collections.sort(expectSort);
        ArrayList actualSort = new ArrayList(actual);
        Collections.sort(actualSort);
        Assert.assertArrayEquals(failmsg, expectSort.toArray(), actualSort.toArray());
    }

    public static Matcher<String> matchesRegex(final String regex) {
        return new TypeSafeMatcher<String>() {
            @Override
            public void describeTo(Description description) {

            }

            @Override
            protected boolean matchesSafely(final String item) {
                return item.matches(regex);
            }
        };
    }
    */
   

   

    //  This is the private key for the above cert. Right now we don't need this and there's some class loader issues doing this here.

//    private static final String MOCK_NOT_SO_PRIVATE_KEY = "-----BEGIN PRIVATE KEY-----\n" +
//            "MIGHAgEAMBMGByqGSM49AgEGCCqGSM49AwEHBG0wawIBAQQghnA7rdgbZi/wndus\n" +
//            "iXjyf0KgE6OKZjQ+5INjwelRAC6hRANCAASb3u+hY+U/FZvhYDN6d08HJ1v56UJU\n" +
//            "yz/n2NHyJgTg6kC05AaJMeGIinEF0JeJtRDNVQGzoQJQYjnzUTS9FvGh\n" +
//            "-----END PRIVATE KEY-----";

//    private static final  PrivateKey mockNotSoPrivateKey = getPrivateKeyFromBytes(MOCK_NOT_SO_PRIVATE_KEY.getBytes(StandardCharsets.UTF_8));
//
//    static PrivateKey getPrivateKeyFromBytes(byte[] data) {
//        try {
//            final Reader pemReader = new StringReader(new String(data));
//
//            final PrivateKeyInfo pemPair;
//            try (PEMParser pemParser = new PEMParser(pemReader)) {
//                pemPair = (PrivateKeyInfo) pemParser.readObject();
//            }
//
//            return new JcaPEMKeyConverter().setProvider(BouncyCastleProvider.PROVIDER_NAME).getPrivateKey(pemPair);
//        } catch (IOException e) {
//            throw new RuntimeException(e);
//        }
//    }



}
}