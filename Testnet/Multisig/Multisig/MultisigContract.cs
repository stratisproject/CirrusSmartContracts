using System;
using Stratis.SmartContracts;

[Deploy]
public class MultisigContract : SmartContract
{
    public const string OwnerPrefix = "O";
    public const string ConfirmationPrefix = "C";
    public const string ConfirmationCountPrefix = "N";
    public const string TransactionPrefix = "T";
    public const string TransactionIndexPrefix = "I";

    public MultisigContract(ISmartContractState smartContractState, byte[] addresses, uint required) : base(smartContractState)
    {
        Assert(addresses.Length > 0, "At least one contract owner address must be specified.");
        Assert(required <= addresses.Length, "The number of required confirmations cannot exceed the number of owners.");

        // We do not store the message sender as an owner; once the contract is deployed only the addresses passed in the constructor can interact with the authenticated contract methods.
        SetOwners(addresses);

        // This is set after the owners are initially added so that it only needs to be initially set once, rather than after every owner is added.
        Required = required;
    }
    
    /// <summary>
    /// The current count of contract owners.
    /// </summary>
    public uint OwnerCount
    {
        get => State.GetUInt32(nameof(OwnerCount));
        private set => State.SetUInt32(nameof(OwnerCount), value);
    }

    /// <summary>
    /// The number of confirmations required to execute a multisig transaction.
    /// <remarks>Must be less than or equal to the number of owners at all times.</remarks>
    /// </summary>
    public uint Required
    {
        get => State.GetUInt32(nameof(Required));
        private set => State.SetUInt32(nameof(Required), value);
    }

    /// <summary>
    /// The transaction counter ensures that each submitted multisig transaction has a unique identifier.
    /// </summary>
    public ulong TransactionCount
    {
        get => State.GetUInt64(nameof(TransactionCount));
        private set => State.SetUInt64(nameof(TransactionCount), value);
    }

    public bool IsOwner(Address address)
    {
        return State.GetBool($"{OwnerPrefix}:{address}");
    }

    public bool IsConfirmedBy(ulong transactionId, Address address)
    {
        return State.GetBool($"{ConfirmationPrefix}:{transactionId}:{address}");
    }

    public uint Confirmations(ulong transactionId)
    {
        return State.GetUInt32($"{ConfirmationCountPrefix}:{transactionId}");
    }

    public Transaction GetTransaction(ulong transactionId)
    {
        Assert(transactionId <= TransactionCount, "Can't retrieve a transaction that doesn't exist yet.");

        return State.GetStruct<Transaction>($"{TransactionPrefix}:{transactionId}");
    }
    
    public void AddOwner(Address address)
    {
        EnsureWalletOnly();

        Assert(!IsOwner(address), "Can't add an existing owner.");

        SetOwner(address);

        OwnerCount++;
    }

    public void RemoveOwner(Address address)
    {
        EnsureWalletOnly();

        Assert(IsOwner(address), "Can't remove a non-owner.");
        Assert(OwnerCount > Required, "Can't have less owners than the minimum confirmation requirement.");

        UnsetOwner(address);

        OwnerCount--;
    }

    /// <summary>
    /// Only called within the contract constructor to initialise the owners.
    /// </summary>
    /// <param name="addresses">The array of addresses serialised as bytes.</param>
    private void SetOwners(byte[] addresses)
    {
        Address[] addressList = Serializer.ToArray<Address>(addresses);

        foreach (var address in addressList)
        {
            SetOwner(address);
        }

        OwnerCount = (uint)addressList.Length;
    }

    private void SetOwner(Address address)
    {
        State.SetBool($"{OwnerPrefix}:{address}", true);

        Log(new OwnerAddition() { Owner = address });
    }

    private void UnsetOwner(Address address)
    {
        State.SetBool($"{OwnerPrefix}:{address}", false);

        Log(new OwnerRemoval() { Owner = address });
    }

