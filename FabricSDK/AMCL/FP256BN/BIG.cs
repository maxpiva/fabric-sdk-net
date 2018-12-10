/*
Licensed to the Apache Software Foundation (ASF) under one
or more contributor license agreements.  See the NOTICE file
distributed with this work for additional information
regarding copyright ownership.  The ASF licenses this file
to you under the Apache License, Version 2.0 (the
"License"); you may not use this file except in compliance
with the License.  You may obtain a copy of the License at

  http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing,
software distributed under the License is distributed on an
"AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
KIND, either express or implied.  See the License for the
specific language governing permissions and limitations
under the License.
*/

/* AMCL BIG number class */
// ReSharper disable All

using System;

#pragma warning disable 162
namespace Hyperledger.Fabric.SDK.AMCL.FP256BN
{
    public class BIG
    {
        public const int CHUNK = 64; // Set word size

        public const int MODBYTES = 32; //(1+(MODBITS-1)/8);
        public const int BASEBITS = 56;

        public static readonly int NLEN = 1 + (8 * MODBYTES - 1) / BASEBITS;
        public static readonly int DNLEN = 2 * NLEN;
        public static readonly long BMASK = ((long) 1 << BASEBITS) - 1;

        public static readonly int HBITS = BASEBITS / 2;
        public static readonly long HMASK = ((long) 1 << HBITS) - 1;
        public static readonly int NEXCESS = 1 << (CHUNK - BASEBITS - 1);
        public static readonly int BIGBITS = MODBYTES * 8;


        protected internal long[] w = new long[NLEN];

        /* Constructors */
        public BIG()
        {
            for (int i = 0; i < NLEN; i++)
            {
                w[i] = 0;
            }
        }

        public BIG(int x)
        {
            w[0] = x;
            for (int i = 1; i < NLEN; i++)
            {
                w[i] = 0;
            }
        }

        public BIG(BIG x)
        {
            for (int i = 0; i < NLEN; i++)
            {
                w[i] = x.w[i];
            }
        }

        public BIG(DBIG x)
        {
            for (int i = 0; i < NLEN; i++)
            {
                w[i] = x.w[i];
            }
        }

        public BIG(long[] x)
        {
            for (int i = 0; i < NLEN; i++)
            {
                w[i] = x[i];
            }
        }

        public virtual long Get(int i)
        {
            return w[i];
        }

        public virtual void Set(int i, long x)
        {
            w[i] = x;
        }


        /* Conditional swap of two bigs depending on d using XOR - no branches */
        public virtual void CSwap(BIG b, int d)
        {
            int i;
            long t, c = d;
            c = ~(c - 1);

            for (i = 0; i < NLEN; i++)
            {
                t = c & (w[i] ^ b.w[i]);
                w[i] ^= t;
                b.w[i] ^= t;
            }
        }

        public virtual void CMove(BIG g, int d)
        {
            int i;
            long b = -d;

            for (i = 0; i < NLEN; i++)
            {
                w[i] ^= (w[i] ^ g.w[i]) & b;
            }
        }

        public static long CastToChunk(int x)
        {
            return x;
        }

        /* normalise BIG - force all digits < 2^BASEBITS */
        public virtual long Norm()
        {
            long d, carry = 0;
            for (int i = 0; i < NLEN - 1; i++)
            {
                d = w[i] + carry;
                w[i] = d & BMASK;
                carry = d >> BASEBITS;
            }

            w[NLEN - 1] = w[NLEN - 1] + carry;
            return w[NLEN - 1] >> (8 * MODBYTES % BASEBITS);
        }

        /* return number of bits */
        public virtual int NBits()
        {
            BIG t = new BIG(this);
            int bts, k = NLEN - 1;
            long c;
            t.Norm();
            while (k >= 0 && t.w[k] == 0)
            {
                k--;
            }

            if (k < 0)
            {
                return 0;
            }

            bts = BASEBITS * k;
            c = t.w[k];
            while (c != 0)
            {
                c /= 2;
                bts++;
            }

            return bts;
        }

        public virtual string ToRawString()
        {
            BIG b = new BIG(this);
            string s = "(";
            for (int i = 0; i < NLEN - 1; i++)
            {
                s += b.w[i].ToString("x");
                s += ",";
            }

            s += b.w[NLEN - 1].ToString("x");
            s += ")";
            return s;
        }

