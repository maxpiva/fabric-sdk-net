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

/* AMCL Fp^12 functions */
/* FP12 elements are of the form a+i.b+i^2.c */
// ReSharper disable All
#pragma warning disable 162
namespace Hyperledger.Fabric.SDK.AMCL.FP256BN
{
	public sealed class FP12
	{
		private readonly FP4 a;
		private readonly FP4 b;
		private readonly FP4 c;
	/* reduce all components of this mod Modulus */
		public void Reduce()
		{
			a.Reduce();
			b.Reduce();
			c.Reduce();
		}
	/* normalise all components of this */
		public void Norm()
		{
			a.Norm();
			b.Norm();
			c.Norm();
		}
	/* test x==0 ? */
		public bool IsZilch()
		{
			//reduce();
			return (a.IsZilch() && b.IsZilch() && c.IsZilch());
		}

		public void CMove(FP12 g, int d)
		{
			a.CMove(g.a,d);
			b.CMove(g.b,d);
			c.CMove(g.c,d);
		}


	/* return 1 if b==c, no branching */
		public static int Teq(int b, int c)
		{
			int x = b ^ c;
			x -= 1; // if x=0, x now -1
			return ((x >> 31) & 1);
		}

	/* Constant time select from pre-computed table */
		public void Select(FP12[] g, int b)
		{
			int m = b >> 31;
			int babs = (b ^ m) - m;

			babs = (babs - 1) / 2;

			CMove(g[0],Teq(babs,0)); // conditional move
			CMove(g[1],Teq(babs,1));
			CMove(g[2],Teq(babs,2));
			CMove(g[3],Teq(babs,3));
			CMove(g[4],Teq(babs,4));
			CMove(g[5],Teq(babs,5));
			CMove(g[6],Teq(babs,6));
			CMove(g[7],Teq(babs,7));

			FP12 invf = new FP12(this);
			invf.Conj();
			CMove(invf,(int)(m & 1));
		}


	/* test x==1 ? */
		public bool IsUnity()
		{
			FP4 one = new FP4(1);
			return (a.Equals(one) && b.IsZilch() && c.IsZilch());
		}
	/* return 1 if x==y, else 0 */
		public bool Equals(FP12 x)
		{
			return (a.Equals(x.a) && b.Equals(x.b) && c.Equals(x.c));
		}
	/* extract a from this */
		public FP4 GetA()
		{
			return a;
		}
	/* extract b */
		public FP4 GetB()
		{
			return b;
		}
	/* extract c */
		public FP4 GetC()
		{
			return c;
		}
	/* copy this=x */
		public void Copy(FP12 x)
		{
			a.Copy(x.a);
			b.Copy(x.b);
			c.Copy(x.c);
		}
	/* set this=1 */
		public void One()
		{
			a.One();
			b.Zero();
			c.Zero();
		}
	/* this=conj(this) */
		public void Conj()
		{
			a.Conj();
			b.NConj();
			c.Conj();
		}
	/* Constructors */
		public FP12(FP4 d)
		{
			a = new FP4(d);
			b = new FP4(0);
			c = new FP4(0);
		}

		public FP12(int d)
		{
			a = new FP4(d);
			b = new FP4(0);
			c = new FP4(0);
		}

		public FP12(FP4 d, FP4 e, FP4 f)
		{
			a = new FP4(d);
			b = new FP4(e);
			c = new FP4(f);
		}

		public FP12(FP12 x)
		{
			a = new FP4(x.a);
			b = new FP4(x.b);
			c = new FP4(x.c);
		}

	/* Granger-Scott Unitary Squaring */
		public void USqr()
		{
	//System.out.println("Into usqr");
			FP4 A = new FP4(a);
			FP4 B = new FP4(c);
			FP4 C = new FP4(b);
			FP4 D = new FP4(0);

			a.Sqr();
			D.Copy(a);
			D.Add(a);
			a.Add(D);

			a.Norm();
			A.NConj();

			A.Add(A);
			a.Add(A);
			B.Sqr();
			B.Times_I();

			D.Copy(B);
			D.Add(B);
			B.Add(D);
			B.Norm();

			C.Sqr();
			D.Copy(C);
			D.Add(C);
			C.Add(D);
			C.Norm();

			b.Conj();
			b.Add(b);
			c.NConj();

			c.Add(c);
			b.Add(B);
			c.Add(C);
	//System.out.println("Out of usqr 1");
			Reduce();
	//System.out.println("Out of usqr 2");
		}

