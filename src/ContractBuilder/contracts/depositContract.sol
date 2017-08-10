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
    function cashinTokens(address erc20TokenAddress, address tokenAdapterAddress, uint amount) onlyowner {
        var erc20Token = ERC20Interface(erc20TokenAddress);
        var tokenBalance = erc20Token.balanceOf(this);

        if (tokenBalance < amount) {
            throw;
        }

        var coin_contract = Coin(tokenAdapterAddress);

        if (!erc20Token.transfer(tokenAdapterAddress, amount)) {
            throw;
        }
        
        address userAddress = getDepositContractUser();

        if (!coin_contract.cashin(userAddress, amount)) {
            throw;
        }
    }

    //new cashin workflow
    function cashinEth(address ethAdapterAddress, uint amount) onlyowner {
        if (this.balance < amount) {
            throw;
        }

        address userAddress = getDepositContractUser();
        var coin_contract = Coin(ethAdapterAddress);
        if (!coin_contract.cashin.value(this.balance)(userAddress, amount)){
            throw;
        }
    }

    //old cashin workflow for old adapter.
    //TODO: Remove that method after migration and update config accordingly
    function cashin(address ethAdapterAddress, uint amount) onlyowner {
        if (this.balance < amount) {
            throw;
        }

        var coin_contract = Coin(ethAdapterAddress);
        if (!coin_contract.cashin.value(this.balance)(this, amount)){
            throw;
        }
    }

    function getDepositContractUser() private returns(address user){
        var depositAdmin = DepositAdminContract(_depositAdminContract);
        address userAddress = depositAdmin.getDepositContractUser(this);

        return userAddress;
    }
}
