pragma solidity ^0.4.9;
import "./erc20Token.sol";
import "./lykkeTokenBase.sol";
import "./nonEmissiveToken.sol";

contract Lkk2yToken is NonEmissiveToken {

  function Lkk2yToken(address hotWalletAddress) NonEmissiveToken(hotWalletAddress, "Lykke 2-year Forward Token", 18, "LKK2Y", "1.0.0", 25000000000000000000000000 )
  {
  }
}
