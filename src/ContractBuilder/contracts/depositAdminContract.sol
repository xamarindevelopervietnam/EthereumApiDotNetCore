pragma solidity ^0.4.9;
import "./coin.sol";
import "./transferBaseContract.sol";
import "./token/erc20Contract.sol";

contract DepositAdminContract {

    address _owner;
    mapping (address => address) public depositContractUser;

    modifier onlyowner { if (msg.sender == _owner) _; }

    function DepositAdminContract() {
        _owner = msg.sender;
    }

   function addDepositContractUser(address depositContractAddress, address userAddress) returns(bool isChanged){
        if (depositContractUser[depositContractAddress] == address(0)){
            return false;
        }
        
        depositContractUser[depositContractAddress] = userAddress;

        return true;
   }

   
   function getDepositContractUser(address depositContractAddress) constant returns(address userAddress){
        return depositContractUser[depositContractAddress];
   }
}