        /* Convert to Hex String */
        public override string ToString()
        {
            BIG b;
            string s = "";
            int len = NBits();

            if (len % 4 == 0)
            {
                len /= 4;
            }
            else
            {
                len /= 4;
                len++;
            }

            if (len < MODBYTES * 2)
            {
                len = MODBYTES * 2;
            }

            for (int i = len - 1; i >= 0; i--)
            {
                b = new BIG(this);
                b.Shr(i * 4);
                s += (b.w[0] & 15).ToString("x");
            }

            return s;
        }

        /* set this[i]+=x*y+c, and return high part */

        public static long[] MulAdd(long a, long b, long c, long r)
        {
            long x0, x1, y0, y1;
            long[] tb = new long[2];
            x0 = a & HMASK;
            x1 = a >> HBITS;
            y0 = b & HMASK;
            y1 = b >> HBITS;
            long bot = x0 * y0;
            long top = x1 * y1;
            long mid = x0 * y1 + x1 * y0;
            x0 = mid & HMASK;
            x1 = mid >> HBITS;
            bot += x0 << HBITS;
            bot += c;
            bot += r;
            top += x1;
            long carry = bot >> BASEBITS;
            bot &= BMASK;
            top += carry;
            tb[0] = top;
            tb[1] = bot;
            return tb;
        }

        /* this*=x, where x is >NEXCESS */
        public virtual long PMul(int c)
        {
            long ak, carry = 0;
            long[] cr;

            for (int i = 0; i < NLEN; i++)
            {
                ak = w[i];
                w[i] = 0;

                cr = MulAdd(ak, c, carry, w[i]);
                carry = cr[0];
                w[i] = cr[1];
            }

            return carry;
        }

        /* return this*c and catch overflow in DBIG */
        public virtual DBIG PXMul(int c)
        {
            DBIG m = new DBIG(0);
            long[] cr;
            long carry = 0;
            for (int j = 0; j < NLEN; j++)
            {
                cr = MulAdd(w[j], (long) c, carry, m.w[j]);
                carry = cr[0];
                m.w[j] = cr[1];
            }

            m.w[NLEN] = carry;
            return m;
        }

        /* divide by 3 */
        public virtual int Div3()
        {
            long ak, @base, carry = 0;
            Norm();
            @base = (long) 1 << BASEBITS;
            for (int i = NLEN - 1; i >= 0; i--)
            {
                ak = carry * @base + w[i];
                w[i] = ak / 3;
                carry = ak % 3;
            }

            return (int) carry;
        }

        /* return a*b where result fits in a BIG */
        public static BIG SMul(BIG a, BIG b)
        {
            long carry;
            long[] cr = new long[2];
            BIG c = new BIG(0);
            for (int i = 0; i < NLEN; i++)
            {
                carry = 0;
                for (int j = 0; j < NLEN; j++)
                {
                    if (i + j < NLEN)
                    {
                        cr = MulAdd(a.w[i], b.w[j], carry, c.w[i + j]);
                        carry = cr[0];
                        c.w[i + j] = cr[1];
                    }
                }
            }

            return c;
        }

        /* return a*b as DBIG */
        /* Inputs must be normed */
        public static DBIG Mul(BIG a, BIG b)
        {
            DBIG c = new DBIG(0);
            long carry;
            long[] cr = new long[2];

            for (int i = 0; i < NLEN; i++)
            {
                carry = 0;
                for (int j = 0; j < NLEN; j++)
                {
                    cr = MulAdd(a.w[i], b.w[j], carry, c.w[i + j]);
                    carry = cr[0];
                    c.w[i + j] = cr[1];
                }

                c.w[NLEN + i] = carry;
            }

            return c;
        }

        /* return a^2 as DBIG */
        /* Input must be normed */
        public static DBIG Sqr(BIG a)
        {
            DBIG c = new DBIG(0);
            long carry;
            long[] cr = new long[2];

            for (int i = 0; i < NLEN; i++)
            {
                carry = 0;
                for (int j = i + 1; j < NLEN; j++)
                {
                    cr = MulAdd(2 * a.w[i], a.w[j], carry, c.w[i + j]);
                    carry = cr[0];
                    c.w[i + j] = cr[1];
                }

                c.w[NLEN + i] = carry;
            }

            for (int i = 0; i < NLEN; i++)
            {
                cr = MulAdd(a.w[i], a.w[i], 0, c.w[2 * i]);
                c.w[2 * i + 1] += cr[0];
                c.w[2 * i] = cr[1];
            }

            c.Norm();
            return c;
        }

