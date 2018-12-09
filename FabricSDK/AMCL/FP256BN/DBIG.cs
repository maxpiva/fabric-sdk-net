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

/* AMCL double length DBIG number class */

namespace Hyperledger.Fabric.SDK.AMCL.FP256BN
{
	public class DBIG
	{
		protected internal long[] w = new long[BIG.DNLEN];

	/* normalise this */
		public virtual void Norm()
		{
			long d, carry = 0;
			for (int i = 0;i < BIG.DNLEN - 1;i++)
			{
				d = w[i] + carry;
				carry = d >> BIG.BASEBITS;
				w[i] = d & BIG.BMASK;
			}
			w[BIG.DNLEN - 1] = (w[BIG.DNLEN - 1] + carry);
		}


	/*
		public String toRawString()
		{
			DBIG b=new DBIG(this);
			String s="(";
			for (int i=0;i<BIG.DNLEN-1;i++)
			{
				s+=Long.toHexString(b.w[i]); s+=",";
			}
			s+=Long.toHexString(b.w[BIG.DNLEN-1]); s+=")";
			return s;
		}
	*/

	/* split DBIG at position n, return higher half, keep lower half */
		public virtual BIG Split(int n)
		{
			BIG t = new BIG(0);
			int m = n % BIG.BASEBITS;
			long nw, carry = w[BIG.DNLEN - 1] << (BIG.BASEBITS - m);

			for (int i = BIG.DNLEN - 2;i >= BIG.NLEN - 1;i--)
			{
				nw = (w[i] >> m) | carry;
				carry = (w[i] << (BIG.BASEBITS - m)) & BIG.BMASK;
				t.w[i - BIG.NLEN + 1] = nw;
				//t.set(i-BIG.NLEN+1,nw);
			}
			w[BIG.NLEN - 1] &= (((long)1 << m) - 1);
			return t;
		}

	/// <summary>
	///************************************************************************* </summary>

	/* return number of bits in this */
		public virtual int NBits()
		{
			int bts, k = BIG.DNLEN - 1;
			long c;
			Norm();
			while (w[k] == 0 && k >= 0)
			{
				k--;
			}
			if (k < 0)
			{
				return 0;
			}
			bts = BIG.BASEBITS * k;
			c = w[k];
			while (c != 0)
			{
				c /= 2;
				bts++;
			}
			return bts;
		}

	/* convert this to string */
		public override string ToString()
		{
			DBIG b;
			string s = "";
			int len = NBits();
			if (len % 4 == 0)
			{
				len >>= 2; //len/=4;
			}
			else
			{
				len >>= 2;
				len++;
			}

			for (int i = len - 1;i >= 0;i--)
			{
				b = new DBIG(this);
				b.Shr(i * 4);
				s += ((int)(b.w[0] & 15)).ToString("x");
			}
			return s;
		}

		public virtual void CMove(DBIG g, int d)
		{
			int i;
			for (i = 0;i < BIG.DNLEN;i++)
			{
				w[i] ^= (w[i] ^ g.w[i]) & BIG.CastToChunk(-d);
			}
		}

	/* Constructors */
		public DBIG(int x)
		{
			w[0] = x;
			for (int i = 1;i < BIG.DNLEN;i++)
			{
				w[i] = 0;
			}
		}

		public DBIG(DBIG x)
		{
			for (int i = 0;i < BIG.DNLEN;i++)
			{
				w[i] = x.w[i];
			}
		}

		public DBIG(BIG x)
		{
			for (int i = 0;i < BIG.NLEN - 1;i++)
			{
				w[i] = x.w[i]; //get(i);
			}

			w[BIG.NLEN - 1] = x.w[(BIG.NLEN - 1)] & BIG.BMASK; // top word normalized
			w[BIG.NLEN] = (x.w[(BIG.NLEN - 1)] >> BIG.BASEBITS);

			for (int i = BIG.NLEN + 1;i < BIG.DNLEN;i++)
			{
				w[i] = 0;
			}
		}

	/* Copy from another DBIG */
		public virtual void Copy(DBIG x)
		{
			for (int i = 0;i < BIG.DNLEN;i++)
			{
				w[i] = x.w[i];
			}
		}

