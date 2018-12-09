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

/* Finite Field arithmetic */
/* AMCL mod p functions */
// ReSharper disable All
#pragma warning disable 162
namespace Hyperledger.Fabric.SDK.AMCL.FP256BN
{
	public sealed class FP
	{

		public const int NOT_SPECIAL = 0;
		public const int PSEUDO_MERSENNE = 1;
		public const int MONTGOMERY_FRIENDLY = 2;
		public const int GENERALISED_MERSENNE = 3;

		public const int MODBITS = 256; // Number of bits in Modulus
		public const int MOD8 = 3; // Modulus mod 8
		public const int MODTYPE = NOT_SPECIAL;

		public static readonly int FEXCESS = ((int)1 << 24); // BASEBITS*NLEN-MODBITS or 2^30 max!
		public static readonly long OMASK = (long)(-1) << (MODBITS % BIG.BASEBITS);
		public static readonly int TBITS = MODBITS % BIG.BASEBITS; // Number of active bits in top word
		public static readonly long TMASK = ((long)1 << TBITS) - 1;


		public readonly BIG x;
		//public BIG p=new BIG(ROM.Modulus);
		//public BIG r2modp=new BIG(ROM.R2modp);
		public int XES;

	/// <summary>
	///************** 64-bit specific *********************** </summary>

	/* reduce a DBIG to a BIG using the appropriate form of the modulus */
		public static BIG Mod(DBIG d)
		{
			if (MODTYPE == PSEUDO_MERSENNE)
			{
				BIG b;
				long v, tw;
				BIG t = d.Split(MODBITS);
				b = new BIG(d);

				v = t.PMul(unchecked((int)ROM.MConst));

				t.Add(b);
				t.Norm();

				tw = t.w[BIG.NLEN - 1];
				t.w[BIG.NLEN - 1] &= FP.TMASK;
				t.w[0] += (ROM.MConst * ((tw >> TBITS) + (v << (BIG.BASEBITS - TBITS))));

				t.Norm();
				return t;
			}
			if (FP.MODTYPE == MONTGOMERY_FRIENDLY)
			{
				BIG b;
				long[] cr = new long[2];
				for (int i = 0;i < BIG.NLEN;i++)
				{
					cr = BIG.MulAdd(d.w[i],ROM.MConst - 1,d.w[i],d.w[BIG.NLEN + i - 1]);
					d.w[BIG.NLEN + i] += cr[0];
					d.w[BIG.NLEN + i - 1] = cr[1];
				}

				b = new BIG(0);
				for (int i = 0;i < BIG.NLEN;i++)
				{
					b.w[i] = d.w[BIG.NLEN + i];
				}
				b.Norm();
				return b;
			}
			if (MODTYPE == GENERALISED_MERSENNE)
			{ // GoldiLocks Only
				BIG b;
				BIG t = d.Split(MODBITS);
				b = new BIG(d);
				b.Add(t);
				DBIG dd = new DBIG(t);
				dd.Shl(MODBITS / 2);

				BIG tt = dd.Split(MODBITS);
				BIG lo = new BIG(dd);
				b.Add(tt);
				b.Add(lo);
				b.Norm();
				tt.Shl(MODBITS / 2);
				b.Add(tt);

				long carry = b.w[BIG.NLEN - 1] >> TBITS;
				b.w[BIG.NLEN - 1] &= FP.TMASK;
				b.w[0] += carry;

				b.w[224 / BIG.BASEBITS] += carry << (224 % BIG.BASEBITS);
				b.Norm();
				return b;
			}
			if (MODTYPE == NOT_SPECIAL)
			{
				return BIG.Monty(new BIG(ROM.Modulus),ROM.MConst,d);
			}

			return new BIG(0);
		}



	/// <summary>
	///****************************************************** </summary>


	/* Constructors */
		public FP(int a)
		{
			x = new BIG(a);
			NRes();
		}

		public FP()
		{
			x = new BIG(0);
			XES = 1;
		}

		public FP(BIG a)
		{
			x = new BIG(a);
			NRes();
		}

		public FP(FP a)
		{
			x = new BIG(a.x);
			XES = a.XES;
		}

