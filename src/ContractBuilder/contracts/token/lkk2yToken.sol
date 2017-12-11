pragma solidity ^0.4.9;
import "./erc20Token.sol";
import "./lykkeTokenBase.sol";
import "./nonEmissiveToken.sol";

contract Lkk2yToken is NonEmissiveToken {

  function Lkk2yToken() NonEmissiveToken(0xd1BF1706306C7B667c67fFB5C1f76cC7637685bD, 0xaD66EcE9Bf8C71870AeCdaf01b06dCf4b3c2F579, "Lykke 2-year Forward Token", 18, "LKK2Y", "1.0.0", 25000000000000000000000000 ){
  }
}
