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

/* AMCL BN Curve Pairing functions */

// ReSharper disable All
#pragma warning disable 162
namespace Hyperledger.Fabric.SDK.AMCL.FP256BN
{
    public sealed class PAIR
    {
        public const bool USE_GLV = true;
        public const bool USE_GS_G2 = true;
        public const bool USE_GS_GT = true;
        public const bool GT_STRONG = false;


        /* Line function */
        public static FP12 Line(ECP2 A, ECP2 B, FP Qx, FP Qy)
        {
            //System.out.println("Into line");
            FP4 a, b, c; // Edits here
            //		c=new FP4(0);
            if (A == B)
            {
                // Doubling
                FP2 XX = new FP2(A.GetX()); //X
                FP2 YY = new FP2(A.GetY()); //Y
                FP2 ZZ = new FP2(A.GetZ()); //Z
                FP2 YZ = new FP2(YY); //Y
                YZ.Mul(ZZ); //YZ
                XX.Sqr(); //X^2
                YY.Sqr(); //Y^2
                ZZ.Sqr(); //Z^2

                YZ.IMul(4);
                YZ.Neg();
                YZ.Norm(); //-2YZ
                YZ.PMul(Qy); //-2YZ.Ys

                XX.IMul(6); //3X^2
                XX.PMul(Qx); //3X^2.Xs

                int sb = 3 * ROM.CURVE_B_I;
                ZZ.IMul(sb);

                if (ECP.SEXTIC_TWIST == ECP.D_TYPE)
                {
                    ZZ.Div_Ip2();
                }

                if (ECP.SEXTIC_TWIST == ECP.M_TYPE)
                {
                    ZZ.Mul_Ip();
                    ZZ.Add(ZZ);
                    YZ.Mul_Ip();
                    YZ.Norm();
                }

                ZZ.Norm(); // 3b.Z^2

                YY.Add(YY);
                ZZ.Sub(YY);
                ZZ.Norm(); // 3b.Z^2-Y^2

                a = new FP4(YZ, ZZ); // -2YZ.Ys | 3b.Z^2-Y^2 | 3X^2.Xs
                if (ECP.SEXTIC_TWIST == ECP.D_TYPE)
                {
                    b = new FP4(XX); // L(0,1) | L(0,0) | L(1,0)
                    c = new FP4(0);
                }

                if (ECP.SEXTIC_TWIST == ECP.M_TYPE)
                {
                    b = new FP4(0);
                    c = new FP4(XX);
                    c.Times_I();
                }

                A.Dbl();
            }
            else
            {
                // Addition - assume B is affine

                FP2 X1 = new FP2(A.GetX()); // X1
                FP2 Y1 = new FP2(A.GetY()); // Y1
                FP2 T1 = new FP2(A.GetZ()); // Z1
                FP2 T2 = new FP2(A.GetZ()); // Z1

                T1.Mul(B.GetY()); // T1=Z1.Y2
                T2.Mul(B.GetX()); // T2=Z1.X2

                X1.Sub(T2);
                X1.Norm(); // X1=X1-Z1.X2
                Y1.Sub(T1);
                Y1.Norm(); // Y1=Y1-Z1.Y2

                T1.Copy(X1); // T1=X1-Z1.X2
                X1.PMul(Qy); // X1=(X1-Z1.X2).Ys

                if (ECP.SEXTIC_TWIST == ECP.M_TYPE)
                {
                    X1.Mul_Ip();
                    X1.Norm();
                }

                T1.Mul(B.GetY()); // T1=(X1-Z1.X2).Y2

                T2.Copy(Y1); // T2=Y1-Z1.Y2
                T2.Mul(B.GetX()); // T2=(Y1-Z1.Y2).X2
                T2.Sub(T1);
                T2.Norm(); // T2=(Y1-Z1.Y2).X2 - (X1-Z1.X2).Y2
                Y1.PMul(Qx);
                Y1.Neg();
                Y1.Norm(); // Y1=-(Y1-Z1.Y2).Xs

                a = new FP4(X1, T2); // (X1-Z1.X2).Ys  |  (Y1-Z1.Y2).X2 - (X1-Z1.X2).Y2  | - (Y1-Z1.Y2).Xs
                if (ECP.SEXTIC_TWIST == ECP.D_TYPE)
                {
                    b = new FP4(Y1);
                    c = new FP4(0);
                }

                if (ECP.SEXTIC_TWIST == ECP.M_TYPE)
                {
                    b = new FP4(0);
                    c = new FP4(Y1);
                    c.Times_I();
                }

                A.Add(B);
            }

            //System.out.println("Out of line");
            return new FP12(a, b, c);
        }

