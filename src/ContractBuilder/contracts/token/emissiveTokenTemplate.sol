pragma solidity ^0.4.9;
import "./erc20Token.sol";
import "./lykkeTokenBase.sol";
import "./emissiveToken.sol";

contract EmissiveTokenTemplate is EmissiveToken {

  function EmissiveTokenTemplate() EmissiveToken(0xfe2b80f7aa6c3d9b4fafeb57d0c9d98005d0e4b6, 0xae4d8b0c887508750ddb6b32752a82431941e2e7, "Lykke USDT Token", 6, "USDT", "1.0.0"){
  }
}
