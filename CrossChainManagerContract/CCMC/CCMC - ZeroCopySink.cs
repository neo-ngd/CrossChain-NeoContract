using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Neo.SmartContract.Framework;

namespace CrossChainManagerContract.CCMC
{
    public partial class CCMC : SmartContract
    {
        public static byte[] PadRight(byte[] value, int length)
        {
            var byteLength = value.Length;
            if (byteLength > length) return value;
            for (int i = 0; i < length - byteLength; i++)
            {
                value = value.Concat(new byte[] { 0x00 });
            }
            return value;
        }

        public static byte[] WriteUint16(BigInteger value, byte[] Source)
        {
            return Source.Concat(PadRight(value.ToByteArray(), 2));
        }

        private static byte[] WriteUint16(byte[] Source, byte[] Target) 
        {
            return WriteVarInt(Target.Length, Source).Concat(Target);
        }

        public static byte[] WriteVarInt(BigInteger value, byte[] Source)
        {
            if (value < 0)
            {
                return Source;
            }
            else if (value < 0xFD)
            {
                return Source.Concat(value.ToByteArray());
            }
            else if (value < 0xFFFF)
            {
                byte[] length = new byte[] { 0xFD };
                var v = PadRight(value.ToByteArray(), 2);
                return Source.Concat(length).Concat(v);
            }
            else if (value < 0xFFFFFFFF)
            {
                byte[] length = new byte[] { 0xFE };
                var v = PadRight(value.ToByteArray(), 4);
                return Source.Concat(length).Concat(v);
            }
            else 
            {
                byte[] length = new byte[] { 0xFF };
                var v = PadRight(value.ToByteArray(), 8);
                return Source.Concat(length).Concat(v);
            }            
        }
    }
}