        /* Optimal R-ate pairing */
        public static FP12 Ate(ECP2 P1, ECP Q1)
        {
            FP2 f;
            BIG x = new BIG(ROM.CURVE_Bnx);
            BIG n = new BIG(x);
            ECP2 K = new ECP2();
            FP12 lv;
            int bt;

            // P is needed in affine form for line function, Q for (Qx,Qy) extraction
            ECP2 P = new ECP2(P1);
            ECP Q = new ECP(Q1);

            P.Affine();
            Q.Affine();

            if (ECP.CURVE_PAIRING_TYPE == ECP.BN)
            {
                f = new FP2(new BIG(ROM.Fra), new BIG(ROM.Frb));
                if (ECP.SEXTIC_TWIST == ECP.M_TYPE)
                {
                    f.Inverse();
                    f.Norm();
                }

                n.PMul(6);
                if (ECP.SIGN_OF_X == ECP.POSITIVEX)
                {
                    n.Inc(2);
                }
                else
                {
                    n.Dec(2);
                }
            }
            else
            {
                n.Copy(x);
            }

            n.Norm();

            BIG n3 = new BIG(n);
            n3.PMul(3);
            n3.Norm();

            FP Qx = new FP(Q.GetX());
            FP Qy = new FP(Q.GetY());

            ECP2 A = new ECP2();
            FP12 r = new FP12(1);
            A.Copy(P);

            ECP2 MP = new ECP2();
            MP.Copy(P);
            MP.Neg();

            int nb = n3.NBits();

            for (int i = nb - 2; i >= 1; i--)
            {
                r.Sqr();
                lv = Line(A, A, Qx, Qy);
                r.SMul(lv, ECP.SEXTIC_TWIST);

                bt = n3.Bit(i) - n.Bit(i); // bt=n.bit(i);
                if (bt == 1)
                {
                    lv = Line(A, P, Qx, Qy);
                    r.SMul(lv, ECP.SEXTIC_TWIST);
                }

                if (bt == -1)
                {
                    //P.neg();
                    lv = Line(A, MP, Qx, Qy);
                    r.SMul(lv, ECP.SEXTIC_TWIST);
                    //P.neg();
                }
            }

            if (ECP.SIGN_OF_X == ECP.NEGATIVEX)
            {
                r.Conj();
            }

            /* R-ate fixup required for BN curves */
            if (ECP.CURVE_PAIRING_TYPE == ECP.BN)
            {
                if (ECP.SIGN_OF_X == ECP.NEGATIVEX)
                {
                    //r.conj();
                    A.Neg();
                }

                K.Copy(P);
                K.Frob(f);
                lv = Line(A, K, Qx, Qy);
                r.SMul(lv, ECP.SEXTIC_TWIST);
                K.Frob(f);
                K.Neg();
                lv = Line(A, K, Qx, Qy);
                r.SMul(lv, ECP.SEXTIC_TWIST);
            }

            return r;
        }

