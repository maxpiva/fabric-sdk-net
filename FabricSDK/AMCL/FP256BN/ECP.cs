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

/* Elliptic Curve Point class */
// ReSharper disable All

using System;

#pragma warning disable 162
namespace Hyperledger.Fabric.SDK.AMCL.FP256BN
{
    public sealed class ECP
    {
        public const int WEIERSTRASS = 0;
        public const int EDWARDS = 1;
        public const int MONTGOMERY = 2;
        public const int NOT = 0;
        public const int BN = 1;
        public const int BLS = 2;
        public const int D_TYPE = 0;
        public const int M_TYPE = 1;
        public const int POSITIVEX = 0;
        public const int NEGATIVEX = 1;

        public const int CURVETYPE = WEIERSTRASS;
        public const int CURVE_PAIRING_TYPE = BN;
        public const int SEXTIC_TWIST = M_TYPE;
        public const int SIGN_OF_X = NEGATIVEX;

        public const int SHA256 = 32;
        public const int SHA384 = 48;
        public const int SHA512 = 64;

        public const int HASH_TYPE = 32;
        public const int AESKEY = 16;

        private readonly FP x;
        private readonly FP y;

        private readonly FP z;
        //	private boolean INF;

        /* Constructor - set to O */
        public ECP()
        {
            //INF=true;
            x = new FP(0);
            y = new FP(1);
            if (CURVETYPE == EDWARDS)
            {
                z = new FP(1);
            }
            else
            {
                z = new FP(0);
            }
        }

        public ECP(ECP e)
        {
            x = new FP(e.x);
            y = new FP(e.y);
            z = new FP(e.z);
        }

        /* set (x,y) from two BIGs */
        public ECP(BIG ix, BIG iy)
        {
            x = new FP(ix);
            y = new FP(iy);
            z = new FP(1);
            FP rhs = RHS(x);

            if (CURVETYPE == MONTGOMERY)
            {
                if (rhs.Jacobi() != 1)
                {
                    Inf();
                }

                //if (rhs.jacobi()==1) INF=false;
                //else inf();
            }
            else
            {
                FP y2 = new FP(y);
                y2.Sqr();
                if (!y2.Equals(rhs))
                {
                    Inf();
                }

                //if (y2.equals(rhs)) INF=false;
                //else inf();
            }
        }

        /* set (x,y) from BIG and a bit */
        public ECP(BIG ix, int s)
        {
            x = new FP(ix);
            FP rhs = RHS(x);
            y = new FP(0);
            z = new FP(1);
            if (rhs.Jacobi() == 1)
            {
                FP ny = rhs.Sqrt();
                if (ny.Redc().Parity() != s)
                {
                    ny.Neg();
                }

                y.Copy(ny);
                //INF=false;
            }
            else
            {
                Inf();
            }
        }

        /* set from x - calculate y from curve equation */
        public ECP(BIG ix)
        {
            x = new FP(ix);
            FP rhs = RHS(x);
            y = new FP(0);
            z = new FP(1);
            if (rhs.Jacobi() == 1)
            {
                if (CURVETYPE != MONTGOMERY)
                {
                    y.Copy(rhs.Sqrt());
                }

                //INF=false;
            }
            else
            {
                Inf(); //INF=true;
            }
        }

        /* extract x as a BIG */
        public BIG X
        {
            get
            {
                ECP W = new ECP(this);
                W.Affine();
                return W.x.Redc();
            }
        }

        /* extract y as a BIG */
        public BIG Y
        {
            get
            {
                ECP W = new ECP(this);
                W.Affine();
                return W.y.Redc();
            }
        }

        /* get sign of Y */
        public int S
        {
            get
            {
                //affine();
                BIG y = Y;
                return y.Parity();
            }
        }

        /* test for O point-at-infinity */
        public bool IsInfinity()
        {
            //		if (INF) return true;                            // Edits made
            if (CURVETYPE == EDWARDS)
            {
                return x.IsZilch() && y.Equals(z);
            }

            if (CURVETYPE == WEIERSTRASS)
            {
                return x.IsZilch() && z.IsZilch();
            }

            if (CURVETYPE == MONTGOMERY)
            {
                return z.IsZilch();
            }

            return true;
        }

        /* Conditional swap of P and Q dependant on d */
        private void CSwap(ECP Q, int d)
        {
            x.CSwap(Q.x, d);
            if (CURVETYPE != MONTGOMERY)
            {
                y.CSwap(Q.y, d);
            }

            z.CSwap(Q.z, d);
            //	if (CURVETYPE!=EDWARDS)
            //	{
            //		boolean bd;
            //		if (d==0) bd=false;
            //		else bd=true;
            //		bd=bd&(INF^Q.INF);
            //		INF^=bd;
            //		Q.INF^=bd;
            //	}
        }

