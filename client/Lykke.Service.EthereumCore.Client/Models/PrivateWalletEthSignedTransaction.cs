// <auto-generated>
// Code generated by Microsoft (R) AutoRest Code Generator.
// Changes may cause incorrect behavior and will be lost if the code is
// regenerated.
// </auto-generated>

namespace Lykke.Service.EthereumCore.Client.Models
{
    using Microsoft.Rest;
    using Newtonsoft.Json;
    using System.Linq;

    public partial class PrivateWalletEthSignedTransaction
    {
        /// <summary>
        /// Initializes a new instance of the PrivateWalletEthSignedTransaction
        /// class.
        /// </summary>
        public PrivateWalletEthSignedTransaction()
        {
            CustomInit();
        }

        /// <summary>
        /// Initializes a new instance of the PrivateWalletEthSignedTransaction
        /// class.
        /// </summary>
        public PrivateWalletEthSignedTransaction(string fromAddress, string signedTransactionHex)
        {
            FromAddress = fromAddress;
            SignedTransactionHex = signedTransactionHex;
            CustomInit();
        }

        /// <summary>
        /// An initialization method that performs custom operations like setting defaults
        /// </summary>
        partial void CustomInit();

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "fromAddress")]
        public string FromAddress { get; set; }

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "signedTransactionHex")]
        public string SignedTransactionHex { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="ValidationException">
        /// Thrown if validation fails
        /// </exception>
        public virtual void Validate()
        {
            if (FromAddress == null)
            {
                throw new ValidationException(ValidationRules.CannotBeNull, "FromAddress");
            }
            if (SignedTransactionHex == null)
            {
                throw new ValidationException(ValidationRules.CannotBeNull, "SignedTransactionHex");
            }
        }
    }
}