	/* Chung-Hasan SQR2 method from http://cacr.uwaterloo.ca/techreports/2006/cacr2006-24.pdf */
		public void Sqr()
		{
	//System.out.println("Into sqr");
			FP4 A = new FP4(a);
			FP4 B = new FP4(b);
			FP4 C = new FP4(c);
			FP4 D = new FP4(a);

			A.Sqr();
			B.Mul(c);
			B.Add(B);
		B.Norm();
			C.Sqr();
			D.Mul(b);
			D.Add(D);

			c.Add(a);
			c.Add(b);
		c.Norm();
			c.Sqr();

			a.Copy(A);

			A.Add(B);
			A.Norm();
			A.Add(C);
			A.Add(D);
			A.Norm();

			A.Neg();
			B.Times_I();
			C.Times_I();

			a.Add(B);

			b.Copy(C);
			b.Add(D);
			c.Add(A);
	//System.out.println("Out of sqr");
			Norm();
		}

	/* FP12 full multiplication this=this*y */
		public void mul(FP12 y)
		{
	//System.out.println("Into mul");
			FP4 z0 = new FP4(a);
			FP4 z1 = new FP4(0);
			FP4 z2 = new FP4(b);
			FP4 z3 = new FP4(0);
			FP4 t0 = new FP4(a);
			FP4 t1 = new FP4(y.a);

			z0.Mul(y.a);
			z2.Mul(y.b);

			t0.Add(b);
			t1.Add(y.b);

		t0.Norm();
		t1.Norm();

			z1.Copy(t0);
			z1.Mul(t1);
			t0.Copy(b);
			t0.Add(c);

			t1.Copy(y.b);
			t1.Add(y.c);

		t0.Norm();
		t1.Norm();

			z3.Copy(t0);
			z3.Mul(t1);

			t0.Copy(z0);
			t0.Neg();
			t1.Copy(z2);
			t1.Neg();

			z1.Add(t0);
			//z1.norm();
			b.Copy(z1);
			b.Add(t1);

			z3.Add(t1);
			z2.Add(t0);

			t0.Copy(a);
			t0.Add(c);
			t1.Copy(y.a);
			t1.Add(y.c);

	t0.Norm();
	t1.Norm();

			t0.Mul(t1);
			z2.Add(t0);

			t0.Copy(c);
			t0.Mul(y.c);
			t1.Copy(t0);
			t1.Neg();

	//		z2.norm();
	//		z3.norm();
	//		b.norm();

			c.Copy(z2);
			c.Add(t1);
			z3.Add(t1);
			t0.Times_I();
			b.Add(t0);
		z3.Norm();
			z3.Times_I();
			a.Copy(z0);
			a.Add(z3);
			Norm();
	//System.out.println("Out of mul");
		}

	/* Special case of multiplication arises from special form of ATE pairing line function */
		public void SMul(FP12 y, int type)
		{
	//System.out.println("Into smul");

			if (type == ECP.D_TYPE)
			{
				FP4 z0 = new FP4(a);
				FP4 z2 = new FP4(b);
				FP4 z3 = new FP4(b);
				FP4 t0 = new FP4(0);
				FP4 t1 = new FP4(y.a);
				z0.Mul(y.a);
				z2.PMul(y.b.Real());
				b.Add(a);
				t1.Real().Add(y.b.Real());

				t1.Norm();
				b.Norm();
				b.Mul(t1);
				z3.Add(c);
				z3.Norm();
				z3.PMul(y.b.Real());

				t0.Copy(z0);
				t0.Neg();
				t1.Copy(z2);
				t1.Neg();

				b.Add(t0);

				b.Add(t1);
				z3.Add(t1);
				z2.Add(t0);

				t0.Copy(a);
				t0.Add(c);
				t0.Norm();
				z3.Norm();
				t0.Mul(y.a);
				c.Copy(z2);
				c.Add(t0);

				z3.Times_I();
				a.Copy(z0);
				a.Add(z3);
			}
			if (type == ECP.M_TYPE)
			{
				FP4 z0 = new FP4(a);
				FP4 z1 = new FP4(0);
				FP4 z2 = new FP4(0);
				FP4 z3 = new FP4(0);
				FP4 t0 = new FP4(a);
				FP4 t1 = new FP4(0);

				z0.Mul(y.a);
				t0.Add(b);
				t0.Norm();

				z1.Copy(t0);
				z1.Mul(y.a);
				t0.Copy(b);
				t0.Add(c);
				t0.Norm();

				z3.Copy(t0); //z3.mul(y.c);
				z3.PMul(y.c.GetB());
				z3.Times_I();

				t0.Copy(z0);
				t0.Neg();

				z1.Add(t0);
				b.Copy(z1);
				z2.Copy(t0);

				t0.Copy(a);
				t0.Add(c);
				t1.Copy(y.a);
				t1.Add(y.c);

				t0.Norm();
				t1.Norm();

				t0.Mul(t1);
				z2.Add(t0);

				t0.Copy(c);

				t0.PMul(y.c.GetB());
				t0.Times_I();

				t1.Copy(t0);
				t1.Neg();

				c.Copy(z2);
				c.Add(t1);
				z3.Add(t1);
				t0.Times_I();
				b.Add(t0);
				z3.Norm();
				z3.Times_I();
				a.Copy(z0);
				a.Add(z3);
			}
			Norm();
	//System.out.println("Out of smul");
		}

