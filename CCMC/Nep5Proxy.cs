using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using System;
using System.ComponentModel;
using System.Numerics;

// [assembly: ContractTitle("optional contract title")]
// [assembly: ContractDescription("optional contract description")]
// [assembly: ContractVersion("optional contract version")]
// [assembly: ContractAuthor("optional contract author")]
// [assembly: ContractEmail("optional contract email")]
[assembly: Features(ContractPropertyState.HasStorage | ContractPropertyState.HasDynamicInvoke | ContractPropertyState.Payable)]

namespace Nep5Proxy
{
    public class Nep5Proxy : SmartContract
    {
        private static readonly byte[] CCMCScriptHash = "eded818748c9f431ab8b7fadbc53fae95581386a".HexToBytes();
        private delegate object DynCall(string method, object[] args); // dynamic call

        private static readonly byte[] Operator = "AQzRMe3zyGS8W177xLJfewRRQZY2kddMun".ToScriptHash(); // Operator address

        public static object Main(string method, object[] args)
        {
            if (Runtime.Trigger == TriggerType.Application)
            {
                var callscript = ExecutionEngine.CallingScriptHash;

                if (method == "bindProxyHash")
                    return BindProxyHash((BigInteger)args[0], (byte[])args[1]);
                if (method == "bindAssetHash")
                    return BindAssetHash((byte[])args[0], (BigInteger)args[1], (byte[])args[2], (BigInteger)args[3], (bool)args[4]);
                if (method == "getProxyHash")
                    return GetProxyHash((BigInteger)args[0]);
                if (method == "getAssetHash")
                    return GetAssetHash((byte[])args[0], (BigInteger)args[1]);
                if (method == "lock")
                    return Lock((byte[])args[0], (byte[])args[1], (BigInteger)args[2], (byte[])args[3], (BigInteger)args[4]);
                if (method == "unlock")
                    return Unlock((byte[])args[0], (byte[])args[1], (BigInteger)args[2], callscript);
                if (method == "getCrossedAmount")
                    return GetCrossedAmount((byte[])args[0], (BigInteger)args[1]);
                if (method == "getCrossedLimit")
                    return GetCrossedLimit((byte[])args[0], (BigInteger)args[1]);
                if (method == "totalLock")
                    return TotalLock((byte[])args[0]);

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
                // following methods are for testing purpose
                //if (method == "testDynCall")
                //    return TestDynCall((byte[])args[0], (byte[])args[1], (BigInteger)args[2]);
                //if (method == "testDynCall2")
                //    return TestDynCall2((byte[])args[0], (BigInteger)args[1], (byte[])args[2], (string)args[3], (byte[])args[4]);
                //if (method == "testDeserialize")
                //{
                //    var x = (byte[])args[0];
                //    Runtime.Notify(x.AsString());
                //    return DeserializeArgs(x);
                //    //return DeserializeArgs((byte[])args[0]);
                //}
                //if (method == "testSerialize")
                //    return SerializeArgs((byte[])args[0], (byte[])args[1], (BigInteger)args[2]);
            }
            return false;
        }

        // add target proxy contract hash according to chain id into contract storage
        [DisplayName("bindProxyHash")]
        public static bool BindProxyHash(BigInteger toChainId, byte[] targetProxyHash)
        {
            if (!Runtime.CheckWitness(Operator)) return false;
            StorageMap proxyHash = Storage.CurrentContext.CreateMap(nameof(proxyHash));
            proxyHash.Put(toChainId.AsByteArray(), targetProxyHash);
            return true;
        }

