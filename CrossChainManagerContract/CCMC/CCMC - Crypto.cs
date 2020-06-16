using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;

namespace CrossChainManagerContract.CCMC
{
    public partial class CCMC : SmartContract
    {
        public static byte[] compressMultiChainPubKey(byte[] key)
        {
            if (key.Length < 34) return key;
            int index = 2;
            byte even = 0x02;
            byte odd = 0x03;
            byte[] newkey = key.Range(0, 35);
            byte[] point = key.Range(66, 1);
            if (point.ToBigInteger() % 2 == 0)
            {
                newkey[index] = even;
            }
            else
            {
                newkey[index] = odd;
            }
            return newkey;
        }

        public static bool verifyHeader(byte[] rawHeader, byte[] signList, byte[][] keepers, int m)
        {
            byte[] hash = Hash256(rawHeader);
            int signListAmounts = signList.Length / MCCHAIN_SIGNATURE_LEN;
            byte[][] slicedSingLists = new byte[signListAmounts][];
            for (int i = 0; i < signListAmounts; i++)
            {
                byte[] r = (signList.Range(i * MCCHAIN_SIGNATURE_LEN, 32));
                byte[] s = (signList.Range(i * MCCHAIN_SIGNATURE_LEN, 32));
                int index = i * MCCHAIN_SIGNATURE_LEN + 64;
                BigInteger v = signList.Range(index, 1).ToBigInteger();
                slicedSingLists[i] = r.Concat(s);
            }
            return Crypto.ECDsa.Secp256k1.CheckMultiSig(rawHeader, keepers, slicedSingLists);
        }

        public static BookKeeper GenerateBookKeeperByPubKey(byte[] pubKeyList)
        {
            if (pubKeyList.Length % MCCHAIN_PUBKEY_LEN != 0)
            {
                Runtime.Notify("pubKeyList length illegal");
                throw new ArgumentOutOfRangeException();
            }
            int n = pubKeyList.Length / MCCHAIN_PUBKEY_LEN;
            int m = n - (n - 1) / 3;
            return getBookKeeper(n, m, pubKeyList);
        }

        public static BookKeeper getBookKeeper(int keyLength, int m, byte[] pubKeyList)
        {
            byte[] buff = new byte[] { };
            buff = WriteUint16(keyLength, buff);
            byte[][] keepers = new byte[keyLength][];
            for (int i = 0; i < keyLength; i++)
            {
                buff = WriteVarBytes(buff, compressMultiChainPubKey(pubKeyList.Range(i * MCCHAIN_PUBKEY_LEN, MCCHAIN_PUBKEY_LEN)));
                byte[] hash = Crypto.SHA256(pubKeyList.Range(i * MCCHAIN_PUBKEY_LEN, MCCHAIN_PUBKEY_LEN));
                keepers[i] = hash;
            }
            BookKeeper bookKeeper = new BookKeeper();
            buff = WriteUint16(m, buff);
            bookKeeper.nextBookKeeper = Hash160(buff);
            bookKeeper.keepers = keepers;
            return bookKeeper;
        }

        public static byte[] Hash256(byte[] source)
        {
            return Crypto.SHA256(Crypto.SHA256(source));
        }

        public static byte[] Hash160(byte[] source)
        {
            return Crypto.RIPEMD160(Crypto.SHA256(source));
        }
    }
}
