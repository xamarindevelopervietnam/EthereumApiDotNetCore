pragma solidity ^0.4.9;
import "./coin.sol";
import "./transferBaseContract.sol";
import "./token/erc20Contract.sol";
import "./depositAdminContract.sol";

contract DepositContract {

    address _owner;
    address _depositAdminContract;

    modifier onlyowner { if (msg.sender == _owner) _; }

    function DepositContract(address depositAdminContract) {
        _owner = msg.sender;
        _depositAdminContract = depositAdminContract;
    }

    function() payable {
    }

    function changeDepositAdminContract(address newDepositAdminContractAddress) onlyowner {
        _depositAdminContract = newDepositAdminContractAddress;
    }

    //new token cashin workflow
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
        
        address userAddress = getDepositContractUser();

        if (!coin_contract.cashin(userAddress, tokenBalance)) {
            throw;
        }
    }

    //new cashin workflow
    function cashinEth(address ethAdapterAddress) onlyowner {
        if (this.balance <= 0) {
            throw;
        }

        address userAddress = getDepositContractUser();
        var coin_contract = Coin(ethAdapterAddress);
        if (!coin_contract.cashin.value(this.balance)(userAddress, this.balance)){
            throw;
        }
    }

    //old cashin workflow for old adapter.
    //TODO: Remove that method after migration and update config accordingly
    function cashin(address ethAdapterAddress) onlyowner {
        if (this.balance <= 0) {
            throw;
        }

        var coin_contract = Coin(ethAdapterAddress);
        if (!coin_contract.cashin.value(this.balance)(this, this.balance)){
            throw;
        }
    }

    function getDepositContractUser() private returns(address user){
        var depositAdmin = DepositAdminContract(_depositAdminContract);
        address userAddress = depositAdmin.getDepositContractUser(this);

        return userAddress;
    }
}