        // add target asset contract hash according to local asset hash & chain id into contract storage
        [DisplayName("bindAssetHash")]
        public static bool BindAssetHash(byte[] fromAssetHash, BigInteger toChainId, byte[] toAssetHash, BigInteger newAssetLimit, bool isTargetChainAsset)
        {
            if (!Runtime.CheckWitness(Operator)) return false;
            StorageMap assetHash = Storage.CurrentContext.CreateMap(nameof(assetHash));
            assetHash.Put(fromAssetHash.Concat(toChainId.AsByteArray()), toAssetHash);
            // this means the fromAssetHash corresbonds to an asset on the target chain, 
            if (isTargetChainAsset)
            {
                var currentLimit = GetCrossedLimit(fromAssetHash, toChainId);
                if (newAssetLimit < currentLimit) return false;
                var increment = newAssetLimit - currentLimit;
                // increment the supply
                StorageMap crossedAmount = Storage.CurrentContext.CreateMap(nameof(crossedAmount));
                crossedAmount.Put(fromAssetHash.Concat(toChainId.ToByteArray()), GetCrossedAmount(fromAssetHash, toChainId) + increment);
            }
            StorageMap crossedLimit = Storage.CurrentContext.CreateMap(nameof(crossedLimit));
            crossedLimit.Put(fromAssetHash.Concat(toChainId.AsByteArray()), newAssetLimit);
            return true;
        }

        // get target proxy contract hash according to chain id
        [DisplayName("getProxyHash")]
        public static byte[] GetProxyHash(BigInteger toChainId)
        {
            StorageMap proxyHash = Storage.CurrentContext.CreateMap(nameof(proxyHash));
            return proxyHash.Get(toChainId.AsByteArray());
        }

        // get target asset contract hash according to local asset hash & chain id
        [DisplayName("getAssetHash")]
        public static byte[] GetAssetHash(byte[] fromAssetHash, BigInteger toChainId)
        {
            StorageMap assetHash = Storage.CurrentContext.CreateMap(nameof(assetHash));
            return assetHash.Get(fromAssetHash.Concat(toChainId.AsByteArray()));
        }

        // used to lock asset into proxy contract
        [DisplayName("lock")]
        public static bool Lock(byte[] fromAssetHash, byte[] fromAddress, BigInteger toChainId, byte[] toAddress, BigInteger amount)
        {
            // check parameters
            if (fromAssetHash.Length != 20)
                throw new InvalidOperationException("The parameter fromAssetHash SHOULD be 20-byte long.");
            if (fromAddress.Length != 20)
                throw new InvalidOperationException("The parameter fromAddress SHOULD be 20-byte long.");
            if (toAddress.Length == 0)
                throw new InvalidOperationException("The parameter toAddress SHOULD not be empty.");
            
            // get the corresbonding asset on target chain
            var toAssetHash = GetAssetHash(fromAssetHash, toChainId);
            if (toAssetHash.Length == 0)
                throw new InvalidOperationException("Target chain asset hash not found.");
            // get the proxy contract on target chain
            var toContract = GetProxyHash(toChainId);
            if (toContract.Length == 0)
                throw new InvalidOperationException("Target chain proxy contract not found.");
            
            // transfer asset from fromAddress to proxy contract address, use dynamic call to call nep5 token's contract "transfer"
            byte[] currentHash = ExecutionEngine.ExecutingScriptHash; // this proxy contract hash
            var nep5Contract = (DynCall)fromAssetHash.ToDelegate();
            bool success = (bool)nep5Contract("transfer", new object[] { fromAddress, currentHash, amount });
            if (!success)
                throw new InvalidOperationException("Failed to transfer NEP5 token to proxy contract.");
            
            // construct args for proxy contract on target chain
            var inputBytes = SerializeArgs(toAssetHash, toAddress, amount);
            
            // constrct params for CCMC 
            var param = new object[] { toChainId, toContract, "unlock", inputBytes };
            // dynamic call CCMC
            var ccmc = (DynCall)CCMCScriptHash.ToDelegate();
            success = (bool)ccmc("CrossChain", param);
            if (!success)
                throw new InvalidOperationException("Failed to call CCMC.");

            // finally, update target chain supply
            BigInteger targetChainSupply = GetCrossedAmount(fromAssetHash, toChainId);
            BigInteger newTargetChainSupply = targetChainSupply + amount;
            if (newTargetChainSupply >= GetCrossedLimit(fromAssetHash, toChainId))
                throw new InvalidOperationException("The parameter amount exceeds the limit.");
            StorageMap crossedAmount = Storage.CurrentContext.CreateMap(nameof(crossedAmount));
            crossedAmount.Put(fromAssetHash.Concat(toChainId.AsByteArray()), newTargetChainSupply);
            return true;
        }

