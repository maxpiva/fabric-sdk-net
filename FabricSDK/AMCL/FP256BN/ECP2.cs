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

/* AMCL Weierstrass elliptic curve functions over FP2 */
// ReSharper disable All
#pragma warning disable 162
using System;

namespace Hyperledger.Fabric.SDK.AMCL.FP256BN
{
	public sealed class ECP2
	{
		private FP2 x;
		private FP2 y;
		private FP2 z;
	//	private boolean INF;

	/* Constructor - set this=O */
		public ECP2()
		{
	//		INF=true;
			x = new FP2(0);
			y = new FP2(1);
			z = new FP2(0);
		}

		public ECP2(ECP2 e)
		{
			this.x = new FP2(e.x);
			this.y = new FP2(e.y);
			this.z = new FP2(e.z);
		}

	/* Test this=O? */
		public bool IsInfinity()
		{
	//		if (INF) return true;                    //******
			return (x.IsZilch() && z.IsZilch());
		}
	/* copy this=P */
		public void Copy(ECP2 P)
		{
			x.Copy(P.x);
			y.Copy(P.y);
			z.Copy(P.z);
	//		INF=P.INF;
		}
	/* set this=O */
		public void Inf()
		{
	//		INF=true;
			x.Zero();
			y.One();
			z.Zero();
		}

	/* Conditional move of Q to P dependant on d */
		public void CMove(ECP2 Q, int d)
		{
			x.CMove(Q.x,d);
			y.CMove(Q.y,d);
			z.CMove(Q.z,d);

		//	boolean bd;
		//	if (d==0) bd=false;
		//	else bd=true;
		//	INF^=(INF^Q.INF)&bd;
		}

	/* return 1 if b==c, no branching */
		public static int Teq(int b, int c)
		{
			int x = b ^ c;
			x -= 1; // if x=0, x now -1
			return ((x >> 31) & 1);
		}

	/* Constant time select from pre-computed table */
		public void Select(ECP2[] W, int b)
		{
			ECP2 MP = new ECP2();
			int m = b >> 31;
			int babs = (b ^ m) - m;

			babs = (babs - 1) / 2;

			CMove(W[0],Teq(babs,0)); // conditional move
			CMove(W[1],Teq(babs,1));
			CMove(W[2],Teq(babs,2));
			CMove(W[3],Teq(babs,3));
			CMove(W[4],Teq(babs,4));
			CMove(W[5],Teq(babs,5));
			CMove(W[6],Teq(babs,6));
			CMove(W[7],Teq(babs,7));

			MP.Copy(this);
			MP.Neg();
			CMove(MP,(int)(m & 1));
		}

	/* Test if P == Q */
		public bool Equals(ECP2 Q)
		{
	//		if (is_infinity() && Q.is_infinity()) return true;
	//		if (is_infinity() || Q.is_infinity()) return false;


			FP2 a = new FP2(x); // *****
			FP2 b = new FP2(Q.x);
			a.Mul(Q.z);
			b.Mul(z);
			if (!a.Equals(b))
			{
				return false;
			}

			a.Copy(y);
			a.Mul(Q.z);
			b.Copy(Q.y);
			b.Mul(z);
			if (!a.Equals(b))
			{
				return false;
			}

			return true;
		}
	/* set this=-this */
		public void Neg()
		{
	//		if (is_infinity()) return;
			y.Norm();
			y.Neg();
			y.Norm();
			return;
		}
	/* set to Affine - (x,y,z) to (x,y) */
		public void Affine()
		{
			if (IsInfinity())
			{
				return;
			}
			FP2 one = new FP2(1);
			if (z.Equals(one))
			{
				x.Reduce();
				y.Reduce();
				return;
			}
			z.Inverse();

			x.Mul(z);
			x.Reduce(); // *****
			y.Mul(z);
			y.Reduce();
			z.Copy(one);
		}
	/* extract affine x as FP2 */
		public FP2 X
		{
			get
			{
				ECP2 W = new ECP2(this);
				W.Affine();
				return W.x;
			}
		}
	/* extract affine y as FP2 */
		public FP2 Y
		{
			get
			{
				ECP2 W = new ECP2(this);
				W.Affine();
				return W.y;
			}
		}
	/* extract projective x */
		public FP2 GetX()
		{
			return x;
		}
	/* extract projective y */
		public FP2 GetY()
		{
			return y;
		}
	/* extract projective z */
		public FP2 GetZ()
		{
			return z;
		}
	/* convert to byte array */
		public void ToBytes(byte[] b)
		{
			byte[] t = new byte[BIG.MODBYTES];
			ECP2 W = new ECP2(this);
			W.Affine();
			W.x.A.ToBytes(t);
			for (int i = 0;i < BIG.MODBYTES;i++)
			{
				b[i] = t[i];
			}
			W.x.B.ToBytes(t);
			for (int i = 0;i < BIG.MODBYTES;i++)
			{
				b[i + BIG.MODBYTES] = t[i];
			}

			W.y.A.ToBytes(t);
			for (int i = 0;i < BIG.MODBYTES;i++)
			{
				b[i + 2 * BIG.MODBYTES] = t[i];
			}
			W.y.B.ToBytes(t);
			for (int i = 0;i < BIG.MODBYTES;i++)
			{
				b[i + 3 * BIG.MODBYTES] = t[i];
			}
		}


