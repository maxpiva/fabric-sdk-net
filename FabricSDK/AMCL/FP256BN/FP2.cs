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

/* Finite Field arithmetic  Fp^2 functions */

/* FP2 elements are of the form a+ib, where i is sqrt(-1) */
// ReSharper disable All
#pragma warning disable 162
namespace Hyperledger.Fabric.SDK.AMCL.FP256BN
{
	public sealed class FP2
	{
		private readonly FP a;
		private readonly FP b;

	/* reduce components mod Modulus */
		public void Reduce()
		{
			a.Reduce();
			b.Reduce();
		}

	/* normalise components of w */
		public void Norm()
		{
			a.Norm();
			b.Norm();
		}

	/* test this=0 ? */
		public bool IsZilch()
		{
			//reduce();
			return (a.IsZilch() && b.IsZilch());
		}

		public void CMove(FP2 g, int d)
		{
			a.CMove(g.a,d);
			b.CMove(g.b,d);
		}

	/* test this=1 ? */
		public bool IsUnity()
		{
			FP one = new FP(1);
			return (a.Equals(one) && b.IsZilch());
		}

	/* test this=x */
		public bool Equals(FP2 x)
		{
			return (a.Equals(x.a) && b.Equals(x.b));
		}

	/* Constructors */
		public FP2(int c)
		{
			a = new FP(c);
			b = new FP(0);
		}

		public FP2(FP2 x)
		{
			a = new FP(x.a);
			b = new FP(x.b);
		}

		public FP2(FP c, FP d)
		{
			a = new FP(c);
			b = new FP(d);
		}

		public FP2(BIG c, BIG d)
		{
			a = new FP(c);
			b = new FP(d);
		}

		public FP2(FP c)
		{
			a = new FP(c);
			b = new FP(0);
		}

		public FP2(BIG c)
		{
			a = new FP(c);
			b = new FP(0);
		}
	/*
		public BIG geta()
		{
			return a.tobig();
		}
	*/
	/* extract a */
		public BIG A
		{
			get
			{
				return a.Redc();
			}
		}

	/* extract b */
		public BIG B
		{
			get
			{
				return b.Redc();
			}
		}

	/* copy this=x */
		public void Copy(FP2 x)
		{
			a.Copy(x.a);
			b.Copy(x.b);
		}

	/* set this=0 */
		public void Zero()
		{
			a.Zero();
			b.Zero();
		}

	/* set this=1 */
		public void One()
		{
			a.One();
			b.Zero();
		}

	/* negate this mod Modulus */
		public void Neg()
		{
			FP m = new FP(a);
			FP t = new FP(0);

			m.Add(b);
			m.Neg();
			t.Copy(m);
			t.Add(b);
			b.Copy(m);
			b.Add(a);
			a.Copy(t);
		}

	/* set to a-ib */
		public void Conj()
		{
			b.Neg();
			b.Norm();
		}

	/* this+=a */
		public void Add(FP2 x)
		{
			a.Add(x.a);
			b.Add(x.b);
		}

	/* this-=a */
		public void Sub(FP2 x)
		{
			FP2 m = new FP2(x);
			m.Neg();
			Add(m);
		}

		public void RSub(FP2 x) // *****
		{
			Neg();
			Add(x);
		}

	/* this*=s, where s is an FP */
		public void PMul(FP s)
		{
			a.Mul(s);
			b.Mul(s);
		}

	/* this*=i, where i is an int */
		public void IMul(int c)
		{
			a.IMul(c);
			b.IMul(c);
		}

	/* this*=this */
		public void Sqr()
		{
			FP w1 = new FP(a);
			FP w3 = new FP(a);
			FP mb = new FP(b);

			w1.Add(b);
			mb.Neg();

			w3.Add(a);
			w3.Norm();
			b.Mul(w3);

			a.Add(mb);

			w1.Norm();
			a.Norm();

			a.Mul(w1);
		}