        // used to unlock asset from proxy contract
        [DisplayName("unlock")]
        public static bool Unlock(byte[] inputBytes, byte[] fromProxyContract, BigInteger fromChainId, byte[] caller)
        {
            // only allowed to be called by CCMC
            if (caller.ToBigInteger() != CCMCScriptHash.ToBigInteger()) return false;
            // check the fromContract is stored, so we can trust it
            if (fromProxyContract.ToBigInteger() != GetProxyHash(fromChainId).ToBigInteger())
                throw new InvalidOperationException("From proxy contract not found.");
            
            // parse the args bytes constructed in source chain proxy contract, passed by multi-chain
            object[] results = DeserializeArgs(inputBytes);
            var assetHash = (byte[])results[0];
            var toAddress = (byte[])results[1];
            var amount = (BigInteger)results[2];
            if (assetHash.Length != 20) throw new InvalidOperationException("Asset script hash SHOULD be 20-byte long.");
            if (toAddress.Length != 20) throw new InvalidOperationException("Account address SHOULD be 20-byte long.");
            if (amount < 0) throw new InvalidOperationException("Amount SHOULD not be less than 0.");

            // transfer asset from proxy contract to toAddress
            byte[] currentHash = ExecutionEngine.ExecutingScriptHash; // this proxy contract hash
            var nep5Contract = (DynCall)assetHash.ToDelegate();
            bool success = (bool)nep5Contract("transfer", new object[] { currentHash, toAddress, amount });
            if (!success)
                throw new InvalidOperationException("Failed to transfer NEP5 token to toAddress.");
            
            // update target chain circulating supply
            var supply = GetCrossedAmount(assetHash, fromChainId);
            var newCrossedAmount = supply - amount;
            if (newCrossedAmount < 0) throw new InvalidOperationException("Insufficient crossed amount.");
            StorageMap crossedAmount = Storage.CurrentContext.CreateMap(nameof(crossedAmount));
            crossedAmount.Put(assetHash, newCrossedAmount);

            return true;
        }

        // get target chain circulating supply
        [DisplayName("getCrossedAmount")]
        public static BigInteger GetCrossedAmount(byte[] fromAssetHash, BigInteger toChainId)
        {
            StorageMap crossedAmount = Storage.CurrentContext.CreateMap(nameof(crossedAmount));
            return crossedAmount.Get(fromAssetHash.Concat(toChainId.AsByteArray())).AsBigInteger();
        }

        // get target chain supply limit
        [DisplayName("getCrossedLimit")]
        public static BigInteger GetCrossedLimit(byte[] fromAssetHash, BigInteger toChainId)
        {
            StorageMap crossedLimit = Storage.CurrentContext.CreateMap(nameof(crossedLimit));
            return crossedLimit.Get(fromAssetHash.Concat(toChainId.AsByteArray())).AsBigInteger();
        }

        // get the total locked amount of a NEP5 token from the proxy contract
        [DisplayName("totalLock")]
        public static BigInteger TotalLock(byte[] assetHash)
        {
            if (assetHash.Length != 20)
                throw new InvalidOperationException("The parameter assetHash SHOULD be 20-byte long.");
            byte[] currentHash = ExecutionEngine.ExecutingScriptHash; // this proxy contract hash
            var nep5Contract = (DynCall)assetHash.ToDelegate();
            var balance = (BigInteger)nep5Contract("balanceOf", new object[] { currentHash });
            return balance;
        }

        // used to upgrade CGAS/CNEO contract, todo
        [DisplayName("upgrade")]
        public static bool Upgrade(byte[] newScript, byte[] paramList, byte returnType, ContractPropertyState cps, 
            string name, string version, string author, string email, string description)
        {
            if (!Runtime.CheckWitness(Operator)) return false;
            var contract = Contract.Migrate(newScript, paramList, returnType, cps, name, version, author, email, description);
            Runtime.Notify("Proxy contract upgraded");
            return true;
        }

        [DisplayName("testDynCall")]
        public static bool TestDynCall(byte[] fromAssetHash, byte[] fromAddress, BigInteger amount)
        {
            byte[] currentHash = ExecutionEngine.ExecutingScriptHash; // this proxy contract hash
            var nep5Contract = (DynCall)fromAssetHash.ToDelegate();
            bool success = (bool)nep5Contract("transfer", new object[] { fromAddress, currentHash, amount });
            return success;
        }

