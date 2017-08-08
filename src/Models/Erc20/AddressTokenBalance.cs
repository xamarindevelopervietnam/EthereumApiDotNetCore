using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace BusinessModels.Erc20
{
    public class AddressTokenBalance
    {
        public string Erc20TokenAddress { get; set; }
        public string UserAddress { get; set; }
        public BigInteger Balance { get; set; }
    }
}