        /* Optimal R-ate double pairing e(P,Q).e(R,S) */
        public static FP12 Ate2(ECP2 P1, ECP Q1, ECP2 R1, ECP S1)
        {
            FP2 f;
            BIG x = new BIG(ROM.CURVE_Bnx);
            BIG n = new BIG(x);
            ECP2 K = new ECP2();
            FP12 lv;
            int bt;

            ECP2 P = new ECP2(P1);
            ECP Q = new ECP(Q1);

            P.Affine();
            Q.Affine();

            ECP2 R = new ECP2(R1);
            ECP S = new ECP(S1);

            R.Affine();
            S.Affine();

            if (ECP.CURVE_PAIRING_TYPE == ECP.BN)
            {
                f = new FP2(new BIG(ROM.Fra), new BIG(ROM.Frb));
                if (ECP.SEXTIC_TWIST == ECP.M_TYPE)
                {
                    f.Inverse();
                    f.Norm();
                }

                n.PMul(6);
                if (ECP.SIGN_OF_X == ECP.POSITIVEX)
                {
                    n.Inc(2);
                }
                else
                {
                    n.Dec(2);
                }
            }
            else
            {
                n.Copy(x);
            }

            n.Norm();

            BIG n3 = new BIG(n);
            n3.PMul(3);
            n3.Norm();

            FP Qx = new FP(Q.GetX());
            FP Qy = new FP(Q.GetY());
            FP Sx = new FP(S.GetX());
            FP Sy = new FP(S.GetY());

            ECP2 A = new ECP2();
            ECP2 B = new ECP2();
            FP12 r = new FP12(1);

            A.Copy(P);
            B.Copy(R);

            ECP2 MP = new ECP2();
            MP.Copy(P);
            MP.Neg();
            ECP2 MR = new ECP2();
            MR.Copy(R);
            MR.Neg();


            int nb = n3.NBits();

            for (int i = nb - 2; i >= 1; i--)
            {
                r.Sqr();
                lv = Line(A, A, Qx, Qy);
                r.SMul(lv, ECP.SEXTIC_TWIST);

                lv = Line(B, B, Sx, Sy);
                r.SMul(lv, ECP.SEXTIC_TWIST);

                bt = n3.Bit(i) - n.Bit(i); // bt=n.bit(i);
                if (bt == 1)
                {
                    lv = Line(A, P, Qx, Qy);
                    r.SMul(lv, ECP.SEXTIC_TWIST);
                    lv = Line(B, R, Sx, Sy);
                    r.SMul(lv, ECP.SEXTIC_TWIST);
                }

                if (bt == -1)
                {
                    //P.neg(); 
                    lv = Line(A, MP, Qx, Qy);
                    r.SMul(lv, ECP.SEXTIC_TWIST);
                    //P.neg(); 
                    //R.neg();
                    lv = Line(B, MR, Sx, Sy);
                    r.SMul(lv, ECP.SEXTIC_TWIST);
                    //R.neg();
                }
            }

            if (ECP.SIGN_OF_X == ECP.NEGATIVEX)
            {
                r.Conj();
            }

            /* R-ate fixup required for BN curves */
            if (ECP.CURVE_PAIRING_TYPE == ECP.BN)
            {
                if (ECP.SIGN_OF_X == ECP.NEGATIVEX)
                {
                    //	r.conj();
                    A.Neg();
                    B.Neg();
                }

                K.Copy(P);
                K.Frob(f);

                lv = Line(A, K, Qx, Qy);
                r.SMul(lv, ECP.SEXTIC_TWIST);
                K.Frob(f);
                K.Neg();
                lv = Line(A, K, Qx, Qy);
                r.SMul(lv, ECP.SEXTIC_TWIST);
                K.Copy(R);
                K.Frob(f);
                lv = Line(B, K, Sx, Sy);
                r.SMul(lv, ECP.SEXTIC_TWIST);
                K.Frob(f);
                K.Neg();
                lv = Line(B, K, Sx, Sy);
                r.SMul(lv, ECP.SEXTIC_TWIST);
            }

            return r;
        }