	/* convert from byte array to point */
		public static ECP2 FromBytes(byte[] b)
		{
			byte[] t = new byte[BIG.MODBYTES];
			BIG ra;
			BIG rb;

			for (int i = 0;i < BIG.MODBYTES;i++)
			{
				t[i] = b[i];
			}
			ra = BIG.FromBytes(t);
			for (int i = 0;i < BIG.MODBYTES;i++)
			{
				t[i] = b[i + BIG.MODBYTES];
			}
			rb = BIG.FromBytes(t);
			FP2 rx = new FP2(ra,rb);

			for (int i = 0;i < BIG.MODBYTES;i++)
			{
				t[i] = b[i + 2 * BIG.MODBYTES];
			}
			ra = BIG.FromBytes(t);
			for (int i = 0;i < BIG.MODBYTES;i++)
			{
				t[i] = b[i + 3 * BIG.MODBYTES];
			}
			rb = BIG.FromBytes(t);
			FP2 ry = new FP2(ra,rb);

			return new ECP2(rx,ry);
		}
	/* convert this to hex string */
		public override string ToString()
		{
			ECP2 W = new ECP2(this);
			W.Affine();
			if (W.IsInfinity())
			{
				return "infinity";
			}
			return "(" + W.x.ToString() + "," + W.y.ToString() + ")";
		}

	/* Calculate RHS of twisted curve equation x^3+B/i */
		public static FP2 RHS(FP2 x)
		{
			x.Norm();
			FP2 r = new FP2(x);
			r.Sqr();
			FP2 b = new FP2(new BIG(ROM.CURVE_B));

			if (ECP.SEXTIC_TWIST == ECP.D_TYPE)
			{
				b.Div_Ip();
			}
			if (ECP.SEXTIC_TWIST == ECP.M_TYPE)
			{
				b.Norm();
				b.Mul_Ip();
				b.Norm();
			}


			r.Mul(x);
			r.Add(b);

			r.Reduce();
			return r;
		}

	/* construct this from (x,y) - but set to O if not on curve */
		public ECP2(FP2 ix, FP2 iy)
		{
			x = new FP2(ix);
			y = new FP2(iy);
			z = new FP2(1);
			FP2 rhs = RHS(x);
			FP2 y2 = new FP2(y);
			y2.Sqr();
			if (!y2.Equals(rhs))
			{
				Inf();
			}
	//		if (y2.equals(rhs)) INF=false;
	//		else {x.zero();INF=true;}
		}

	/* construct this from x - but set to O if not on curve */
		public ECP2(FP2 ix)
		{
			x = new FP2(ix);
			y = new FP2(1);
			z = new FP2(1);
			FP2 rhs = RHS(x);
			if (rhs.Sqrt())
			{
				y.Copy(rhs);
				//INF=false;
			}
			else
			{
				Inf();
			}
		}

