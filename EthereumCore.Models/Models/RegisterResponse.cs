using System;

namespace EthereumApiSelfHosted.Models
{
    public class RegisterResponse
    {
        public string Contract { get; set; }
    }

    public class EventResponse
    {
        public Guid EventId { get; set; }
    }
}