        /* Conditional move of Q to P dependant on d */
        private void CMove(ECP Q, int d)
        {
            x.CMove(Q.x, d);
            if (CURVETYPE != MONTGOMERY)
            {
                y.CMove(Q.y, d);
            }

            z.CMove(Q.z, d);
            //	if (CURVETYPE!=EDWARDS)
            //	{
            //		boolean bd;
            //		if (d==0) bd=false;
            //		else bd=true;
            //		INF^=(INF^Q.INF)&bd;
            //	}
        }

        /* return 1 if b==c, no branching */
        private static int Teq(int b, int c)
        {
            int x = b ^ c;
            x -= 1; // if x=0, x now -1
            return (x >> 31) & 1;
        }

        /* Constant time select from pre-computed table */
        private void Select(ECP[] W, int b)
        {
            ECP MP = new ECP();
            int m = b >> 31;
            int babs = (b ^ m) - m;

            babs = (babs - 1) / 2;
            CMove(W[0], Teq(babs, 0)); // conditional move
            CMove(W[1], Teq(babs, 1));
            CMove(W[2], Teq(babs, 2));
            CMove(W[3], Teq(babs, 3));
            CMove(W[4], Teq(babs, 4));
            CMove(W[5], Teq(babs, 5));
            CMove(W[6], Teq(babs, 6));
            CMove(W[7], Teq(babs, 7));

            MP.Copy(this);
            MP.Neg();
            CMove(MP, (int) (m & 1));
        }

        /* Test P == Q */
        public bool Equals(ECP Q)
        {
            //		if (is_infinity() && Q.is_infinity()) return true;
            //		if (is_infinity() || Q.is_infinity()) return false;

            FP a = new FP(0); // Edits made
            FP b = new FP(0);
            a.Copy(x);
            a.Mul(Q.z);
            b.Copy(Q.x);
            b.Mul(z);
            if (!a.Equals(b))
            {
                return false;
            }

            if (CURVETYPE != MONTGOMERY)
            {
                a.Copy(y);
                a.Mul(Q.z);
                b.Copy(Q.y);
                b.Mul(z);
                if (!a.Equals(b))
                {
                    return false;
                }
            }

            return true;
        }

        /* this=P */
        public void Copy(ECP P)
        {
            x.Copy(P.x);
            if (CURVETYPE != MONTGOMERY)
            {
                y.Copy(P.y);
            }

            z.Copy(P.z);
            //INF=P.INF;
        }

        /* this=-this */
        public void Neg()
        {
            //		if (is_infinity()) return;
            if (CURVETYPE == WEIERSTRASS)
            {
                y.Neg();
                y.Norm();
            }

            if (CURVETYPE == EDWARDS)
            {
                x.Neg();
                x.Norm();
            }

            return;
        }

        /* set this=O */
        public void Inf()
        {
            //		INF=true;
            x.Zero();
            if (CURVETYPE != MONTGOMERY)
            {
                y.One();
            }

            if (CURVETYPE != EDWARDS)
            {
                z.Zero();
            }
            else
            {
                z.One();
            }
        }

        /* Calculate RHS of curve equation */
        public static FP RHS(FP x)
        {
            x.Norm();
            FP r = new FP(x);
            r.Sqr();

            if (CURVETYPE == WEIERSTRASS)
            {
                // x^3+Ax+B
                FP b = new FP(new BIG(ROM.CURVE_B));
                r.Mul(x);
                if (ROM.CURVE_A == -3)
                {
                    FP cx = new FP(x);
                    cx.IMul(3);
                    cx.Neg();
                    cx.Norm();
                    r.Add(cx);
                }

                r.Add(b);
            }

            if (CURVETYPE == EDWARDS)
            {
                // (Ax^2-1)/(Bx^2-1)
                FP b = new FP(new BIG(ROM.CURVE_B));

                FP one = new FP(1);
                b.Mul(r);
                b.Sub(one);
                b.Norm();
                if (ROM.CURVE_A == -1)
                {
                    r.Neg();
                }

                r.Sub(one);
                r.Norm();
                b.Inverse();

                r.Mul(b);
            }

            if (CURVETYPE == MONTGOMERY)
            {
                // x^3+Ax^2+x
                FP x3 = new FP(0);
                x3.Copy(r);
                x3.Mul(x);
                r.IMul(ROM.CURVE_A);
                r.Add(x3);
                r.Add(x);
            }

            r.Reduce();
            return r;
        }