        /* final exponentiation - keep separate for multi-pairings and to avoid thrashing stack */
        public static FP12 FExp(FP12 m)
        {
            FP2 f = new FP2(new BIG(ROM.Fra), new BIG(ROM.Frb));
            BIG x = new BIG(ROM.CURVE_Bnx);
            FP12 r = new FP12(m);

            /* Easy part of final exp */
            FP12 lv = new FP12(r);
            lv.Inverse();
            r.Conj();

            r.mul(lv);
            lv.Copy(r);
            r.Frob(f);
            r.Frob(f);
            r.mul(lv);
            /* Hard part of final exp */
            if (ECP.CURVE_PAIRING_TYPE == ECP.BN)
            {
                FP12 x0, x1, x2, x3, x4, x5;
                lv.Copy(r);
                lv.Frob(f);
                x0 = new FP12(lv);
                x0.Frob(f);
                lv.mul(r);
                x0.mul(lv);
                x0.Frob(f);
                x1 = new FP12(r);
                x1.Conj();
                x4 = r.Pow(x);
                if (ECP.SIGN_OF_X == ECP.POSITIVEX)
                {
                    x4.Conj();
                }

                x3 = new FP12(x4);
                x3.Frob(f);

                x2 = x4.Pow(x);
                if (ECP.SIGN_OF_X == ECP.POSITIVEX)
                {
                    x2.Conj();
                }

                x5 = new FP12(x2);
                x5.Conj();
                lv = x2.Pow(x);
                if (ECP.SIGN_OF_X == ECP.POSITIVEX)
                {
                    lv.Conj();
                }

                x2.Frob(f);
                r.Copy(x2);
                r.Conj();

                x4.mul(r);
                x2.Frob(f);

                r.Copy(lv);
                r.Frob(f);
                lv.mul(r);

                lv.USqr();
                lv.mul(x4);
                lv.mul(x5);
                r.Copy(x3);
                r.mul(x5);
                r.mul(lv);
                lv.mul(x2);
                r.USqr();
                r.mul(lv);
                r.USqr();
                lv.Copy(r);
                lv.mul(x1);
                r.mul(x0);
                lv.USqr();
                r.mul(lv);
                r.Reduce();
            }
            else
            {
                FP12 y0, y1, y2, y3;
                // Ghamman & Fouotsa Method
                y0 = new FP12(r);
                y0.USqr();
                y1 = y0.Pow(x);
                if (ECP.SIGN_OF_X == ECP.NEGATIVEX)
                {
                    y1.Conj();
                }

                x.FShr(1);
                y2 = y1.Pow(x);
                if (ECP.SIGN_OF_X == ECP.NEGATIVEX)
                {
                    y2.Conj();
                }

                x.FShl(1);
                y3 = new FP12(r);
                y3.Conj();
                y1.mul(y3);

                y1.Conj();
                y1.mul(y2);

                y2 = y1.Pow(x);
                if (ECP.SIGN_OF_X == ECP.NEGATIVEX)
                {
                    y2.Conj();
                }

                y3 = y2.Pow(x);
                if (ECP.SIGN_OF_X == ECP.NEGATIVEX)
                {
                    y3.Conj();
                }

                y1.Conj();
                y3.mul(y1);

                y1.Conj();
                y1.Frob(f);
                y1.Frob(f);
                y1.Frob(f);
                y2.Frob(f);
                y2.Frob(f);
                y1.mul(y2);

                y2 = y3.Pow(x);
                if (ECP.SIGN_OF_X == ECP.NEGATIVEX)
                {
                    y2.Conj();
                }

                y2.mul(y0);
                y2.mul(r);

                y1.mul(y2);
                y2.Copy(y3);
                y2.Frob(f);
                y1.mul(y2);
                r.Copy(y1);
                r.Reduce();
            }

            return r;
        }

        /* GLV method */
        public static BIG[] Glv(BIG e)
        {
            BIG[] u = new BIG[2];
            if (ECP.CURVE_PAIRING_TYPE == ECP.BN)
            {
                int i, j;
                BIG t = new BIG(0);
                BIG q = new BIG(ROM.CURVE_Order);

                BIG[] v = new BIG[2];
                for (i = 0; i < 2; i++)
                {
                    t.Copy(new BIG(ROM.CURVE_W[i])); // why not just t=new BIG(ROM.CURVE_W[i]);
                    DBIG d = BIG.Mul(t, e);
                    v[i] = new BIG(d.Div(q));
                    u[i] = new BIG(0);
                }

                u[0].Copy(e);
                for (i = 0; i < 2; i++)
                {
                    for (j = 0; j < 2; j++)
                    {
                        t.Copy(new BIG(ROM.CURVE_SB[j][i]));
                        t.Copy(BIG.ModMul(v[j], t, q));
                        u[i].Add(q);
                        u[i].Sub(t);
                        u[i].Mod(q);
                    }
                }
            }
            else
            {
                // -(x^2).P = (Beta.x,y)
                BIG q = new BIG(ROM.CURVE_Order);
                BIG x = new BIG(ROM.CURVE_Bnx);
                BIG x2 = BIG.SMul(x, x);
                u[0] = new BIG(e);
                u[0].Mod(x2);
                u[1] = new BIG(e);
                u[1].Div(x2);
                u[1].RSub(q);
            }

            return u;
        }

        /* Galbraith & Scott Method */
        public static BIG[] GS(BIG e)
        {
            BIG[] u = new BIG[4];
            if (ECP.CURVE_PAIRING_TYPE == ECP.BN)
            {
                int i, j;
                BIG t = new BIG(0);
                BIG q = new BIG(ROM.CURVE_Order);
                BIG[] v = new BIG[4];
                for (i = 0; i < 4; i++)
                {
                    t.Copy(new BIG(ROM.CURVE_WB[i]));
                    DBIG d = BIG.Mul(t, e);
                    v[i] = new BIG(d.Div(q));
                    u[i] = new BIG(0);
                }

                u[0].Copy(e);
                for (i = 0; i < 4; i++)
                {
                    for (j = 0; j < 4; j++)
                    {
                        t.Copy(new BIG(ROM.CURVE_BB[j][i]));
                        t.Copy(BIG.ModMul(v[j], t, q));
                        u[i].Add(q);
                        u[i].Sub(t);
                        u[i].Mod(q);
                    }
                }
            }
            else
            {
                BIG q = new BIG(ROM.CURVE_Order);
                BIG x = new BIG(ROM.CURVE_Bnx);
                BIG w = new BIG(e);
                for (int i = 0; i < 3; i++)
                {
                    u[i] = new BIG(w);
                    u[i].Mod(x);
                    w.Div(x);
                }

                u[3] = new BIG(w);
                if (ECP.SIGN_OF_X == ECP.NEGATIVEX)
                {
                    u[1].Copy(BIG.ModNeg(u[1], q));
                    u[3].Copy(BIG.ModNeg(u[3], q));
                }
            }

            return u;
        }

