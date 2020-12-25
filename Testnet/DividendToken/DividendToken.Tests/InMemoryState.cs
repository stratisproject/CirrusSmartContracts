using NBitcoin;
using Stratis.SmartContracts;
using System;
using System.Collections.Generic;

namespace DividendTokenContract.Tests
{
    public class InMemoryState : IPersistentState
    {
        private readonly Dictionary<string, object> storage = new Dictionary<string, object>();

        public void Clear(string key) => this.storage.Remove(key);

        public T GetValue<T>(string key) => (T)this.storage.GetValueOrDefault(key, default(T));

        public Address GetAddress(string key) => this.GetValue<Address>(key);

        public T[] GetArray<T>(string key) => this.GetValue<T[]>(key);

        public bool GetBool(string key) => this.GetValue<bool>(key);

        public byte[] GetBytes(byte[] key) => throw new NotImplementedException();

        public byte[] GetBytes(string key) => this.GetValue<byte[]>(key);

        public char GetChar(string key) => this.GetValue<char>(key);

        public int GetInt32(string key) => this.GetValue<int>(key);

        public long GetInt64(string key) => this.GetValue<long>(key);

        public string GetString(string key) => this.GetValue<string>(key);

        public T GetStruct<T>(string key)
                where T : struct => this.GetValue<T>(key);

        public uint GetUInt32(string key) => this.GetValue<uint>(key);

        public ulong GetUInt64(string key) => this.GetValue<ulong>(key);

        public bool IsContract(Address address)
        {
            throw new NotImplementedException();
        }

        public void SetAddress(string key, Address value) => this.storage.AddOrReplace(key, value);

        public void SetArray(string key, Array a) => this.storage.AddOrReplace(key, a);

        public void SetBool(string key, bool value) => this.storage.AddOrReplace(key, value);

        public void SetBytes(byte[] key, byte[] value)
        {
            throw new NotImplementedException();
        }

        public void SetBytes(string key, byte[] value) => this.storage.AddOrReplace(key, value);

        public void SetChar(string key, char value) => this.storage.AddOrReplace(key, value);

        public void SetInt32(string key, int value) => this.storage.AddOrReplace(key, value);

        public void SetInt64(string key, long value) => this.storage.AddOrReplace(key, value);

        public void SetString(string key, string value) => this.storage.AddOrReplace(key, value);

        public void SetStruct<T>(string key, T value)
                where T : struct => this.storage.AddOrReplace(key, value);

        public void SetUInt32(string key, uint value) => this.storage.AddOrReplace(key, value);

        public void SetUInt64(string key, ulong value) => this.storage.AddOrReplace(key, value);
    }
}
