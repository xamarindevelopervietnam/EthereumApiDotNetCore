pragma solidity ^0.4.9;
import "./coin.sol";

contract EthCoin is Coin {

    function EthCoin(address exchangeContractAddress, address depositAdminContract) Coin(exchangeContractAddress, depositAdminContract) {
     }

    function cashin(address userAddress, uint amount) onlyFromDepositContract payable returns(bool){
        coinBalanceMultisig[userAddress] += msg.value;

        CoinCashIn(userAddress, msg.value);
        
        return true;
    }

    function cashout(address client, address to, uint amount) onlyFromExchangeContract {
        if (coinBalanceMultisig[client] < amount) {
            throw;
        }

        if (!to.send(amount)) throw;

        coinBalanceMultisig[client] -= amount;

        CoinCashOut(msg.sender, client, amount, to);
    }
}