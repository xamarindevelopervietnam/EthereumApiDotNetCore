// <auto-generated>
// Code generated by Microsoft (R) AutoRest Code Generator.
// Changes may cause incorrect behavior and will be lost if the code is
// regenerated.
// </auto-generated>

namespace Lykke.Service.EthereumCore.Client.Models
{
    using Newtonsoft.Json;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    public partial class FilteredTokenAddressHistoryResponse
    {
        /// <summary>
        /// Initializes a new instance of the
        /// FilteredTokenAddressHistoryResponse class.
        /// </summary>
        public FilteredTokenAddressHistoryResponse()
        {
            CustomInit();
        }

        /// <summary>
        /// Initializes a new instance of the
        /// FilteredTokenAddressHistoryResponse class.
        /// </summary>
        public FilteredTokenAddressHistoryResponse(IList<TokenAddressHistoryResponse> history = default(IList<TokenAddressHistoryResponse>))
        {
            History = history;
            CustomInit();
        }

        /// <summary>
        /// An initialization method that performs custom operations like setting defaults
        /// </summary>
        partial void CustomInit();

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "history")]
        public IList<TokenAddressHistoryResponse> History { get; set; }

    }
}
