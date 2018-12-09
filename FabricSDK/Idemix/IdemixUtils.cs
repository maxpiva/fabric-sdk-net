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

using System.Linq;
using Google.Protobuf;
using Hyperledger.Fabric.SDK.AMCL;
using Hyperledger.Fabric.SDK.AMCL.FP256BN;
using Org.BouncyCastle.Security;

namespace Hyperledger.Fabric.SDK.Idemix
{
    /**
     * The class IdemixUtils consists of all needed utility functions for Idemix.
     * The class uses the apache milagro crypto library.
     */
    public static class IdemixUtils
    {
        public static readonly BIG GX = new BIG(ROM.CURVE_Gx);
        public static readonly BIG GY = new BIG(ROM.CURVE_Gy);
        public static readonly ECP GenG1 = new ECP(GX, GY);
        public static readonly BIG Pxa = new BIG(ROM.CURVE_Pxa);
        public static readonly BIG Pxb = new BIG(ROM.CURVE_Pxb);
        public static readonly FP2 Px = new FP2(Pxa, Pxb);
        public static readonly BIG Pya = new BIG(ROM.CURVE_Pya);
        public static readonly BIG Pyb = new BIG(ROM.CURVE_Pyb);
        public static readonly FP2 Py = new FP2(Pya, Pyb);
        public static readonly ECP2 GenG2 = new ECP2(Px, Py);
        public static readonly FP12 GenGT = PAIR.FExp(PAIR.Ate(GenG2, GenG1));
        public static readonly BIG GROUP_ORDER = new BIG(ROM.CURVE_Order);
        public static readonly int FIELD_BYTES = BIG.MODBYTES;



        /**
         * Returns a random number generator, amcl.RAND,
         * initialized with a fresh seed.
         *
         * @return a random number generator
         */
        public static RAND GetRand()
        {
            // construct a secure seed
            int seedLength = FIELD_BYTES;
            SecureRandom random = new SecureRandom();
            byte[] seed = random.GenerateSeed(seedLength);

            // create a new amcl.RAND and initialize it with the generated seed
            RAND rng = new RAND();
            rng.Clean();
            rng.Seed(seedLength, seed);

            return rng;
        }

        /**
         * @return a random BIG in 0, ..., GROUP_ORDER-1
         */
        public static BIG RandModOrder(this RAND rng)
        {
            BIG q = new BIG(ROM.CURVE_Order);

            // Takes random element in this Zq.
            return BIG.RandomNum(q, rng);
        }

        /**
         * hashModOrder hashes bytes to an amcl.BIG
         * in 0, ..., GROUP_ORDER
         *
         * @param data the data to be hashed
         * @return a BIG in 0, ..., GROUP_ORDER-1 that is the hash of the data
         */
        public static BIG HashModOrder(this byte[] data)
        {
            HASH256 hash = new HASH256();
            foreach (byte b in data)
            {
                hash.Process(b);
            }

            byte[] hasheddata = hash.Hash();

            BIG ret = BIG.FromBytes(hasheddata);
            ret.Mod(GROUP_ORDER);

            return ret;
        }

        /**
         * bigToBytes turns a BIG into a byte array
         *
         * @param big the BIG to turn into bytes
         * @return a byte array representation of the BIG
         */
        public static byte[] ToBytes(this BIG big)
        {
            byte[] ret = new byte[FIELD_BYTES];
            big.ToBytes(ret);
            return ret;
        }

        /**
         * ecpToBytes turns an ECP into a byte array
         *
         * @param e the ECP to turn into bytes
         * @return a byte array representation of the ECP
         */
        public static byte[] ToBytes(this ECP e)
        {
            byte[] ret = new byte[2 * FIELD_BYTES + 1];
            e.ToBytes(ret, false);
            return ret;
        }

        /**
         * ecpToBytes turns an ECP2 into a byte array
         *
         * @param e the ECP2 to turn into bytes
         * @return a byte array representation of the ECP2
         */
        public static byte[] ToBytes(this ECP2 e)
        {
            byte[] ret = new byte[4 * FIELD_BYTES];
            e.ToBytes(ret);
            return ret;
        }

        /**
         * append appends a byte array to an existing byte array
         *
         * @param data     the data to which we want to append
         * @param toAppend the data to be appended
         * @return a new byte[] of data + toAppend
         */
        public static byte[] Append(this byte[] data, byte[] toAppend)
        {
            return data.Concat(toAppend).ToArray();
        }