        internal static BIG Monty(BIG md, long MC, DBIG d)
        {
            BIG b;
            long m, carry;
            long[] cr = new long[2];
            for (int i = 0; i < NLEN; i++)
            {
                if (MC == -1)
                {
                    m = -d.w[i] & BMASK;
                }
                else
                {
                    if (MC == 1)
                    {
                        m = d.w[i];
                    }
                    else
                    {
                        m = (MC * d.w[i]) & BMASK;
                    }
                }

                carry = 0;
                for (int j = 0; j < NLEN; j++)
                {
                    cr = MulAdd(m, md.w[j], carry, d.w[i + j]);
                    carry = cr[0];
                    d.w[i + j] = cr[1];
                }

                d.w[NLEN + i] += carry;
            }

            b = new BIG(0);
            for (int i = 0; i < NLEN; i++)
            {
                b.w[i] = d.w[NLEN + i];
            }

            b.Norm();
            return b;
        }


        /// <summary>
        ///     *************************************************************************
        /// </summary>
        public virtual void XorTop(long x)
        {
            w[NLEN - 1] ^= x;
        }

        /* set x = x mod 2^m */
        public virtual void Mod2m(int m)
        {
            int i, wd, bt;
            wd = m / BASEBITS;
            bt = m % BASEBITS;
            w[wd] &= (CastToChunk(1) << bt) - 1;
            for (i = wd + 1; i < NLEN; i++)
            {
                w[i] = 0;
            }
        }

        /* return n-th bit */
        public virtual int Bit(int n)
        {
            if ((w[n / BASEBITS] & (CastToChunk(1) << (n % BASEBITS))) > 0)
            {
                return 1;
            }
            else
            {
                return 0;
            }
        }

        /* Shift right by less than a word */
        public virtual int FShr(int k)
        {
            int r = (int) (w[0] & ((CastToChunk(1) << k) - 1)); // shifted out part
            for (int i = 0; i < NLEN - 1; i++)
            {
                w[i] = (w[i] >> k) | ((w[i + 1] << (BASEBITS - k)) & BMASK);
            }

            w[NLEN - 1] = w[NLEN - 1] >> k;
            return r;
        }

        /* Shift right by less than a word */
        public virtual int FShl(int k)
        {
            w[NLEN - 1] = (w[NLEN - 1] << k) | (w[NLEN - 2] >> (BASEBITS - k));
            for (int i = NLEN - 2; i > 0; i--)
            {
                w[i] = ((w[i] << k) & BMASK) | (w[i - 1] >> (BASEBITS - k));
            }

            w[0] = (w[0] << k) & BMASK;
            return (int) (w[NLEN - 1] >> (8 * MODBYTES % BASEBITS)); // return excess - only used in FF.java
        }

        /* test for zero */
        public virtual bool IsZilch()
        {
            for (int i = 0; i < NLEN; i++)
            {
                if (w[i] != 0)
                {
                    return false;
                }
            }

            return true;
        }

        /* set to zero */
        public virtual void Zero()
        {
            for (int i = 0; i < NLEN; i++)
            {
                w[i] = 0;
            }
        }

        /* set to one */
        public virtual void One()
        {
            w[0] = 1;
            for (int i = 1; i < NLEN; i++)
            {
                w[i] = 0;
            }
        }

        /* Test for equal to one */
        public virtual bool IsUnity()
        {
            for (int i = 1; i < NLEN; i++)
            {
                if (w[i] != 0)
                {
                    return false;
                }
            }

            if (w[0] != 1)
            {
                return false;
            }

            return true;
        }

        /* Copy from another BIG */
        public virtual void Copy(BIG x)
        {
            for (int i = 0; i < NLEN; i++)
            {
                w[i] = x.w[i];
            }
        }

        public virtual void Copy(DBIG x)
        {
            for (int i = 0; i < NLEN; i++)
            {
                w[i] = x.w[i];
            }
        }

        /* general shift right */
        public virtual void Shr(int k)
        {
            int n = k % BASEBITS;
            int m = k / BASEBITS;
            for (int i = 0; i < NLEN - m - 1; i++)
            {
                w[i] = (w[m + i] >> n) | ((w[m + i + 1] << (BASEBITS - n)) & BMASK);
            }

            if (NLEN > m)
            {
                w[NLEN - m - 1] = w[NLEN - 1] >> n;
            }

            for (int i = NLEN - m; i < NLEN; i++)
            {
                w[i] = 0;
            }
        }