        /* set to affine - from (x,y,z) to (x,y) */
        public void Affine()
        {
            if (IsInfinity())
            {
                return;
            }

            FP one = new FP(1);
            if (z.Equals(one))
            {
                return;
            }

            z.Inverse();
            x.Mul(z);
            x.Reduce();
            if (CURVETYPE != MONTGOMERY) // Edits made
            {
                y.Mul(z);
                y.Reduce();
            }

            z.Copy(one);
        }

        /* extract x as an FP */
        public FP GetX()
        {
            return x;
        }

        /* extract y as an FP */
        public FP GetY()
        {
            return y;
        }

        /* extract z as an FP */
        public FP GetZ()
        {
            return z;
        }

        public void ToBytes(byte[] b, bool compress)
        {
            ToBytes((sbyte[])(Array)b,compress);
        }
        /* convert to byte array */
        public void ToBytes(sbyte[] b, bool compress)
        {
            sbyte[] t = new sbyte[BIG.MODBYTES];
            ECP W = new ECP(this);
            W.Affine();

            W.x.Redc().ToBytes(t);
            for (int i = 0; i < BIG.MODBYTES; i++)
            {
                b[i + 1] = t[i];
            }

            if (CURVETYPE == MONTGOMERY)
            {
                b[0] = 0x06;
                return;
            }

            if (compress)
            {
                b[0] = 0x02;
                if (y.Redc().Parity() == 1)
                {
                    b[0] = 0x03;
                }
                
                return;
            }

            b[0] = 0x04;

            W.y.Redc().ToBytes(t);
            for (int i = 0; i < BIG.MODBYTES; i++)
            {
                b[i + BIG.MODBYTES + 1] = t[i];
            }
        }


        /* convert from byte array to point */
        public static ECP FromBytes(sbyte[] b)
        {
            sbyte[] t = new sbyte[BIG.MODBYTES];
            BIG p = new BIG(ROM.Modulus);

            for (int i = 0; i < BIG.MODBYTES; i++)
            {
                t[i] = b[i + 1];
            }

            BIG px = BIG.FromBytes(t);
            if (BIG.Comp(px, p) >= 0)
            {
                return new ECP();
            }

            if (CURVETYPE == MONTGOMERY)
            {
                return new ECP(px);
            }

            if (b[0] == 0x04)
            {
                for (int i = 0; i < BIG.MODBYTES; i++)
                {
                    t[i] = b[i + BIG.MODBYTES + 1];
                }

                BIG py = BIG.FromBytes(t);
                if (BIG.Comp(py, p) >= 0)
                {
                    return new ECP();
                }

                return new ECP(px, py);
            }

            if (b[0] == 0x02 || b[0] == 0x03)
            {
                return new ECP(px, (int) (b[0] & 1));
            }

            return new ECP();
        }

        /* convert to hex string */
        public override string ToString()
        {
            ECP W = new ECP(this);
            W.Affine();
            if (W.IsInfinity())
            {
                return "infinity";
            }

            if (CURVETYPE == MONTGOMERY)
            {
                return "(" + W.x.Redc().ToString() + ")";
            }
            else
            {
                return "(" + W.x.Redc().ToString() + "," + W.y.Redc().ToString() + ")";
            }
        }

        /* convert to hex string */
        public string ToRawString()
        {
            //if (is_infinity()) return "infinity";
            //affine();
            ECP W = new ECP(this);
            if (CURVETYPE == MONTGOMERY)
            {
                return "(" + W.x.Redc().ToString() + "," + W.z.Redc().ToString() + ")";
            }
            else
            {
                return "(" + W.x.Redc().ToString() + "," + W.y.Redc().ToString() + "," + W.z.Redc().ToString() + ")";
            }
        }

