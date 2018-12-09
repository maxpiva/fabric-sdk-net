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

/* Finite Field arithmetic  Fp^4 functions */

/* FP4 elements are of the form a+ib, where i is sqrt(-1+sqrt(-1))  */
// ReSharper disable All
#pragma warning disable 162
namespace Hyperledger.Fabric.SDK.AMCL.FP256BN
{
	public sealed class FP4
	{
		private readonly FP2 a;
		private readonly FP2 b;
	/* reduce all components of this mod Modulus */
		public void Reduce()
		{
			a.Reduce();
			b.Reduce();
		}
	/* normalise all components of this mod Modulus */
		public void Norm()
		{
			a.Norm();
			b.Norm();
		}
	/* test this==0 ? */
		public bool IsZilch()
		{
			//reduce();
			return (a.IsZilch() && b.IsZilch());
		}

		public void CMove(FP4 g, int d)
		{
			a.CMove(g.a,d);
			b.CMove(g.b,d);
		}

	/* test this==1 ? */
		public bool IsUnity()
		{
			FP2 one = new FP2(1);
			return (a.Equals(one) && b.IsZilch());
		}

	/* test is w real? That is in a+ib test b is zero */
		public bool IsReal()
		{
			return b.IsZilch();
		}
	/* extract real part a */
		public FP2 Real()
		{
			return a;
		}

		public FP2 GetA()
		{
			return a;
		}
	/* extract imaginary part b */
		public FP2 GetB()
		{
			return b;
		}
	/* test this=x? */
		public bool Equals(FP4 x)
		{
			return (a.Equals(x.a) && b.Equals(x.b));
		}
	/* constructors */
		public FP4(int c)
		{
			a = new FP2(c);
			b = new FP2(0);
		}

		public FP4(FP4 x)
		{
			a = new FP2(x.a);
			b = new FP2(x.b);
		}

		public FP4(FP2 c, FP2 d)
		{
			a = new FP2(c);
			b = new FP2(d);
		}

		public FP4(FP2 c)
		{
			a = new FP2(c);
			b = new FP2(0);
		}
	/* copy this=x */
		public void Copy(FP4 x)
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
	/* set this=-this */
		public void Neg()
		{
			Norm();
			FP2 m = new FP2(a);
			FP2 t = new FP2(0);
			m.Add(b);
	//	m.norm();
			m.Neg();
		//	m.norm();
			t.Copy(m);
			t.Add(b);
			b.Copy(m);
			b.Add(a);
			a.Copy(t);
		Norm();
		}
	/* this=conjugate(this) */
		public void Conj()
		{
			b.Neg();
			Norm();
		}
	/* this=-conjugate(this) */
		public void NConj()
		{
			a.Neg();
			Norm();
		}
	/* this+=x */
		public void Add(FP4 x)
		{
			a.Add(x.a);
			b.Add(x.b);
		}
	/* this-=x */
		public void Sub(FP4 x)
		{
			FP4 m = new FP4(x);
			m.Neg();
			Add(m);
		}

	/* this*=s where s is FP2 */
		public void PMul(FP2 s)
		{
			a.Mul(s);
			b.Mul(s);
		}

	/* this=x-this */
		public void RSub(FP4 x)
		{
			Neg();
			Add(x);
		}


	/* this*=c where c is int */
		public void IMul(int c)
		{
			a.IMul(c);
			b.IMul(c);
		}
	/* this*=this */	
		public void Sqr()
		{
	//		norm();

			FP2 t1 = new FP2(a);
			FP2 t2 = new FP2(b);
			FP2 t3 = new FP2(a);

			t3.Mul(b);
			t1.Add(b);
			t2.Mul_Ip();

			t2.Add(a);

			t1.Norm();
			t2.Norm();

			a.Copy(t1);

			a.Mul(t2);

			t2.Copy(t3);
			t2.Mul_Ip();
			t2.Add(t3);
			t2.Norm();
			t2.Neg();
			a.Add(t2);

			b.Copy(t3);
			b.Add(t3);

			Norm();
		}
	/* this*=y */
		public void Mul(FP4 y)
		{
	//		norm();

			FP2 t1 = new FP2(a);
			FP2 t2 = new FP2(b);
			FP2 t3 = new FP2(0);
			FP2 t4 = new FP2(b);

			t1.Mul(y.a);
			t2.Mul(y.b);
			t3.Copy(y.b);
			t3.Add(y.a);
			t4.Add(a);

		t3.Norm();
		t4.Norm();

			t4.Mul(t3);

		t3.Copy(t1);
		t3.Neg();
		t4.Add(t3);
		t4.Norm();

		//	t4.sub(t1);
		//	t4.norm();

		t3.Copy(t2);
		t3.Neg();
		b.Copy(t4);
		b.Add(t3);

		//	b.copy(t4);
		//	b.sub(t2);

			t2.Mul_Ip();
			a.Copy(t2);
			a.Add(t1);

			Norm();
		}
	/* convert this to hex string */
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
	//		norm();

