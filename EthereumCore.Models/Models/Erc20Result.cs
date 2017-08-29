using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace EthereumApi.Models
{
    [DataContract]
    public class Erc20Result
    {
        [DataMember]
        public string TokenAddress { get; set; }

        [DataMember]
        public string TokenName { get; set; }
    }
}
