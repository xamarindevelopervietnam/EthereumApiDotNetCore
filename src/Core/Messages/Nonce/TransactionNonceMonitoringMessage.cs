using Core.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Messages.Nonce
{
    public class TransactionNonceMonitoringMessage : QueueMessageBase
    {
        public string FromAddress { get; set; }
        public string TransactionHash { get; set; }
        public string Nonce { get; set; }
    }
}