	/* this+=this */
		public int Dbl()
		{
	//		if (INF) return -1;      
	//System.out.println("Into dbl");
			FP2 iy = new FP2(y);
			if (ECP.SEXTIC_TWIST == ECP.D_TYPE)
			{
				iy.Mul_Ip();
				iy.Norm();
			}
			FP2 t0 = new FP2(y); //***** Change
			t0.Sqr();
			if (ECP.SEXTIC_TWIST == ECP.D_TYPE)
			{
				t0.Mul_Ip();
			}
			FP2 t1 = new FP2(iy);
			t1.Mul(z);
			FP2 t2 = new FP2(z);
			t2.Sqr();

			z.Copy(t0);
			z.Add(t0);
			z.Norm();
			z.Add(z);
			z.Add(z);
			z.Norm();

			t2.IMul(3 * ROM.CURVE_B_I);
			if (ECP.SEXTIC_TWIST == ECP.M_TYPE)
			{
				t2.Mul_Ip();
				t2.Norm();
			}

			FP2 x3 = new FP2(t2);
			x3.Mul(z);

			FP2 y3 = new FP2(t0);

			y3.Add(t2);
			y3.Norm();
			z.Mul(t1);
			t1.Copy(t2);
			t1.Add(t2);
			t2.Add(t1);
			t2.Norm();
			t0.Sub(t2);
			t0.Norm(); //y^2-9bz^2
			y3.Mul(t0);
			y3.Add(x3); //(y^2+3z*2)(y^2-9z^2)+3b.z^2.8y^2
			t1.Copy(x);
			t1.Mul(iy);
			x.Copy(t0);
			x.Norm();
			x.Mul(t1);
			x.Add(x); //(y^2-9bz^2)xy2

			x.Norm();
			y.Copy(y3);
			y.Norm();
	//System.out.println("Out of dbl");
			return 1;
		}

	/* this+=Q - return 0 for add, 1 for double, -1 for O */
		public int Add(ECP2 Q)
		{
	//		if (INF)
	//		{
	//			copy(Q);
	//			return -1;
	//		}
	//		if (Q.INF) return -1;
	//System.out.println("Into add");
			int b = 3 * ROM.CURVE_B_I;
			FP2 t0 = new FP2(x);
			t0.Mul(Q.x); // x.Q.x
			FP2 t1 = new FP2(y);
			t1.Mul(Q.y); // y.Q.y

			FP2 t2 = new FP2(z);
			t2.Mul(Q.z);
			FP2 t3 = new FP2(x);
			t3.Add(y);
			t3.Norm(); //t3=X1+Y1
			FP2 t4 = new FP2(Q.x);
			t4.Add(Q.y);
			t4.Norm(); //t4=X2+Y2
			t3.Mul(t4); //t3=(X1+Y1)(X2+Y2)
			t4.Copy(t0);
			t4.Add(t1); //t4=X1.X2+Y1.Y2

			t3.Sub(t4);
			t3.Norm();
			if (ECP.SEXTIC_TWIST == ECP.D_TYPE)
			{
				t3.Mul_Ip();
				t3.Norm(); //t3=(X1+Y1)(X2+Y2)-(X1.X2+Y1.Y2) = X1.Y2+X2.Y1
			}
			t4.Copy(y);
			t4.Add(z);
			t4.Norm(); //t4=Y1+Z1
			FP2 x3 = new FP2(Q.y);
			x3.Add(Q.z);
			x3.Norm(); //x3=Y2+Z2

			t4.Mul(x3); //t4=(Y1+Z1)(Y2+Z2)
			x3.Copy(t1);
			x3.Add(t2); //X3=Y1.Y2+Z1.Z2

			t4.Sub(x3);
			t4.Norm();
			if (ECP.SEXTIC_TWIST == ECP.D_TYPE)
			{
				t4.Mul_Ip();
				t4.Norm(); //t4=(Y1+Z1)(Y2+Z2) - (Y1.Y2+Z1.Z2) = Y1.Z2+Y2.Z1
			}
			x3.Copy(x);
			x3.Add(z);
			x3.Norm(); // x3=X1+Z1
			FP2 y3 = new FP2(Q.x);
			y3.Add(Q.z);
			y3.Norm(); // y3=X2+Z2
			x3.Mul(y3); // x3=(X1+Z1)(X2+Z2)
			y3.Copy(t0);
			y3.Add(t2); // y3=X1.X2+Z1+Z2
			y3.RSub(x3);
			y3.Norm(); // y3=(X1+Z1)(X2+Z2) - (X1.X2+Z1.Z2) = X1.Z2+X2.Z1

			if (ECP.SEXTIC_TWIST == ECP.D_TYPE)
			{
				t0.Mul_Ip();
				t0.Norm(); // x.Q.x
				t1.Mul_Ip();
				t1.Norm(); // y.Q.y
			}
			x3.Copy(t0);
			x3.Add(t0);
			t0.Add(x3);
			t0.Norm();
			t2.IMul(b);
			if (ECP.SEXTIC_TWIST == ECP.M_TYPE)
			{
				t2.Mul_Ip();
				t2.Norm();
			}
			FP2 z3 = new FP2(t1);
			z3.Add(t2);
			z3.Norm();
			t1.Sub(t2);
			t1.Norm();
			y3.IMul(b);
			if (ECP.SEXTIC_TWIST == ECP.M_TYPE)
			{
				y3.Mul_Ip();
				y3.Norm();
			}
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
			return 0;
		}

