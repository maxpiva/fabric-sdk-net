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

/* Fixed Data in ROM - Field and Curve parameters */


namespace Hyperledger.Fabric.SDK.AMCL.FP256BN
{
    public class ROM
    {
        public const long MConst = 0x6C964E0537E5E5L;

        public const int CURVE_A = 0;
        public const int CURVE_B_I = 3;

        public const int CURVE_Cof_I = 1;

        // Base Bits= 56
        public static readonly long[] Modulus = {0x292DDBAED33013L, 0x65FB12980A82D3L, 0x5EEE71A49F0CDCL, 0xFFFCF0CD46E5F2L, 0xFFFFFFFFL};
        public static readonly long[] R2modp = {0xEDE336303B9F8BL, 0x92FFEE9FEC54E8L, 0x13C1C063C55F79L, 0xA12F2EAC0123FAL, 0x8E559B2AL};
        public static readonly long[] CURVE_B = {0x3L, 0x0L, 0x0L, 0x0L, 0x0L};
        public static readonly long[] CURVE_Order = {0x2D536CD10B500DL, 0x65FB1299921AF6L, 0x5EEE71A49E0CDCL, 0xFFFCF0CD46E5F2L, 0xFFFFFFFFL};
        public static readonly long[] CURVE_Gx = {0x1L, 0x0L, 0x0L, 0x0L, 0x0L};
        public static readonly long[] CURVE_Gy = {0x2L, 0x0L, 0x0L, 0x0L, 0x0L};

        public static readonly long[] Fra = {0x760328AF943106L, 0x71511E3AB28F74L, 0x8DDB0867CF39A1L, 0xCA786F352D1A6EL, 0x3D617662L};
        public static readonly long[] Frb = {0xB32AB2FF3EFF0DL, 0xF4A9F45D57F35EL, 0xD113693CCFD33AL, 0x3584819819CB83L, 0xC29E899DL};
        public static readonly long[] CURVE_Bnx = {0x82F5C030B0A801L, 0x68L, 0x0L, 0x0L, 0x0L};
        public static readonly long[] CURVE_Cof = {0x1L, 0x0L, 0x0L, 0x0L, 0x0L};
        public static readonly long[] CURVE_Cru = {0x1C0A24A3A1B807L, 0xD79DF1932D1EDBL, 0x40921018659BCDL, 0x13988E1L, 0x0L};
        public static readonly long[] CURVE_Pxa = {0x2616B689C09EFBL, 0x539A12BF843CD2L, 0x577C28913ACE1CL, 0xB4C96C2028560FL, 0xFE0C3350L};
        public static readonly long[] CURVE_Pxb = {0x69ED34A37E6A2BL, 0x78E287D03589D2L, 0xC637D813B924DDL, 0x738AC054DB5AE1L, 0x4EA66057L};
        public static readonly long[] CURVE_Pya = {0x9B481BEDC27FFL, 0x24758D615848E9L, 0x75124E3E51EFCBL, 0xC542A3B376770DL, 0x702046E7L};
        public static readonly long[] CURVE_Pyb = {0x1281114AAD049BL, 0xBE80821A98B3E0L, 0x49297EB29F8B4CL, 0xD388C29042EEA6L, 0x554E3BCL};

        public static readonly long[][] CURVE_W = {new[] {0xF0036E1B054003L, 0xFFFFFFFE78663AL, 0xFFFFL, 0x0L, 0x0L}, new[] {0x5EB8061615001L, 0xD1L, 0x0L, 0x0L, 0x0L}};

        public static readonly long[][][] CURVE_SB = {new[] {new[] {0xF5EEEE7C669004L, 0xFFFFFFFE78670BL, 0xFFFFL, 0x0L, 0x0L}, new[] {0x5EB8061615001L, 0xD1L, 0x0L, 0x0L, 0x0L}}, new[] {new[] {0x5EB8061615001L, 0xD1L, 0x0L, 0x0L, 0x0L}, new[] {0x3D4FFEB606100AL, 0x65FB129B19B4BBL, 0x5EEE71A49D0CDCL, 0xFFFCF0CD46E5F2L, 0xFFFFFFFFL}}};

        public static readonly long[][] CURVE_WB = {new[] {0x20678F0D30A800L, 0x55555554D2CC10L, 0x5555L, 0x0L, 0x0L}, new[] {0xD6764C0D7DC805L, 0x8FBEA10BC3AD1AL, 0x806160104467DEL, 0xD105EBL, 0x0L}, new[] {0xACB6061F173803L, 0x47DF5085E1D6C1L, 0xC030B0082233EFL, 0x6882F5L, 0x0L}, new[] {0x26530F6E91F801L, 0x55555554D2CCE1L, 0x5555L, 0x0L, 0x0L}};

        public static readonly long[][][] CURVE_BB = {new[] {new[] {0xAA5DACA05AA80DL, 0x65FB1299921A8DL, 0x5EEE71A49E0CDCL, 0xFFFCF0CD46E5F2L, 0xFFFFFFFFL}, new[] {0xAA5DACA05AA80CL, 0x65FB1299921A8DL, 0x5EEE71A49E0CDCL, 0xFFFCF0CD46E5F2L, 0xFFFFFFFFL}, new[] {0xAA5DACA05AA80CL, 0x65FB1299921A8DL, 0x5EEE71A49E0CDCL, 0xFFFCF0CD46E5F2L, 0xFFFFFFFFL}, new[] {0x5EB8061615002L, 0xD1L, 0x0L, 0x0L, 0x0L}}, new[] {new[] {0x5EB8061615001L, 0xD1L, 0x0L, 0x0L, 0x0L}, new[] {0xAA5DACA05AA80CL, 0x65FB1299921A8DL, 0x5EEE71A49E0CDCL, 0xFFFCF0CD46E5F2L, 0xFFFFFFFFL}, new[] {0xAA5DACA05AA80DL, 0x65FB1299921A8DL, 0x5EEE71A49E0CDCL, 0xFFFCF0CD46E5F2L, 0xFFFFFFFFL}, new[] {0xAA5DACA05AA80CL, 0x65FB1299921A8DL, 0x5EEE71A49E0CDCL, 0xFFFCF0CD46E5F2L, 0xFFFFFFFFL}}, new[] {new[] {0x5EB8061615002L, 0xD1L, 0x0L, 0x0L, 0x0L}, new[] {0x5EB8061615001L, 0xD1L, 0x0L, 0x0L, 0x0L}, new[] {0x5EB8061615001L, 0xD1L, 0x0L, 0x0L, 0x0L}, new[] {0x5EB8061615001L, 0xD1L, 0x0L, 0x0L, 0x0L}}, new[] {new[] {0x82F5C030B0A802L, 0x68L, 0x0L, 0x0L, 0x0L}, new[] {0xBD700C2C2A002L, 0x1A2L, 0x0L, 0x0L, 0x0L}, new[] {0x2767EC6FAA000AL, 0x65FB1299921A25L, 0x5EEE71A49E0CDCL, 0xFFFCF0CD46E5F2L, 0xFFFFFFFFL}, new[] {0x82F5C030B0A802L, 0x68L, 0x0L, 0x0L, 0x0L}}};
    }
}