        /* this*=2 */
        public void Dbl()
        {
            //		if (INF) return;

            if (CURVETYPE == WEIERSTRASS)
            {
                if (ROM.CURVE_A == 0)
                {
                    //System.out.println("Into dbl");
                    FP t0 = new FP(y); // Edits made
                    t0.Sqr();
                    FP t1 = new FP(y);
                    t1.Mul(z);
                    FP t2 = new FP(z);
                    t2.Sqr();

                    z.Copy(t0);
                    z.Add(t0);
                    z.Norm();
                    z.Add(z);
                    z.Add(z);
                    z.Norm();
                    t2.IMul(3 * ROM.CURVE_B_I);

                    FP x3 = new FP(t2);
                    x3.Mul(z);

                    FP y3 = new FP(t0);
                    y3.Add(t2);
                    y3.Norm();
                    z.Mul(t1);
                    t1.Copy(t2);
                    t1.Add(t2);
                    t2.Add(t1);
                    t0.Sub(t2);
                    t0.Norm();
                    y3.Mul(t0);
                    y3.Add(x3);
                    t1.Copy(x);
                    t1.Mul(y);
                    x.Copy(t0);
                    x.Norm();
                    x.Mul(t1);
                    x.Add(x);
                    x.Norm();
                    y.Copy(y3);
                    y.Norm();
                    //System.out.println("Out of dbl");
                }
                else
                {
                    FP t0 = new FP(x);
                    FP t1 = new FP(y);
                    FP t2 = new FP(z);
                    FP t3 = new FP(x);
                    FP z3 = new FP(z);
                    FP y3 = new FP(0);
                    FP x3 = new FP(0);
                    FP b = new FP(0);

                    if (ROM.CURVE_B_I == 0)
                    {
                        b.Copy(new FP(new BIG(ROM.CURVE_B)));
                    }

                    t0.Sqr(); //1    x^2
                    t1.Sqr(); //2    y^2
                    t2.Sqr(); //3

                    t3.Mul(y); //4
                    t3.Add(t3);
                    t3.Norm(); //5
                    z3.Mul(x); //6
                    z3.Add(z3);
                    z3.Norm(); //7
                    y3.Copy(t2);

                    if (ROM.CURVE_B_I == 0)
                    {
                        y3.Mul(b); //8
                    }
                    else
                    {
                        y3.IMul(ROM.CURVE_B_I);
                    }

                    y3.Sub(z3); //y3.norm(); //9  ***
                    x3.Copy(y3);
                    x3.Add(y3);
                    x3.Norm(); //10

                    y3.Add(x3); //y3.norm();//11
                    x3.Copy(t1);
                    x3.Sub(y3);
                    x3.Norm(); //12
                    y3.Add(t1);
                    y3.Norm(); //13
                    y3.Mul(x3); //14
                    x3.Mul(t3); //15
                    t3.Copy(t2);
                    t3.Add(t2); //t3.norm(); //16
                    t2.Add(t3); //t2.norm(); //17

                    if (ROM.CURVE_B_I == 0)
                    {
                        z3.Mul(b); //18
                    }
                    else
                    {
                        z3.IMul(ROM.CURVE_B_I);
                    }

                    z3.Sub(t2); //z3.norm();//19
                    z3.Sub(t0);
                    z3.Norm(); //20  ***
                    t3.Copy(z3);
                    t3.Add(z3); //t3.norm();//21

                    z3.Add(t3);
                    z3.Norm(); //22
                    t3.Copy(t0);
                    t3.Add(t0); //t3.norm(); //23
                    t0.Add(t3); //t0.norm();//24
                    t0.Sub(t2);
                    t0.Norm(); //25

                    t0.Mul(z3); //26
                    y3.Add(t0); //y3.norm();//27
                    t0.Copy(y);
                    t0.Mul(z); //28
                    t0.Add(t0);
                    t0.Norm(); //29
                    z3.Mul(t0); //30
                    x3.Sub(z3); //x3.norm();//31
                    t0.Add(t0);
                    t0.Norm(); //32
                    t1.Add(t1);
                    t1.Norm(); //33
                    z3.Copy(t0);
                    z3.Mul(t1); //34

                    x.Copy(x3);
                    x.Norm();
                    y.Copy(y3);
                    y.Norm();
                    z.Copy(z3);
                    z.Norm();
                }
            }

            if (CURVETYPE == EDWARDS)
            {
                //System.out.println("Into dbl");
                FP C = new FP(x);
                FP D = new FP(y);
                FP H = new FP(z);
                FP J = new FP(0);

                x.Mul(y);
                x.Add(x);
                x.Norm();
                C.Sqr();
                D.Sqr();

                if (ROM.CURVE_A == -1)
                {
                    C.Neg();
                }

                y.Copy(C);
                y.Add(D);
                y.Norm();
                H.Sqr();
                H.Add(H);

                z.Copy(y);
                J.Copy(y);

                J.Sub(H);
                J.Norm();
                x.Mul(J);

                C.Sub(D);
                C.Norm();
                y.Mul(C);
                z.Mul(J);
                //System.out.println("Out of dbl");
            }

            if (CURVETYPE == MONTGOMERY)
            {
                FP A = new FP(x);
                FP B = new FP(x);
                FP AA = new FP(0);
                FP BB = new FP(0);
                FP C = new FP(0);

                A.Add(z);
                A.Norm();
                AA.Copy(A);
                AA.Sqr();
                B.Sub(z);
                B.Norm();
                BB.Copy(B);
                BB.Sqr();
                C.Copy(AA);
                C.Sub(BB);
                C.Norm();
                x.Copy(AA);
                x.Mul(BB);

                A.Copy(C);
                A.IMul((ROM.CURVE_A + 2) / 4);

                BB.Add(A);
                BB.Norm();
                z.Copy(BB);
                z.Mul(C);
            }

            return;
        }