	/* this=1/this */
		public void Inverse()
		{
			FP4 f0 = new FP4(a);
			FP4 f1 = new FP4(b);
			FP4 f2 = new FP4(a);
			FP4 f3 = new FP4(0);

			Norm();
			f0.Sqr();
			f1.Mul(c);
			f1.Times_I();
			f0.Sub(f1);
		f0.Norm();

			f1.Copy(c);
			f1.Sqr();
			f1.Times_I();
			f2.Mul(b);
			f1.Sub(f2);
		f1.Norm();

			f2.Copy(b);
			f2.Sqr();
			f3.Copy(a);
			f3.Mul(c);
			f2.Sub(f3);
		f2.Norm();

			f3.Copy(b);
			f3.Mul(f2);
			f3.Times_I();
			a.Mul(f0);
			f3.Add(a);
			c.Mul(f1);
			c.Times_I();

			f3.Add(c);
		f3.Norm();
			f3.Inverse();
			a.Copy(f0);
			a.Mul(f3);
			b.Copy(f1);
			b.Mul(f3);
			c.Copy(f2);
			c.Mul(f3);
		}

	/* this=this^p using Frobenius */
		public void Frob(FP2 f)
		{
			FP2 f2 = new FP2(f);
			FP2 f3 = new FP2(f);

			f2.Sqr();
			f3.Mul(f2);

			a.Frob(f3);
			b.Frob(f3);
			c.Frob(f3);

			b.PMul(f);
			c.PMul(f2);
		}

	/* trace function */
		public FP4 Trace()
		{
			FP4 t = new FP4(0);
			t.Copy(a);
			t.IMul(3);
			t.Reduce();
			return t;
		}