	/* convert to string */
		public override string ToString()
		{
			string s = Redc().ToString();
			return s;
		}

		public string ToRawString()
		{
			string s = x.ToRawString();
			return s;
		}

	/* convert to Montgomery n-residue form */
		public void NRes()
		{
			if (MODTYPE != PSEUDO_MERSENNE && MODTYPE != GENERALISED_MERSENNE)
			{
				DBIG d = BIG.Mul(x,new BIG(ROM.R2modp)); //** Change **
				x.Copy(Mod(d));
				XES = 2;
			}
			else
			{
				XES = 1;
			}
		}

	/* convert back to regular form */
		public BIG Redc()
		{
			if (MODTYPE != PSEUDO_MERSENNE && MODTYPE != GENERALISED_MERSENNE)
			{
				DBIG d = new DBIG(x);
				return Mod(d);
			}
			else
			{
				BIG r = new BIG(x);
				return r;
			}
		}

	/* test this=0? */
		public bool IsZilch()
		{
			FP z = new FP(this);
			z.Reduce();
			return z.x.IsZilch();

		}

	/* copy from FP b */
		public void Copy(FP b)
		{
			x.Copy(b.x);
			XES = b.XES;
		}

	/* set this=0 */
		public void Zero()
		{
			x.Zero();
			XES = 1;
		}

	/* set this=1 */
		public void One()
		{
			x.One();
			NRes();
		}

	/* normalise this */
		public void Norm()
		{
			x.Norm();
		}

	/* swap FPs depending on d */
		public void CSwap(FP b, int d)
		{
			x.CSwap(b.x,d);
			int t, c = d;
			c = ~(c - 1);
			t = c & (XES ^ b.XES);
			XES ^= t;
			b.XES ^= t;
		}

	/* copy FPs depending on d */
		public void CMove(FP b, int d)
		{
			x.CMove(b.x,d);
			XES ^= (XES ^ b.XES) & (-d);

		}

	/* this*=b mod Modulus */
		public void Mul(FP b)
		{
			if ((long)XES * b.XES > (long)FEXCESS)
			{
				Reduce();
			}

			DBIG d = BIG.Mul(x,b.x);
			x.Copy(Mod(d));
			XES = 2;
		}

	/* this*=c mod Modulus, where c is a small int */
		public void IMul(int c)
		{
	//		norm();
			bool s = false;
			if (c < 0)
			{
				c = -c;
				s = true;
			}

			if (MODTYPE == PSEUDO_MERSENNE || MODTYPE == GENERALISED_MERSENNE)
			{
				DBIG d = x.PXMul(c);
				x.Copy(Mod(d));
				XES = 2;
			}
			else
			{
				if (XES * c <= FEXCESS)
				{
					x.PMul(c);
					XES *= c;
				}
				else
				{ // this is not good
					FP n = new FP(c);
					Mul(n);
				}
			}

	/*
			if (c<=BIG.NEXCESS && XES*c<=FEXCESS)
			{
				x.imul(c);
				XES*=c;
				x.norm();
			}
			else
			{
				DBIG d=x.pxmul(c);
				x.copy(mod(d));
				XES=2;
			}
	*/
			if (s)
			{
				Neg();
				Norm();
			}

		}

	/* this*=this mod Modulus */
		public void Sqr()
		{
			DBIG d;
			if ((long)XES * XES > (long)FEXCESS)
			{
				Reduce();
			}

			d = BIG.Sqr(x);
			x.Copy(Mod(d));
			XES = 2;
		}

	/* this+=b */
		public void Add(FP b)
		{
			x.Add(b.x);
			XES += b.XES;
			if (XES > FEXCESS)
			{
				Reduce();
			}
		}

	// https://graphics.stanford.edu/~seander/bithacks.html
	// constant time log to base 2 (or number of bits in)

		private static int LogB2(int v)
		{
			int r;
			v |= (int)((uint)v >> 1);
			v |= (int)((uint)v >> 2);
			v |= (int)((uint)v >> 4);
			v |= (int)((uint)v >> 8);
			v |= (int)((uint)v >> 16);

			v = v - (((int)((uint)v >> 1)) & 0x55555555);
			v = (v & 0x33333333) + (((int)((uint)v >> 2)) & 0x33333333);
			r = (int)((uint)((v + ((int)((uint)v >> 4)) & 0xF0F0F0F) * 0x1010101) >> 24);
			return r;
		}