        /* this+=Q */
        public void Add(ECP Q)
        {
            //		if (INF)
            //		{
            //			copy(Q);
            //			return;
            //		}
            //		if (Q.INF) return;

            if (CURVETYPE == WEIERSTRASS)
            {
                if (ROM.CURVE_A == 0)
                {
                    // Edits made
                    //System.out.println("Into add");
                    int b = 3 * ROM.CURVE_B_I;
                    FP t0 = new FP(x);
                    t0.Mul(Q.x);
                    FP t1 = new FP(y);
                    t1.Mul(Q.y);
                    FP t2 = new FP(z);
                    t2.Mul(Q.z);
                    FP t3 = new FP(x);
                    t3.Add(y);
                    t3.Norm();
                    FP t4 = new FP(Q.x);
                    t4.Add(Q.y);
                    t4.Norm();
                    t3.Mul(t4);
                    t4.Copy(t0);
                    t4.Add(t1);

                    t3.Sub(t4);
                    t3.Norm();
                    t4.Copy(y);
                    t4.Add(z);
                    t4.Norm();
                    FP x3 = new FP(Q.y);
                    x3.Add(Q.z);
                    x3.Norm();

                    t4.Mul(x3);
                    x3.Copy(t1);
                    x3.Add(t2);

                    t4.Sub(x3);
                    t4.Norm();
                    x3.Copy(x);
                    x3.Add(z);
                    x3.Norm();
                    FP y3 = new FP(Q.x);
                    y3.Add(Q.z);
                    y3.Norm();
                    x3.Mul(y3);
                    y3.Copy(t0);
                    y3.Add(t2);
                    y3.RSub(x3);
                    y3.Norm();
                    x3.Copy(t0);
                    x3.Add(t0);
                    t0.Add(x3);
                    t0.Norm();
                    t2.IMul(b);

                    FP z3 = new FP(t1);
                    z3.Add(t2);
                    z3.Norm();
                    t1.Sub(t2);
                    t1.Norm();
                    y3.IMul(b);

                    x3.Copy(y3);
                    x3.Mul(t4);
                    t2.Copy(t3);
                    t2.Mul(t1);
                    x3.RSub(t2);
                    y3.Mul(t0);
                    t1.Mul(z3);
                    y3.Add(t1);
                    t0.Mul(t3);
                    z3.Mul(t4);
                    z3.Add(t0);

                    x.Copy(x3);
                    x.Norm();
                    y.Copy(y3);
                    y.Norm();
                    z.Copy(z3);
                    z.Norm();
                    //System.out.println("Out of add");
                }
                else
                {
                    FP t0 = new FP(x);
                    FP t1 = new FP(y);
                    FP t2 = new FP(z);
                    FP t3 = new FP(x);
                    FP t4 = new FP(Q.x);
                    FP z3 = new FP(0);
                    FP y3 = new FP(Q.x);
                    FP x3 = new FP(Q.y);
                    FP b = new FP(0);

                    if (ROM.CURVE_B_I == 0)
                    {
                        b.Copy(new FP(new BIG(ROM.CURVE_B)));
                    }

                    t0.Mul(Q.x); //1
                    t1.Mul(Q.y); //2
                    t2.Mul(Q.z); //3

                    t3.Add(y);
                    t3.Norm(); //4
                    t4.Add(Q.y);
                    t4.Norm(); //5
                    t3.Mul(t4); //6
                    t4.Copy(t0);
                    t4.Add(t1); //t4.norm(); //7
                    t3.Sub(t4);
                    t3.Norm(); //8
                    t4.Copy(y);
                    t4.Add(z);
                    t4.Norm(); //9
                    x3.Add(Q.z);
                    x3.Norm(); //10
                    t4.Mul(x3); //11
                    x3.Copy(t1);
                    x3.Add(t2); //x3.norm();//12

                    t4.Sub(x3);
                    t4.Norm(); //13
                    x3.Copy(x);
                    x3.Add(z);
                    x3.Norm(); //14
                    y3.Add(Q.z);
                    y3.Norm(); //15

                    x3.Mul(y3); //16
                    y3.Copy(t0);
                    y3.Add(t2); //y3.norm();//17

                    y3.RSub(x3);
                    y3.Norm(); //18
                    z3.Copy(t2);


                    if (ROM.CURVE_B_I == 0)
                    {
                        z3.Mul(b); //18
                    }
                    else
                    {
                        z3.IMul(ROM.CURVE_B_I);
                    }

                    x3.Copy(y3);
                    x3.Sub(z3);
                    x3.Norm(); //20
                    z3.Copy(x3);
                    z3.Add(x3); //z3.norm(); //21

                    x3.Add(z3); //x3.norm(); //22
                    z3.Copy(t1);
                    z3.Sub(x3);
                    z3.Norm(); //23
                    x3.Add(t1);
                    x3.Norm(); //24

                    if (ROM.CURVE_B_I == 0)
                    {
                        y3.Mul(b); //18
                    }
                    else
                    {
                        y3.IMul(ROM.CURVE_B_I);
                    }

                    t1.Copy(t2);
                    t1.Add(t2); //t1.norm();//26
                    t2.Add(t1); //t2.norm();//27

                    y3.Sub(t2); //y3.norm(); //28

                    y3.Sub(t0);
                    y3.Norm(); //29
                    t1.Copy(y3);
                    t1.Add(y3); //t1.norm();//30
                    y3.Add(t1);
                    y3.Norm(); //31

                    t1.Copy(t0);
                    t1.Add(t0); //t1.norm(); //32
                    t0.Add(t1); //t0.norm();//33
                    t0.Sub(t2);
                    t0.Norm(); //34
                    t1.Copy(t4);
                    t1.Mul(y3); //35
                    t2.Copy(t0);
                    t2.Mul(y3); //36
                    y3.Copy(x3);
                    y3.Mul(z3); //37
                    y3.Add(t2); //y3.norm();//38
                    x3.Mul(t3); //39
                    x3.Sub(t1); //40
                    z3.Mul(t4); //41
                    t1.Copy(t3);
                    t1.Mul(t0); //42
                    z3.Add(t1);
                    x.Copy(x3);
                    x.Norm();
                    y.Copy(y3);
                    y.Norm();
                    z.Copy(z3);
                    z.Norm();
                }
            }

            if (CURVETYPE == EDWARDS)
            {
                //System.out.println("Into add");
                FP A = new FP(z);
                FP B = new FP(0);
                FP C = new FP(x);
                FP D = new FP(y);
                FP E = new FP(0);
                FP F = new FP(0);
                FP G = new FP(0);

                A.Mul(Q.z);
                B.Copy(A);
                B.Sqr();
                C.Mul(Q.x);
                D.Mul(Q.y);

                E.Copy(C);
                E.Mul(D);

                if (ROM.CURVE_B_I == 0)
                {
                    FP b = new FP(new BIG(ROM.CURVE_B));
                    E.Mul(b);
                }
                else
                {
                    E.IMul(ROM.CURVE_B_I);
                }

                F.Copy(B);
                F.Sub(E);
                G.Copy(B);
                G.Add(E);

                if (ROM.CURVE_A == 1)
                {
                    E.Copy(D);
                    E.Sub(C);
                }

                C.Add(D);

                B.Copy(x);
                B.Add(y);
                D.Copy(Q.x);
                D.Add(Q.y);
                B.Norm();
                D.Norm();
                B.Mul(D);
                B.Sub(C);
                B.Norm();
                F.Norm();
                B.Mul(F);
                x.Copy(A);
                x.Mul(B);
                G.Norm();
                if (ROM.CURVE_A == 1)
                {
                    E.Norm();
                    C.Copy(E);
                    C.Mul(G);
                }

                if (ROM.CURVE_A == -1)
                {
                    C.Norm();
                    C.Mul(G);
                }

                y.Copy(A);
                y.Mul(C);

                z.Copy(F);
                z.Mul(G);
                //System.out.println("Out of add");
            }

            return;
        }

