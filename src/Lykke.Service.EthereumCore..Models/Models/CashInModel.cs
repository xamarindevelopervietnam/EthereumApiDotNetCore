﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Lykke.Service.EthereumCore.Models
{
    public class CashInModel : BaseCoinRequestModel
    {
        [Required]
        public string Coin { get; set; }

        [Required]
        public string Receiver { get; set; }
    }
}
