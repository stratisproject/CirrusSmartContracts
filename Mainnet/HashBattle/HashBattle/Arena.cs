using Stratis.SmartContracts;
using System;
using System.Text;

/// <summary>
/// A Stratis smart contract for running a game battle where owner will start the battle and maximum 4 users can enter a battle
/// </summary>
public class Arena : SmartContract
{
    public Arena(ISmartContractState smartContractState)
    : base(smartContractState)
    {
        BattleOwner = Message.Sender;
    }

    /// <summary>
    /// Set the address deploying the contract as battle owner
    /// </summary>
    private Address BattleOwner
    {
        get => PersistentState.GetAddress(nameof(BattleOwner));
        set => PersistentState.SetAddress(nameof(BattleOwner), value);
    }

    /// <summary>
    /// Battle owner will start the battle
    /// </summary>
    public bool StartBattle(ulong battleId, ulong fee)
    {
        Assert(Message.Sender == BattleOwner, "Only battle owner can start game.");

        var battle = new BattleMain();
        battle.BattleId = battleId;
        battle.MaxUsers = 4;
        battle.Fee = fee;
        battle.Users = new Address[battle.MaxUsers];
        SetBattle(battleId, battle);

        Log(battle);
        return true;
    }

    /// <summary>
    /// 4 different user will enter the battle
    /// </summary>
    public bool EnterBattle(ulong battleId, uint userindex)
    {
        var battle = GetBattle(battleId);

        Assert(battle.Winner == Address.Zero, "Battle ended.");

        Assert(battle.Fee == Message.Value, "Battle amount is not matching.");

        var user = GetUser(battleId, Message.Sender);

        Assert(!user.ScoreSubmitted, "The user already submitted score.");

        user.Address = Message.Sender;

        SetUser(battleId, Message.Sender, user);

        battle.Users.SetValue(user.Address, userindex);
        SetBattle(battleId, battle);

        Log(battle);
        return true;
    }

    /// <summary>
    /// 4 different user will end the battle and submit the score
    /// </summary>
    public bool EndBattle(Address userAddress, ulong battleId, uint score, bool IsBattleOver)
    {
        Assert(Message.Sender == BattleOwner, "Only battle owner can end game.");

        var battle = GetBattle(battleId);

        Assert(battle.Winner == Address.Zero, "Battle ended.");

        var user = GetUser(battleId, userAddress);

        Assert(!user.ScoreSubmitted, "The user already submitted score.");

        user.Score = score;
        user.ScoreSubmitted = true;

        SetUser(battleId, userAddress, user);

        if (IsBattleOver)
            ProcessWinner(battle);

        Log(user);
        return true;
    }

    /// <summary>
    /// Get winner address
    /// </summary>
    public Address GetWinner(ulong battleId)
    {
        var battle = GetBattle(battleId);
        Log(battle);
        return battle.Winner;
    }

    /// <summary>
    /// Process winner when all user scores are submitted
    /// </summary>
    private void ProcessWinner(BattleMain battle)
    {
        if (battle.Users.Length <= 4)
        {
            foreach (Address userAddress in battle.Users)
            {
                var user = GetUser(battle.BattleId, userAddress);
                if (!user.ScoreSubmitted)
                    return;
            }
        }
        uint winnerIndex = GetWinnerIndex(battle.BattleId, battle.Users);
        if (battle.Winner == Address.Zero)
        {
            battle.Winner = battle.Users[winnerIndex];
            SetBattle(battle.BattleId, battle);
            ProcessPrize(battle.BattleId);
        }
    }

    /// <summary>
    /// Get winner user index from battle users
    /// </summary>
    private uint GetWinnerIndex(ulong battleid, Address[] users)
    {
        uint winningScore = 0;
        uint winningScoreIndex = 0;
        for (uint i = 0; i < users.Length; i++)
        {
            var user = GetUser(battleid, users[i]);
            if (user.Score > winningScore)
            {
                winningScore = user.Score;
                winningScoreIndex = i;
            }
        }
        return winningScoreIndex;
    }

    /// <summary>
    /// Send 3/4 amount to winner and 1/4 amount to battle owner
    /// </summary>
    private void ProcessPrize(ulong battleid)
    {
        var battle = GetBattle(battleid);
        ulong prize = battle.Fee * (battle.MaxUsers - 1);
        Transfer(battle.Winner, prize);
        Transfer(BattleOwner, battle.Fee);
    }

    private void SetUser(ulong battleid, Address address, BattleUser user)
    {
        PersistentState.SetStruct($"user:{battleid}:{address}", user);
    }

    private BattleUser GetUser(ulong battleid, Address address)
    {
        return PersistentState.GetStruct<BattleUser>($"user:{battleid}:{address}");
    }

    private void SetBattle(ulong battleid, BattleMain battle)
    {
        PersistentState.SetStruct($"battle:{battleid}", battle);
    }

    private BattleMain GetBattle(ulong battleid)
    {
        return PersistentState.GetStruct<BattleMain>($"battle:{battleid}");
    }

    public struct BattleMain
    {
        public ulong BattleId;
        public Address Winner;
        public Address[] Users;
        public uint MaxUsers;
        public ulong Fee;
    }

    public struct BattleUser
    {
        public Address Address;
        public uint Score;
        public bool ScoreSubmitted;
    }
}
