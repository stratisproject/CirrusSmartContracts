using NBitcoin;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Networks;
using Xunit;

namespace ICOContract.Integration.Tests
{
    public class ICOContractTests
    {
        private const ulong Satoshis = 100_000_000;

        [Fact]
        public void Deployment_Test()
        {
            // Compile the contract we want to deploy
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("ICOContract.cs");
            Assert.True(compilationResult.Success);
            using (var chain = new TestChain().Initialize())
            {
                var owner = chain.PreloadedAddresses[0];
                var totalSupply = 100ul;
                var serializer = new Serializer(new ContractPrimitiveSerializer(new SmartContractsPoARegTest()));

                var periods = new SalePeriodInput[] { new SalePeriodInput { PricePerToken = 2, DurationBlocks = 1 } };
                var parameters = new object[] { totalSupply, "Gluon", "Glu", serializer.Serialize(periods) };

                var createResult = chain.SendCreateContractTransaction(owner, compilationResult.Compilation, 0, parameters);

                // Mine a block which will contain our sent transaction
                chain.MineBlocks(1);

                // Check the receipt to see that contract deployment was successful
                var receipt = chain.GetReceipt(createResult.TransactionId);
                Assert.Equal(owner, receipt.From);

                // Check that the code is indeed saved on-chain
                var savedCode = chain.GetCode(createResult.NewContractAddress);
                Assert.NotNull(savedCode);
            }
        }

        [Fact]
        public void Verify_Invest()
        {
            // Compile the contract we want to deploy
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("ICOContract.cs");
            Assert.True(compilationResult.Success);

            using var chain = new TestChain().Initialize();

            // Get an address we can use for deploying
            var owner = chain.PreloadedAddresses[0];
            var totalSupply = 100ul;
            var amount = 10.00;
            var gasPrice = 100ul;
            var fee = 0.01d;
            var serializer = new Serializer(new ContractPrimitiveSerializer(new SmartContractsPoARegTest()));

            var periods = new SalePeriodInput[] { new SalePeriodInput { PricePerToken = (ulong)(0.4 * Satoshis), DurationBlocks = 1 } };
            var parameters = new object[] { totalSupply, "Gluon", "Glu", serializer.Serialize(periods) };

            var createResult = chain.SendCreateContractTransaction(owner, compilationResult.Compilation, 0, parameters);

            chain.MineBlocks(1);

            var investor = chain.PreloadedAddresses[1];

            var currentBalance = chain.GetBalance(investor);
            var investResult = chain.SendCallContractTransaction(investor, "Invest", createResult.NewContractAddress, amount, gasPrice: gasPrice, feeAmount: fee);
            chain.MineBlocks(1);

            var receipt = chain.GetReceipt(investResult.TransactionId);

            var localCallResult = chain.CallContractMethodLocally(owner, "TokenBalance", createResult.NewContractAddress, 0);

            Assert.Equal(75ul, (ulong)localCallResult.Return);

            var contractBalance = chain.GetBalance(createResult.NewContractAddress);

            Assert.Equal(Money.Coins((ulong)amount), contractBalance);

            var investorTokenBalance = (ulong)chain.CallContractMethodLocally(owner, "GetBalance", createResult.NewContractAddress, 0, new object[] { investor }).Return;

            // Verify investor's token balance
            Assert.Equal(25ul, investorTokenBalance);

            var transactionCost = Money.Satoshis(receipt.GasUsed * gasPrice) + Money.Coins((decimal)fee);
            var spendAmount = currentBalance - chain.GetBalance(investor);

            Assert.Equal(Money.Coins((decimal)amount) + transactionCost, spendAmount);
        }

        [Fact]
        public void OverSold_Investment_Test()
        {
            // Compile the contract we want to deploy
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("ICOContract.cs");
            Assert.True(compilationResult.Success);
            using var chain = new TestChain().Initialize();
            // Get an address we can use for deploying
            var owner = chain.PreloadedAddresses[0];
            var totalSupply = 50ul;
            var amount = 20;
            var gasPrice = 100ul;
            var fee = 0.01d;
            var serializer = new Serializer(new ContractPrimitiveSerializer(new SmartContractsPoARegTest()));

            var periods = new SalePeriodInput[] { new SalePeriodInput { PricePerToken = (ulong)Money.Coins(0.2m).Satoshi, DurationBlocks = 1 } };
            var parameters = new object[] { totalSupply, "Gluon", "Glu", serializer.Serialize(periods) };

            var createResult = chain.SendCreateContractTransaction(owner, compilationResult.Compilation, 0, parameters);

            chain.MineBlocks(1);

            var investor = chain.PreloadedAddresses[1];

            var currentBalance = chain.GetBalance(investor);
            var investResult = chain.SendCallContractTransaction(investor, "Invest", createResult.NewContractAddress, amount, gasPrice: gasPrice, feeAmount: fee);
            chain.MineBlocks(1);

            var receipt = chain.GetReceipt(investResult.TransactionId);

            var localCallResult = chain.CallContractMethodLocally(owner, "TokenBalance", createResult.NewContractAddress, 0);

            Assert.Equal(0ul, (ulong)localCallResult.Return); // All tokens are sold

            var contractBalance = chain.GetBalance(createResult.NewContractAddress);

            Assert.Equal(Money.Coins(10), contractBalance); // 10 allowed and 10 refunded

            var investorTokenBalance = (ulong)chain.CallContractMethodLocally(owner, "GetBalance", createResult.NewContractAddress, 0, new object[] { investor }).Return;

            // Verify investor's token balance
            Assert.Equal(totalSupply, investorTokenBalance);

            var cost = Money.Satoshis(receipt.GasUsed * gasPrice) + Money.Coins((decimal)fee);
            var spendAmount = currentBalance - chain.GetBalance(investor);

            Assert.Equal(Money.Coins(10) + cost, spendAmount); // 20 invested, 10 spend(allowed) + trx cost
        }
    }
}