        /* general shift left */
        public virtual void Shl(int k)
        {
            int n = k % BASEBITS;
            int m = k / BASEBITS;

            w[NLEN - 1] = w[NLEN - 1 - m] << n;
            if (NLEN >= m + 2)
            {
                w[NLEN - 1] |= w[NLEN - m - 2] >> (BASEBITS - n);
            }

            for (int i = NLEN - 2; i > m; i--)
            {
                w[i] = ((w[i - m] << n) & BMASK) | (w[i - m - 1] >> (BASEBITS - n));
            }

            w[m] = (w[0] << n) & BMASK;
            for (int i = 0; i < m; i++)
            {
                w[i] = 0;
            }
        }

        /* return this+x */
        public virtual BIG Plus(BIG x)
        {
            BIG s = new BIG(0);
            for (int i = 0; i < NLEN; i++)
            {
                s.w[i] = w[i] + x.w[i];
            }

            return s;
        }

        /* this+=x */
        public virtual void Add(BIG x)
        {
            for (int i = 0; i < NLEN; i++)
            {
                w[i] += x.w[i];
            }
        }

        /* this|=x */
        public virtual void Or(BIG x)
        {
            for (int i = 0; i < NLEN; i++)
            {
                w[i] |= x.w[i];
            }
        }


        /* this+=x, where x is int */
        public virtual void Inc(int x)
        {
            Norm();
            w[0] += x;
        }

        /* this+=x, where x is long */
        public virtual void Incl(long x)
        {
            Norm();
            w[0] += x;
        }

        /* return this.x */
        public virtual BIG Minus(BIG x)
        {
            BIG d = new BIG(0);
            for (int i = 0; i < NLEN; i++)
            {
                d.w[i] = w[i] - x.w[i];
            }

            return d;
        }

        /* this-=x */
        public virtual void Sub(BIG x)
        {
            for (int i = 0; i < NLEN; i++)
            {
                w[i] -= x.w[i];
            }
        }

        /* reverse subtract this=x-this */
        public virtual void RSub(BIG x)
        {
            for (int i = 0; i < NLEN; i++)
            {
                w[i] = x.w[i] - w[i];
            }
        }

        /* this-=x where x is int */
        public virtual void Dec(int x)
        {
            Norm();
            w[0] -= x;
        }

        /* this*=x, where x is small int<NEXCESS */
        public virtual void IMul(int c)
        {
            for (int i = 0; i < NLEN; i++)
            {
                w[i] *= c;
            }
        }

        /* convert this BIG to byte array */
        public virtual void ToByteArray(sbyte[] b, int n)
        {
            BIG c = new BIG(this);
            c.Norm();

            for (int i = MODBYTES - 1; i >= 0; i--)
            {
                b[i + n] = (sbyte) c.w[0];
                c.FShr(8);
            }
        }

        /* convert from byte array to BIG */
        public static BIG FromByteArray(sbyte[] b, int n)
        {
            BIG m = new BIG(0);

            for (int i = 0; i < MODBYTES; i++)
            {
                m.FShl(8);
                m.w[0] += (int) b[i + n] & 0xff;
                //m.inc((int)b[i]&0xff);
            }

            return m;
        }

        public virtual void ToBytes(sbyte[] b)
        {
            ToByteArray(b, 0);
        }
        public virtual void ToBytes(byte[] b)
        {
            ToByteArray((sbyte[])(Array)b, 0);
        }

        public static BIG FromBytes(sbyte[] b)
        {
            return FromByteArray(b, 0);
        }
        public static BIG FromBytes(byte[] b)
        {
            return FromByteArray((sbyte[])(Array)b, 0);
        }
        /* Compare a and b, return 0 if a==b, -1 if a<b, +1 if a>b. Inputs must be normalised */
        public static int Comp(BIG a, BIG b)
        {
            for (int i = NLEN - 1; i >= 0; i--)
            {
                if (a.w[i] == b.w[i])
                {
                    continue;
                }

                if (a.w[i] > b.w[i])
                {
                    return 1;
                }
                else
                {
                    return -1;
                }
            }

            return 0;
        }

