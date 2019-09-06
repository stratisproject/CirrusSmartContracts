# Airdrop Smart Contract

Generic Airdrop contract that can be used to automate distribution to registrants. Owners of the airdrop can specify a future endblock to close on or choose to manually close the registration period of the contract. Users register by sending their address, when registration is closed, users can call the contracts withdraw method to retrieve their tokens. The airdrop contract calls the tokens contract that is being distributed to transfer the allotted amount. The airdrop contract must be authorized to transfer up to the total supply being distributed.

## Deploying

Parameters required:

- `Address TokenContractAddress` - The contract address of the token being airdropped
- `ulong TotalSupply` - The total supply that will be given out, must be greater than 0
- `ulong EndBlock` - The endblock for the registration period to close on. Set to 0 to close registration manually

Upon deployment, the owners address will be set to the address used to deploy this contract. The owner will have special privaledges and also will be used upon distribution in the withdraw method.

## Registrations

### Public User Registration

Users will can register for the airdrop by calling the `Register` method as long as the registration period is open and there are not more registrants that total supply. Upon successfull registration, a status of `Enrolled` will be set for the address and the new status will be logged.

### Owner Adding Registrants

The contract owner can manually add registrants (not themselves) to the airdrop using the `AddRegistrant` method. This call must supply a parameter of an `Address` type, with the registrants address to add.

## Withdrawing Funds

After the registration period is closed, users can withdraw their funds from the airdrop by calling the `Withdraw` method on the contract with no parameters supplied. This will call the `TransferFrom` method of the smart contract at the `TokenContractAddress` that was set during deployment. This will transfer the tokens from the Owners wallet to the registrants.

**Important For Owners**

The `Owners` wallet address from this airdrop contract, must hold, at minimum, the total supply of this contract. The `Owner` must also approve this deployed airdrop contracts address to send up to the total supply of this contract.

On success funds will be transferred to the registrant and this contract will update the registrants status to `Funded` and log the result.

## Closing the Registration Period

Owners of the contract can call the `CloseRegistration` method to not allow any more registrations and open up the withdraw functionality at any time.
