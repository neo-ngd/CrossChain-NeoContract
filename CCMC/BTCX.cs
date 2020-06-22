using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using System;
using System.Diagnostics;
using System.Numerics;

namespace CrossChainContract
{
    public class BTCX : SmartContract
    {
        // dynamic call
        private delegate object DynCall(string method, object[] args);

        //constant value
        private static readonly BigInteger total_amount = 21000000;
        private static readonly byte[] CCMCScriptHash = "".HexToBytes();
        private static readonly byte[] Operator = "ALsa2JWWsKiMuqZkCpKvZx2iSoBXjNdpZo".ToScriptHash();

        //event
        public static event Action<byte[], byte[], BigInteger> Transferred;
        public static event Action<byte[], BigInteger, byte[], byte[], byte[], BigInteger> LockEvent;
        public static event Action<byte[], byte[], BigInteger> UnlockEvent;
        //delegate
        delegate object DyncCall(string method, object[] args);
        public static object Main(string method, object[] args)
        {
            if (Runtime.Trigger == TriggerType.Verification)
            {
                return Runtime.CheckWitness(GetOwner());
            }
            else if (Runtime.Trigger == TriggerType.Application)
            {
                var callscript = ExecutionEngine.CallingScriptHash;
                var currentHash = ExecutionEngine.ExecutingScriptHash;

                // Contract deployment
                if (method == "deploy")
                    return Deploy();
                if (method == "isDeployed")
                    return IsDeployed();

                // NEP5 standard methods
                if (method == "balanceOf") return BalanceOf((byte[])args[0]);

                if (method == "decimals") return Decimals();

                if (method == "name") return Name();

                if (method == "symbol") return Symbol();

                if (method == "totalSupply") return TotalSupply();

                if (method == "transfer") return Transfer((byte[])args[0], (byte[])args[1], (BigInteger)args[2], callscript);

                // Owner management
                if (method == "transferOwnership")
                    return TransferOwnership((byte[])args[0]);
                if (method == "getOwner")
                    return GetOwner();

                // Contract management
                if (method == "supportedStandards")
                    return SupportedStandards();
                if (method == "pause")
                    return Pause();
                if (method == "unpause")
                    return Unpause();
                if (method == "isPaused")
                    return IsPaused();
                if (method == "upgrade")
                {
                    Runtime.Notify("In upgrade");
                    if (args.Length < 9) return false;
                    byte[] script = (byte[])args[0];
                    byte[] plist = (byte[])args[1];
                    byte rtype = (byte)args[2];
                    ContractPropertyState cps = (ContractPropertyState)args[3];
                    string name = (string)args[4];
                    string version = (string)args[5];
                    string author = (string)args[6];
                    string email = (string)args[7];
                    string description = (string)args[8];
                    return Upgrade(script, plist, rtype, cps, name, version, author, email, description);
                }
            }
            return false;
        }

        #region nep5 method
        public static bool Deploy()
        {
            if (!Runtime.CheckWitness(Operator))
            {
                Runtime.Notify("Only owner can deploy this contract.");
                return false;
            }
            if (IsDeployed())
            {
                Runtime.Notify("Already deployed");
                return false;
            }

            StorageMap contract = Storage.CurrentContext.CreateMap(nameof(contract));
            contract.Put("totalSupply", total_amount);
            contract.Put("owner", Operator);

            StorageMap asset = Storage.CurrentContext.CreateMap(nameof(asset));
            asset.Put(Operator, total_amount);
            Transferred(null, Operator, total_amount);
            return true;
        }

        public static bool IsDeployed()
        {
            // if totalSupply has value, means deployed
            StorageMap contract = Storage.CurrentContext.CreateMap(nameof(contract));
            byte[] total_supply = contract.Get("totalSupply");
            return total_supply.Length != 0;
        }

        public static BigInteger BalanceOf(byte[] account)
        {
            if (!IsAddress(account))
            {
                Runtime.Notify("The parameter account SHOULD be a legal address.");
                return 0;
            }
            StorageMap asset = Storage.CurrentContext.CreateMap(nameof(asset));
            return asset.Get(account).AsBigInteger();
        }

        public static byte Decimals() => 0;

        public static string Name() => "BTCx"; //name of the token