        /* Arazi and Qi inversion mod 256 */
        public static int InvMod256(int a)
        {
            int U, t1, t2, b, c;
            t1 = 0;
            c = (a >> 1) & 1;
            t1 += c;
            t1 &= 1;
            t1 = 2 - t1;
            t1 <<= 1;
            U = t1 + 1;

            // i=2
            b = a & 3;
            t1 = U * b;
            t1 >>= 2;
            c = (a >> 2) & 3;
            t2 = (U * c) & 3;
            t1 += t2;
            t1 *= U;
            t1 &= 3;
            t1 = 4 - t1;
            t1 <<= 2;
            U += t1;

            // i=4
            b = a & 15;
            t1 = U * b;
            t1 >>= 4;
            c = (a >> 4) & 15;
            t2 = (U * c) & 15;
            t1 += t2;
            t1 *= U;
            t1 &= 15;
            t1 = 16 - t1;
            t1 <<= 4;
            U += t1;

            return U;
        }

        /* a=1/a mod 2^256. This is very fast! */
        public virtual void InvMod2m()
        {
            int i;
            BIG U = new BIG(0);
            BIG b = new BIG(0);
            BIG c = new BIG(0);

            U.Inc(InvMod256(LastBits(8)));

            for (i = 8; i < BIGBITS; i <<= 1)
            {
                U.Norm();
                b.Copy(this);
                b.Mod2m(i);
                BIG t1 = SMul(U, b);
                t1.Shr(i);

                c.Copy(this);
                c.Shr(i);
                c.Mod2m(i);
                BIG t2 = SMul(U, c);
                t2.Mod2m(i);

                t1.Add(t2);
                t1.Norm();
                b = SMul(t1, U);
                t1.Copy(b);
                t1.Mod2m(i);

                t2.One();
                t2.Shl(i);
                t1.RSub(t2);
                t1.Norm();

                t1.Shl(i);
                U.Add(t1);
            }

            U.Mod2m(BIGBITS);
            Copy(U);
            Norm();
        }

        /* reduce this mod m */
        public virtual void Mod(BIG m1)
        {
            int k = 0;
            BIG r = new BIG(0);
            BIG m = new BIG(m1);

            Norm();
            if (Comp(this, m) < 0)
            {
                return;
            }

            do
            {
                m.FShl(1);
                k++;
            } while (Comp(this, m) >= 0);

            while (k > 0)
            {
                m.FShr(1);

                r.Copy(this);
                r.Sub(m);
                r.Norm();
                CMove(r, (int) (1 - ((r.w[NLEN - 1] >> (CHUNK - 1)) & 1)));
                k--;
            }
        }

        /* divide this by m */
        public virtual void Div(BIG m1)
        {
            int d, k = 0;
            Norm();
            BIG e = new BIG(1);
            BIG m = new BIG(m1);
            BIG b = new BIG(this);
            BIG r = new BIG(0);
            Zero();

            while (Comp(b, m) >= 0)
            {
                e.FShl(1);
                m.FShl(1);
                k++;
            }

            while (k > 0)
            {
                m.FShr(1);
                e.FShr(1);

                r.Copy(b);
                r.Sub(m);
                r.Norm();
                d = (int) (1 - ((r.w[NLEN - 1] >> (CHUNK - 1)) & 1));
                b.CMove(r, d);
                r.Copy(this);
                r.Add(e);
                r.Norm();
                CMove(r, d);
                k--;
            }
        }

        /* return parity */
        public virtual int Parity()
        {
            return (int) (w[0] % 2);
        }

        /* return n last bits */
        public virtual int LastBits(int n)
        {
            int msk = (1 << n) - 1;
            Norm();
            return (int) w[0] & msk;
        }

        /* get 8*MODBYTES size random number */
        public static BIG Random(RAND rng)
        {
            BIG m = new BIG(0);
            int i, b, j = 0, r = 0;

            /* generate random BIG */
            for (i = 0; i < 8 * MODBYTES; i++)
            {
                if (j == 0)
                {
                    r = rng.Byte;
                }
                else
                {
                    r >>= 1;
                }

                b = r & 1;
                m.Shl(1);
                m.w[0] += b; // m.inc(b);
                j++;
                j &= 7;
            }

            return m;
        }

        /* Create random BIG in portable way, one bit at a time */
        public static BIG RandomNum(BIG q, RAND rng)
        {
            DBIG d = new DBIG(0);
            int i, b, j = 0, r = 0;
            for (i = 0; i < 2 * q.NBits(); i++)
            {
                if (j == 0)
                {
                    r = rng.Byte;
                }
                else
                {
                    r >>= 1;
                }

                b = r & 1;
                d.Shl(1);
                d.w[0] += b; // m.inc(b);
                j++;
                j &= 7;
            }

            BIG m = d.Mod(q);
            return m;
        }

