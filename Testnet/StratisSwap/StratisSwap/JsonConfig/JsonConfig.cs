using Stratis.SmartContracts;

[Deploy]
public class JsonConfig : SmartContract
{
    /// <summary>
    /// Manages small json configurations for decentralized projects.
    /// </summary>
    /// <param name="smartContractState">The execution state for the contract.</param>
    /// <param name="config">JSON payload to log.</param>
    public JsonConfig(ISmartContractState smartContractState, string config)
        : base(smartContractState)
    {
        UpdateAdminExecute(Message.Sender, true);
        UpdateConfigExecute(config);
    }

    private const string AdminKey = "Admin";
    private const string ContributorKey = "Contributor";

    /// <summary>
    /// Checks if an address is an admin.
    /// </summary>
    /// <param name="address">The address to check.</param>
    /// <returns>true or false</returns>
    public bool IsAdmin(Address address)
    {
        return PersistentState.GetBool($"{AdminKey}:{address}");
    }

    /// <summary>
    /// Checks if an address is a contributor.
    /// </summary>
    /// <param name="address">The address to check.</param>
    /// <returns>true or false</returns>
    public bool IsContributor(Address address)
    {
        return PersistentState.GetBool($"{ContributorKey}:{address}");
    }

    /// <summary>
    /// Updates the status of an admin. Requires admin privileges.
    /// </summary>
    /// <param name="address">The address to update.</param>
    /// <param name="value">Status as boolean, true for approved admin, false for not admin.</param>
    public void UpdateAdmin(Address address, bool value)
    {
        Assert(IsAdmin(Message.Sender));

        UpdateAdminExecute(address, value);
    }

    private void UpdateAdminExecute(Address address, bool value)
    {
        PersistentState.SetBool($"{AdminKey}:{address}", value);

        Log(new RoleLog
        {
            Blame = Message.Sender,
            UpdatedAddress = address,
            UpdatedValue = value,
            Action = nameof(UpdateAdmin),
            Block = Block.Number
        });
    }

    /// <summary>
    /// Updates the status of a contributor. Requires admin privileges.
    /// </summary>
    /// <param name="address">The address to update.</param>
    /// <param name="value">Status as boolean, true for approved contributor, false for not contributor.</param>
    public void UpdateContributor(Address address, bool value)
    {
        Assert(IsAdmin(Message.Sender));

        PersistentState.SetBool($"{ContributorKey}:{address}", value);

        Log(new RoleLog
        {
            Blame = Message.Sender,
            UpdatedAddress = address,
            UpdatedValue = value,
            Action = nameof(UpdateContributor),
            Block = Block.Number
        });
    }

    /// <summary>
    /// Adds a new configuration receipt to the contract.
    /// </summary>
    /// <param name="config">JSON payload as string.</param>
    public void UpdateConfig(string config)
    {
        Assert(IsAdmin(Message.Sender) || IsContributor(Message.Sender));

        UpdateConfigExecute(config);
    }

    private void UpdateConfigExecute(string config)
    {
        Log(new ConfigLog
        {
            Config = config,
            Blame = Message.Sender,
            Block = Block.Number
        });
    }

    public struct RoleLog
    {
        /// <summary>
        /// The address responsible for the role update.
        /// </summary>
        [Index]
        public Address Blame;

        /// <summary>
        /// The address who's role was updated.
        /// </summary>
        [Index]
        public Address UpdatedAddress;

        /// <summary>
        /// The action taken (e.g. UpdateAdmin or UpdateContributor)
        /// </summary>
        public string Action;

        /// <summary>
        /// The boolean value the status was updated too.
        /// </summary>
        public bool UpdatedValue;

        /// <summary>
        /// The block the update occurred.
        /// </summary>
        public ulong Block;
    }

    public struct ConfigLog
    {
        /// <summary>
        /// The address responsible for the config update.
        /// </summary>
        [Index]
        public Address Blame;

        /// <summary>
        /// JSON config payload.
        /// </summary>
        public string Config;

        /// <summary>
        /// The block the update occurred.
        /// </summary>
        public ulong Block;
    }
}