	/* set this-=Q */
		public int Sub(ECP2 Q)
		{
			ECP2 NQ = new ECP2(Q);
			NQ.Neg();
			int D = Add(NQ);
			//Q.neg();
			//int D=add(Q);
			//Q.neg();
			return D;
		}
	/* set this*=q, where q is Modulus, using Frobenius */
		public void Frob(FP2 X)
		{
	//		if (INF) return;
			FP2 X2 = new FP2(X);

			X2.Sqr();
			x.Conj();
			y.Conj();
			z.Conj();
			z.Reduce();
			x.Mul(X2);

			y.Mul(X2);
			y.Mul(X);
		}

	/* P*=e */
		public ECP2 Mul(BIG e)
		{
	/* fixed size windows */
			int i, nb, s, ns;
			BIG mt = new BIG();
			BIG t = new BIG();
			ECP2 P = new ECP2();
			ECP2 Q = new ECP2();
			ECP2 C = new ECP2();
			ECP2[] W = new ECP2[8];
			byte[] w = new byte[1 + (BIG.NLEN * BIG.BASEBITS + 3) / 4];

			if (IsInfinity())
			{
				return new ECP2();
			}

			//affine();

	/* precompute table */
			Q.Copy(this);
			Q.Dbl();
			W[0] = new ECP2();
			W[0].Copy(this);

			for (i = 1;i < 8;i++)
			{
				W[i] = new ECP2();
				W[i].Copy(W[i - 1]);
				W[i].Add(Q);
			}

	/* make exponent odd - add 2P if even, P if odd */
			t.Copy(e);
			s = t.Parity();
			t.Inc(1);
			t.Norm();
			ns = t.Parity();
			mt.Copy(t);
			mt.Inc(1);
			mt.Norm();
			t.CMove(mt,s);
			Q.CMove(this,ns);
			C.Copy(Q);

			nb = 1 + (t.NBits() + 3) / 4;
	/* convert exponent to signed 4-bit window */
			for (i = 0;i < nb;i++)
			{
				w[i] = (byte)(t.LastBits(5) - 16);
				t.Dec(w[i]);
				t.Norm();
				t.FShr(4);
			}
			w[nb] = (byte)t.LastBits(5);

			P.Copy(W[(w[nb] - 1) / 2]);
			for (i = nb - 1;i >= 0;i--)
			{
				Q.Select(W,w[i]);
				P.Dbl();
				P.Dbl();
				P.Dbl();
				P.Dbl();
				P.Add(Q);
			}
			P.Sub(C);
			P.Affine();
			return P;
		}

	/* P=u0.Q0+u1*Q1+u2*Q2+u3*Q3 */
	// Bos & Costello https://eprint.iacr.org/2013/458.pdf
	// Faz-Hernandez & Longa & Sanchez  https://eprint.iacr.org/2013/158.pdf
	// Side channel attack secure 

