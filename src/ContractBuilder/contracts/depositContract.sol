pragma solidity ^0.4.9;
import "./coin.sol";
import "./transferBaseContract.sol";
import "./token/erc20Contract.sol";
import "./depositAdminContract.sol";

contract DepositContract {

    address _ethAdapterAddress;
    address _owner;
    address _depositAdminContract;

    modifier onlyowner { if (msg.sender == _owner) _; }

    function DepositContract(address ethAdapterAddress, address depositAdminContract) {
        _owner = msg.sender;
        _ethAdapterAddress = ethAdapterAddress;
        _depositAdminContract = depositAdminContract;
    }

    function() payable {
    }

    function changeDepositAdminContract(address newDepositAdminContractAddress) onlyowner {
        _depositAdminContract = newDepositAdminContractAddress;
    }

    function cashinTokens(address erc20TokenAddress, address tokenAdapterAddress) onlyowner {
        var erc20Token = ERC20Interface(erc20TokenAddress);
        var tokenBalance = erc20Token.balanceOf(this);

        if (tokenBalance <= 0) {
            throw;
        }

        var coin_contract = Coin(tokenAdapterAddress);

        if (!erc20Token.transfer(tokenAdapterAddress, tokenBalance)) {
            throw;
        }
        
        var depositAdmin = DepositAdminContract(_depositAdminContract);
        address userAddress = depositAdmin.getDepositContractUser(this);

        if (!coin_contract.cashin(userAddress, tokenBalance)) {
            throw;
        }
    }

    //old cashin workflow
    function cashin() onlyowner {
        if (this.balance <= 0) {
            throw;
        }

        var coin_contract = Coin(_ethAdapterAddress);
        if (!coin_contract.cashin.value(this.balance)(this, this.balance)){
            throw;
        }
    }
}