			FP2 t1 = new FP2(a);
			FP2 t2 = new FP2(b);

			t1.Sqr();
			t2.Sqr();
			t2.Mul_Ip();
		t2.Norm();
			t1.Sub(t2);
			t1.Inverse();
			a.Mul(t1);
			t1.Neg();
		t1.Norm();
			b.Mul(t1);
		}


	/* this*=i where i = sqrt(-1+sqrt(-1)) */
		public void Times_I()
		{
	//		norm();
			FP2 s = new FP2(b);
			FP2 t = new FP2(b);
			s.Times_i();
			t.Add(s);
		//	t.norm();
			b.Copy(a);
			a.Copy(t);
			Norm();
		}

	/* this=this^p using Frobenius */
		public void Frob(FP2 f)
		{
			a.Conj();
			b.Conj();
			b.Mul(f);
		}

	/* this=this^e */
		public FP4 Pow(BIG e)
		{
			Norm();
			e.Norm();
			FP4 w = new FP4(this);
			BIG z = new BIG(e);
			FP4 r = new FP4(1);
			while (true)
			{
				int bt = z.Parity();
				z.FShr(1);
				if (bt == 1)
				{
					r.Mul(w);
				}
				if (z.IsZilch())
				{
					break;
				}
				w.Sqr();
			}
			r.Reduce();
			return r;
		}
	/* XTR xtr_a function */
		public void Xtr_A(FP4 w, FP4 y, FP4 z)
		{
			FP4 r = new FP4(w);
			FP4 t = new FP4(w);
		//y.norm();
			r.Sub(y);
		r.Norm();
			r.PMul(a);
			t.Add(y);
		t.Norm();
			t.PMul(b);
			t.Times_I();

			Copy(r);
			Add(t);
			Add(z);

			Norm();
		}

	/* XTR xtr_d function */
		public void Xtr_D()
		{
			FP4 w = new FP4(this);
			Sqr();
			w.Conj();
			w.Add(w);
		w.Norm();
			Sub(w);
			Reduce();
		}

	/* r=x^n using XTR method on traces of FP12s */
		public FP4 Xtr_Pow(BIG n)
		{
			FP4 a = new FP4(3);
			FP4 b = new FP4(this);
			FP4 c = new FP4(b);
			c.Xtr_D();
			FP4 t = new FP4(0);
			FP4 r = new FP4(0);

			n.Norm();
			int par = n.Parity();
			BIG v = new BIG(n);
			v.FShr(1);
			if (par == 0)
			{
				v.Dec(1);
				v.Norm();
			}

			int nb = v.NBits();
			for (int i = nb - 1;i >= 0;i--)
			{
				if (v.Bit(i) != 1)
				{
					t.Copy(b);
					Conj();
					c.Conj();
					b.Xtr_A(a,this,c);
					Conj();
					c.Copy(t);
					c.Xtr_D();
					a.Xtr_D();
				}
				else
				{
					t.Copy(a);
					t.Conj();
					a.Copy(b);
					a.Xtr_D();
					b.Xtr_A(c,this,t);
					c.Xtr_D();
				}
			}
			if (par == 0)
			{
				r.Copy(c);
			}
			else
			{
				r.Copy(b);
			}
			r.Reduce();
			return r;
		}