	/* this*=y */
	/* Now uses Lazy reduction */
		public void Mul(FP2 y)
		{
			if ((long)(a.XES + b.XES) * (y.a.XES + y.b.XES) > (long)FP.FEXCESS)
			{
				if (a.XES > 1)
				{
					a.Reduce();
				}
				if (b.XES > 1)
				{
					b.Reduce();
				}
			}

			DBIG pR = new DBIG(0);
			BIG C = new BIG(a.x);
			BIG D = new BIG(y.a.x);

			pR.UCopy(new BIG(ROM.Modulus));

			DBIG A = BIG.Mul(a.x,y.a.x);
			DBIG B = BIG.Mul(b.x,y.b.x);

			C.Add(b.x);
			C.Norm();
			D.Add(y.b.x);
			D.Norm();

			DBIG E = BIG.Mul(C,D);
			DBIG F = new DBIG(A);
			F.Add(B);
			B.RSub(pR);

			A.Add(B);
			A.Norm();
			E.Sub(F);
			E.Norm();

			a.x.Copy(FP.Mod(A));
			a.XES = 3;
			b.x.Copy(FP.Mod(E));
			b.XES = 2;
		}

	/* sqrt(a+ib) = sqrt(a+sqrt(a*a-n*b*b)/2)+ib/(2*sqrt(a+sqrt(a*a-n*b*b)/2)) */
	/* returns true if this is QR */
		public bool Sqrt()
		{
			if (IsZilch())
			{
				return true;
			}
			FP w1 = new FP(b);
			FP w2 = new FP(a);
			w1.Sqr();
			w2.Sqr();
			w1.Add(w2);
			if (w1.Jacobi() != 1)
			{
				Zero();
				return false;
			}
			w1 = w1.Sqrt();
			w2.Copy(a);
			w2.Add(w1);
			w2.Norm();
			w2.Div2();
			if (w2.Jacobi() != 1)
			{
				w2.Copy(a);
				w2.Sub(w1);
				w2.Norm();
				w2.Div2();
				if (w2.Jacobi() != 1)
				{
					Zero();
					return false;
				}
			}
			w2 = w2.Sqrt();
			a.Copy(w2);
			w2.Add(w2);
			w2.Inverse();
			b.Mul(w2);
			return true;
		}

	/* output to hex string */
		public override string ToString()
		{
			return ("[" + a.ToString() + "," + b.ToString() + "]");
		}

		public string ToRawString()
		{
			return ("[" + a.ToRawString() + "," + b.ToRawString() + "]");
		}

	/* this=1/this */
		public void Inverse()
		{
			Norm();
			FP w1 = new FP(a);
			FP w2 = new FP(b);

			w1.Sqr();
			w2.Sqr();
			w1.Add(w2);
			w1.Inverse();
			a.Mul(w1);
			w1.Neg();
			w1.Norm();
			b.Mul(w1);
		}

	/* this/=2 */
		public void Div2()
		{
			a.Div2();
			b.Div2();
		}

	/* this*=sqrt(-1) */
		public void Times_i()
		{
			FP z = new FP(a);
			a.Copy(b);
			a.Neg();
			b.Copy(z);
		}

	/* w*=(1+sqrt(-1)) */
	/* where X*2-(1+sqrt(-1)) is irreducible for FP4, assumes p=3 mod 8 */
		public void Mul_Ip()
		{
			FP2 t = new FP2(this);
			FP z = new FP(a);
			a.Copy(b);
			a.Neg();
			b.Copy(z);
			Add(t);
		}

		public void Div_Ip2()
		{
			FP2 t = new FP2(0);
			Norm();
			t.a.Copy(a);
			t.a.Add(b);
			t.b.Copy(b);
			t.b.Sub(a);
			Copy(t);
			Norm();
		}

	/* w/=(1+sqrt(-1)) */
		public void Div_Ip()
		{
			FP2 t = new FP2(0);
			Norm();
			t.a.Copy(a);
			t.a.Add(b);
			t.b.Copy(b);
			t.b.Sub(a);
			Copy(t);
			Norm();
			Div2();
		}
	
	}
}