        /* Differential Add for Montgomery curves. this+=Q where W is this-Q and is affine. */
        public void DAdd(ECP Q, ECP W)
        {
            FP A = new FP(x);
            FP B = new FP(x);
            FP C = new FP(Q.x);
            FP D = new FP(Q.x);
            FP DA = new FP(0);
            FP CB = new FP(0);

            A.Add(z);
            B.Sub(z);

            C.Add(Q.z);
            D.Sub(Q.z);
            A.Norm();

            D.Norm();
            DA.Copy(D);
            DA.Mul(A);

            C.Norm();
            B.Norm();
            CB.Copy(C);
            CB.Mul(B);

            A.Copy(DA);
            A.Add(CB);
            A.Norm();
            A.Sqr();
            B.Copy(DA);
            B.Sub(CB);
            B.Norm();
            B.Sqr();

            x.Copy(A);
            z.Copy(W.x);
            z.Mul(B);
        }

        /* this-=Q */
        public void Sub(ECP Q)
        {
            ECP NQ = new ECP(Q);
            NQ.Neg();
            Add(NQ);
        }

        /* constant time multiply by small integer of length bts - use ladder */
        public ECP PinMul(int e, int bts)
        {
            if (CURVETYPE == MONTGOMERY)
            {
                return Mul(new BIG(e));
            }
            else
            {
                int i, b;
                ECP P = new ECP();
                ECP R0 = new ECP();
                ECP R1 = new ECP();
                R1.Copy(this);

                for (i = bts - 1; i >= 0; i--)
                {
                    b = (e >> i) & 1;
                    P.Copy(R1);
                    P.Add(R0);
                    R0.CSwap(R1, b);
                    R1.Copy(P);
                    R0.Dbl();
                    R0.CSwap(R1, b);
                }

                P.Copy(R0);
                P.Affine();
                return P;
            }
        }