	/* convert from byte array to FP12 */
		public static FP12 FromBytes(byte[] w)
		{
			BIG a, b;
			FP2 c, d;
			FP4 e, f, g;
			byte[] t = new byte[BIG.MODBYTES];

			for (int i = 0;i < BIG.MODBYTES;i++)
			{
				t[i] = w[i];
			}
			a = BIG.FromBytes(t);
			for (int i = 0;i < BIG.MODBYTES;i++)
			{
				t[i] = w[i + BIG.MODBYTES];
			}
			b = BIG.FromBytes(t);
			c = new FP2(a,b);

			for (int i = 0;i < BIG.MODBYTES;i++)
			{
				t[i] = w[i + 2 * BIG.MODBYTES];
			}
			a = BIG.FromBytes(t);
			for (int i = 0;i < BIG.MODBYTES;i++)
			{
				t[i] = w[i + 3 * BIG.MODBYTES];
			}
			b = BIG.FromBytes(t);
			d = new FP2(a,b);

			e = new FP4(c,d);


			for (int i = 0;i < BIG.MODBYTES;i++)
			{
				t[i] = w[i + 4 * BIG.MODBYTES];
			}
			a = BIG.FromBytes(t);
			for (int i = 0;i < BIG.MODBYTES;i++)
			{
				t[i] = w[i + 5 * BIG.MODBYTES];
			}
			b = BIG.FromBytes(t);
			c = new FP2(a,b);

			for (int i = 0;i < BIG.MODBYTES;i++)
			{
				t[i] = w[i + 6 * BIG.MODBYTES];
			}
			a = BIG.FromBytes(t);
			for (int i = 0;i < BIG.MODBYTES;i++)
			{
				t[i] = w[i + 7 * BIG.MODBYTES];
			}
			b = BIG.FromBytes(t);
			d = new FP2(a,b);

			f = new FP4(c,d);


			for (int i = 0;i < BIG.MODBYTES;i++)
			{
				t[i] = w[i + 8 * BIG.MODBYTES];
			}
			a = BIG.FromBytes(t);
			for (int i = 0;i < BIG.MODBYTES;i++)
			{
				t[i] = w[i + 9 * BIG.MODBYTES];
			}
			b = BIG.FromBytes(t);
			c = new FP2(a,b);

			for (int i = 0;i < BIG.MODBYTES;i++)
			{
				t[i] = w[i + 10 * BIG.MODBYTES];
			}
			a = BIG.FromBytes(t);
			for (int i = 0;i < BIG.MODBYTES;i++)
			{
				t[i] = w[i + 11 * BIG.MODBYTES];
			}
			b = BIG.FromBytes(t);
			d = new FP2(a,b);

			g = new FP4(c,d);

			return new FP12(e,f,g);
		}

	/* convert this to byte array */
		public void ToBytes(byte[] w)
		{
			byte[] t = new byte[BIG.MODBYTES];
			a.GetA().A.ToBytes(t);
			for (int i = 0;i < BIG.MODBYTES;i++)
			{
				w[i] = t[i];
			}
			a.GetA().B.ToBytes(t);
			for (int i = 0;i < BIG.MODBYTES;i++)
			{
				w[i + BIG.MODBYTES] = t[i];
			}
			a.GetB().A.ToBytes(t);
			for (int i = 0;i < BIG.MODBYTES;i++)
			{
				w[i + 2 * BIG.MODBYTES] = t[i];
			}
			a.GetB().B.ToBytes(t);
			for (int i = 0;i < BIG.MODBYTES;i++)
			{
				w[i + 3 * BIG.MODBYTES] = t[i];
			}

			b.GetA().A.ToBytes(t);
			for (int i = 0;i < BIG.MODBYTES;i++)
			{
				w[i + 4 * BIG.MODBYTES] = t[i];
			}
			b.GetA().B.ToBytes(t);
			for (int i = 0;i < BIG.MODBYTES;i++)
			{
				w[i + 5 * BIG.MODBYTES] = t[i];
			}
			b.GetB().A.ToBytes(t);
			for (int i = 0;i < BIG.MODBYTES;i++)
			{
				w[i + 6 * BIG.MODBYTES] = t[i];
			}
			b.GetB().B.ToBytes(t);
			for (int i = 0;i < BIG.MODBYTES;i++)
			{
				w[i + 7 * BIG.MODBYTES] = t[i];
			}

			c.GetA().A.ToBytes(t);
			for (int i = 0;i < BIG.MODBYTES;i++)
			{
				w[i + 8 * BIG.MODBYTES] = t[i];
			}
			c.GetA().B.ToBytes(t);
			for (int i = 0;i < BIG.MODBYTES;i++)
			{
				w[i + 9 * BIG.MODBYTES] = t[i];
			}
			c.GetB().A.ToBytes(t);
			for (int i = 0;i < BIG.MODBYTES;i++)
			{
				w[i + 10 * BIG.MODBYTES] = t[i];
			}
			c.GetB().B.ToBytes(t);
			for (int i = 0;i < BIG.MODBYTES;i++)
			{
				w[i + 11 * BIG.MODBYTES] = t[i];
			}
		}

	/* convert to hex string */
		public override string ToString()
		{
			return ("[" + a.ToString() + "," + b.ToString() + "," + c.ToString() + "]");
		}