        /* Multiply P by e in group G1 */
        public static ECP G1Mul(ECP P, BIG e)
        {
            ECP R;
            if (USE_GLV)
            {
                //P.affine();
                R = new ECP();
                R.Copy(P);
                int np, nn;
                ECP Q = new ECP();
                Q.Copy(P);
                Q.Affine();
                BIG q = new BIG(ROM.CURVE_Order);
                FP cru = new FP(new BIG(ROM.CURVE_Cru));
                BIG t = new BIG(0);
                BIG[] u = Glv(e);
                Q.GetX().Mul(cru);

                np = u[0].NBits();
                t.Copy(BIG.ModNeg(u[0], q));
                nn = t.NBits();
                if (nn < np)
                {
                    u[0].Copy(t);
                    R.Neg();
                }

                np = u[1].NBits();
                t.Copy(BIG.ModNeg(u[1], q));
                nn = t.NBits();
                if (nn < np)
                {
                    u[1].Copy(t);
                    Q.Neg();
                }

                u[0].Norm();
                u[1].Norm();
                R = R.Mul2(u[0], Q, u[1]);
            }
            else
            {
                R = P.Mul(e);
            }

            return R;
        }

        /* Multiply P by e in group G2 */
        public static ECP2 G2Mul(ECP2 P, BIG e)
        {
            ECP2 R;
            if (USE_GS_G2)
            {
                ECP2[] Q = new ECP2[4];
                FP2 f = new FP2(new BIG(ROM.Fra), new BIG(ROM.Frb));

                if (ECP.SEXTIC_TWIST == ECP.M_TYPE)
                {
                    f.Inverse();
                    f.Norm();
                }

                BIG q = new BIG(ROM.CURVE_Order);
                BIG[] u = GS(e);

                BIG t = new BIG(0);
                int i, np, nn;
                //P.affine();

                Q[0] = new ECP2();
                Q[0].Copy(P);
                for (i = 1; i < 4; i++)
                {
                    Q[i] = new ECP2();
                    Q[i].Copy(Q[i - 1]);
                    Q[i].Frob(f);
                }

                for (i = 0; i < 4; i++)
                {
                    np = u[i].NBits();
                    t.Copy(BIG.ModNeg(u[i], q));
                    nn = t.NBits();
                    if (nn < np)
                    {
                        u[i].Copy(t);
                        Q[i].Neg();
                    }

                    u[i].Norm();
                    //Q[i].affine();
                }

                R = ECP2.Mul4(Q, u);
            }
            else
            {
                R = P.Mul(e);
            }

            return R;
        }

        /* f=f^e */
        /* Note that this method requires a lot of RAM! Better to use compressed XTR method, see FP4.java */
        public static FP12 GTPow(FP12 d, BIG e)
        {
            FP12 r;
            if (USE_GS_GT)
            {
                FP12[] g = new FP12[4];
                FP2 f = new FP2(new BIG(ROM.Fra), new BIG(ROM.Frb));
                BIG q = new BIG(ROM.CURVE_Order);
                BIG t = new BIG(0);
                int i, np, nn;
                BIG[] u = GS(e);

                g[0] = new FP12(d);
                for (i = 1; i < 4; i++)
                {
                    g[i] = new FP12(0);
                    g[i].Copy(g[i - 1]);
                    g[i].Frob(f);
                }

                for (i = 0; i < 4; i++)
                {
                    np = u[i].NBits();
                    t.Copy(BIG.ModNeg(u[i], q));
                    nn = t.NBits();
                    if (nn < np)
                    {
                        u[i].Copy(t);
                        g[i].Conj();
                    }

                    u[i].Norm();
                }

                r = FP12.Pow4(g, u);
            }
            else
            {
                r = d.Pow(e);
            }

            return r;
        }
        
    }
}