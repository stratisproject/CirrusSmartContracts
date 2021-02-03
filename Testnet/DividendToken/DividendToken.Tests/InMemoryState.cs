using Stratis.SmartContracts;
using System;
using System.Collections.Generic;

namespace DividendTokenContract.Tests
{
    public class InMemoryState : IPersistentState
    {
        private readonly Dictionary<string, object> storage = new Dictionary<string, object>();
        public bool IsContractResult { get; set; }
        public void Clear(string key) => storage.Remove(key);

        public T GetValue<T>(string key) => (T)storage.GetValueOrDefault(key, default(T));

        public void AddOrReplace(string key, object value)
        {
            if (!storage.TryAdd(key, value))
                storage[key] = value;
        }
        public Address GetAddress(string key) => GetValue<Address>(key);

        public T[] GetArray<T>(string key) => GetValue<T[]>(key);

        public bool GetBool(string key) => GetValue<bool>(key);

        public byte[] GetBytes(byte[] key) => throw new NotImplementedException();

        public byte[] GetBytes(string key) => GetValue<byte[]>(key);

        public char GetChar(string key) => GetValue<char>(key);

        public int GetInt32(string key) => GetValue<int>(key);

        public long GetInt64(string key) => GetValue<long>(key);

        public string GetString(string key) => GetValue<string>(key);

        public T GetStruct<T>(string key)
                where T : struct => GetValue<T>(key);

        public uint GetUInt32(string key) => GetValue<uint>(key);

        public ulong GetUInt64(string key) => GetValue<ulong>(key);

        public UInt128 GetUInt128(string key) => GetValue<UInt128>(key);

        public UInt256 GetUInt256(string key) => GetValue<UInt256>(key);

        public bool IsContract(Address address) => IsContractResult;

        public void SetAddress(string key, Address value) => AddOrReplace(key, value);

        public void SetArray(string key, Array a) => AddOrReplace(key, a);

        public void SetBool(string key, bool value) => AddOrReplace(key, value);

        public void SetBytes(byte[] key, byte[] value)
        {
            throw new NotImplementedException();
        }

        public void SetBytes(string key, byte[] value) => AddOrReplace(key, value);

        public void SetChar(string key, char value) => AddOrReplace(key, value);

        public void SetInt32(string key, int value) => AddOrReplace(key, value);

        public void SetInt64(string key, long value) => AddOrReplace(key, value);

        public void SetString(string key, string value) => AddOrReplace(key, value);

        public void SetStruct<T>(string key, T value)
                where T : struct => AddOrReplace(key, value);

        public void SetUInt32(string key, uint value) => AddOrReplace(key, value);

        public void SetUInt64(string key, ulong value) => AddOrReplace(key, value);

        public void SetUInt128(string key, UInt128 value) => AddOrReplace(key, value);

        public void SetUInt256(string key, UInt256 value) => AddOrReplace(key, value);
    }
}