    /// <summary>
    /// Submits method and parameter metadata describing a contract call that the multisig contract should invoke once it is sufficiently confirmed.
    /// </summary>
    /// <param name="destination">The address of the contract that the multisig contract will invoke a method on.</param>
    /// <param name="methodName">The name of the method that the multisig contract will invoke on the destination contract.</param>
    /// <param name="data">An array of method parameters encoded as packed byte arrays. See the <see cref="Transaction"/> struct for further information.</param>
    /// <returns>The transactionId of the submitted multisig transaction. This is used for confirmation by other multisig nodes, and for looking up execution status etc.</returns>
    /// <remarks>The submitter implicitly provides a confirmation for the submitted transaction.</remarks>
    public ulong Submit(Address destination, string methodName, byte[] data)
    {
        EnsureOwnersOnly();

        TransactionCount++;

        State.SetStruct($"{TransactionPrefix}:{TransactionCount}", new Transaction()
        {
            Destination = destination,
            Executed = false,
            Value = 0,
            MethodName = methodName,
            Parameters = data
        });

        Log(new Submission() { TransactionId = TransactionCount });

        Confirm(TransactionCount);

        return TransactionCount;
    }

    /// <summary>
    /// Submits method and parameter metadata describing a contract call that the multisig contract should invoke once enough nodes have called this method.
    /// </summary>
    /// <param name="destination">The address of the contract that the multisig contract will invoke a method on.</param>
    /// <param name="methodName">The name of the method that the multisig contract will invoke on the destination contract.</param>
    /// <param name="data">An array of method parameters encoded as packed byte arrays. See the <see cref="Transaction"/> struct for further information.</param>
    /// <returns>The transactionId of the submitted multisig transaction.</returns>
    /// <remarks>The submitter implicitly provides a confirmation for the submitted transaction. Subsequent callers provide additional confirmations only.</remarks>
    public ulong SubmitOrConfirm(Address destination, string methodName, byte[] data)
    {
        EnsureOwnersOnly();

        Transaction tx = new Transaction()
        {
            Destination = destination,
            Executed = false,
            Value = 0,
            MethodName = methodName,
            Parameters = data
        };

        ulong transactionId = GetTransactionId(tx);
        if (transactionId == 0)
        {
            TransactionCount++;
            transactionId = TransactionCount;
            State.SetStruct($"{TransactionPrefix}:{transactionId}", tx);
            Log(new Submission() { TransactionId = transactionId });
            SetTransactionId(tx, transactionId);
        }

        Confirm(transactionId);

        return transactionId;
    }

    private string GetTransactionIndexKey(Transaction tx)
    {
        return $"{TransactionIndexPrefix}:{tx.Destination}:{tx.Value}:{tx.MethodName}:{Serializer.ToUInt256(Keccak256(tx.Parameters))}";
    }

    private ulong GetTransactionId(Transaction tx)
    {
        return State.GetUInt64(GetTransactionIndexKey(tx));
    }

    private void SetTransactionId(Transaction tx, ulong transactionId)
    {
        State.SetUInt64(GetTransactionIndexKey(tx), transactionId);
    }

