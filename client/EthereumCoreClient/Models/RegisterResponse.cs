// <auto-generated>
// Code generated by Microsoft (R) AutoRest Code Generator.
// Changes may cause incorrect behavior and will be lost if the code is
// regenerated.
// </auto-generated>

namespace Lykke.EthereumCoreClient.Models
{
    using Newtonsoft.Json;
    using System.Linq;

    public partial class RegisterResponse
    {
        /// <summary>
        /// Initializes a new instance of the RegisterResponse class.
        /// </summary>
        public RegisterResponse()
        {
            CustomInit();
        }

        /// <summary>
        /// Initializes a new instance of the RegisterResponse class.
        /// </summary>
        public RegisterResponse(string contract = default(string))
        {
            Contract = contract;
            CustomInit();
        }

        /// <summary>
        /// An initialization method that performs custom operations like setting defaults
        /// </summary>
        partial void CustomInit();

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "contract")]
        public string Contract { get; set; }

    }
}