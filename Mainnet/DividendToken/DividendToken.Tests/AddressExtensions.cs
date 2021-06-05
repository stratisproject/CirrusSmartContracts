using Stratis.SmartContracts;
using System;
using System.Collections.Generic;
using System.Text;

namespace DividendTokenContract.Tests
{
    public static class AddressExtensions
    {
        private static byte[] HexStringToBytes(string val)
        {
            if (val.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                val = val.Substring(2);

            byte[] ret = new byte[val.Length / 2];
            for (int i = 0; i < val.Length; i = i + 2)
            {
                string hexChars = val.Substring(i, 2);
                ret[i / 2] = byte.Parse(hexChars, System.Globalization.NumberStyles.HexNumber);
            }
            return ret;
        }

        public static Address HexToAddress(this string hexString)
        {
            // uint160 only parses a big-endian hex string
            var result = HexStringToBytes(hexString);
            return CreateAddress(result);
        }

        private static Address CreateAddress(byte[] bytes)
        {
            uint pn0 = BitConverter.ToUInt32(bytes, 0);
            uint pn1 = BitConverter.ToUInt32(bytes, 4);
            uint pn2 = BitConverter.ToUInt32(bytes, 8);
            uint pn3 = BitConverter.ToUInt32(bytes, 12);
            uint pn4 = BitConverter.ToUInt32(bytes, 16);

            return new Address(pn0, pn1, pn2, pn3, pn4);
        }
    }
}