    /// <summary>
    /// Called by contract owners to indicate that they accept the proposed method invocation provided via <see cref="Submit"/>.
    /// </summary>
    /// <param name="transactionId">The transactionId of the submitted multisig transaction.</param>
    /// <remarks>Once the confirmation count matches or exceeds <see cref="Required"/> the contract call will be executed.</remarks>
    public void Confirm(ulong transactionId)
    {
        Assert(IsOwner(Message.Sender), "Sender is not authorized to confirm.");
        Assert(transactionId <= TransactionCount, "Transaction ID does not exist.");
        Assert(!IsConfirmedBy(transactionId, Message.Sender), "Sender has already confirmed the transaction.");

        State.SetBool($"{ConfirmationPrefix}:{transactionId}:{Message.Sender}", true);

        uint confirmationCount = Confirmations(transactionId);

        State.SetUInt32($"{ConfirmationCountPrefix}:{transactionId}", ++confirmationCount);

        Log(new Confirmation() { Sender = Message.Sender, TransactionId = transactionId });

        if (confirmationCount < Required)
            return;

        // If there were sufficient confirmations then automatically execute the transaction.
        var tx = State.GetStruct<Transaction>($"{TransactionPrefix}:{transactionId}");

        Assert(!tx.Executed, "Cannot confirm an already-executed transaction.");

        object[] parameters = { };

        if (tx.Parameters != null && tx.Parameters.Length > 0)
        {
            byte[][] parametersBytes = Serializer.ToArray<byte[]>(tx.Parameters);

            foreach (byte[] parameter in parametersBytes)
            {
                if (parameter == null || parameter.Length == 0)
                {
                    continue;
                }

                int parameterType = (int)parameter[0];

                byte[] parameterEncoded = { };

                Array.Resize(ref parameterEncoded, parameter.Length - 1);

                Array.Copy(parameter, 1, parameterEncoded, 0, parameter.Length - 1);

                object parameterDecoded = null;
                
                switch (parameterType)
                {
                    case 1: // Boolean
                        parameterDecoded = Serializer.ToBool(parameterEncoded);
                        
                        break;
                    case 2: // Byte
                        parameterDecoded = parameterEncoded[0];
                        
                        break;
                    case 3: // Char
                        parameterDecoded = Serializer.ToChar(parameterEncoded);
                        
                        break;
                    case 4: // String
                        parameterDecoded = Serializer.ToString(parameterEncoded);
                        
                        break;
                    case 5: // UInt32
                        parameterDecoded = Serializer.ToUInt32(parameterEncoded);
                        
                        break;
                    case 6: // Int32
                        parameterDecoded = Serializer.ToInt32(parameterEncoded);
                        
                        break;
                    case 7: // UInt64
                        parameterDecoded = Serializer.ToUInt64(parameterEncoded);
                        
                        break;
                    case 8: // Int64
                        parameterDecoded = Serializer.ToInt64(parameterEncoded);
                        
                        break;
                    case 9: // Address
                        parameterDecoded = Serializer.ToAddress(parameterEncoded);
                        
                        break;
                    case 10: // Byte[]
                        parameterDecoded = parameterEncoded;
                        
                        break;
                    case 11: // UInt128
                        parameterDecoded = Serializer.ToUInt128(parameterEncoded);
                        
                        break;
                    case 12: // Uint256
                        parameterDecoded = Serializer.ToUInt256(parameterEncoded);
                        
                        break;
                    default:
                        Assert(false, "Unknown parameter type");
                        break;
                }
                
                int currentLength = parameters.Length;
                Array.Resize(ref parameters, currentLength + 1);
                parameters[currentLength] = parameterDecoded;
            }
        }
        
        ITransferResult result = this.Call(tx.Destination, tx.Value, tx.MethodName, parameters);

        if (result?.Success ?? false)
            Log(new Execution() { TransactionId = transactionId });
        else
            Log(new ExecutionFailure() { TransactionId = transactionId });

        tx.Executed = true;

        State.SetStruct($"{TransactionPrefix}:{transactionId}", tx);
    }

    public void ChangeRequirement(uint required)
    {
        EnsureWalletOnly();

        Assert(required > 0, "Can't have a quorum of less than 1.");
        Assert(required <= OwnerCount, "Can't set quorum higher than the number of owners.");

        Required = required;

        Log(new RequirementChange() { Requirement = required });
    }

    /// <summary>
    /// In the nomenclature of the Gnosis MultisigWallet contract this is based on, the 'wallet' refers to the contract itself.
    /// In other words, methods with this restriction are intended to only be called by the contract itself.
    /// </summary>
    private void EnsureWalletOnly()
    {
        Assert(Message.Sender == Message.ContractAddress, "Can only be called by the contract itself.");
    }

    private void EnsureOwnersOnly()
    {
        Assert(IsOwner(Message.Sender), "Must be one of the contract owners.");
    }

    public struct Transaction
    {
        public Address Destination;

        public uint Value;

        public string MethodName;

        /// <summary>
        /// Array of parameters encoded in packed byte format.
        /// Internally the array is an array of zero or more byte arrays.
        /// Each of these sub-arrays consists of a single packed parameter.
        /// -> The integral value of byte 0 of the packed parameter indicates the parameter's type
        /// -> The remainder of the packed parameter is a byte array containing the parameter in serialised format
        /// </summary>
        public byte[] Parameters;

        public bool Executed;
    }

    public struct Confirmation
    {
        [Index] public Address Sender;

        [Index] public ulong TransactionId;
    }

    public struct Submission
    {
        [Index] public ulong TransactionId;
    }

    public struct Execution
    {
        [Index] public ulong TransactionId;
    }

    public struct ExecutionFailure
    {
        [Index] public ulong TransactionId;
    }

    public struct OwnerAddition
    {
        [Index] public Address Owner;
    }

    public struct OwnerRemoval
    {
        [Index] public Address Owner;
    }

    public struct RequirementChange
    {
        public uint Requirement;
    }
}
