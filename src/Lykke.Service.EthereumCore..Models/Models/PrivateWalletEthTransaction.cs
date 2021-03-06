﻿using Lykke.Service.EthereumCore.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text;

namespace Lykke.Service.EthereumCore.Models.Models
{
    [DataContract]
    public class EthTransactionBase
    {
        [DataMember]
        [Required]
        public string FromAddress { get; set; }

        [DataMember]
        [Required]
        public string ToAddress { get; set; }

        [DataMember]
        [Required]
        [RegularExpression(Constants.BigIntTemplate)]
        public string GasAmount { get; set; }

        [DataMember]
        [Required]
        [RegularExpression(Constants.BigIntTemplate)]
        public string GasPrice { get; set; }

        [DataMember]
        [Required]
        [RegularExpression(Constants.BigIntTemplate)]
        public virtual string Value { get; set; }
    }

    [DataContract]
    public class PrivateWalletEthTransaction : EthTransactionBase
    {
        [DataMember]
        [Required]
        [RegularExpression(Constants.BigIntTemplate)]
        public string Value { get; set; }
    }

    [DataContract]
    public class PrivateWalletErc20Transaction : EthTransactionBase
    {
        [DataMember]
        [Required]
        public string TokenAddress { get; set; }

        [DataMember]
        [Required]
        [RegularExpression(Constants.BigIntTemplate)]
        public string TokenAmount { get; set; }

        [DataMember]
        [Required]
        [RegularExpression(Constants.BigIntAllowZeroTemplate)]
        public override string Value { get; set; }
    }

    [DataContract]
    public class PrivateWalletEthSignedTransaction
    {
        [DataMember]
        [Required]
        public string FromAddress { get; set; }

        [DataMember]
        [Required]
        public string SignedTransactionHex { get; set; }
    }
}