        /* return a*b mod m */
        public static BIG ModMul(BIG a1, BIG b1, BIG m)
        {
            BIG a = new BIG(a1);
            BIG b = new BIG(b1);
            a.Mod(m);
            b.Mod(m);
            DBIG d = Mul(a, b);
            return d.Mod(m);
        }

        /* return a^2 mod m */
        public static BIG ModSqr(BIG a1, BIG m)
        {
            BIG a = new BIG(a1);
            a.Mod(m);
            DBIG d = Sqr(a);
            return d.Mod(m);
        }

        /* return -a mod m */
        public static BIG ModNeg(BIG a1, BIG m)
        {
            BIG a = new BIG(a1);
            a.Mod(m);
            return m.Minus(a);
        }

        /* return this^e mod m */
        public virtual BIG PowMod(BIG e1, BIG m)
        {
            BIG e = new BIG(e1);
            int bt;
            Norm();
            e.Norm();
            BIG a = new BIG(1);
            BIG z = new BIG(e);
            BIG s = new BIG(this);
            while (true)
            {
                bt = z.Parity();
                z.FShr(1);
                if (bt == 1)
                {
                    a = ModMul(a, s, m);
                }

                if (z.IsZilch())
                {
                    break;
                }

                s = ModSqr(s, m);
            }

            return a;
        }

        /* Jacobi Symbol (this/p). Returns 0, 1 or -1 */
        public virtual int Jacobi(BIG p)
        {
            int n8, k, m = 0;
            BIG t = new BIG(0);
            BIG x = new BIG(0);
            BIG n = new BIG(0);
            BIG zilch = new BIG(0);
            BIG one = new BIG(1);
            if (p.Parity() == 0 || Comp(this, zilch) == 0 || Comp(p, one) <= 0)
            {
                return 0;
            }

            Norm();
            x.Copy(this);
            n.Copy(p);
            x.Mod(p);

            while (Comp(n, one) > 0)
            {
                if (Comp(x, zilch) == 0)
                {
                    return 0;
                }

                n8 = n.LastBits(3);
                k = 0;
                while (x.Parity() == 0)
                {
                    k++;
                    x.Shr(1);
                }

                if (k % 2 == 1)
                {
                    m += (n8 * n8 - 1) / 8;
                }

                m += (n8 - 1) * (x.LastBits(2) - 1) / 4;
                t.Copy(n);
                t.Mod(x);
                n.Copy(x);
                x.Copy(t);
                m %= 2;
            }

            if (m == 0)
            {
                return 1;
            }
            else
            {
                return -1;
            }
        }

        /* this=1/this mod p. Binary method */
        public virtual void InvModp(BIG p)
        {
            Mod(p);
            BIG u = new BIG(this);
            BIG v = new BIG(p);
            BIG x1 = new BIG(1);
            BIG x2 = new BIG(0);
            BIG t = new BIG(0);
            BIG one = new BIG(1);

            while (Comp(u, one) != 0 && Comp(v, one) != 0)
            {
                while (u.Parity() == 0)
                {
                    u.FShr(1);
                    if (x1.Parity() != 0)
                    {
                        x1.Add(p);
                        x1.Norm();
                    }

                    x1.FShr(1);
                }

                while (v.Parity() == 0)
                {
                    v.FShr(1);
                    if (x2.Parity() != 0)
                    {
                        x2.Add(p);
                        x2.Norm();
                    }

                    x2.FShr(1);
                }

                if (Comp(u, v) >= 0)
                {
                    u.Sub(v);
                    u.Norm();
                    if (Comp(x1, x2) >= 0)
                    {
                        x1.Sub(x2);
                    }
                    else
                    {
                        t.Copy(p);
                        t.Sub(x2);
                        x1.Add(t);
                    }

                    x1.Norm();
                }
                else
                {
                    v.Sub(u);
                    v.Norm();
                    if (Comp(x2, x1) >= 0)
                    {
                        x2.Sub(x1);
                    }
                    else
                    {
                        t.Copy(p);
                        t.Sub(x1);
                        x2.Add(t);
                    }

                    x2.Norm();
                }
            }

            if (Comp(u, one) == 0)
            {
                Copy(x1);
            }
            else
            {
                Copy(x2);
            }
        }
    }
}