        public static string Symbol() => "BTCx"; //symbol of the token

        public static BigInteger TotalSupply()
        {
            StorageMap contract = Storage.CurrentContext.CreateMap(nameof(contract));
            return contract.Get("totalSupply").AsBigInteger();
        }

        private static bool Transfer(byte[] from, byte[] to, BigInteger amount, byte[] callscript)
        {
            if (IsPaused())
            {
                Runtime.Notify("BTCx contract is paused.");
                return false;
            }
            if (!IsAddress(from) || !IsAddress(to))
            {
                Runtime.Notify("The parameters from and to SHOULD be legal addresses.");
                return false;
            }
            if (amount <= 0)
            {
                Runtime.Notify("The parameter amount MUST be greater than 0.");
                return false;
            }
            if (!IsPayable(to))
            {
                Runtime.Notify("The to account is not payable.");
                return false;
            }
            if (!Runtime.CheckWitness(from) && from.AsBigInteger() != callscript.AsBigInteger())
            {
                Runtime.Notify("Not authorized by the from account");
                return false;
            }

            StorageMap asset = Storage.CurrentContext.CreateMap(nameof(asset));
            var fromAmount = asset.Get(from).AsBigInteger();
            if (fromAmount < amount)
            {
                Runtime.Notify("Insufficient funds");
                return false;
            }
            if (from == to)
                return true;

            if (fromAmount == amount)
                asset.Delete(from);
            else
                asset.Put(from, fromAmount - amount);

            var toAmount = asset.Get(to).AsBigInteger();
            asset.Put(to, toAmount + amount);

            Transferred(from, to, amount);
            return true;
        }

        public static bool TransferOwnership(byte[] newOwner)
        {
            // transfer contract ownership from current owner to a new owner
            if (!Runtime.CheckWitness(GetOwner()))
            {
                Runtime.Notify("Only allowed to be called by owner.");
                return false;
            }
            if (!IsAddress(newOwner))
            {
                Runtime.Notify("The parameter newOwner SHOULD be a legal address.");
                return false;
            }

            StorageMap contract = Storage.CurrentContext.CreateMap(nameof(contract));
            contract.Put("owner", newOwner);
            return true;
        }

        public static byte[] GetOwner()
        {
            StorageMap contract = Storage.CurrentContext.CreateMap(nameof(contract));
            var owner = contract.Get("owner");
            return owner;
        }


        public static string[] SupportedStandards() => new string[] { "NEP-5", "NEP-7", "NEP-10" };

        public static bool Pause()
        {
            // Set the smart contract to paused state, the token can not be transfered, approved.
            // Only can invoke some get interface, like getOwner.
            if (!Runtime.CheckWitness(GetOwner()))
            {
                Runtime.Notify("Only allowed to be called by owner.");
                return false;
            }
            StorageMap contract = Storage.CurrentContext.CreateMap(nameof(contract));
            contract.Put("paused", 1);
            return true;
        }

        public static bool Unpause()
        {
            if (!Runtime.CheckWitness(GetOwner()))
            {
                Runtime.Notify("Only allowed to be called by owner.");
                return false;
            }
            StorageMap contract = Storage.CurrentContext.CreateMap(nameof(contract));
            contract.Put("paused", 0);
            return true;
        }

        public static bool IsPaused()
        {
            StorageMap contract = Storage.CurrentContext.CreateMap(nameof(contract));
            return contract.Get("paused").AsBigInteger() != 0;
        }

        public static bool Upgrade(byte[] newScript, byte[] paramList, byte returnType, ContractPropertyState cps,
            string name, string version, string author, string email, string description)
        {
            if (!Runtime.CheckWitness(GetOwner()))
            {
                Runtime.Notify("Only allowed to be called by owner.");
                return false;
            }
            var contract = Contract.Migrate(newScript, paramList, returnType, cps, name, version, author, email, description);
            Runtime.Notify("Proxy contract upgraded");
            return true;
        }

        private static bool IsAddress(byte[] address)
        {
            return address.Length == 20;
        }

        private static bool IsPayable(byte[] to)
        {
            var c = Blockchain.GetContract(to);
            return c == null || c.IsPayable;
        }
        #endregion

