/*
 *
 *  Copyright 2017, 2018 IBM Corp. All Rights Reserved.
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
using Google.Protobuf;
using Hyperledger.Fabric.Protos.Idemix;
using Hyperledger.Fabric.SDK.AMCL;
using Hyperledger.Fabric.SDK.AMCL.FP256BN;
using ECP = Hyperledger.Fabric.SDK.AMCL.FP256BN.ECP;
using ECP2 = Hyperledger.Fabric.SDK.AMCL.FP256BN.ECP2;

namespace Hyperledger.Fabric.SDK.Idemix
{
    /**
     * IdemixCredential represents a user's idemix credential,
     * which is a BBS+ signature (see "Constant-Size Dynamic k-TAA" by Man Ho Au, Willy Susilo, Yi Mu)
     * on the user's secret key and attribute values.
     */
    public class IdemixCredential
    {
        /**
         * Constructor creating a new credential
         *
         * @param key   the issuer key pair
         * @param m     a credential request
         * @param attrs an array of attribute values as BIG
         */
        public IdemixCredential(IdemixIssuerKey key, IdemixCredRequest m, BIG[] attrs)
        {
            if (key == null || key.Ipk == null || m == null || attrs == null)
                throw new ArgumentException("Cannot create idemix credential from null input");
            if (attrs.Length != key.Ipk.AttributeNames.Length)
                throw new ArgumentException("Amount of attribute values does not match amount of attributes in issuer public key");
            RAND rng = IdemixUtils.GetRand();
            // Place a BBS+ signature on the user key and the attribute values
            // (For BBS+, see "Constant-Size Dynamic k-TAA" by Man Ho Au, Willy Susilo, Yi Mu)
            E = rng.RandModOrder();
            S = rng.RandModOrder();

            B = new ECP();
            B.Copy(IdemixUtils.GenG1);
            B.Add(m.Nym);
            B.Add(key.Ipk.HRand.Mul(S));

            for (int i = 0; i < attrs.Length / 2; i++)
            {
                B.Add(key.Ipk.HAttrs[2 * i].Mul2(attrs[2 * i], key.Ipk.HAttrs[2 * i + 1], attrs[2 * i + 1]));
            }

            if (attrs.Length % 2 != 0)
            {
                B.Add(key.Ipk.HAttrs[attrs.Length - 1].Mul(attrs[attrs.Length - 1]));
            }

            BIG exp = new BIG(key.Isk).Plus(E);
            exp.Mod(IdemixUtils.GROUP_ORDER);
            exp.InvModp(IdemixUtils.GROUP_ORDER);
            A = B.Mul(exp);

            Attrs = new byte[attrs.Length][];
            for (int i = 0; i < attrs.Length; i++)
            {
                byte[] b = new byte[IdemixUtils.FIELD_BYTES];
                attrs[i].ToBytes(b);
                Attrs[i] = b;
            }
        }

        /**
         * Construct an IdemixCredential from a serialized credential
         *
         * @param proto a protobuf representation of a credential
         */
        public IdemixCredential(Credential proto)
        {
            if (proto == null)
                throw new ArgumentException("Cannot create idemix credential from null input");

            A = proto.A.ToECP();
            B = proto.B.ToECP();
            E = BIG.FromBytes(proto.E.ToByteArray());
            S = BIG.FromBytes(proto.S.ToByteArray());
            Attrs = new byte[proto.Attrs.Count][];
            for (int i = 0; i < proto.Attrs.Count; i++)
            {
                Attrs[i] = proto.Attrs[i].ToByteArray();
            }
        }

        public ECP A { get; }
        public ECP B { get; }
        public BIG E { get; }
        public BIG S { get; }
        public byte[][] Attrs { get; }

        /**
         * verify cryptographically verifies the credential
         *
         * @param sk  the secret key of the user
         * @param ipk the public key of the issuer
         * @return true iff valid
         */
        public bool Verify(BIG sk, IdemixIssuerPublicKey ipk)
        {
            if (ipk == null || Attrs.Length != ipk.AttributeNames.Length)
            {
                return false;
            }

            foreach (byte[] attr in Attrs)
            {
                if (attr == null)
                    return false;
            }

            ECP bPrime = new ECP();
            bPrime.Copy(IdemixUtils.GenG1);
            bPrime.Add(ipk.Hsk.Mul2(sk, ipk.HRand, S));
            for (int i = 0; i < Attrs.Length / 2; i++)
            {
                bPrime.Add(ipk.HAttrs[2 * i].Mul2(BIG.FromBytes(Attrs[2 * i]), ipk.HAttrs[2 * i + 1], BIG.FromBytes(Attrs[2 * i + 1])));
            }

            if (Attrs.Length % 2 != 0)
            {
                bPrime.Add(ipk.HAttrs[Attrs.Length - 1].Mul(BIG.FromBytes(Attrs[Attrs.Length - 1])));
            }

            if (!B.Equals(bPrime))
            {
                return false;
            }

            ECP2 a = IdemixUtils.GenG2.Mul(E);
            a.Add(ipk.W);
            a.Affine();
            return PAIR.FExp(PAIR.Ate(a, A)).Equals(PAIR.FExp(PAIR.Ate(IdemixUtils.GenG2, B)));
        }

        /**
         * @return A proto representation of this credential
         */
        public Credential ToProto()
        {
            Credential cr = new Credential
            {
                A = A.ToProto(),
                B = B.ToProto(),
                E = ByteString.CopyFrom(E.ToBytes()),
                S = ByteString.CopyFrom(S.ToBytes())
            };

            foreach (byte[] attr in Attrs)
                cr.Attrs.Add(ByteString.CopyFrom(attr));

            return cr;
        }
    }
}