        //[DisplayName("testDynCall2")]
        //public static bool TestDynCall2(byte[] ccmcHash, BigInteger toChainId, byte[] toProxyContract, string methodName, byte[] inputBytes)
        //{
        //    var param = new object[] { toChainId, toProxyContract, "unlock", inputBytes };
        //    // dynamic call CCMC
        //    var ccmc = (DynCall)ccmcHash.ToDelegate();
        //    var success = (bool)ccmc("createCrossChainTx", param);
        //    return success;
        //}

        [DisplayName("testSerialize")]
        public static byte[] SerializeArgs(byte[] assetHash, byte[] address, BigInteger amount)
        {
            var buffer = new byte[] { };
            buffer = WriteVarBytes(assetHash, buffer);
            buffer = WriteVarBytes(address, buffer);
            buffer = WriteVarInt(amount, buffer);
            return buffer;
        }

        [DisplayName("testDeserialize")]
        public static object[] DeserializeArgs(byte[] buffer)
        {
            var offset = 0;
            var res = ReadVarBytes(buffer, offset);
            var assetAddress = res[0];

            res = ReadVarBytes(buffer, (int)res[1]);
            var toAddress = res[0];

            res = ReadVarInt(buffer, (int)res[1]);
            var amount = res[0];

            return new object[] { assetAddress, toAddress, amount };
        }


        // return [BigInteger: value, int: offset]
        private static object[] ReadVarInt(byte[] buffer, int offset)
        {
            var res = ReadBytes(buffer, offset, 1); // read the first byte
            var fb = (byte[])res[0];
            if (fb.Length != 1) throw new ArgumentOutOfRangeException();
            var newOffset = (int)res[1];
            if (fb == new byte[] { 0xFD })
            {
                return new object[] { buffer.Range(newOffset, 2).ToBigInteger(), newOffset + 2 };
            }
            else if (fb == new byte[] { 0xFE })
            {
                return new object[] { buffer.Range(newOffset, 4).ToBigInteger(), newOffset + 4 };
            }
            else if (fb == new byte[] { 0xFF })
            {
                return new object[] { buffer.Range(newOffset, 8).ToBigInteger(), newOffset + 8 };
            }
            else
            {
                return new object[] { fb.ToBigInteger(), newOffset };
            }
        }

        // return [byte[], new offset]
        private static object[] ReadVarBytes(byte[] buffer, int offset)
        {
            var res = ReadVarInt(buffer, offset);
            var count = (int)res[0];
            var newOffset = (int)res[1];
            return ReadBytes(buffer, newOffset, count);
        }

        // return [byte[], new offset]
        private static object[] ReadBytes(byte[] buffer, int offset, int count)
        {
            if (offset + count > buffer.Length) throw new ArgumentOutOfRangeException();
            return new object[] { buffer.Range(offset, count), offset + count };
        }

        private static byte[] WriteVarInt(BigInteger value, byte[] Source)
        {
            if (value < 0)
            {
                return Source;
            }
            else if (value < 0xFD)
            {
                return Source.Concat(value.ToByteArray());
            }
            else if (value <= 0xFFFF) // 0xff, need to pad 1 0x00
            {
                byte[] length = new byte[] { 0xFD };
                var v = PadRight(value.ToByteArray(), 2);
                return Source.Concat(length).Concat(v);
            }
            else if (value <= 0XFFFFFFFF) //0xffffff, need to pad 1 0x00 
            {
                byte[] length = new byte[] { 0xFE };
                var v = PadRight(value.ToByteArray(), 4);
                return Source.Concat(length).Concat(v);
            }
            else //0x ff ff ff ff ff, need to pad 3 0x00
            {
                byte[] length = new byte[] { 0xFF };
                var v = PadRight(value.ToByteArray(), 8);
                return Source.Concat(length).Concat(v);
            }
        }

        private static byte[] WriteVarBytes(byte[] value, byte[] Source)
        {
            return WriteVarInt(value.Length, Source).Concat(value); 
        }

        // add padding zeros on the right
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
    }
}
