// <auto-generated>
// Code generated by Microsoft (R) AutoRest Code Generator.
// Changes may cause incorrect behavior and will be lost if the code is
// regenerated.
// </auto-generated>

namespace Lykke.Service.EthereumCore.Client.Models
{
    using Newtonsoft.Json;
    using System.Linq;

    public partial class ApiError
    {
        /// <summary>
        /// Initializes a new instance of the ApiError class.
        /// </summary>
        public ApiError()
        {
            CustomInit();
        }

        /// <summary>
        /// Initializes a new instance of the ApiError class.
        /// </summary>
        /// <param name="code">Possible values include: 'None',
        /// 'ContractPoolEmpty', 'MissingRequiredParams', 'WrongParams',
        /// 'EntityAlreadyExists', 'WrongSign', 'OperationWithIdAlreadyExists',
        /// 'NotEnoughFunds', 'TransactionExists',
        /// 'TransactionRequiresMoreGas'</param>
        public ApiError(string code = default(string), string message = default(string))
        {
            Code = code;
            Message = message;
            CustomInit();
        }

        /// <summary>
        /// An initialization method that performs custom operations like setting defaults
        /// </summary>
        partial void CustomInit();

        /// <summary>
        /// Gets or sets possible values include: 'None', 'ContractPoolEmpty',
        /// 'MissingRequiredParams', 'WrongParams', 'EntityAlreadyExists',
        /// 'WrongSign', 'OperationWithIdAlreadyExists', 'NotEnoughFunds',
        /// 'TransactionExists', 'TransactionRequiresMoreGas'
        /// </summary>
        [JsonProperty(PropertyName = "code")]
        public string Code { get; set; }

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "message")]
        public string Message { get; set; }

    }
}
