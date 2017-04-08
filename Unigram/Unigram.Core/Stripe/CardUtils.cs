using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unigram.Core.Stripe
{
    public class CardUtils
    {
        public static int LENGTH_COMMON_CARD = 16;
        public static int LENGTH_AMERICAN_EXPRESS = 15;
        public static int LENGTH_DINERS_CLUB = 14;

        public static int CVC_LENGTH_COMMON = 3;
        public static int CVC_LENGTH_AMEX = 4;

        public static bool IsValidCardNumber(string cardNumber)
        {
            var normalizedNumber = StripeTextUtils.RemoveSpacesAndHyphens(cardNumber);
            return IsValidLuhnNumber(normalizedNumber) && IsValidCardLength(normalizedNumber);
        }

        /// <summary>
        /// Checks the input string to see whether or not it is a valid Luhn number.
        /// </summary>
        /// <param name="cardNumber">a String that may or may not represent a valid Luhn number</param>
        /// <returns><see langword="true"> if and only if the input value is a valid Luhn number</returns>
        public static bool IsValidLuhnNumber(string cardNumber)
        {
            if (cardNumber == null)
            {
                return false;
            }

            bool isOdd = true;
            int sum = 0;

            for (int index = cardNumber.Length - 1; index >= 0; index--)
            {
                char c = cardNumber[index];
                if (!char.IsDigit(c))
                {
                    return false;
                }

                int digitInteger = c - '0';
                isOdd = !isOdd;

                if (isOdd)
                {
                    digitInteger *= 2;
                }

                if (digitInteger > 9)
                {
                    digitInteger -= 9;
                }

                sum += digitInteger;
            }

            return sum % 10 == 0;
        }

        /// <summary>
        /// Checks to see whether the input number is of the correct length, after determining its brand.
        /// This function does not perform a Luhn check.
        /// </summary>
        /// <param name="cardNumber">the card number with no spaces or dashes</param>
        /// <returns><see langword="true"> if the card number is of known type and the correct length</returns>
        public static bool IsValidCardLength(string cardNumber)
        {
            if (cardNumber == null)
            {
                return false;
            }

            return IsValidCardLength(cardNumber, GetPossibleCardType(cardNumber, false));
        }

        /// <summary>
        /// Checks to see whether the input number is of the correct length, given the assumed brand of
        /// the card. This function does not perform a Luhn check.
        /// </summary>
        /// <param name="cardNumber">the card number with no spaces or dashes</param>
        /// <param name="cardBrand">a {@link CardBrand} used to get the correct size</param>
        /// <returns><see langword="true"> if the card number is the correct length for the assumed brand</returns>
        public static bool IsValidCardLength(string cardNumber, string cardBrand)
        {
            if (cardNumber == null || Card.UNKNOWN.Equals(cardBrand))
            {
                return false;
            }

            int length = cardNumber.Length;
            switch (cardBrand)
            {
                case Card.AMERICAN_EXPRESS:
                    return length == LENGTH_AMERICAN_EXPRESS;
                case Card.DINERS_CLUB:
                    return length == LENGTH_DINERS_CLUB;
                default:
                    return length == LENGTH_COMMON_CARD;
            }
        }

        /// <summary>
        /// Returns a {@link CardBrand} corresponding to a partial card number,
        /// or {@link Card#UNKNOWN} if the card brand can't be determined from the input value.
        /// </summary>
        /// <param name="cardNumber">a credit card number or partial card number</param>
        /// <returns>the {@link CardBrand} corresponding to that number,
        /// or {@link Card#UNKNOWN} if it can't be determined</returns>
        public static string GetPossibleCardType(string cardNumber)
        {
            return GetPossibleCardType(cardNumber, true);
        }

        //@NonNull
        //@CardBrand
        private static string GetPossibleCardType(string cardNumber, bool shouldNormalize)
        {
            if (string.IsNullOrWhiteSpace(cardNumber))
            {
                return Card.UNKNOWN;
            }

            var spacelessCardNumber = cardNumber;
            if (shouldNormalize)
            {
                spacelessCardNumber = StripeTextUtils.RemoveSpacesAndHyphens(cardNumber);
            }

            if (StripeTextUtils.HasAnyPrefix(spacelessCardNumber, Card.PREFIXES_AMERICAN_EXPRESS))
            {
                return Card.AMERICAN_EXPRESS;
            }
            else if (StripeTextUtils.HasAnyPrefix(spacelessCardNumber, Card.PREFIXES_DISCOVER))
            {
                return Card.DISCOVER;
            }
            else if (StripeTextUtils.HasAnyPrefix(spacelessCardNumber, Card.PREFIXES_JCB))
            {
                return Card.JCB;
            }
            else if (StripeTextUtils.HasAnyPrefix(spacelessCardNumber, Card.PREFIXES_DINERS_CLUB))
            {
                return Card.DINERS_CLUB;
            }
            else if (StripeTextUtils.HasAnyPrefix(spacelessCardNumber, Card.PREFIXES_VISA))
            {
                return Card.VISA;
            }
            else if (StripeTextUtils.HasAnyPrefix(spacelessCardNumber, Card.PREFIXES_MASTERCARD))
            {
                return Card.MASTERCARD;
            }
            else
            {
                return Card.UNKNOWN;
            }
        }

        /// <summary>
        /// Separates a card number according to the brand requirements, including prefixes of card
        /// numbers, so that the groups can be easily displayed if the user is typing them in.
        /// Note that this does not verify that the card number is valid, or even that it is a number.
        /// </summary>
        /// <param name="spacelessCardNumber">the raw card number, without spaces</param>
        /// <param name="brand">the {@link CardBrand} to use as a separating scheme</param>
        /// <returns>an array of strings with the number groups, in order. If the number is not complete,
        /// some of the array entries may be <see langword="null">.</returns>
        public static String[] SeparateCardNumberGroups(string spacelessCardNumber, string brand)
        {
            String[] numberGroups;
            if (brand.Equals(Card.AMERICAN_EXPRESS))
            {
                numberGroups = new String[3];

                int length = spacelessCardNumber.Length;
                int lastUsedIndex = 0;
                if (length > 4)
                {
                    numberGroups[0] = spacelessCardNumber.Substring(0, 4);
                    lastUsedIndex = 4;
                }

                if (length > 10)
                {
                    numberGroups[1] = spacelessCardNumber.Substring(4, 6);
                    lastUsedIndex = 10;
                }

                for (int i = 0; i < 3; i++)
                {
                    if (numberGroups[i] != null)
                    {
                        continue;
                    }
                    numberGroups[i] = spacelessCardNumber.Substring(lastUsedIndex);
                    break;
                }

            }
            else
            {
                numberGroups = new String[4];
                int i = 0;
                int previousStart = 0;
                while ((i + 1) * 4 < spacelessCardNumber.Length)
                {
                    String group = spacelessCardNumber.Substring(previousStart, ((i + 1) * 4) - previousStart);
                    numberGroups[i] = group;
                    previousStart = (i + 1) * 4;
                    i++;
                }
                // Always stuff whatever is left into the next available array entry. This handles
                // incomplete numbers, full 16-digit numbers, and full 14-digit numbers
                numberGroups[i] = spacelessCardNumber.Substring(previousStart);
            }
            return numberGroups;
        }

    }
}
