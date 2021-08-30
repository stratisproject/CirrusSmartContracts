using Stratis.SmartContracts;
/// <summary>
/// A non fungible token contract.
/// </summary>
public class NonFungibleToken : SmartContract
{

    /// <summary>
    /// Function to check which interfaces are supported by this contract.
    /// </summary>
    /// <param name="interfaceID">Id of the interface.</param>
    /// <returns>True if <see cref="interfaceID"/> is supported, false otherwise.</returns>
    public bool SupportsInterface(uint interfaceID)
    {
        return State.GetBool($"SupportedInterface:{interfaceID}");
    }

    /// <summary>
    /// Sets the supported interface value.
    /// </summary>
    /// <param name="interfaceId">The interface id.</param>
    /// <param name="value">A value indicating if the interface id is supported.</param>
    private void SetSupportedInterfaces(uint interfaceId, bool value) => State.SetBool($"SupportedInterface:{interfaceId}", value);

    /// <summary>
    /// Gets the key to the persistent state for the owner by NFT ID.
    /// </summary>
    /// <param name="id">The NFT ID.</param>
    /// <returns>The persistent storage key to get or set the NFT owner.</returns>
    private string GetIdToOwnerKey(ulong id) => $"IdToOwner:{id}";

    ///<summary>
    /// Gets the address of the owner of the NFT ID. 
    ///</summary>
    /// <param name="id">The ID of the NFT</param>
    ///<returns>The owner address.</returns>
    private Address GetIdToOwner(ulong id) => State.GetAddress(GetIdToOwnerKey(id));

    /// <summary>
    /// Sets the owner to the NFT ID.
    /// </summary>
    /// <param name="id">The ID of the NFT</param>
    /// <param name="value">The address of the owner.</param>
    private void SetIdToOwner(ulong id, Address value) => State.SetAddress(GetIdToOwnerKey(id), value);

    /// <summary>
    /// Gets the key to the persistent state for the approval address by NFT ID.
    /// </summary>
    /// <param name="id">The NFT ID.</param>
    /// <returns>The persistent storage key to get or set the NFT approval.</returns>
    private string GetIdToApprovalKey(ulong id) => $"IdToApproval:{id}";

    /// <summary>
    /// Getting from NFT ID the approval address.
    /// </summary>
    /// <param name="id">The ID of the NFT</param>
    /// <returns>Address of the approval.</returns>
    private Address GetIdToApproval(ulong id) => State.GetAddress(GetIdToApprovalKey(id));

    /// <summary>
    /// Setting to NFT ID to approval address.
    /// </summary>
    /// <param name="id">The ID of the NFT</param>
    /// <param name="value">The address of the approval.</param>
    private void SetIdToApproval(ulong id, Address value) => State.SetAddress(GetIdToApprovalKey(id), value);

    /// <summary>
    /// Gets the amount of non fungible tokens the owner has.
    /// </summary>
    /// <param name="address">The address of the owner.</param>
    /// <returns>The amount of non fungible tokens.</returns>
    private ulong GetBalance(Address address) => State.GetUInt64($"Balance:{address}");

    /// <summary>
    /// Sets the owner count of this non fungible tokens.
    /// </summary>
    /// <param name="address">The address of the owner.</param>
    /// <param name="value">The amount of tokens.</param>
    private void SetBalance(Address address, ulong value) => State.SetUInt64($"Balance:{address}", value);

    /// <summary>
    /// Gets the permission value of the operator authorization to perform actions on behalf of the owner.
    /// </summary>
    /// <param name="owner">The owner address of the NFT.</param>
    /// <param name="operatorAddress">>Address of the authorized operators</param>
    /// <returns>A value indicating if the operator has permissions to act on behalf of the owner.</returns>
    private bool GetOwnerToOperator(Address owner, Address operatorAddress) => State.GetBool($"OwnerToOperator:{owner}:{operatorAddress}");

    /// <summary>
    /// Sets the owner to operator permission.
    /// </summary>
    /// <param name="owner">The owner address of the NFT.</param>
    /// <param name="operatorAddress">>Address to add to the set of authorized operators.</param>
    /// <param name="value">The permission value.</param>
    private void SetOwnerToOperator(Address owner, Address operatorAddress, bool value) => State.SetBool($"OwnerToOperator:{owner}:{operatorAddress}", value);

    /// <summary>
    /// Owner of the contract is responsible to for minting/burning 
    /// </summary>
    public Address Owner
    {
        get => State.GetAddress(nameof(Owner));
        private set => State.SetAddress(nameof(Owner), value);
    }

