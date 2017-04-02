using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Unigram.Core.Stripe
{
    public class StripeTextUtils
    {
        /// <summary>
        /// Check to see if the input number has any of the given prefixes.
        /// </summary>
        /// <param name="number">the number to test</param>
        /// <param name="prefixes">the prefixes to test against</param>
        /// <returns><see langword="true"> if number begins with any of the input prefixes</returns>
        public static bool HasAnyPrefix(String number, params String[] prefixes)
        {
            if (number == null)
            {
                return false;
            }

            foreach (String prefix in prefixes)
            {
                if (number.StartsWith(prefix))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Check to see whether the input string is a whole, positive number.
        /// </summary>
        /// <param name="value">the input string to test</param>
        /// <returns><see langword="true"> if the input value consists entirely of integers</returns>
        public static bool IsWholePositiveNumber(String value)
        {
            if (value == null)
            {
                return false;
            }

            // Refraining from using android's TextUtils in order to avoid
            // depending on another package.
            for (int i = 0; i < value.Length; i++)
            {
                if (!char.IsDigit(value[i]))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Swap <see langword="null"> for blank text values.
        /// </summary>
        /// <param name="value">an input string that may or may not be entirely whitespace</param>
        /// <returns><see langword="null"> if the string is entirely whitespace, or the original value if not</returns>
        public static string NullIfBlank(String value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }
            return value;
        }

        /// <summary>
        /// Converts a card number that may have spaces between the numbers into one without any spaces.
        /// Note: method does not check that all characters are digits or spaces.
        /// </summary>
        /// <param name="cardNumberWithSpaces">a card number, for instance "4242 4242 4242 4242"</param>
        /// <returns>the input number minus any spaces, for instance "4242424242424242".
        /// Returns <see langword="null"> if the input was <see langword="null"> or all spaces.</returns>
        public static String RemoveSpacesAndHyphens(string cardNumberWithSpaces)
        {
            if (string.IsNullOrWhiteSpace(cardNumberWithSpaces))
            {
                return null;
            }
            return Regex.Replace(cardNumberWithSpaces, "\\s|-", "");
        }

        /// <summary>
        /// Converts an unchecked String value to a {@link CardBrand} or <see langword="null">.
        /// </summary>
        /// <param name="possibleCardType">a String that might match a {@link CardBrand} or be empty.</param>
        /// <returns><see langword="null"> if the input is blank, else the appropriate {@link CardBrand}.</returns>
        public static string AsCardBrand(string possibleCardType)
        {
            if (string.IsNullOrWhiteSpace(possibleCardType))
            {
                return null;
            }

            if (Card.AMERICAN_EXPRESS.Equals(possibleCardType, StringComparison.OrdinalIgnoreCase))
            {
                return Card.AMERICAN_EXPRESS;
            }
            else if (Card.MASTERCARD.Equals(possibleCardType, StringComparison.OrdinalIgnoreCase))
            {
                return Card.MASTERCARD;
            }
            else if (Card.DINERS_CLUB.Equals(possibleCardType, StringComparison.OrdinalIgnoreCase))
            {
                return Card.DINERS_CLUB;
            }
            else if (Card.DISCOVER.Equals(possibleCardType, StringComparison.OrdinalIgnoreCase))
            {
                return Card.DISCOVER;
            }
            else if (Card.JCB.Equals(possibleCardType, StringComparison.OrdinalIgnoreCase))
            {
                return Card.JCB;
            }
            else if (Card.VISA.Equals(possibleCardType, StringComparison.OrdinalIgnoreCase))
            {
                return Card.VISA;
            }
            else
            {
                return Card.UNKNOWN;
            }
        }

        /// <summary>
        /// Converts an unchecked String value to a {@link FundingType} or <see langword="null">.
        /// </summary>
        /// <param name="possibleFundingType">a String that might match a {@link FundingType} or be empty</param>
        /// <returns><see langword="null"> if the input is blank, else the appropriate {@link FundingType}</returns>
        public static string AsFundingType(string possibleFundingType)
        {
            if (string.IsNullOrWhiteSpace(possibleFundingType))
            {
                return null;
            }

            if (Card.FUNDING_CREDIT.Equals(possibleFundingType, StringComparison.OrdinalIgnoreCase))
            {
                return Card.FUNDING_CREDIT;
            }
            else if (Card.FUNDING_DEBIT.Equals(possibleFundingType, StringComparison.OrdinalIgnoreCase))
            {
                return Card.FUNDING_DEBIT;
            }
            else if (Card.FUNDING_PREPAID.Equals(possibleFundingType, StringComparison.OrdinalIgnoreCase))
            {
                return Card.FUNDING_PREPAID;
            }
            else
            {
                return Card.FUNDING_UNKNOWN;
            }
        }
    }
}
