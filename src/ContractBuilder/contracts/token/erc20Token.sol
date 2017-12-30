pragma solidity ^0.4.9;

import "./erc20Contract.sol";
import "./safeMath.sol";

contract ERC20Token is ERC20Interface, SafeMath {

  modifier onlyowner { if (msg.sender == contractOwner) _; }
  mapping (address => uint256) accounts;
  mapping (address => mapping (address => uint256)) private allowances;
  bool isBlocked;
  address contractOwner;

  function ERC20Token (address _contractOwner) {
    isBlocked = false;
    contractOwner = _contractOwner;
  }

  function changeOwner (address _newOwner) onlyowner {
    contractOwner = _newOwner;
  }

  function balanceOf (address _owner) constant returns (uint256 balance) {
    return accounts [_owner];
  }

  function transfer (address _to, uint256 _value) returns (bool success) {
    if (isBlocked) throw;
    if (accounts [msg.sender] < _value) return false;
    if (_value > 0 && msg.sender != _to) {
      accounts [msg.sender] = safeSub (accounts [msg.sender], _value);
      accounts [_to] = safeAdd (accounts [_to], _value);
      Transfer (msg.sender, _to, _value);
    }
    return true;
  }

  function transferFrom (address _from, address _to, uint256 _value)
  returns (bool success) {
    if (isBlocked) throw;
    if (allowances [_from][msg.sender] < _value) return false;
    if (accounts [_from] < _value) return false;

    allowances [_from][msg.sender] =
      safeSub (allowances [_from][msg.sender], _value);

    if (_value > 0 && _from != _to) {
      accounts [_from] = safeSub (accounts [_from], _value);
      accounts [_to] = safeAdd (accounts [_to], _value);
      Transfer (_from, _to, _value);
    }
    return true;
  }

  function approve (address _spender, uint256 _value) returns (bool success) {
    if (isBlocked) throw;
    allowances [msg.sender][_spender] = _value;
    Approval (msg.sender, _spender, _value);

    return true;
  }

  function allowance (address _owner, address _spender) constant
  returns (uint256 remaining) {
    return allowances [_owner][_spender];
  }

  function blockTransfers() onlyowner {
    isBlocked = true;
  }

  function unBlockTransfers() onlyowner {
    isBlocked = false;
  }
}