		public static ECP2 Mul4(ECP2[] Q, BIG[] u)
		{
			int i, j, nb, pb;
			ECP2 W = new ECP2();
			ECP2 P = new ECP2();
			ECP2[] T = new ECP2[8];

			BIG mt = new BIG();
			BIG[] t = new BIG[4];

			byte[] w = new byte[BIG.NLEN * BIG.BASEBITS + 1];
			byte[] s = new byte[BIG.NLEN * BIG.BASEBITS + 1];

			for (i = 0;i < 4;i++)
			{
				t[i] = new BIG(u[i]);
				t[i].Norm();
				//Q[i].affine();
			}

			T[0] = new ECP2();
			T[0].Copy(Q[0]); // Q[0]
			T[1] = new ECP2();
			T[1].Copy(T[0]);
			T[1].Add(Q[1]); // Q[0]+Q[1]
			T[2] = new ECP2();
			T[2].Copy(T[0]);
			T[2].Add(Q[2]); // Q[0]+Q[2]
			T[3] = new ECP2();
			T[3].Copy(T[1]);
			T[3].Add(Q[2]); // Q[0]+Q[1]+Q[2]
			T[4] = new ECP2();
			T[4].Copy(T[0]);
			T[4].Add(Q[3]); // Q[0]+Q[3]
			T[5] = new ECP2();
			T[5].Copy(T[1]);
			T[5].Add(Q[3]); // Q[0]+Q[1]+Q[3]
			T[6] = new ECP2();
			T[6].Copy(T[2]);
			T[6].Add(Q[3]); // Q[0]+Q[2]+Q[3]
			T[7] = new ECP2();
			T[7].Copy(T[3]);
			T[7].Add(Q[3]); // Q[0]+Q[1]+Q[2]+Q[3]

		// Make it odd
			pb = 1 - t[0].Parity();
			t[0].Inc(pb);
			t[0].Norm();

		// Number of bits
			mt.Zero();
			for (i = 0;i < 4;i++)
			{
				mt.Or(t[i]);
			}
			nb = 1 + mt.NBits();

		// Sign pivot 
			s[nb - 1] = 1;
			for (i = 0;i < nb - 1;i++)
			{
				t[0].FShr(1);
				s[i] = (byte)(2 * t[0].Parity() - 1);
			}

		// Recoded exponent
			for (i = 0; i < nb; i++)
			{
				w[i] = 0;
				int k = 1;
				for (j = 1; j < 4; j++)
				{
					byte bt = (byte)(s[i] * t[j].Parity());
					t[j].FShr(1);
					t[j].Dec((int)(bt) >> 1);
					t[j].Norm();
					w[i] += (byte)(bt * (byte)k);
					k *= 2;
				}
			}

		// Main loop
			P.Select(T,(int)(2 * w[nb - 1] + 1));
			for (i = nb - 2;i >= 0;i--)
			{
				P.Dbl();
				W.Select(T,(int)(2 * w[i] + s[i]));
				P.Add(W);
			}

		// apply correction
			W.Copy(P);
			W.Sub(Q[0]);
			P.CMove(W,pb);
			P.Affine();
			return P;
		}


	/* P=u0.Q0+u1*Q1+u2*Q2+u3*Q3 */
	/*
		public static ECP2 mul4(ECP2[] Q,BIG[] u)
		{
			int i,j,nb;
			int[] a=new int[4];
			ECP2 T=new ECP2();
			ECP2 C=new ECP2();
			ECP2 P=new ECP2();
			ECP2[] W=new ECP2[8];
	
			BIG mt=new BIG();
			BIG[] t=new BIG[4];
	
			byte[] w=new byte[BIG.NLEN*BIG.BASEBITS+1];
	
			for (i=0;i<4;i++)
			{
				t[i]=new BIG(u[i]);
				Q[i].affine();
			}
	
	// precompute table 
	
			W[0]=new ECP2(); W[0].copy(Q[0]); W[0].sub(Q[1]);
	
			W[1]=new ECP2(); W[1].copy(W[0]);
			W[2]=new ECP2(); W[2].copy(W[0]);
			W[3]=new ECP2(); W[3].copy(W[0]);
			W[4]=new ECP2(); W[4].copy(Q[0]); W[4].add(Q[1]);
			W[5]=new ECP2(); W[5].copy(W[4]);
			W[6]=new ECP2(); W[6].copy(W[4]);
			W[7]=new ECP2(); W[7].copy(W[4]);
			T.copy(Q[2]); T.sub(Q[3]);
			W[1].sub(T);
			W[2].add(T);
			W[5].sub(T);
			W[6].add(T);
			T.copy(Q[2]); T.add(Q[3]);
			W[0].sub(T);
			W[3].add(T);
			W[4].sub(T);
			W[7].add(T);
	
	// if multiplier is even add 1 to multiplier, and add P to correction 
			mt.zero(); C.inf();
			for (i=0;i<4;i++)
			{
				if (t[i].parity()==0)
				{
					t[i].inc(1); t[i].norm();
					C.add(Q[i]);
				}
				mt.add(t[i]); mt.norm();
			}
	
			nb=1+mt.nbits();
	
	// convert exponent to signed 1-bit window 
			for (j=0;j<nb;j++)
			{
				for (i=0;i<4;i++)
				{
					a[i]=(byte)(t[i].lastbits(2)-2);
					t[i].dec(a[i]); t[i].norm(); 
					t[i].fshr(1);
				}
				w[j]=(byte)(8*a[0]+4*a[1]+2*a[2]+a[3]);
			}
			w[nb]=(byte)(8*t[0].lastbits(2)+4*t[1].lastbits(2)+2*t[2].lastbits(2)+t[3].lastbits(2));
	
			P.copy(W[(w[nb]-1)/2]);  
			for (i=nb-1;i>=0;i--)
			{
				T.select(W,w[i]);
				P.dbl();
				P.add(T);
			}
			P.sub(C); // apply correction 
	
			P.affine();
			return P;
		}
	*/

