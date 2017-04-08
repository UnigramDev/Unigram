using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Unigram.Core.Stripe
{
    public class Card
    {
        public enum StringDef
        {
            AMERICAN_EXPRESS,
            DISCOVER,
            JCB,
            DINERS_CLUB,
            VISA,
            MASTERCARD,
            UNKNOWN
        };
        public const String AMERICAN_EXPRESS = "American Express";
        public const String DISCOVER = "Discover";
        public const String JCB = "JCB";
        public const String DINERS_CLUB = "Diners Club";
        public const String VISA = "Visa";
        public const String MASTERCARD = "MasterCard";
        public const String UNKNOWN = "Unknown";

        public enum FUNDING
        {
            FUNDING_CREDIT,
            FUNDING_DEBIT,
            FUNDING_PREPAID,
            FUNDING_UNKNOWN
        };
        public const String FUNDING_CREDIT = "credit";
        public const String FUNDING_DEBIT = "debit";
        public const String FUNDING_PREPAID = "prepaid";
        public const String FUNDING_UNKNOWN = "unknown";

        // Based on http://en.wikipedia.org/wiki/Bank_card_number#Issuer_identification_number_.28IIN.29
        public static String[] PREFIXES_AMERICAN_EXPRESS = { "34", "37" };
        public static String[] PREFIXES_DISCOVER = { "60", "62", "64", "65" };
        public static String[] PREFIXES_JCB = { "35" };
        public static String[] PREFIXES_DINERS_CLUB = { "300", "301", "302", "303", "304", "305", "309", "36", "38", "39" };
        public static String[] PREFIXES_VISA = { "4" };
        public static String[] PREFIXES_MASTERCARD = {
            "2221", "2222", "2223", "2224", "2225", "2226", "2227", "2228", "2229",
            "223", "224", "225", "226", "227", "228", "229",
            "23", "24", "25", "26",
            "270", "271", "2720",
            "50", "51", "52", "53", "54", "55"
        };

        public static int MAX_LENGTH_STANDARD = 16;
        public static int MAX_LENGTH_AMERICAN_EXPRESS = 15;
        public static int MAX_LENGTH_DINERS_CLUB = 14;

        public String Number { get; private set; }
        public String CVC { get; private set; }
        public int ExpiryMonth { get; private set; }
        public int ExpiryYear { get; private set; }
        public String Name { get; set; }
        public String AddressLine1 { get; set; }
        public String AddressLine2 { get; set; }
        public String AddressCity { get; set; }
        public String AddressState { get; set; }
        public String AddressZip { get; set; }
        public String AddressCountry { get; set; }
        public String Last4 { get; private set; }
        public String Brand { get; private set; } // @CardBrand
        public String Funding { get; private set; } // @FundingType
        public String Fingerprint { get; private set; }
        public String Country { get; private set; }
        public String Currency { get; set; }
        public String Id { get; private set; }

        /// <summary>
        /// Card constructor with all available fields.
        /// </summary>
        /// <param name="number">the card number</param>
        /// <param name="expMonth">the expiry month</param>
        /// <param name="expYear">the expiry year</param>
        /// <param name="cvc">the CVC code</param>
        /// <param name="name">the cardholder name</param>
        /// <param name="addressLine1">the first line of the billing address</param>
        /// <param name="addressLine2">the second line of the billing address</param>
        /// <param name="addressCity">the city of the billing address</param>
        /// <param name="addressState">the state of the billing address</param>
        /// <param name="addressZip">the zip code of the billing address</param>
        /// <param name="addressCountry">the country of the billing address</param>
        /// <param name="brand">brand of this card</param>
        /// <param name="last4">last 4 digits of the card</param>
        /// <param name="fingerprint">the card fingerprint</param>
        /// <param name="funding">the funding type of the card</param>
        /// <param name="country">ISO country code of the card itself</param>
        /// <param name="currency">the currency of the card</param>
        /// <param name="id">the cardId</param>
        public Card(
            String number,
            int expMonth,
            int expYear,
            string cvc,
            string name,
            string addressLine1,
            string addressLine2,
            string addressCity,
            string addressState,
            string addressZip,
            string addressCountry,
            string brand,
            string last4,
            string fingerprint,
            string funding,
            string country,
            string currency,
            string id)
        {
            Number = StripeTextUtils.NullIfBlank(NormalizeCardNumber(number));
            ExpiryMonth = expMonth;
            ExpiryYear = expYear;
            CVC = StripeTextUtils.NullIfBlank(cvc);
            Name = StripeTextUtils.NullIfBlank(name);
            AddressLine1 = StripeTextUtils.NullIfBlank(addressLine1);
            AddressLine2 = StripeTextUtils.NullIfBlank(addressLine2);
            AddressCity = StripeTextUtils.NullIfBlank(addressCity);
            AddressState = StripeTextUtils.NullIfBlank(addressState);
            AddressZip = StripeTextUtils.NullIfBlank(addressZip);
            AddressCountry = StripeTextUtils.NullIfBlank(addressCountry);
            Brand = StripeTextUtils.AsCardBrand(brand) == null ? GetBrand() : brand;
            Last4 = StripeTextUtils.NullIfBlank(last4) == null ? GetLast4() : last4;
            Fingerprint = StripeTextUtils.NullIfBlank(fingerprint);
            Funding = StripeTextUtils.AsFundingType(funding);
            Country = StripeTextUtils.NullIfBlank(country);
            Currency = StripeTextUtils.NullIfBlank(currency);
            Id = StripeTextUtils.NullIfBlank(id);
        }

        /// <summary>
        /// Convenience constructor with address and currency.
        /// </summary>
        /// <param name="number">the card number</param>
        /// <param name="expMonth">the expiry month</param>
        /// <param name="expYear">the expiry year</param>
        /// <param name="cvc">the CVC code</param>
        /// <param name="name">the cardholder name</param>
        /// <param name="addressLine1">the first line of the billing address</param>
        /// <param name="addressLine2">the second line of the billing address</param>
        /// <param name="addressCity">the city of the billing address</param>
        /// <param name="addressState">the state of the billing address</param>
        /// <param name="addressZip">the zip code of the billing address</param>
        /// <param name="addressCountry">the country of the billing address</param>
        /// <param name="currency">the currency of the card</param>
        public Card(
            string number,
            int expMonth,
            int expYear,
            string cvc,
            string name,
            string addressLine1,
            string addressLine2,
            string addressCity,
            string addressState,
            string addressZip,
            string addressCountry,
            string currency)
            : this(number,
                   expMonth,
                   expYear,
                   cvc,
                   name,
                   addressLine1,
                   addressLine2,
                   addressCity,
                   addressState,
                   addressZip,
                   addressCountry,
                   null,
                   null,
                   null,
                   null,
                   null,
                   currency,
                   null)
        {
        }

        /// <summary>
        /// Convenience constructor for a Card object with a minimum number of inputs.
        /// </summary>
        /// <param name="number">the card number</param>
        /// <param name="expMonth">the expiry month</param>
        /// <param name="expYear">the expiry year</param>
        /// <param name="cvc">the CVC code</param>
        public Card(String number, int expMonth, int expYear, String cvc)
            : this(number,
                   expMonth,
                   expYear,
                   cvc,
                   null,
                   null,
                   null,
                   null,
                   null,
                   null,
                   null,
                   null,
                   null,
                   null,
                   null,
                   null,
                   null,
                   null)
        {
        }

        /// <summary>
        /// Checks whether <see langword="this"/> represents a valid card.
        /// </summary>
        /// <returns><see langword="true"> if valid, <see langword="false"> otherwise.</returns>
        public bool ValidateCard()
        {
            if (CVC == null)
            {
                return ValidateNumber() && ValidateExpireDate();
            }
            else
            {
                return ValidateNumber() && ValidateExpireDate() && ValidateCVC();
            }
        }

        /// <summary>
        /// Checks whether or not the <see cref="Number"/> property is valid.
        /// </summary>
        /// <returns><see langword="true"> if valid, <see langword="false"> otherwise.</returns>
        public bool ValidateNumber()
        {
            return CardUtils.IsValidCardNumber(Number);
        }

        /// <summary>
        /// Checks whether or not the <see cref="ExpiryMonth"/> and <see cref="ExpiryYear"/> properties represent a valid
        /// </summary>
        /// <returns><see langword="true"> if valid, <see langword="false"> otherwise</returns>
        public bool ValidateExpireDate()
        {
            if (!ValidateExpiryMonth())
            {
                return false;
            }
            if (!ValidateExpiryYear())
            {
                return false;
            }
            return !DateUtils.HasMonthPassed(ExpiryYear, ExpiryMonth);
        }

        /// <summary>
        /// Checks whether or not the <see cref="CVC"/> property is valid.
        /// </summary>
        /// <returns><see langword="true"> if valid, <see langword="false"> otherwise</returns>
        public bool ValidateCVC()
        {
            if (string.IsNullOrWhiteSpace(CVC))
            {
                return false;
            }
            String cvcValue = CVC.Trim();
            String updatedType = GetBrand();
            bool validLength =
                    (updatedType == null && cvcValue.Length >= 3 && cvcValue.Length <= 4)
                    || (AMERICAN_EXPRESS.Equals(updatedType) && cvcValue.Length == 4)
                    || cvcValue.Length == 3;

            return StripeTextUtils.IsWholePositiveNumber(cvcValue) && validLength;
        }

        /// <summary>
        /// Checks whether or not the <see cref="ExpiryMonth"/> property is valid.
        /// </summary>
        /// <returns><see langword="true"> if valid, <see langword="false"> otherwise.</returns>
        public bool ValidateExpiryMonth()
        {
            return ExpiryMonth >= 1 && ExpiryMonth <= 12;
        }

        /// <summary>
        /// Checks whether or not the <see cref="ExpiryYear"/> property is valid.
        /// </summary>
        /// <returns><see langword="true"> if valid, <see langword="false"> otherwise.</returns>
        public bool ValidateExpiryYear()
        {
            return !DateUtils.HasYearPassed(ExpiryYear);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>the <see cref="Last4"/> digits of this card. Sets the value based on the <see cref="Number"/>
        /// if it has not already been set.</returns>
        public string GetLast4()
        {
            if (!string.IsNullOrWhiteSpace(Last4))
            {
                return Last4;
            }

            if (Number != null && Number.Length > 4)
            {
                Last4 = Number.Substring(Number.Length - 4);
                return Last4;
            }

            return null;
        }

        /// <summary>
        /// Gets the <see cref="Brand"/> of this card. Updates the value if none has yet been set, or
        /// if the <see cref="Number"/> has been changed.
        /// </summary>
        /// <returns>the <see cref="Brand"/> of this card</returns>
        public string GetBrand()
        {
            if (string.IsNullOrWhiteSpace(Brand) && !string.IsNullOrWhiteSpace(Number))
            {
                /*@CardBrand*/
                String evaluatedType;
                if (StripeTextUtils.HasAnyPrefix(Number, PREFIXES_AMERICAN_EXPRESS))
                {
                    evaluatedType = AMERICAN_EXPRESS;
                }
                else if (StripeTextUtils.HasAnyPrefix(Number, PREFIXES_DISCOVER))
                {
                    evaluatedType = DISCOVER;
                }
                else if (StripeTextUtils.HasAnyPrefix(Number, PREFIXES_JCB))
                {
                    evaluatedType = JCB;
                }
                else if (StripeTextUtils.HasAnyPrefix(Number, PREFIXES_DINERS_CLUB))
                {
                    evaluatedType = DINERS_CLUB;
                }
                else if (StripeTextUtils.HasAnyPrefix(Number, PREFIXES_VISA))
                {
                    evaluatedType = VISA;
                }
                else if (StripeTextUtils.HasAnyPrefix(Number, PREFIXES_MASTERCARD))
                {
                    evaluatedType = MASTERCARD;
                }
                else
                {
                    evaluatedType = UNKNOWN;
                }

                Brand = evaluatedType;
            }

            return Brand;
        }

        private String NormalizeCardNumber(String number)
        {
            if (number == null)
            {
                return null;
            }

            return Regex.Replace(number.Trim(), "\\s+|-", "");
        }
    }
}
