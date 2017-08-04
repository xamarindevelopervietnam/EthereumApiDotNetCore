pragma solidity ^0.4.9;
import "./depositAdminContract.sol";
contract Coin {

    address _owner;
    address _exchangeContractAddress;
    address _depositAdminContract;
    mapping (address => uint) public coinBalanceMultisig;

    event CoinCashIn(address caller, uint amount);
    event CoinCashOut(address caller, address from, uint amount, address to);
    event CoinTransfer(address caller, address from, address to, uint amount);

    modifier onlyowner { if (msg.sender == _owner) _; }
    //currently on prod ethereum adapter
    //modifier ownerOrTransferContract { if (msg.sender == _owner || transferContractUser[msg.sender] != address(0)) _; }
    modifier onlyFromDepositContract { 
        var userAddress = getDepositContractUser(msg.sender);
        if( userAddress != address(0)) _; 
    }
    modifier onlyFromExchangeContract { if (msg.sender == _exchangeContractAddress) _; }

    function Coin(address exchangeContractAddress, address depositAdminContract) {
        _owner = msg.sender;
        _exchangeContractAddress = exchangeContractAddress;
        _depositAdminContract = depositAdminContract;
    }   

    function changeExchangeContract(address newContractAddress) onlyFromExchangeContract {
        _exchangeContractAddress = newContractAddress;
    }

    // transfer coins (called only from exchange contract)
    function transferMultisig(address from, address to, uint amount) onlyFromExchangeContract {       
        if (coinBalanceMultisig[from] < amount) {
            throw;
        }

        coinBalanceMultisig[from] -= amount;
        coinBalanceMultisig[to] += amount;

        CoinTransfer(msg.sender, from, to, amount);
    }

    // virtual method (if not implemented, then throws)
    function cashin(address userAddress, uint amount) onlyFromDepositContract payable returns(bool) { return false; }

    // virtual method (if not implemented, then throws)
    function cashout(address from, address to, uint amount) onlyFromExchangeContract { throw; }

    function balanceOf(address owner) constant returns(uint) {
         var balance = coinBalanceMultisig[owner];

         return balance;
    }

    function getDepositContractUser(address depositContractAddress) private constant returns(address clientAddress){
        var depositContractAdmin = DepositAdminContract(_depositAdminContract);
        var userAddress = depositContractAdmin.getDepositContractUser(msg.sender);

        return userAddress;
    }
    //function getTransferAddressUser(address transferAddress) constant returns(address){
    //     var userAddress = transferContractUser[transferAddress];
//
  //       return userAddress;
    //}

    //function setTransferAddressUser(address userAddress, address transferAddress) onlyowner{
      //   var oldUserAddress = transferContractUser[transferAddress];
         //
        // if (oldUserAddress != address(0)) {
        //     throw;
        // }
//
  //       transferContractUser[transferAddress] = userAddress;
   // }

    function changeDepositAdminContract(address newDepositAdminContractAddress) onlyowner {
        _depositAdminContract = newDepositAdminContractAddress;
    }
}