	/* needed for SOK */
		public static ECP2 MapIt(byte[] h)
		{
			BIG q = new BIG(ROM.Modulus);
			BIG x = BIG.FromBytes(h);
			BIG one = new BIG(1);
			FP2 X;
			ECP2 Q;
			x.Mod(q);
			while (true)
			{
				X = new FP2(one,x);
				Q = new ECP2(X);
				if (!Q.IsInfinity())
				{
					break;
				}
				x.Inc(1);
				x.Norm();
			}

			BIG Fra = new BIG(ROM.Fra);
			BIG Frb = new BIG(ROM.Frb);
			X = new FP2(Fra,Frb);

			if (ECP.SEXTIC_TWIST == ECP.M_TYPE)
			{
				X.Inverse();
				X.Norm();
			}

			x = new BIG(ROM.CURVE_Bnx);

	/* Fast Hashing to G2 - Fuentes-Castaneda, Knapp and Rodriguez-Henriquez */

			if (ECP.CURVE_PAIRING_TYPE == ECP.BN)
			{
				ECP2 T, K;

				T = new ECP2();
				T.Copy(Q);
				T = T.Mul(x);

				if (ECP.SIGN_OF_X == ECP.NEGATIVEX)
				{
					T.Neg();
				}
				K = new ECP2();
				K.Copy(T);
				K.Dbl();
				K.Add(T); //K.affine();

				K.Frob(X);
				Q.Frob(X);
				Q.Frob(X);
				Q.Frob(X);
				Q.Add(T);
				Q.Add(K);
				T.Frob(X);
				T.Frob(X);
				Q.Add(T);

			}

	/* Efficient hash maps to G2 on BLS curves - Budroni, Pintore */
	/* Q -> x2Q -xQ -Q +F(xQ -Q) +F(F(2Q)) */

			if (ECP.CURVE_PAIRING_TYPE == ECP.BLS)
			{
			//	ECP2 xQ,x2Q;
			//	xQ=new ECP2();
			//	x2Q=new ECP2();

				ECP2 xQ = Q.Mul(x);
				ECP2 x2Q = xQ.Mul(x);

				if (ECP.SIGN_OF_X == ECP.NEGATIVEX)
				{
					xQ.Neg();
				}

				x2Q.Sub(xQ);
				x2Q.Sub(Q);

				xQ.Sub(Q);
				xQ.Frob(X);

				Q.Dbl();
				Q.Frob(X);
				Q.Frob(X);

				Q.Add(x2Q);
				Q.Add(xQ);
			}
			Q.Affine();
			return Q;
		}

		public static ECP2 Generator()
		{
			return new ECP2(new FP2(new BIG(ROM.CURVE_Pxa),new BIG(ROM.CURVE_Pxb)),new FP2(new BIG(ROM.CURVE_Pya),new BIG(ROM.CURVE_Pyb)));
		}


	}
}