        public static bool Lock(byte[] fromAddress, byte[] toUserAddress, BigInteger amount, BigInteger toChainId)
        {
            bool success = false;
            byte[] txData = new byte[] { };
            byte[] AssetHash = new byte[] { };
            byte[] redeemScript = new byte[] { };
            TxArgs txArgs = new TxArgs
            {
                toAddress = toUserAddress,
                amount = amount
            };
            if (toChainId == 1)
            {
                if (amount < 2000)
                {
                    Runtime.Notify("btcx amount should be greater than 2000");
                    return false;
                }
                else 
                {
                    txData = serializeToBtcTxArgs(txArgs, redeemScript);
                }
            }
            else 
            {
                txData = serializeTxArgs(txArgs);
            }
            //call transfer method
            success = Transfer(fromAddress, ExecutionEngine.ExecutingScriptHash, amount, new byte[] { });
            if (!success)
            {
                Runtime.Notify("failed to transfer");
                return success;
            }
            //call CCMC cross chain method 
            AssetHash = GetAssetHash(toChainId);
            var param = new object[] { toChainId, AssetHash, "unlock", txData };
            var ccmc = (DyncCall)CCMCScriptHash.ToDelegate();
            success = (bool)ccmc("CrossChain", param);
            if (!success)
            {
                Runtime.Notify("failed to call CCMC");
                Transfer(ExecutionEngine.ExecutingScriptHash, fromAddress, amount, ExecutionEngine.ExecutingScriptHash);
            }
            else 
            {
                LockEvent(ExecutionEngine.ExecutingScriptHash, toChainId, AssetHash, fromAddress, txArgs.toAddress, amount);
            }            
            return success;
        }

        public static bool Unlock(byte[] argsBytes, byte[] fromContractAddress, BigInteger fromChainId, byte[] caller)
        {
            bool success = false;
            if (caller.Equals(CCMCScriptHash))
            {
                Runtime.Notify("Only allowed to be called by CCMC");
                return false;
            }
            byte[] AssetHash = GetAssetHash(fromChainId);
            if (!fromContractAddress.Equals(AssetHash))
            {
                Runtime.Notify(AssetHash);
                Runtime.Notify(fromContractAddress);
                Runtime.Notify("fromContractAddress not correct");
                return false;
            }
            TxArgs txArgs = new TxArgs();
            txArgs = deserializeTxArgs(argsBytes);
            if (txArgs.amount < 0)
            {
                Runtime.Notify("ToChain Amount SHOULD not be less than 0.");
                return false;
            }
            //transfer asset to toAddress
            success = Transfer(ExecutionEngine.ExecutingScriptHash, txArgs.toAddress, txArgs.amount, ExecutionEngine.ExecutingScriptHash);
            if (!success)
            {
                Runtime.Notify("Failed to transfer NEP5 token to toAddress.");
            }
            else 
            {
                UnlockEvent(fromContractAddress, txArgs.toAddress, txArgs.amount);
            }            
            return success;
        }

        public static bool BindAssetHash(BigInteger toChainId, byte[] toAssetHash, BigInteger initialAmount)
        {
            if (!Runtime.CheckWitness(Operator)) return false;
            StorageMap assetHash = Storage.CurrentContext.CreateMap(nameof(assetHash));
            assetHash.Put(ExecutionEngine.ExecutingScriptHash.Concat(toChainId.AsByteArray()), toAssetHash);
            return true;
        }

        public static byte[] GetAssetHash(BigInteger toChainId)
        {
            StorageMap assetHash = Storage.CurrentContext.CreateMap(nameof(assetHash));
            return assetHash.Get(ExecutionEngine.ExecutingScriptHash.Concat(toChainId.AsByteArray()));
        }

        private static byte[] serializeToBtcTxArgs(TxArgs txArgs, byte[] redeemScript)
        {
            byte[] result = new byte[] { };
            result = WriteVarBytes(txArgs.toAddress, result);
            result = WriteUint64(txArgs.amount, result);
            result = WriteVarBytes(redeemScript, result);
            return result;
        }

        private static byte[] serializeTxArgs(TxArgs txArgs)
        {
            byte[] result = new byte[] { };
            result = WriteVarBytes(txArgs.toAddress, result);
            result = WriteUint64(txArgs.amount, result);
            return result;
        }