	/* this=this^e */ 
	/* Note this is simple square and multiply, so not side-channel safe */
		public FP12 Pow(BIG e)
		{
			Norm();
			e.Norm();
			BIG e3 = new BIG(e);
			e3.PMul(3);
			e3.Norm();

			FP12 w = new FP12(this);

			int nb = e3.NBits();
			for (int i = nb - 2;i >= 1;i--)
			{
				w.USqr();
				int bt = e3.Bit(i) - e.Bit(i);
				if (bt == 1)
				{
					w.mul(this);
				}
				if (bt == -1)
				{
					Conj();
					w.mul(this);
					Conj();
				}
			}
			w.Reduce();
			return w;


	/*
			BIG z=new BIG(e);
			FP12 r=new FP12(1);
	
			while (true)
			{
				int bt=z.parity();
				z.fshr(1);
				if (bt==1) r.mul(w);
				if (z.iszilch()) break;
				w.usqr();
			}
			r.reduce();
			return r; */
		}

	/* constant time powering by small integer of max length bts */
		public void PinPow(int e, int bts)
		{
			int i, b;
			FP12[] R = new FP12[2];
			R[0] = new FP12(1);
			R[1] = new FP12(this);
			for (i = bts - 1;i >= 0;i--)
			{
				b = (e >> i) & 1;
				R[1 - b].mul(R[b]);
				R[b].USqr();
			}
			this.Copy(R[0]);
		}

		public FP4 ComPow(BIG e, BIG r)
		{
			FP12 g1 = new FP12(0);
			FP12 g2 = new FP12(0);
			FP2 f = new FP2(new BIG(ROM.Fra),new BIG(ROM.Frb));
			BIG q = new BIG(ROM.Modulus);

			BIG m = new BIG(q);
			m.Mod(r);

			BIG a = new BIG(e);
			a.Mod(m);

			BIG b = new BIG(e);
			b.Div(m);

			g1.Copy(this);
			g2.Copy(this);

			FP4 c = g1.Trace();

			if (b.IsZilch())
			{
				c = c.Xtr_Pow(e);
				return c;
			}

			g2.Frob(f);
			FP4 cp = g2.Trace();
			g1.Conj();
			g2.mul(g1);
			FP4 cpm1 = g2.Trace();
			g2.mul(g1);
			FP4 cpm2 = g2.Trace();

			c = c.Xtr_Pow2(cp,cpm1,cpm2,a,b);

			return c;
		}

	/* p=q0^u0.q1^u1.q2^u2.q3^u3 */
	// Bos & Costello https://eprint.iacr.org/2013/458.pdf
	// Faz-Hernandez & Longa & Sanchez  https://eprint.iacr.org/2013/158.pdf
	// Side channel attack secure 

		public static FP12 Pow4(FP12[] q, BIG[] u)
		{
			int i, j, nb, pb;
			FP12[] g = new FP12[8];
			FP12 r = new FP12(1);
			FP12 p = new FP12(0);
			BIG[] t = new BIG[4];
			BIG mt = new BIG(0);
			byte[] w = new byte[BIG.NLEN * BIG.BASEBITS + 1];
			byte[] s = new byte[BIG.NLEN * BIG.BASEBITS + 1];

			for (i = 0;i < 4;i++)
			{
				t[i] = new BIG(u[i]);
				t[i].Norm();
			}
			g[0] = new FP12(q[0]); // q[0]
			g[1] = new FP12(g[0]);
			g[1].mul(q[1]); // q[0].q[1]
			g[2] = new FP12(g[0]);
			g[2].mul(q[2]); // q[0].q[2]
			g[3] = new FP12(g[1]);
			g[3].mul(q[2]); // q[0].q[1].q[2]
			g[4] = new FP12(q[0]);
			g[4].mul(q[3]); // q[0].q[3]
			g[5] = new FP12(g[1]);
			g[5].mul(q[3]); // q[0].q[1].q[3]
			g[6] = new FP12(g[2]);
			g[6].mul(q[3]); // q[0].q[2].q[3]
			g[7] = new FP12(g[3]);
			g[7].mul(q[3]); // q[0].q[1].q[2].q[3]

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
			p.Select(g,(int)(2 * w[nb - 1] + 1));
			for (i = nb - 2;i >= 0;i--)
			{
				p.USqr();
				r.Select(g,(int)(2 * w[i] + s[i]));
				p.mul(r);
			}

		// apply correction
			r.Copy(q[0]);
			r.Conj();
			r.mul(p);
			p.CMove(r,pb);

			 p.Reduce();
			return p;
		}

	}

}