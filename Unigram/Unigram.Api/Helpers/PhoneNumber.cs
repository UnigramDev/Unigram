using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Telegram.Helpers
{
    public class PhoneNumber
    {
        public static string Format(string number)
        {
            if (number.Any(x => x < '0' || x > '9'))
            {
                number = Regex.Replace(number, "[^\\d]", string.Empty);
            }

            var groups = Parse(number);
            if (groups.Length == 0)
            {
                return $"+{number}";
            }

            var result = new StringBuilder(number.Length + groups.Length + 1);
            result.Append("+");

            var sum = 0;

            for (int i = 0; i < groups.Length && sum < number.Length; ++i)
            {
                result.Append(number.Substring(sum, Math.Min(number.Length - sum, groups[i])));
                sum += groups[i];
                if (sum < number.Length)
                {
                    result.Append(' ');
                }
            }

            if (sum < number.Length)
            {
                result.Append(number.Substring(sum));
            }

            return result.ToString();
        }

        public static int[] Parse(string number)
        {
            var len = number.Length;
            if (len > 0) switch (number[0])
                {
                    case '9':
                        if (len > 1) switch (number[1])
                            {
                                case '9':
                                    if (len > 2) switch (number[2])
                                        {
                                            case '8': return new int[3] { 3, 2, 7 };
                                            case '6': return new int[1] { 3 };
                                            case '5': return new int[1] { 3 };
                                            case '4': return new int[5] { 3, 2, 3, 2, 2 };
                                            case '3': return new int[3] { 3, 2, 6 };
                                            case '2': return new int[1] { 3 };
                                        }
                                    break;
                                case '8': return new int[4] { 2, 3, 3, 4 };
                                case '7':
                                    if (len > 2) switch (number[2])
                                        {
                                            case '7': return new int[1] { 3 };
                                            case '6': return new int[1] { 3 };
                                            case '5': return new int[1] { 3 };
                                            case '4': return new int[1] { 3 };
                                            case '3': return new int[3] { 3, 4, 4 };
                                            case '2': return new int[4] { 3, 2, 3, 4 };
                                            case '1': return new int[4] { 3, 2, 3, 4 };
                                            case '0': return new int[4] { 3, 3, 2, 4 };
                                        }
                                    break;
                                case '6':
                                    if (len > 2) switch (number[2])
                                        {
                                            case '8': return new int[3] { 3, 4, 4 };
                                            case '7': return new int[4] { 3, 3, 3, 3 };
                                            case '6': return new int[1] { 3 };
                                            case '5': return new int[3] { 3, 4, 4 };
                                            case '4': return new int[4] { 3, 3, 3, 4 };
                                            case '3': return new int[1] { 3 };
                                            case '2': return new int[4] { 3, 1, 4, 4 };
                                            case '1': return new int[1] { 3 };
                                            case '0': return new int[1] { 3 };
                                        }
                                    break;
                                case '5': return new int[1] { 2 };
                                case '4': return new int[4] { 2, 2, 3, 4 };
                                case '3': return new int[4] { 2, 3, 3, 3 };
                                case '2': return new int[4] { 2, 3, 3, 4 };
                                case '1': return new int[3] { 2, 5, 5 };
                                case '0': return new int[4] { 2, 3, 3, 4 };
                            }
                        break;
                    case '8':
                        if (len > 1) switch (number[1])
                            {
                                case '8':
                                    if (len > 2) switch (number[2])
                                        {
                                            case '6': return new int[1] { 3 };
                                            case '0': return new int[1] { 3 };
                                        }
                                    break;
                                case '6': return new int[4] { 2, 3, 4, 4 };
                                case '5':
                                    if (len > 2) switch (number[2])
                                        {
                                            case '6': return new int[1] { 3 };
                                            case '5': return new int[1] { 3 };
                                            case '3': return new int[1] { 3 };
                                            case '2': return new int[1] { 3 };
                                            case '0': return new int[1] { 3 };
                                        }
                                    break;
                                case '4': return new int[1] { 2 };
                                case '2': return new int[1] { 2 };
                                case '1': return new int[4] { 2, 2, 4, 4 };
                            }
                        break;
                    case '7': return new int[5] { 1, 3, 3, 2, 2 };
                    case '6':
                        if (len > 1) switch (number[1])
                            {
                                case '9':
                                    if (len > 2) switch (number[2])
                                        {
                                            case '2': return new int[1] { 3 };
                                            case '1': return new int[1] { 3 };
                                            case '0': return new int[1] { 3 };
                                        }
                                    break;
                                case '8':
                                    if (len > 2) switch (number[2])
                                        {
                                            case '9': return new int[1] { 3 };
                                            case '8': return new int[1] { 3 };
                                            case '7': return new int[1] { 3 };
                                            case '6': return new int[1] { 3 };
                                            case '5': return new int[1] { 3 };
                                            case '3': return new int[1] { 3 };
                                            case '2': return new int[1] { 3 };
                                            case '1': return new int[1] { 3 };
                                            case '0': return new int[1] { 3 };
                                        }
                                    break;
                                case '7':
                                    if (len > 2) switch (number[2])
                                        {
                                            case '9': return new int[1] { 3 };
                                            case '8': return new int[1] { 3 };
                                            case '7': return new int[1] { 3 };
                                            case '6': return new int[1] { 3 };
                                            case '5': return new int[1] { 3 };
                                            case '4': return new int[1] { 3 };
                                            case '3': return new int[3] { 3, 3, 4 };
                                            case '2': return new int[1] { 3 };
                                            case '0': return new int[1] { 3 };
                                        }
                                    break;
                                case '6': return new int[4] { 2, 1, 4, 4 };
                                case '5': return new int[3] { 2, 4, 4 };
                                case '4': return new int[1] { 2 };
                                case '3': return new int[4] { 2, 3, 3, 4 };
                                case '2': return new int[1] { 2 };
                                case '1': return new int[4] { 2, 3, 3, 3 };
                                case '0': return new int[1] { 2 };
                            }
                        break;
                    case '5':
                        if (len > 1) switch (number[1])
                            {
                                case '9':
                                    if (len > 2) switch (number[2])
                                        {
                                            case '9': return new int[1] { 3 };
                                            case '8': return new int[3] { 3, 4, 4 };
                                            case '7': return new int[3] { 3, 3, 4 };
                                            case '6': return new int[1] { 3 };
                                            case '5': return new int[4] { 3, 3, 3, 3 };
                                            case '4': return new int[1] { 3 };
                                            case '3': return new int[1] { 3 };
                                            case '2': return new int[1] { 3 };
                                            case '1': return new int[4] { 3, 1, 3, 4 };
                                            case '0': return new int[1] { 3 };
                                        }
                                    break;
                                case '8': return new int[4] { 2, 3, 3, 4 };
                                case '7': return new int[4] { 2, 3, 3, 4 };
                                case '6': return new int[4] { 2, 1, 4, 4 };
                                case '5': return new int[4] { 2, 2, 5, 4 };
                                case '4': return new int[1] { 2 };
                                case '3': return new int[3] { 2, 4, 4 };
                                case '2': return new int[1] { 2 };
                                case '1': return new int[4] { 2, 3, 3, 3 };
                                case '0':
                                    if (len > 2) switch (number[2])
                                        {
                                            case '9': return new int[1] { 3 };
                                            case '8': return new int[1] { 3 };
                                            case '7': return new int[3] { 3, 4, 4 };
                                            case '6': return new int[1] { 3 };
                                            case '5': return new int[3] { 3, 4, 4 };
                                            case '4': return new int[3] { 3, 4, 4 };
                                            case '3': return new int[3] { 3, 4, 4 };
                                            case '2': return new int[4] { 3, 1, 3, 4 };
                                            case '1': return new int[1] { 3 };
                                            case '0': return new int[1] { 3 };
                                        }
                                    break;
                            }
                        break;
                    case '4':
                        if (len > 1) switch (number[1])
                            {
                                case '9': return new int[3] { 2, 3, 8 };
                                case '8': return new int[4] { 2, 2, 3, 4 };
                                case '7': return new int[3] { 2, 4, 4 };
                                case '6': return new int[4] { 2, 2, 3, 4 };
                                case '5': return new int[3] { 2, 4, 4 };
                                case '4': return new int[3] { 2, 4, 6 };
                                case '3': return new int[1] { 2 };
                                case '2':
                                    if (len > 2) switch (number[2])
                                        {
                                            case '3': return new int[1] { 3 };
                                            case '1': return new int[1] { 3 };
                                            case '0': return new int[1] { 3 };
                                        }
                                    break;
                                case '1': return new int[4] { 2, 2, 3, 4 };
                                case '0': return new int[4] { 2, 3, 3, 3 };
                            }
                        break;
                    case '3':
                        if (len > 1) switch (number[1])
                            {
                                case '9': return new int[4] { 2, 3, 3, 4 };
                                case '8':
                                    if (len > 2) switch (number[2])
                                        {
                                            case '9': return new int[1] { 3 };
                                            case '7': return new int[1] { 3 };
                                            case '6': return new int[1] { 3 };
                                            case '5': return new int[1] { 3 };
                                            case '2': return new int[1] { 3 };
                                            case '1': return new int[4] { 3, 2, 3, 4 };
                                            case '0': return new int[5] { 3, 2, 3, 2, 2 };
                                        }
                                    break;
                                case '7':
                                    if (len > 2) switch (number[2])
                                        {
                                            case '8': return new int[4] { 3, 3, 3, 4 };
                                            case '7': return new int[3] { 3, 4, 4 };
                                            case '6': return new int[4] { 3, 2, 2, 2 };
                                            case '5': return new int[4] { 3, 2, 3, 4 };
                                            case '4': return new int[4] { 3, 2, 3, 3 };
                                            case '3': return new int[4] { 3, 2, 3, 3 };
                                            case '2': return new int[1] { 3 };
                                            case '1': return new int[3] { 3, 3, 5 };
                                            case '0': return new int[3] { 3, 3, 5 };
                                        }
                                    break;
                                case '6': return new int[4] { 2, 2, 3, 4 };
                                case '5':
                                    if (len > 2) switch (number[2])
                                        {
                                            case '9': return new int[1] { 3 };
                                            case '8': return new int[1] { 3 };
                                            case '7': return new int[3] { 3, 4, 4 };
                                            case '6': return new int[5] { 3, 2, 2, 2, 2 };
                                            case '5': return new int[4] { 3, 2, 3, 4 };
                                            case '4': return new int[3] { 3, 3, 4 };
                                            case '3': return new int[4] { 3, 2, 3, 4 };
                                            case '2': return new int[1] { 3 };
                                            case '1': return new int[4] { 3, 1, 4, 4 };
                                            case '0': return new int[3] { 3, 4, 4 };
                                        }
                                    break;
                                case '4': return new int[4] { 2, 3, 3, 3 };
                                case '3': return new int[6] { 2, 1, 2, 2, 2, 2 };
                                case '2': return new int[5] { 2, 3, 2, 2, 2 };
                                case '1': return new int[6] { 2, 1, 2, 2, 2, 2 };
                                case '0': return new int[4] { 2, 2, 4, 4 };
                            }
                        break;
                    case '2':
                        if (len > 1) switch (number[1])
                            {
                                case '9':
                                    if (len > 2) switch (number[2])
                                        {
                                            case '9': return new int[3] { 3, 3, 3 };
                                            case '8': return new int[3] { 3, 3, 3 };
                                            case '7': return new int[3] { 3, 3, 4 };
                                            case '1': return new int[4] { 3, 1, 3, 3 };
                                            case '0': return new int[3] { 3, 2, 3 };
                                        }
                                    break;
                                case '7': return new int[4] { 2, 2, 3, 4 };
                                case '6':
                                    if (len > 2) switch (number[2])
                                        {
                                            case '9': return new int[3] { 3, 3, 4 };
                                            case '8': return new int[3] { 3, 4, 4 };
                                            case '7': return new int[4] { 3, 2, 3, 3 };
                                            case '6': return new int[4] { 3, 2, 3, 3 };
                                            case '5': return new int[1] { 3 };
                                            case '4': return new int[4] { 3, 2, 3, 4 };
                                            case '3': return new int[4] { 3, 2, 3, 4 };
                                            case '2': return new int[4] { 3, 3, 3, 3 };
                                            case '1': return new int[5] { 3, 2, 2, 3, 2 };
                                            case '0': return new int[4] { 3, 2, 3, 4 };
                                        }
                                    break;
                                case '5':
                                    if (len > 2) switch (number[2])
                                        {
                                            case '8': return new int[4] { 3, 2, 3, 4 };
                                            case '7': return new int[4] { 3, 2, 2, 4 };
                                            case '6': return new int[4] { 3, 2, 3, 4 };
                                            case '5': return new int[4] { 3, 2, 3, 4 };
                                            case '4': return new int[4] { 3, 3, 3, 3 };
                                            case '3': return new int[5] { 3, 2, 2, 2, 2 };
                                            case '2': return new int[4] { 3, 2, 3, 3 };
                                            case '1': return new int[4] { 3, 2, 3, 4 };
                                            case '0': return new int[4] { 3, 3, 3, 3 };
                                        }
                                    break;
                                case '4':
                                    if (len > 2) switch (number[2])
                                        {
                                            case '9': return new int[4] { 3, 2, 3, 4 };
                                            case '8': return new int[5] { 3, 1, 2, 2, 2 };
                                            case '7': return new int[2] { 3, 4 };
                                            case '6': return new int[3] { 3, 3, 4 };
                                            case '5': return new int[3] { 3, 3, 4 };
                                            case '4': return new int[4] { 3, 3, 3, 3 };
                                            case '3': return new int[4] { 3, 2, 3, 4 };
                                            case '2': return new int[4] { 3, 2, 3, 4 };
                                            case '1': return new int[5] { 3, 1, 2, 2, 2 };
                                            case '0': return new int[4] { 3, 3, 3, 3 };
                                        }
                                    break;
                                case '3':
                                    if (len > 2) switch (number[2])
                                        {
                                            case '9': return new int[3] { 3, 2, 5 };
                                            case '8': return new int[3] { 3, 3, 4 };
                                            case '7': return new int[3] { 3, 4, 4 };
                                            case '6': return new int[5] { 3, 2, 2, 2, 2 };
                                            case '5': return new int[5] { 3, 2, 2, 2, 2 };
                                            case '4': return new int[1] { 3 };
                                            case '3': return new int[1] { 3 };
                                            case '2': return new int[4] { 3, 2, 3, 3 };
                                            case '1': return new int[1] { 3 };
                                            case '0': return new int[1] { 3 };
                                        }
                                    break;
                                case '2':
                                    if (len > 2) switch (number[2])
                                        {
                                            case '9': return new int[4] { 3, 2, 3, 3 };
                                            case '8': return new int[4] { 3, 2, 3, 3 };
                                            case '7': return new int[5] { 3, 2, 2, 2, 2 };
                                            case '6': return new int[5] { 3, 2, 2, 2, 2 };
                                            case '5': return new int[4] { 3, 2, 3, 3 };
                                            case '4': return new int[4] { 3, 3, 3, 3 };
                                            case '3': return new int[3] { 3, 4, 4 };
                                            case '2': return new int[3] { 3, 4, 4 };
                                            case '1': return new int[4] { 3, 2, 3, 4 };
                                            case '0': return new int[3] { 3, 3, 4 };
                                        }
                                    break;
                                case '1':
                                    if (len > 2) switch (number[2])
                                        {
                                            case '8': return new int[4] { 3, 2, 3, 4 };
                                            case '6': return new int[4] { 3, 2, 3, 3 };
                                            case '3': return new int[5] { 3, 3, 2, 2, 2 };
                                            case '2': return new int[4] { 3, 2, 3, 4 };
                                            case '1': return new int[4] { 3, 2, 3, 4 };
                                        }
                                    break;
                                case '0': return new int[4] { 2, 2, 3, 4 };
                            }
                        break;
                    case '1':
                        if (len > 1) switch (number[1])
                            {
                                case '8':
                                    if (len > 2) switch (number[2])
                                        {
                                            case '7':
                                                if (len > 3) switch (number[3])
                                                    {
                                                        case '6': return new int[3] { 4, 3, 4 };
                                                    }
                                                break;
                                            case '6':
                                                if (len > 3) switch (number[3])
                                                    {
                                                        case '9': return new int[3] { 4, 3, 4 };
                                                        case '8': return new int[3] { 4, 3, 4 };
                                                    }
                                                break;
                                        }
                                    break;
                                case '7':
                                    if (len > 2) switch (number[2])
                                        {
                                            case '8':
                                                if (len > 3) switch (number[3])
                                                    {
                                                        case '4': return new int[3] { 4, 3, 4 };
                                                    }
                                                break;
                                            case '6':
                                                if (len > 3) switch (number[3])
                                                    {
                                                        case '7': return new int[3] { 4, 3, 4 };
                                                    }
                                                break;
                                            case '5':
                                                if (len > 3) switch (number[3])
                                                    {
                                                        case '8': return new int[3] { 4, 3, 4 };
                                                    }
                                                break;
                                            case '2':
                                                if (len > 3) switch (number[3])
                                                    {
                                                        case '1': return new int[3] { 4, 3, 4 };
                                                    }
                                                break;
                                        }
                                    break;
                                case '6':
                                    if (len > 2) switch (number[2])
                                        {
                                            case '8':
                                                if (len > 3) switch (number[3])
                                                    {
                                                        case '4': return new int[3] { 4, 3, 4 };
                                                    }
                                                break;
                                            case '7':
                                                if (len > 3) switch (number[3])
                                                    {
                                                        case '1': return new int[3] { 4, 3, 4 };
                                                        case '0': return new int[3] { 4, 3, 4 };
                                                    }
                                                break;
                                            case '6':
                                                if (len > 3) switch (number[3])
                                                    {
                                                        case '4': return new int[3] { 4, 3, 4 };
                                                    }
                                                break;
                                            case '4':
                                                if (len > 3) switch (number[3])
                                                    {
                                                        case '9': return new int[3] { 4, 3, 4 };
                                                    }
                                                break;
                                        }
                                    break;
                                case '4':
                                    if (len > 2) switch (number[2])
                                        {
                                            case '7':
                                                if (len > 3) switch (number[3])
                                                    {
                                                        case '3': return new int[3] { 4, 3, 4 };
                                                    }
                                                break;
                                            case '4':
                                                if (len > 3) switch (number[3])
                                                    {
                                                        case '1': return new int[3] { 4, 3, 4 };
                                                        case '5': return new int[3] { 4, 3, 4 };
                                                        case '0': return new int[3] { 4, 3, 4 };
                                                    }
                                                break;
                                        }
                                    break;
                                case '2':
                                    if (len > 2) switch (number[2])
                                        {
                                            case '8':
                                                if (len > 3) switch (number[3])
                                                    {
                                                        case '4': return new int[3] { 4, 3, 4 };
                                                    }
                                                break;
                                            case '6':
                                                if (len > 3) switch (number[3])
                                                    {
                                                        case '8': return new int[3] { 4, 3, 4 };
                                                        case '4': return new int[3] { 4, 3, 4 };
                                                    }
                                                break;
                                            case '4':
                                                if (len > 3) switch (number[3])
                                                    {
                                                        case '6': return new int[3] { 4, 3, 4 };
                                                        case '2': return new int[3] { 4, 3, 4 };
                                                    }
                                                break;
                                        }
                                    break;
                            }
                        return new int[4] { 1, 3, 3, 4 };
                }

            return new int[0];
        }

    }
}