        private static TxArgs deserializeTxArgs(byte[] source)
        {
            TxArgs txArgs = new TxArgs();
            int offset = 0;
            var Temp = ReadVarBytes(source, offset);
            txArgs.toAddress = (byte[])Temp[0];
            offset = (int)Temp[1];

            txArgs.amount = ReadUint64(source, offset);
            offset += 8;
            return txArgs;
        }

        private static byte[] WriteUint16(BigInteger value, byte[] source)
        {
            return source.Concat(PadRight(value.ToByteArray(), 2));
        }

        private static byte[] WriteUint64(BigInteger value, byte[] source)
        {
            return source.Concat(PadRight(value.ToByteArray(), 8));
        }

        private static BigInteger ReadUint64(byte[] buffer, int offset)
        {
            return buffer.Range(offset, 8).ToBigInteger();
        }

        private static byte[] WriteVarBytes(byte[] Target, byte[] source)
        {
            return WriteVarInt(Target.Length, source).Concat(Target);
        }

        private static byte[] WriteVarInt(BigInteger value, byte[] source)
        {
            if (value < 0)
            {
                return source;
            }
            else if (value < 0xFD)
            {
                return source.Concat(value.ToByteArray());
            }
            else if (value <= 0xFFFF) // 0xff, need to pad 1 0x00
            {
                byte[] length = new byte[] { 0xFD };
                var v = PadRight(value.ToByteArray(), 2);
                return source.Concat(length).Concat(v);
            }
            else if (value <= 0XFFFFFFFF) //0xffffff, need to pad 1 0x00 
            {
                byte[] length = new byte[] { 0xFE };
                var v = PadRight(value.ToByteArray(), 4);
                return source.Concat(length).Concat(v);
            }
            else //0x ff ff ff ff ff, need to pad 3 0x00
            {
                byte[] length = new byte[] { 0xFF };
                var v = PadRight(value.ToByteArray(), 8);
                return source.Concat(length).Concat(v);
            }
        }

        private static byte[] ReadHash(byte[] source, int offset)
        {
            if (offset + 32 <= source.Length)
            {
                return source.Range(offset, 32);
            }
            throw new ArgumentOutOfRangeException();
        }

        private static object[] ReadVarInt(byte[] buffer, int offset)
        {
            var res = ReadBytes(buffer, offset, 1); // read the first byte
            var fb = (byte[])res[0];
            if (fb.Length != 1) throw new ArgumentOutOfRangeException();
            var newOffset = (int)res[1];
            if (fb.Equals(new byte[] { 0xFD }))
            {
                return new object[] { buffer.Range(newOffset, 2).ToBigInteger(), newOffset + 2 };
            }
            else if (fb.Equals(new byte[] { 0xFE }))
            {
                return new object[] { buffer.Range(newOffset, 4).ToBigInteger(), newOffset + 4 };
            }
            else if (fb.Equals(new byte[] { 0xFF }))
            {
                return new object[] { buffer.Range(newOffset, 8).ToBigInteger(), newOffset + 8 };
            }
            else
            {
                return new object[] { fb.Concat(new byte[] { 0x00 }).ToBigInteger(), newOffset };
            }
        }

        private static object[] ReadVarBytes(byte[] buffer, int offset)
        {
            var res = ReadVarInt(buffer, offset);
            var count = (int)res[0];
            var newOffset = (int)res[1];
            return ReadBytes(buffer, newOffset, count);
        }

        private static object[] ReadBytes(byte[] buffer, int offset, int count)
        {
            if (offset + count > buffer.Length) throw new ArgumentOutOfRangeException();
            return new object[] { buffer.Range(offset, count), offset + count };
        }

        private static byte[] PadRight(byte[] value, int length)
        {
            var l = value.Length;
            if (l > length)
                return value;
            for (int i = 0; i < length - l; i++)
            {
                value = value.Concat(new byte[] { 0x00 });
            }
            return value;
        }

        public struct TxArgs 
        {
            public byte[] toAddress;
            public BigInteger amount;
        }

        [Syscall("Neo.Cryptography.Secp256k1Recover")]
        public static extern byte[] Secp256k1Recover(byte[] r, byte[] s, bool v, byte[] message);
    }
}
