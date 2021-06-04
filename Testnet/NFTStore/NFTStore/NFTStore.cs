using System;
using System.Collections.Generic;
using System.Text;

namespace NFTStore
{
    using Stratis.SmartContracts;
    using Stratis.SmartContracts.Standards;
    using System;

    [Deploy]
    public class NFTStore : SmartContract
    {

        public SaleInfo GetSaleInfo(Address contract, ulong tokenId) => State.GetStruct<SaleInfo>($"SaleInfo:{contract}:{tokenId}");
        private void SetSaleInfo(Address contract, ulong tokenId, SaleInfo value) => State.SetStruct<SaleInfo>($"SaleInfo:{contract}:{tokenId}", value);

        public void ClearSaleInfo(Address contract, ulong tokenId) => State.Clear($"SaleInfo:{contract}:{tokenId}");

        public NFTStore(ISmartContractState state)
            : base(state)
        {
            EnsureNotPayable();
            Owner = Message.Sender;
        }

        /// <summary>
        /// Ensure the token is approved on the non fungible contract for store contract as pre-condition.
        /// </summary>
        public void Sale(Address contract, ulong tokenId, ulong price)
        {
            EnsureNotPayable();

            Assert(price > 0, "Price should be higher than zero.");

            var tokenOwner = GetOwner(contract, tokenId);

            Assert(tokenOwner == Address, "The token is already on sale.");

            EnsureCallerCanOperate(contract, tokenOwner);

            TransferToken(contract, tokenId, tokenOwner, Address);

            SetSaleInfo(contract, tokenId, new SaleInfo { Price = price, Seller = tokenOwner });

            Log(new TokenOnSaleLog { Contract = contract, TokenId = tokenId, Price = price, Seller = tokenOwner });
        }

        public void Buy(Address contract, ulong tokenId)
        {
            var saleInfo = GetSaleInfo(contract, tokenId);

            Assert(Message.Value == saleInfo.Price, "Transferred amount is not matching exact price of the token.");

            SafeTransferToken(contract, tokenId, Address, Message.Sender);

            ClearSaleInfo(contract, tokenId);

            var result = Transfer(saleInfo.Seller, saleInfo.Price);

            Assert(result.Success, "Transfer failed.");

            Log(new TokenPurchasedLog { Contract = contract, TokenId = tokenId, Purchaser = Message.Sender, Seller = GetOwner(contract, tokenId) });
        }

        public void CancelSale(Address contract, ulong tokenId)
        {
            EnsureNotPayable();
            var saleInfo = GetSaleInfo(contract, tokenId);

            Assert(saleInfo.Seller != Address.Zero, "The token is not on sale");

            EnsureCallerCanOperate(contract, saleInfo.Seller);

            SafeTransferToken(contract, tokenId, Address, saleInfo.Seller);

            ClearSaleInfo(contract, tokenId);

            Log(new TokenSaleCanceledLog { Contract = contract, TokenId = tokenId, Seller = saleInfo.Seller });
        }

        private bool IsApprovedForAll(Address contract, Address tokenOwner)
        {
            var result = Call(contract, 0, "IsApprovedForAll", new object[] { tokenOwner, Message.Sender });

            Assert(result.Success, "IsApprovedForAll method call failed.");

            return result.ReturnValue is bool success && success;
        }

        private void TransferToken(Address contract, ulong tokenId, Address from, Address to)
        {
            var result = Call(contract, 0, "TransferFrom", new object[] { from, to, tokenId });

            Assert(result.Success && result.ReturnValue is bool success && success, "The token transfer failed. Be sure sender is approved to transfer token.");
        }

        private void SafeTransferToken(Address contract, ulong tokenId, Address from, Address to)
        {
            var result = Call(contract, 0, "SafeTransferFrom", new object[] { from, to, tokenId });

            Assert(result.Success && result.ReturnValue is bool success && success, "The token transfer failed. Be sure sender is approved to transfer token.");
        }

        private Address GetOwner(Address contract, ulong tokenId)
        {
            var result = Call(contract, 0, "GetOwner", new object[] { tokenId });

            Assert(result.Success && result.ReturnValue is Address, "GetOwner method call failed.");

            return (Address)result.ReturnValue;
        }

        private void EnsureCallerCanOperate(Address contract, Address tokenOwner)
        {
            Assert(Message.Sender == tokenOwner || IsApprovedForAll(contract, tokenOwner), "The caller is not owner of the token nor approved for all.");
        }

        private void EnsureNotPayable() => Assert(Message.Value == 0, "The method is not payable.");

        private struct TokenOnSaleLog
        {
            public Address Contract;
            internal Address Seller;
            public ulong TokenId;
            public ulong Price;
        }

        public struct TokenSaleCanceledLog
        {
            [Index]
            public Address Contract;
            [Index]
            public ulong TokenId;
            [Index]
            public Address Seller;
        }

        public struct TokenPurchasedLog
        {
            [Index]
            public Address Contract;
            public ulong TokenId;
            [Index]
            public Address Purchaser;
            [Index]
            public Address Seller;
        }
        public struct SaleInfo
        {
            public ulong Price;
            public Address Seller;
        }
    }
}