	/* r=ck^a.cl^n using XTR double exponentiation method on traces of FP12s. See Stam thesis. */
		public FP4 Xtr_Pow2(FP4 ck, FP4 ckml, FP4 ckm2l, BIG a, BIG b)
		{
			a.Norm();
			b.Norm();
			BIG e = new BIG(a);
			BIG d = new BIG(b);
			BIG w = new BIG(0);

			FP4 cu = new FP4(ck); // can probably be passed in w/o copying
			FP4 cv = new FP4(this);
			FP4 cumv = new FP4(ckml);
			FP4 cum2v = new FP4(ckm2l);
			FP4 r = new FP4(0);
			FP4 t = new FP4(0);

			int f2 = 0;
			while (d.Parity() == 0 && e.Parity() == 0)
			{
				d.FShr(1);
				e.FShr(1);
				f2++;
			}

			while (BIG.Comp(d,e) != 0)
			{
				if (BIG.Comp(d,e) > 0)
				{
					w.Copy(e);
					w.IMul(4);
					w.Norm();
					if (BIG.Comp(d,w) <= 0)
					{
						w.Copy(d);
						d.Copy(e);
						e.RSub(w);
						e.Norm();

						t.Copy(cv);
						t.Xtr_A(cu,cumv,cum2v);
						cum2v.Copy(cumv);
						cum2v.Conj();
						cumv.Copy(cv);
						cv.Copy(cu);
						cu.Copy(t);

					}
					else if (d.Parity() == 0)
					{
						d.FShr(1);
						r.Copy(cum2v);
						r.Conj();
						t.Copy(cumv);
						t.Xtr_A(cu,cv,r);
						cum2v.Copy(cumv);
						cum2v.Xtr_D();
						cumv.Copy(t);
						cu.Xtr_D();
					}
					else if (e.Parity() == 1)
					{
						d.Sub(e);
						d.Norm();
						d.FShr(1);
						t.Copy(cv);
						t.Xtr_A(cu,cumv,cum2v);
						cu.Xtr_D();
						cum2v.Copy(cv);
						cum2v.Xtr_D();
						cum2v.Conj();
						cv.Copy(t);
					}
					else
					{
						w.Copy(d);
						d.Copy(e);
						d.FShr(1);
						e.Copy(w);
						t.Copy(cumv);
						t.Xtr_D();
						cumv.Copy(cum2v);
						cumv.Conj();
						cum2v.Copy(t);
						cum2v.Conj();
						t.Copy(cv);
						t.Xtr_D();
						cv.Copy(cu);
						cu.Copy(t);
					}
				}
				if (BIG.Comp(d,e) < 0)
				{
					w.Copy(d);
					w.IMul(4);
					w.Norm();
					if (BIG.Comp(e,w) <= 0)
					{
						e.Sub(d);
						e.Norm();
						t.Copy(cv);
						t.Xtr_A(cu,cumv,cum2v);
						cum2v.Copy(cumv);
						cumv.Copy(cu);
						cu.Copy(t);
					}
					else if (e.Parity() == 0)
					{
						w.Copy(d);
						d.Copy(e);
						d.FShr(1);
						e.Copy(w);
						t.Copy(cumv);
						t.Xtr_D();
						cumv.Copy(cum2v);
						cumv.Conj();
						cum2v.Copy(t);
						cum2v.Conj();
						t.Copy(cv);
						t.Xtr_D();
						cv.Copy(cu);
						cu.Copy(t);
					}
					else if (d.Parity() == 1)
					{
						w.Copy(e);
						e.Copy(d);
						w.Sub(d);
						w.Norm();
						d.Copy(w);
						d.FShr(1);
						t.Copy(cv);
						t.Xtr_A(cu,cumv,cum2v);
						cumv.Conj();
						cum2v.Copy(cu);
						cum2v.Xtr_D();
						cum2v.Conj();
						cu.Copy(cv);
						cu.Xtr_D();
						cv.Copy(t);
					}
					else
					{
						d.FShr(1);
						r.Copy(cum2v);
						r.Conj();
						t.Copy(cumv);
						t.Xtr_A(cu,cv,r);
						cum2v.Copy(cumv);
						cum2v.Xtr_D();
						cumv.Copy(t);
						cu.Xtr_D();
					}
				}
			}
			r.Copy(cv);
			r.Xtr_A(cu,cumv,cum2v);
			for (int i = 0;i < f2;i++)
			{
				r.Xtr_D();
			}
			r = r.Xtr_Pow(d);
			return r;
		}

	/* this/=2 */
		public void Div2()
		{
			a.Div2();
			b.Div2();
		}

		public void Div_I()
		{
			FP2 u = new FP2(a);
			FP2 v = new FP2(b);
			u.Div_Ip();
			a.Copy(v);
			b.Copy(u);
		}

		public void Div_2I()
		{
			FP2 u = new FP2(a);
			FP2 v = new FP2(b);
			u.Div_Ip2();
			v.Add(v);
			v.Norm();
			a.Copy(v);
			b.Copy(u);
		}


	/* sqrt(a+ib) = sqrt(a+sqrt(a*a-n*b*b)/2)+ib/(2*sqrt(a+sqrt(a*a-n*b*b)/2)) */
	/* returns true if this is QR */
		public bool Sqrt()
		{
			if (IsZilch())
			{
				return true;
			}
			FP2 wa = new FP2(a);
			FP2 ws = new FP2(b);
			FP2 wt = new FP2(a);

			if (ws.IsZilch())
			{
				if (wt.Sqrt())
				{
					a.Copy(wt);
					b.Zero();
				}
				else
				{
					wt.Div_Ip();
					wt.Sqrt();
					b.Copy(wt);
					a.Zero();
				}
				return true;
			}

			ws.Sqr();
			wa.Sqr();
			ws.Mul_Ip();
			ws.Norm();
			wa.Sub(ws);

			ws.Copy(wa);
			if (!ws.Sqrt())
			{
				return false;
			}

			wa.Copy(wt);
			wa.Add(ws);
			wa.Norm();
			wa.Div2();

			if (!wa.Sqrt())
			{
				wa.Copy(wt);
				wa.Sub(ws);
				wa.Norm();
				wa.Div2();
				if (!wa.Sqrt())
				{
					return false;
				}
			}
			wt.Copy(b);
			ws.Copy(wa);
			ws.Add(wa);
			ws.Inverse();

			wt.Mul(ws);
			a.Copy(wa);
			b.Copy(wt);

			return true;
		}

	/* this*=s where s is FP */
		public void QMul(FP s)
		{
			a.PMul(s);
			b.PMul(s);
		}


	}

}