	/* Copy into upper part */
		public virtual void UCopy(BIG x)
		{
			for (int i = 0;i < BIG.NLEN;i++)
			{
				w[i] = 0;
			}
			for (int i = BIG.NLEN;i < BIG.DNLEN;i++)
			{
				w[i] = x.w[i - BIG.NLEN];
			}
		}

	/* test this=0? */
		public virtual bool IsZilch()
		{
			for (int i = 0;i < BIG.DNLEN;i++)
			{
				if (w[i] != 0)
				{
					return false;
				}
			}
			return true;
		}

	/* shift this right by k bits */
		public virtual void Shr(int k)
		{
			int n = k % BIG.BASEBITS;
			int m = k / BIG.BASEBITS;
			for (int i = 0;i < BIG.DNLEN - m - 1;i++)
			{
				w[i] = (w[m + i] >> n) | ((w[m + i + 1] << (BIG.BASEBITS - n)) & BIG.BMASK);
			}
			w[BIG.DNLEN - m - 1] = w[BIG.DNLEN - 1] >> n;
			for (int i = BIG.DNLEN - m;i < BIG.DNLEN;i++)
			{
				w[i] = 0;
			}
		}

	/* shift this left by k bits */
		public virtual void Shl(int k)
		{
			int n = k % BIG.BASEBITS;
			int m = k / BIG.BASEBITS;

			w[BIG.DNLEN - 1] = ((w[BIG.DNLEN - 1 - m] << n)) | (w[BIG.DNLEN - m - 2]>>(BIG.BASEBITS - n));
			for (int i = BIG.DNLEN - 2;i > m;i--)
			{
				w[i] = ((w[i - m] << n) & BIG.BMASK) | (w[i - m - 1]>>(BIG.BASEBITS - n));
			}
			w[m] = (w[0] << n) & BIG.BMASK;
			for (int i = 0;i < m;i++)
			{
				w[i] = 0;
			}
		}

	/* this+=x */
		public virtual void Add(DBIG x)
		{
			for (int i = 0;i < BIG.DNLEN;i++)
			{
				w[i] += x.w[i];
			}
		}

	/* this-=x */
		public virtual void Sub(DBIG x)
		{
			for (int i = 0;i < BIG.DNLEN;i++)
			{
				w[i] -= x.w[i];
			}
		}

		public virtual void RSub(DBIG x)
		{
			for (int i = 0;i < BIG.DNLEN;i++)
			{
				w[i] = x.w[i] - w[i];
			}
		}

	/* Compare a and b, return 0 if a==b, -1 if a<b, +1 if a>b. Inputs must be normalised */
		public static int Comp(DBIG a, DBIG b)
		{
			for (int i = BIG.DNLEN - 1;i >= 0;i--)
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

	/* reduces this DBIG mod a BIG, and returns the BIG */
		public virtual BIG Mod(BIG c)
		{
			int k = 0;
			Norm();
			DBIG m = new DBIG(c);
			DBIG r = new DBIG(0);

			if (Comp(this,m) < 0)
			{
				return new BIG(this);
			}

			do
			{
				m.Shl(1);
				k++;
			} while (Comp(this,m) >= 0);

			while (k > 0)
			{
				m.Shr(1);

				r.Copy(this);
				r.Sub(m);
				r.Norm();
				CMove(r,(int)(1 - ((r.w[BIG.DNLEN - 1] >> (BIG.CHUNK - 1)) & 1)));

				k--;
			}
			return new BIG(this);
		}

	/* return this/c */
		public virtual BIG Div(BIG c)
		{
			int d, k = 0;
			DBIG m = new DBIG(c);
			DBIG dr = new DBIG(0);
			BIG r = new BIG(0);
			BIG a = new BIG(0);
			BIG e = new BIG(1);
			Norm();

			while (Comp(this,m) >= 0)
			{
				e.FShl(1);
				m.Shl(1);
				k++;
			}

			while (k > 0)
			{
				m.Shr(1);
				e.Shr(1);

				dr.Copy(this);
				dr.Sub(m);
				dr.Norm();
				d = (int)(1 - ((dr.w[BIG.DNLEN - 1] >> (BIG.CHUNK - 1)) & 1));
				CMove(dr,d);
				r.Copy(a);
				r.Add(e);
				r.Norm();
				a.CMove(r,d);
				k--;
			}
			return a;
		}
	}

}