    /// <summary>
    /// Name for non-fungible token contract
    /// </summary>
    public string Name
    {
        get => State.GetString(nameof(Name));
        private set => State.SetString(nameof(Name), value);
    }

    /// <summary>
    /// Symbol for non-fungible token contract
    /// </summary>
    public string Symbol
    {
        get => State.GetString(nameof(Symbol));
        private set => State.SetString(nameof(Symbol), value);
    }

    private string TokenURIFormat
    {
        get => State.GetString(nameof(TokenURIFormat));
        set => State.SetString(nameof(TokenURIFormat), value);
    }

    private bool OwnerOnlyMinting
    {
        get => State.GetBool(nameof(OwnerOnlyMinting));
        set => State.SetBool(nameof(OwnerOnlyMinting), value);
    }

    /// <summary>
    /// The next token id which is going to be minted
    /// </summary>
    private ulong TokenIdCounter
    {
        get => State.GetUInt64(nameof(TokenIdCounter));
        set => State.SetUInt64(nameof(TokenIdCounter), value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="state"></param>
    /// <param name="name"></param>
    /// <param name="symbol"></param>
    /// <param name="tokenURIFormat">Format takes tokenId and contractAddress as parameter.
    /// <para>string.Format(tokenUriFormat,tokenId,contractAddress)</para>
    /// <para>{0} for tokenId and {1} is for contract address in hex format</para>
    /// <para>Examples</para>
    /// <para>https://example.com/tokens/{0} => https://example.com/tokens/20 </para>
    /// <para>https://example.com/contracts/{1}/tokens/{0}/metadata => https://example.com/contracts/0x0000000000000000000000000000000000000002/token/125/metadata </para>
    /// </param>
    public NonFungibleToken(ISmartContractState state, string name, string symbol, string tokenURIFormat, bool ownerOnlyMinting) : base(state)
    {
        // todo: discuss callback handling and supported interface numbering with community.
        this.SetSupportedInterfaces((uint)0x00000001, true); // (ERC165) - ISupportsInterface
        this.SetSupportedInterfaces((uint)0x00000002, true); // (ERC721) - INonFungibleToken,
        this.SetSupportedInterfaces((uint)0x00000003, false); // (ERC721) - INonFungibleTokenReceiver
        this.SetSupportedInterfaces((uint)0x00000004, true); // (ERC721) - INonFungibleTokenMetadata
        this.SetSupportedInterfaces((uint)0x00000005, false); // (ERC721) - IERC721Enumerable

        this.Name = name;
        this.Symbol = symbol;
        this.Owner = Message.Sender;
        this.TokenURIFormat = tokenURIFormat;
        this.OwnerOnlyMinting = ownerOnlyMinting;
    }

    /// <summary>
    /// Transfers the ownership of an NFT from one address to another address. This function can
    /// be changed to payable.
    /// </summary>
    /// <remarks>Throws unless <see cref="Message.Sender"/> is the current owner, an authorized operator, or the
    /// approved address for this NFT.Throws if 'from' is not the current owner.Throws if 'to' is
    /// the zero address.Throws if 'tokenId' is not a valid NFT. When transfer is complete, this
    /// function checks if 'to' is a smart contract. If so, it calls
    /// 'OnNonFungibleTokenReceived' on 'to' and throws if the return value true.</remarks>
    /// <param name="from">The current owner of the NFT.</param>
    /// <param name="to">The new owner.</param>
    /// <param name="tokenId">The NFT to transfer.</param>
    /// <param name="data">Additional data with no specified format, sent in call to 'to'.</param>
    public void SafeTransferFrom(Address from, Address to, ulong tokenId, byte[] data)
    {
        SafeTransferFromInternal(from, to, tokenId, data);
    }

    /// <summary>
    /// Transfers the ownership of an NFT from one address to another address. This function can
    /// be changed to payable.
    /// </summary>
    /// <remarks>This works identically to the other function with an extra data parameter, except this
    /// function just sets data to an empty byte array.</remarks>
    /// <param name="from">The current owner of the NFT.</param>
    /// <param name="to">The new owner.</param>
    /// <param name="tokenId">The NFT to transfer.</param>
    public void SafeTransferFrom(Address from, Address to, ulong tokenId)
    {
        SafeTransferFromInternal(from, to, tokenId, new byte[0]);
    }

    /// <summary>
    /// Throws unless <see cref="Message.Sender"/> is the current owner, an authorized operator, or the approved
    /// address for this NFT.Throws if <see cref="from"/> is not the current owner.Throws if <see cref="to"/> is the zero
    /// address.Throws if <see cref="tokenId"/> is not a valid NFT. This function can be changed to payable.
    /// </summary>
    /// <remarks>The caller is responsible to confirm that <see cref="to"/> is capable of receiving NFTs or else
    /// they maybe be permanently lost.</remarks>
    /// <param name="from">The current owner of the NFT.</param>
    /// <param name="to">The new owner.</param>
    /// <param name="tokenId">The NFT to transfer.</param>
    public void TransferFrom(Address from, Address to, ulong tokenId)
    {
        CanTransfer(tokenId);

        Address tokenOwner = GetIdToOwner(tokenId);
        EnsureAddressIsNotEmpty(tokenOwner);
        EnsureAddressIsNotEmpty(to);
        Assert(tokenOwner == from);

        TransferInternal(to, tokenId);
    }

    /// <summary>
    /// Set or reaffirm the approved address for an NFT. This function can be changed to payable.
    /// </summary>
    /// <remarks>
    /// The zero address indicates there is no approved address. Throws unless <see cref="Message.Sender"/> is
    /// the current NFT owner, or an authorized operator of the current owner.
    /// </remarks>
    /// <param name="approved">Address to be approved for the given NFT ID.</param>
    /// <param name="tokenId">ID of the token to be approved.</param>
    public void Approve(Address approved, ulong tokenId)
    {
        CanOperate(tokenId);
        ValidateToken(tokenId);

        Address tokenOwner = GetIdToOwner(tokenId);
        Assert(approved != tokenOwner);

        SetIdToApproval(tokenId, approved);
        LogApproval(tokenOwner, approved, tokenId);
    }

    /// <summary>
    /// Enables or disables approval for a third party ("operator") to manage all of
    /// <see cref="Message.Sender"/>'s assets. It also Logs the ApprovalForAll event.
    /// </summary>
    /// <remarks>This works even if sender doesn't own any tokens at the time.</remarks>
    /// <param name="operatorAddress">Address to add to the set of authorized operators.</param>
    /// <param name="approved">True if the operators is approved, false to revoke approval.</param>
    public void SetApprovalForAll(Address operatorAddress, bool approved)
    {
        SetOwnerToOperator(Message.Sender, operatorAddress, approved);
        LogApprovalForAll(Message.Sender, operatorAddress, approved);
    }

    /// <summary>
    /// Returns the number of NFTs owned by 'owner'. NFTs assigned to the zero address are
    /// considered invalid, and this function throws for queries about the zero address.
    /// </summary>
    /// <param name="owner">Address for whom to query the balance.</param>
    /// <returns>Balance of owner.</returns>
    public ulong BalanceOf(Address owner)
    {
        EnsureAddressIsNotEmpty(owner);
        return GetBalance(owner);
    }

    /// <summary>
    /// Returns the address of the owner of the NFT. NFTs assigned to zero address are considered invalid, and queries about them do throw.
    /// </summary>
    /// <param name="tokenId">The identifier for an NFT.</param>
    /// <returns>Address of tokenId owner.</returns>
    public Address OwnerOf(ulong tokenId)
    {
        Address owner = GetIdToOwner(tokenId);
        EnsureAddressIsNotEmpty(owner);
        return owner;
    }

    /// <summary>
    /// Get the approved address for a single NFT.
    /// </summary>
    /// <remarks>Throws if 'tokenId' is not a valid NFT.</remarks>
    /// <param name="tokenId">ID of the NFT to query the approval of.</param>
    /// <returns>Address that tokenId is approved for. </returns>
    public Address GetApproved(ulong tokenId)
    {
        ValidateToken(tokenId);

        return GetIdToApproval(tokenId);
    }

    /// <summary>
    /// Checks if 'operator' is an approved operator for 'owner'.
    /// </summary>
    /// <param name="owner">The address that owns the NFTs.</param>
    /// <param name="operatorAddress">The address that acts on behalf of the owner.</param>
    /// <returns>True if approved for all, false otherwise.</returns>
    public bool IsApprovedForAll(Address owner, Address operatorAddress)
    {
        return GetOwnerToOperator(owner, operatorAddress);
    }

    /// <summary>
    /// Actually preforms the transfer.
    /// </summary>
    /// <remarks>Does NO checks.</remarks>
    /// <param name="to">Address of a new owner.</param>
    /// <param name="tokenId">The NFT that is being transferred.</param>
    private void TransferInternal(Address to, ulong tokenId)
    {
        Address from = GetIdToOwner(tokenId);
        ClearApproval(tokenId);

        RemoveToken(from, tokenId);
        AddToken(to, tokenId);

        LogTransfer(from, to, tokenId);
    }

    /// <summary>
    /// Removes a NFT from owner.
    /// </summary>
    /// <remarks>Use and override this function with caution. Wrong usage can have serious consequences.</remarks>
    /// <param name="from">Address from wich we want to remove the NFT.</param>
    /// <param name="tokenId">Which NFT we want to remove.</param>
    private void RemoveToken(Address from, ulong tokenId)
    {
        Assert(GetIdToOwner(tokenId) == from);
        var tokenCount = GetBalance(from);
        SetBalance(from, checked(tokenCount - 1));
        SetIdToOwner(tokenId, Address.Zero);
    }

    /// <summary>
    /// Assignes a new NFT to owner.
    /// </summary>
    /// <remarks>Use and override this function with caution. Wrong usage can have serious consequences.</remarks>
    /// <param name="to">Address to which we want to add the NFT.</param>
    /// <param name="tokenId">Which NFT we want to add.</param>
    private void AddToken(Address to, ulong tokenId)
    {
        Assert(GetIdToOwner(tokenId) == Address.Zero);

        SetIdToOwner(tokenId, to);
        ulong currentTokenAmount = GetBalance(to);
        SetBalance(to, checked(currentTokenAmount + 1));
    }

    /// <summary>
    /// Actually perform the safeTransferFrom.
    /// </summary>
    /// <param name="from">The current owner of the NFT.</param>
    /// <param name="to">The new owner.</param>
    /// <param name="tokenId">The NFT to transfer.</param>
    /// <param name="data">Additional data with no specified format, sent in call to 'to' if it is a contract.</param>
    private void SafeTransferFromInternal(Address from, Address to, ulong tokenId, byte[] data)
    {
        CanTransfer(tokenId);
        ValidateToken(tokenId);

        Address tokenOwner = GetIdToOwner(tokenId);
        Assert(tokenOwner == from);
        EnsureAddressIsNotEmpty(to);

        TransferInternal(to, tokenId);

        EnsureContractReceivedToken(from, to, tokenId, data);
    }

    /// <summary>
    /// Clears the current approval of a given NFT ID.
    /// </summary>
    /// <param name="tokenId">ID of the NFT to be transferred</param>
    private void ClearApproval(ulong tokenId)
    {
        if (GetIdToApproval(tokenId) != Address.Zero)
        {
            SetIdToApproval(tokenId, Address.Zero);
        }
    }

    /// <summary>
    /// This logs when ownership of any NFT changes by any mechanism. This event logs when NFTs are
    /// created('from' == 0) and destroyed('to' == 0). Exception: during contract creation, any
    /// number of NFTs may be created and assigned without logging Transfer.At the time of any
    /// transfer, the approved Address for that NFT (if any) is reset to none.
    /// </summary>
    /// <param name="from">The from address.</param>
    /// <param name="to">The to address.</param>
    /// <param name="tokenId">The NFT ID.</param>
    private void LogTransfer(Address from, Address to, ulong tokenId)
    {
        Log(new TransferLog() { From = from, To = to, TokenId = tokenId });
    }

    /// <summary>
    /// This logs when the approved Address for an NFT is changed or reaffirmed. The zero
    /// Address indicates there is no approved Address. When a Transfer logs, this also
    /// indicates that the approved Address for that NFT (if any) is reset to none.
    /// </summary>
    /// <param name="owner">The owner address.</param>
    /// <param name="operatorAddress">The approved address.</param>
    /// <param name="tokenId">The NFT ID.</param>
    private void LogApproval(Address owner, Address approved, ulong tokenId)
    {
        Log(new ApprovalLog() { Owner = owner, Approved = approved, TokenId = tokenId });
    }

    /// <summary>
    /// This logs when an operator is enabled or disabled for an owner. The operator can manage all NFTs of the owner.
    /// </summary>
    /// <param name="owner">The owner address</param>
    /// <param name="operatorAddress">The operator address.</param>
    /// <param name="approved">A boolean indicating if it has been approved.</param>        
    private void LogApprovalForAll(Address owner, Address operatorAddress, bool approved)
    {
        Log(new ApprovalForAllLog() { Owner = owner, Operator = operatorAddress, Approved = approved });
    }


    /// <summary>
    /// Guarantees that the <see cref="Message.Sender"/> is an owner or operator of the given NFT.
    /// </summary>
    /// <param name="tokenId">ID of the NFT to validate.</param>
    private void CanOperate(ulong tokenId)
    {
        Address tokenOwner = GetIdToOwner(tokenId);
        Assert(tokenOwner == Message.Sender || GetOwnerToOperator(tokenOwner, Message.Sender));
    }

    /// <summary>
    /// Guarantees that the msg.sender is allowed to transfer NFT.
    /// </summary>
    /// <param name="tokenId">ID of the NFT to transfer.</param>
    private void CanTransfer(ulong tokenId)
    {
        Address tokenOwner = GetIdToOwner(tokenId);
        Assert(
          tokenOwner == Message.Sender
          || GetIdToApproval(tokenId) == Message.Sender
          || GetOwnerToOperator(tokenOwner, Message.Sender)
        );
    }

    /// <summary>
    /// Guarantees that tokenId is a valid Token.
    /// </summary>
    /// <param name="tokenId">ID of the NFT to validate.</param>
    private void ValidateToken(ulong tokenId)
    {
        Address tokenOwner = GetIdToOwner(tokenId);
        EnsureAddressIsNotEmpty(tokenOwner);
    }

    /// <summary>
    /// Sets the contract owner who can mint/burn
    /// </summary>
    /// <param name="owner"></param>
    public void TransferOwnership(Address owner)
    {
        EnsureOwnerOnly();
        EnsureAddressIsNotEmpty(owner);

        Log(new OwnershipTransferedLog { PreviousOwner = Owner, NewOwner = owner });

        Owner = owner;
    }

    private void EnsureOwnerOnly()
    {
        Assert(Message.Sender == Owner, "Only owner of the contract can set new owner.");
    }

    /// <summary>
    /// Mints new tokens
    /// </summary>
    /// <param name="to">The address that will own the minted NFT</param>
    public ulong Mint(Address to)
    {
        var tokenId = checked(++TokenIdCounter);

        Mint(to, tokenId);

        return tokenId;
    }

    /// <summary>
    /// Mints new tokens
    /// </summary>
    /// <param name="to">The address that will own the minted NFT</param>
    /// <param name="data">The data param will passed destination contract</param>
    public ulong SafeMint(Address to, byte[] data)
    {
        var tokenId = checked(++TokenIdCounter);

        Mint(to, tokenId);

        EnsureContractReceivedToken(Address.Zero, to, tokenId, data);

        return tokenId;
    }

    private void Mint(Address to, ulong tokenId)
    {
        if (OwnerOnlyMinting)
        {
            EnsureOwnerOnly();
        }

        EnsureAddressIsNotEmpty(to);

        AddToken(to, tokenId);

        LogTransfer(Address.Zero, to, tokenId);
    }

    /// <summary>
    /// Notify contract for received token if destination(to) address is a contract.
    /// Raise exception if notification fails. 
    /// </summary>
    /// <param name="from">The address which previously owned the token.</param>
    /// <param name="to">Destination address that will receive the token</param>
    /// <param name="tokenId">The token identifier which is being transferred.</param>
    /// <param name="data">Additional data with no specified format.</param>
    private void EnsureContractReceivedToken(Address from, Address to, ulong tokenId, byte[] data)
    {
        if (State.IsContract(to))
        {
            var result = Call(to, 0, "OnNonFungibleTokenReceived", new object[] { Message.Sender, from, tokenId, data }, 0);
            Assert((bool)result.ReturnValue);
        }
    }

    public void Burn(ulong tokenId)
    {
        Address tokenOwner = GetIdToOwner(tokenId);

        Assert(tokenOwner == Message.Sender, "Only token owner can burn the token.");

        ClearApproval(tokenId);
        RemoveToken(tokenOwner, tokenId);

        LogTransfer(tokenOwner, Address.Zero, tokenId);
    }

    public string TokenURI(ulong tokenId)
    {
        return string.Format(TokenURIFormat, tokenId, Address);
    }
    private void EnsureAddressIsNotEmpty(Address address)
    {
        Assert(address != Address.Zero, "The address can not be zero.");
    }

    public struct TransferLog
    {
        [Index]
        public Address From;
        [Index]
        public Address To;
        [Index]
        public ulong TokenId;
    }

    public struct ApprovalLog
    {
        [Index]
        public Address Owner;
        [Index]
        public Address Approved;
        [Index]
        public ulong TokenId;
    }

    public struct ApprovalForAllLog
    {
        [Index]
        public Address Owner;
        [Index]
        public Address Operator;

        public bool Approved;
    }

    public struct OwnershipTransferedLog
    {
        [Index]
        public Address PreviousOwner;

        [Index]
        public Address NewOwner;
    }
}