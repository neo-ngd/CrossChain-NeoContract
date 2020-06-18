using System;
using System.Collections.Generic;
using System.Text;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;

namespace CrossChainManagerContract.CCMC
{
    public partial class CCMC : SmartContract
    {
        //Reuqest prefix
        private static readonly byte[] requestIDPrefix = new byte[] { 0x01, 0x01 };
        private static readonly byte[] requestPreifx = new byte[] { 0x01, 0x02 };

        //Header prefix
        private static readonly byte[] latestHeightPrefix = new byte[] { 0x02, 0x01 };
        private static readonly byte[] mCBlockHeadersPrefix = new byte[] { 0x02, 0x02 };
        private static readonly byte[] latestBookKeeperHeightPrefix = new byte[] { 0x02, 0x03 };
        private static readonly byte[] mCKeeperPubKeysPrefix = new byte[] { 0x02, 0x04 };
        private static readonly byte[] mCKeeperHeightPrefix = new byte[] { 0x02, 0x05 };

        //transaction part
        private static readonly byte[] transactionPrefix = new byte[] { 0x03, 0x01 };

        //IsGenesised part
        private static readonly byte[] IsInitGenesisBlock = new byte[] { 0x04, 0x01 };

        //proxy part
        private static readonly byte[] ProxyPrefix = new byte[] { 0x05, 0x01 };

        //constant value
        private static readonly int MCCHAIN_PUBKEY_LEN = 67;
        private static readonly int MCCHAIN_SIGNATURE_LEN = 65;

        public static bool InitialGenesisBlock(byte[] rawHeader, byte[] pubKeyList)        
        {
            if (IsGenesised()) return false;
            Header header = deserializeHeader(rawHeader);
            Runtime.Notify("header deserialize");
            if (!CheckPubKey(pubKeyList)) throw new ArgumentOutOfRangeException();
            BookKeeper bookKeeper = GenerateBookKeeperByPubKey(pubKeyList);
            Runtime.Notify("bookKeeper generated");
            Storage.Put(latestHeightPrefix, header.height);//最新区块高度
            Storage.Put(latestBookKeeperHeightPrefix, header.height);//共识公钥高度
            Storage.Put(IsInitGenesisBlock, 1);//是否已同步创世区块
            Storage.Put(mCBlockHeadersPrefix.Concat(header.height.ToByteArray()), rawHeader);//存放完整区块头

            return true;
        }

        private static bool IsGenesised()
        {
            return Storage.Get(IsInitGenesisBlock).Equals(true);
        }
    }
}
