pragma solidity ^0.4.1;
import "./coin.sol";

contract MainExchange {

    function MainExchange() {
        _owner = msg.sender;
    }

    modifier onlyowner { if (msg.sender == _owner || (now - _lastPing) > 30 days) _; }

    // can be called only from contract owner
    // create swap transaction signed by exchange and check client signs
    function swap(address client_a, address client_b, address coinAddress_a, address coinAddress_b, uint amount_a, uint amount_b, bytes client_a_sign, bytes client_b_sign) onlyowner returns(bool) {
        
        bytes32  hash = sha3(client_a, client_b, coinAddress_a, coinAddress_b, amount_a, amount_b); 

        if (!_checkClientSign(client_a, hash, client_a_sign)) {
            throw;                    
        }
        if (!_checkClientSign(client_b, hash, client_b_sign)) {
            throw;
        }

        // trasfer amount_a in coin_a from client_a to client_b
        _transferCoins(coinAddress_a, client_a, client_b, amount_a, hash, client_a_sign);

        // trasfer amount_b in coin_b from client_b to client_a
        _transferCoins(coinAddress_b, client_b, client_a, amount_b, hash, client_b_sign);

        return true;
    }

    function cashout(address coinAddress, address client, address to, uint amount, bytes client_sign) onlyowner {
         
        bytes32 hash = sha3(coinAddress, client, to, amount);
            
        if (!_checkClientSign(client, hash, client_sign)) {
            throw;                    
        }

        var coin_contract = Coin(coinAddress);
        coin_contract.cashout(client, to, amount, hash, client_sign);
    }

    // change coin exchange contract
    function changeMainContractInCoin(address coinContract, address newMainContract) onlyowner {
        var coin_contract = Coin(coinContract);
        coin_contract.changeExchangeContract(newMainContract);
    }

    function _transferCoins(address contractAddress, address from, address to, uint amount, bytes32 hash, bytes sig) private {
        var coin_contract = Coin(contractAddress);
        coin_contract.transferMultisig(from, to, amount, hash, sig);
    }

    function _checkClientSign(address client_addr, bytes32 hash, bytes sig) private returns(bool) {
        bytes32 r;
        bytes32 s;
        uint8 v;

        assembly {
            r := mload(add(sig, 32))
            s := mload(add(sig, 64))
            v := mload(add(sig, 65))
        }

        return client_addr == ecrecover(hash, v, r, s);
    }

    function ping() {
        _lastPing = now;
    }

    //private fields

    address _owner;
    uint _lastPing;
}