        /* return e.this */

        public ECP Mul(BIG e)
        {
            if (e.IsZilch() || IsInfinity())
            {
                return new ECP();
            }

            ECP P = new ECP();
            if (CURVETYPE == MONTGOMERY)
            {
                /* use Ladder */
                int nb, i, b;
                ECP D = new ECP();
                ECP R0 = new ECP();
                R0.Copy(this);
                ECP R1 = new ECP();
                R1.Copy(this);
                R1.Dbl();

                D.Copy(this);
                D.Affine();
                nb = e.NBits();
                for (i = nb - 2; i >= 0; i--)
                {
                    b = e.Bit(i);
                    P.Copy(R1);

                    P.DAdd(R0, D);
                    R0.CSwap(R1, b);
                    R1.Copy(P);
                    R0.Dbl();
                    R0.CSwap(R1, b);
                }

                P.Copy(R0);
            }
            else
            {
                // fixed size windows 
                int i, nb, s, ns;
                BIG mt = new BIG();
                BIG t = new BIG();
                ECP Q = new ECP();
                ECP C = new ECP();
                ECP[] W = new ECP[8];
                sbyte[] w = new sbyte[1 + (BIG.NLEN * BIG.BASEBITS + 3) / 4];

                //affine();

                // precompute table 
                Q.Copy(this);

                Q.Dbl();
                W[0] = new ECP();
                W[0].Copy(this);

                for (i = 1; i < 8; i++)
                {
                    W[i] = new ECP();
                    W[i].Copy(W[i - 1]);
                    W[i].Add(Q);
                }

                // make exponent odd - add 2P if even, P if odd 
                t.Copy(e);
                s = t.Parity();
                t.Inc(1);
                t.Norm();
                ns = t.Parity();
                mt.Copy(t);
                mt.Inc(1);
                mt.Norm();
                t.CMove(mt, s);
                Q.CMove(this, ns);
                C.Copy(Q);

                nb = 1 + (t.NBits() + 3) / 4;

                // convert exponent to signed 4-bit window 
                for (i = 0; i < nb; i++)
                {
                    w[i] = (sbyte) (t.LastBits(5) - 16);
                    t.Dec(w[i]);
                    t.Norm();
                    t.FShr(4);
                }

                w[nb] = (sbyte) t.LastBits(5);

                P.Copy(W[(w[nb] - 1) / 2]);
                for (i = nb - 1; i >= 0; i--)
                {
                    Q.Select(W, w[i]);
                    P.Dbl();
                    P.Dbl();
                    P.Dbl();
                    P.Dbl();
                    P.Add(Q);
                }

                P.Sub(C); // apply correction
            }

            P.Affine();
            return P;
        }

        /* Return e.this+f.Q */

