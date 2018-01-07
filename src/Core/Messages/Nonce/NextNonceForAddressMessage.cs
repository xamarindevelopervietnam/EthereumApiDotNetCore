using Core.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Messages.Nonce
{
    public class NextNonceForAddressMessage : QueueMessageBase
    {
        public string Nonce { get; set; }
    }
}
