using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unigram.Core.Stripe
{
    /// <summary>
    /// Utility class for static functions useful for networking and data transfer. You probably will
    /// not need to call functions from this class in your code.
    /// </summary>
    public class StripeNetworkUtils
    {
        /// <summary>
        /// A utility function to map the fields of a <see cref="Card"/> object into a <see langword="Dictionary"/> we
        /// can use in network communications.
        /// </summary>
        /// <param name="card">the <see cref="Card"/> to be read</param>
        /// <returns>a <see langword="Dictionary"/> containing the appropriate values read from the card</returns>
        public static Dictionary<string, string> HashMapFromCard(Card card)
        {
            var cardParams = new Dictionary<string, string>();
            cardParams["card[number]"] = StripeTextUtils.NullIfBlank(card.Number);
            cardParams["card[cvc]"] = StripeTextUtils.NullIfBlank(card.CVC);
            cardParams["card[exp_month]"] = card.ExpiryMonth.ToString();
            cardParams["card[exp_year]"] = card.ExpiryYear.ToString();
            cardParams["card[name]"] = StripeTextUtils.NullIfBlank(card.Name);
            cardParams["card[currency]"] = StripeTextUtils.NullIfBlank(card.Currency);
            cardParams["card[address_line1]"] = StripeTextUtils.NullIfBlank(card.AddressLine1);
            cardParams["card[address_line2]"] = StripeTextUtils.NullIfBlank(card.AddressLine2);
            cardParams["card[address_city]"] = StripeTextUtils.NullIfBlank(card.AddressCity);
            cardParams["card[address_zip]"] = StripeTextUtils.NullIfBlank(card.AddressZip);
            cardParams["card[address_state]"] = StripeTextUtils.NullIfBlank(card.AddressState);
            cardParams["card[address_country]"] = StripeTextUtils.NullIfBlank(card.AddressCountry);

            // Remove all null values; they cause validation errors
            RemoveNullParams(cardParams);

            return cardParams;
        }

        /// <summary>
        /// Remove null values from a map. This helps with JSON conversion and validation.
        /// </summary>
        /// <param name="mapToEdit">a <see langword="Dictionary"/> from which to remove the keys that have <see langword="null"> values</param>
        private static void RemoveNullParams(Dictionary<string, string> mapToEdit)
        {
            // Remove all null values; they cause validation errors
            foreach (String key in mapToEdit.Keys.ToList())
            {
                if (mapToEdit[key] == null)
                {
                    mapToEdit.Remove(key);
                }
            }
        }
    }
}