	/* this = -this mod Modulus */
		public void Neg()
		{
			int sb;
			BIG m = new BIG(ROM.Modulus);

			sb = LogB2(XES - 1);
			m.FShl(sb);
			x.RSub(m);

			XES = (1 << sb);
			if (XES > FEXCESS)
			{
				Reduce();
			}
		}

	/* this-=b */
		public void Sub(FP b)
		{
			FP n = new FP(b);
			n.Neg();
			this.Add(n);
		}

		public void RSub(FP b)
		{
			FP n = new FP(this);
			n.Neg();
			this.Copy(b);
			this.Add(n);
		}

	/* this/=2 mod Modulus */
		public void Div2()
		{
			if (x.Parity() == 0)
			{
				x.FShr(1);
			}
			else
			{
				x.Add(new BIG(ROM.Modulus));
				x.Norm();
				x.FShr(1);
			}
		}

	/* this=1/this mod Modulus */
		public void Inverse()
		{
	/*
			BIG r=redc();
			r.invmodp(p);
			x.copy(r);
			nres();
	*/
			BIG m2 = new BIG(ROM.Modulus);
			m2.Dec(2);
			m2.Norm();
			Copy(Pow(m2));

		}

	/* return TRUE if this==a */
		public bool Equals(FP a)
		{
			FP f = new FP(this);
			FP s = new FP(a);
			f.Reduce();
			s.Reduce();
			if (BIG.Comp(f.x,s.x) == 0)
			{
				return true;
			}
			return false;
		}

	/* reduce this mod Modulus */
		public void Reduce()
		{
			x.Mod(new BIG(ROM.Modulus));
			XES = 1;
		}

		public FP Pow(BIG e)
		{
			byte[] w = new byte[1 + (BIG.NLEN * BIG.BASEBITS + 3) / 4];
			FP[] tb = new FP[16];
			BIG t = new BIG(e);
			t.Norm();
			int nb = 1 + (t.NBits() + 3) / 4;

			for (int i = 0;i < nb;i++)
			{
				int lsbs = t.LastBits(4);
				t.Dec(lsbs);
				t.Norm();
				w[i] = (byte)lsbs;
				t.FShr(4);
			}
			tb[0] = new FP(1);
			tb[1] = new FP(this);
			for (int i = 2;i < 16;i++)
			{
				tb[i] = new FP(tb[i - 1]);
				tb[i].Mul(this);
			}
			FP r = new FP(tb[w[nb - 1]]);
			for (int i = nb - 2;i >= 0;i--)
			{
				r.Sqr();
				r.Sqr();
				r.Sqr();
				r.Sqr();
				r.Mul(tb[w[i]]);
			}
			r.Reduce();
			return r;
		}

	/* return this^e mod Modulus 
		public FP pow(BIG e)
		{
			int bt;
			FP r=new FP(1);
			e.norm();
			x.norm();
			FP m=new FP(this);
			while (true)
			{
				bt=e.parity();
				e.fshr(1);
				if (bt==1) r.mul(m);
				if (e.iszilch()) break;
				m.sqr();
			}
			r.x.mod(p);
			return r;
		} */

	/* return sqrt(this) mod Modulus */
		public FP Sqrt()
		{
			Reduce();
			BIG b = new BIG(ROM.Modulus);
			if (MOD8 == 5)
			{
				b.Dec(5);
				b.Norm();
				b.Shr(3);
				FP i = new FP(this);
				i.x.Shl(1);
				FP v = i.Pow(b);
				i.Mul(v);
				i.Mul(v);
				i.x.Dec(1);
				FP r = new FP(this);
				r.Mul(v);
				r.Mul(i);
				r.Reduce();
				return r;
			}
			else
			{
				b.Inc(1);
				b.Norm();
				b.Shr(2);
				return Pow(b);
			}
		}

	/* return jacobi symbol (this/Modulus) */
		public int Jacobi()
		{
			BIG w = Redc();
			return w.Jacobi(new BIG(ROM.Modulus));
		}

	}

}