        public ECP Mul2(BIG e, ECP Q, BIG f)
        {
            BIG te = new BIG();
            BIG tf = new BIG();
            BIG mt = new BIG();
            ECP S = new ECP();
            ECP T = new ECP();
            ECP C = new ECP();
            ECP[] W = new ECP[8];
            sbyte[] w = new sbyte[1 + (BIG.NLEN * BIG.BASEBITS + 1) / 2];
            int i, s, ns, nb;
            sbyte a, b;

            //affine();
            //Q.affine();

            te.Copy(e);
            tf.Copy(f);

            // precompute table 
            W[1] = new ECP();
            W[1].Copy(this);
            W[1].Sub(Q);
            W[2] = new ECP();
            W[2].Copy(this);
            W[2].Add(Q);
            S.Copy(Q);
            S.Dbl();
            W[0] = new ECP();
            W[0].Copy(W[1]);
            W[0].Sub(S);
            W[3] = new ECP();
            W[3].Copy(W[2]);
            W[3].Add(S);
            T.Copy(this);
            T.Dbl();
            W[5] = new ECP();
            W[5].Copy(W[1]);
            W[5].Add(T);
            W[6] = new ECP();
            W[6].Copy(W[2]);
            W[6].Add(T);
            W[4] = new ECP();
            W[4].Copy(W[5]);
            W[4].Sub(S);
            W[7] = new ECP();
            W[7].Copy(W[6]);
            W[7].Add(S);

            // if multiplier is odd, add 2, else add 1 to multiplier, and add 2P or P to correction 

            s = te.Parity();
            te.Inc(1);
            te.Norm();
            ns = te.Parity();
            mt.Copy(te);
            mt.Inc(1);
            mt.Norm();
            te.CMove(mt, s);
            T.CMove(this, ns);
            C.Copy(T);

            s = tf.Parity();
            tf.Inc(1);
            tf.Norm();
            ns = tf.Parity();
            mt.Copy(tf);
            mt.Inc(1);
            mt.Norm();
            tf.CMove(mt, s);
            S.CMove(Q, ns);
            C.Add(S);

            mt.Copy(te);
            mt.Add(tf);
            mt.Norm();
            nb = 1 + (mt.NBits() + 1) / 2;

            // convert exponent to signed 2-bit window 
            for (i = 0; i < nb; i++)
            {
                a = (sbyte) (te.LastBits(3) - 4);
                te.Dec(a);
                te.Norm();
                te.FShr(2);
                b = (sbyte) (tf.LastBits(3) - 4);
                tf.Dec(b);
                tf.Norm();
                tf.FShr(2);
                w[i] = (sbyte) (4 * a + b);
            }

            w[nb] = (sbyte) (4 * te.LastBits(3) + tf.LastBits(3));
            S.Copy(W[(w[nb] - 1) / 2]);

            for (i = nb - 1; i >= 0; i--)
            {
                T.Select(W, w[i]);
                S.Dbl();
                S.Dbl();
                S.Add(T);
            }

            S.Sub(C); // apply correction
            S.Affine();
            return S;
        }

        // multiply a point by the curves cofactor
        public void Cfp()
        {
            int cf = ROM.CURVE_Cof_I;
            if (cf == 1)
            {
                return;
            }

            if (cf == 4)
            {
                Dbl();
                Dbl();
                //affine();
                return;
            }

            if (cf == 8)
            {
                Dbl();
                Dbl();
                Dbl();
                //affine();
                return;
            }

            BIG c = new BIG(ROM.CURVE_Cof);
            Copy(Mul(c));
        }

        /* Map byte string to curve point */
        public static ECP MapIt(sbyte[] h)
        {
            BIG q = new BIG(ROM.Modulus);
            BIG x = BIG.FromBytes(h);
            x.Mod(q);
            ECP P;

            while (true)
            {
                while (true)
                {
                    if (CURVETYPE != MONTGOMERY)
                    {
                        P = new ECP(x, 0);
                    }
                    else
                    {
                        P = new ECP(x);
                    }

                    x.Inc(1);
                    x.Norm();
                    if (!P.IsInfinity())
                    {
                        break;
                    }
                }

                P.Cfp();
                if (!P.IsInfinity())
                {
                    break;
                }
            }

            return P;
        }

        public static ECP Generator()
        {
            ECP G;
            BIG gx, gy;
            gx = new BIG(ROM.CURVE_Gx);

            if (CURVETYPE != MONTGOMERY)
            {
                gy = new BIG(ROM.CURVE_Gy);
                G = new ECP(gx, gy);
            }
            else
            {
                G = new ECP(gx);
            }

            return G;
        }

    }
}