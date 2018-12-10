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

/*
 *   Cryptographic strong random number generator 
 *
 *   Unguessable seed -> SHA -> PRNG internal state -> SHA -> random numbers
 *   Slow - but secure
 *
 *   See ftp://ftp.rsasecurity.com/pub/pdfs/bull-1.pdf for a justification
 */

/* Marsaglia & Zaman Random number generator constants */


using System;

namespace Hyperledger.Fabric.SDK.AMCL
{
	public sealed class RAND
	{
	/* Cryptographically strong pseudo-random number generator */

		private const int NK = 21;
		private const int NJ = 6;
		private const int NV = 8;
		private int[] ira = new int[NK]; // random number...
		private int rndptr; // ...array & pointer
		private int borrow;
		private int pool_ptr;
		private sbyte[] pool = new sbyte[32]; // random pool

		public RAND()
		{
			Clean();
		}

		private int sbrand()
		{ // Marsaglia & Zaman random number generator
			int i, k;
			long pdiff, t;

			rndptr++;
			if (rndptr < NK)
			{
				return ira[rndptr];
			}
			rndptr = 0;
			for (i = 0,k = NK - NJ;i < NK;i++,k++)
			{ // calculate next NK values
				if (k == NK)
				{
					k = 0;
				}
				t = (ira[k]) & 0xffffffffL;
				pdiff = (t - (ira[i] & 0xffffffffL) - borrow) & 0xffffffffL;
				if (pdiff < t)
				{
					borrow = 0;
				}
				if (pdiff > t)
				{
					borrow = 1;
				}
				ira[i] = unchecked((int)(pdiff & 0xffffffffL));
			}

			return ira[0];
		}

		public void SIRand(int seed)
		{
			int i, @in;
			int t, m = 1;
			borrow = 0;
			rndptr = 0;
			ira[0] ^= seed;
			for (i = 1;i < NK;i++)
			{ // fill initialisation vector
				@in = (NV * i) % NK;
				ira[@in] ^= m; // note XOR
				t = m;
				m = seed - m;
				seed = t;
			}
			for (i = 0;i < 10000;i++)
			{
				sbrand(); // "warm-up" & stir the generator
			}
		}

		private void fill_pool()
		{
			HASH256 sh = new HASH256();
			for (int i = 0;i < 128;i++)
			{
				sh.Process(sbrand());
			}
			pool = sh.Hash();
			pool_ptr = 0;
		}

		private static int pack(sbyte[] b)
		{ // pack 4 bytes into a 32-bit Word
			return ((b[3] & 0xff) << 24) | ((b[2] & 0xff) << 16) | ((b[1] & 0xff) << 8) | (b[0] & 0xff);
		}

        public void Seed(int rawlen, byte[] raw)
        {
            Seed(rawlen,(sbyte[])(Array)raw);
        }
    /* Initialize RNG with some real entropy from some external source */
        public void Seed(int rawlen, sbyte[] raw)
		{ // initialise from at least 128 byte string of raw random entropy
			int i;
			sbyte[] digest;
			sbyte[] b = new sbyte[4];
			HASH256 sh = new HASH256();
			pool_ptr = 0;
			for (i = 0;i < NK;i++)
			{
				ira[i] = 0;
			}
			if (rawlen > 0)
			{
				for (i = 0;i < rawlen;i++)
				{
					sh.Process(raw[i]);
				}
				digest = sh.Hash();

	/* initialise PRNG from distilled randomness */

				for (i = 0;i < 8;i++)
				{
					b[0] = digest[4 * i];
					b[1] = digest[4 * i + 1];
					b[2] = digest[4 * i + 2];
					b[3] = digest[4 * i + 3];
					SIRand(pack(b));
				}
			}
			fill_pool();
		}

	/* Terminate and clean up */
		public void Clean()
		{ // kill internal state
			int i;
			pool_ptr = rndptr = 0;
			for (i = 0;i < 32;i++)
			{
				pool[i] = 0;
			}
			for (i = 0;i < NK;i++)
			{
				ira[i] = 0;
			}
			borrow = 0;
		}

	/* get random byte */
		public int Byte
		{
			get
			{
				int r;
				r = pool[pool_ptr++];
				if (pool_ptr >= 32)
				{
					fill_pool();
				}
				return (r & 0xff);
			}
		}


	}

}