        /**
         * append appends a boolean array to an existing byte array
         * @param data     the data to which we want to append
         * @param toAppend the data to be appended
         * @return a new byte[] of data + toAppend
         */
        public static byte[] Append(this byte[] data, bool[] toAppend)
        {
            return data.Concat(toAppend.Select(a => a ? (byte)1 : (byte)0)).ToArray();
        }

        /**
         * Returns an amcl.BN256.ECP on input of an ECP protobuf object.
         *
         * @param w a protobuf object representing an ECP
         * @return a ECP created from the protobuf object
         */
        public static ECP ToECP(this Protos.Idemix.ECP w)
        {
            byte[] valuex = w.X.ToByteArray();
            byte[] valuey = w.Y.ToByteArray();
            return new ECP(BIG.FromBytes(valuex), BIG.FromBytes(valuey));
        }

        /**
         * Returns an amcl.BN256.ECP2 on input of an ECP2 protobuf object.
         *
         * @param w a protobuf object representing an ECP2
         * @return a ECP2 created from the protobuf object
         */
        public static ECP2 ToECP2(this Protos.Idemix.ECP2 w)
        {
            byte[] valuexa = w.Xa.ToByteArray();
            byte[] valuexb = w.Xb.ToByteArray();
            byte[] valueya = w.Ya.ToByteArray();
            byte[] valueyb = w.Yb.ToByteArray();
            FP2 valuex = new FP2(BIG.FromBytes(valuexa), BIG.FromBytes(valuexb));
            FP2 valuey = new FP2(BIG.FromBytes(valueya), BIG.FromBytes(valueyb));
            return new ECP2(valuex, valuey);
        }

        /**
         * Converts an amcl.BN256.ECP2 into an ECP2 protobuf object.
         *
         * @param w an ECP2 to be transformed into a protobuf object
         * @return a protobuf representation of the ECP2
         */
        public static Protos.Idemix.ECP2 ToProto(this ECP2 w)
        {
            byte[] valueXA = new byte[FIELD_BYTES];
            byte[] valueXB = new byte[FIELD_BYTES];
            byte[] valueYA = new byte[FIELD_BYTES];
            byte[] valueYB = new byte[FIELD_BYTES];

            w.X.A.ToBytes(valueXA);
            w.X.B.ToBytes(valueXB);
            w.Y.A.ToBytes(valueYA);
            w.Y.B.ToBytes(valueYB);
            Protos.Idemix.ECP2 ecp2 = new Protos.Idemix.ECP2();
            ecp2.Xa = ByteString.CopyFrom(valueXA);
            ecp2.Xb = ByteString.CopyFrom(valueXB);
            ecp2.Ya = ByteString.CopyFrom(valueYA);
            ecp2.Yb = ByteString.CopyFrom(valueYB);
            return ecp2;
        }

        /**
         * Converts an amcl.BN256.ECP into an ECP protobuf object.
         *
         * @param w an ECP to be transformed into a protobuf object
         * @return a protobuf representation of the ECP
         */
        public static Protos.Idemix.ECP ToProto(this ECP w)
        {
            byte[] valueX = new byte[FIELD_BYTES];
            byte[] valueY = new byte[FIELD_BYTES];

            w.X.ToBytes(valueX);
            w.Y.ToBytes(valueY);
            Protos.Idemix.ECP ecp = new Protos.Idemix.ECP();
            ecp.X = ByteString.CopyFrom(valueX);
            ecp.Y = ByteString.CopyFrom(valueY);
            return ecp;
        }

        /**
         * Takes input BIGs a, b, m and returns a+b modulo m
         *
         * @param a the first BIG to add
         * @param b the second BIG to add
         * @param m the modulus
         * @return Returns a+b (mod m)
         */
        public static BIG ModAdd(this BIG a, BIG b, BIG m)
        {
            BIG c = a.Plus(b);
            c.Mod(m);
            return c;
        }

        /**
         * Modsub takes input BIGs a, b, m and returns a-b modulo m
         *
         * @param a the minuend of the modular subtraction
         * @param b the subtrahend of the modular subtraction
         * @param m the modulus
         * @return returns a-b (mod m)
         */
        public static BIG ModSub(this BIG a, BIG b, BIG m)
        {
            return a.ModAdd(BIG.ModNeg(b, m), m);
        }
    }
}