using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace EthereumApi.Models
{
    public class CreateCoinAdapterModelWithToken
    {
        public string ExternalTokenAddress { get; set; }
        public string TokenName { get; set; }
    }
}
