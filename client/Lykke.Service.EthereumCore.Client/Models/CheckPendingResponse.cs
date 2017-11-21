// <auto-generated>
// Code generated by Microsoft (R) AutoRest Code Generator.
// Changes may cause incorrect behavior and will be lost if the code is
// regenerated.
// </auto-generated>

namespace Lykke.Service.EthereumCore.Client.Models
{
    using Newtonsoft.Json;
    using System.Linq;

    public partial class CheckPendingResponse
    {
        /// <summary>
        /// Initializes a new instance of the CheckPendingResponse class.
        /// </summary>
        public CheckPendingResponse()
        {
            CustomInit();
        }

        /// <summary>
        /// Initializes a new instance of the CheckPendingResponse class.
        /// </summary>
        public CheckPendingResponse(bool isSynced)
        {
            IsSynced = isSynced;
            CustomInit();
        }

        /// <summary>
        /// An initialization method that performs custom operations like setting defaults
        /// </summary>
        partial void CustomInit();

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "isSynced")]
        public bool IsSynced { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="Microsoft.Rest.ValidationException">
        /// Thrown if validation fails
        /// </exception>
        public virtual void Validate()
        {
            //Nothing to validate
        }
    }
}
