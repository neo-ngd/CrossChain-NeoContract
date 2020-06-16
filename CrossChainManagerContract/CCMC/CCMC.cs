using System;
using System.Collections.Generic;
using System.Text;
using Neo.SmartContract.Framework;

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

        //proxy part
        private static readonly byte[] ProxyPrefix = new byte[] { 0x04, 0x01 };

        //constant value
        private static readonly int MCCHAIN_PUBKEY_LEN = 67;
        private static readonly int MCCHAIN_SIGNATURE_LEN = 65;
    }
}
