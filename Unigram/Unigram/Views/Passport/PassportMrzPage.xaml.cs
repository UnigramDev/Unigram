using Microsoft.Graphics.Canvas;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Unigram.Common;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Views.Passport
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PassportMrzPage : Page
    {
        public PassportMrzPage()
        {
            this.InitializeComponent();

            OpenCommand = new RelayCommand(OpenExecute);
        }

        public static double Hypotenuse(double a, double b)
        {
            return Math.Sqrt(Math.Pow(a, 2) + Math.Pow(b, 2));
        }

        private RelayCommand OpenCommand { get; }
        private async void OpenExecute()
        {
            var picker = new FileOpenPicker();
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.AddRange(Constants.PhotoTypes);

            var file = await picker.PickSingleFileAsync();
            if (file == null)
            {
                return;
            }

            var stream = await file.OpenReadAsync();
            {
                var points = Native.MrzRecognizer.FindCornerPoints(stream);
                if (points != null)
                {
                    var pointsScale = 1.0f;

                    Vector2 topLeft = new Vector2(points[0], points[1]), topRight = new Vector2(points[2], points[3]),
                        bottomLeft = new Vector2(points[4], points[5]), bottomRight = new Vector2(points[6], points[7]);
                    if (topRight.X < topLeft.X)
                    {
                        Vector2 tmp = topRight;
                        topRight = topLeft;
                        topLeft = tmp;
                        tmp = bottomRight;
                        bottomRight = bottomLeft;
                        bottomLeft = tmp;
                    }
                    double topLength = Hypotenuse(topRight.X - topLeft.X, topRight.Y - topLeft.Y);
                    double bottomLength = Hypotenuse(bottomRight.X - bottomLeft.X, bottomRight.Y - bottomLeft.Y);
                    double leftLength = Hypotenuse(bottomLeft.X - topLeft.X, bottomLeft.Y - topLeft.Y);
                    double rightLength = Hypotenuse(bottomRight.X - topRight.X, bottomRight.Y - topRight.Y);
                    double tlRatio = topLength / leftLength;
                    double trRatio = topLength / rightLength;
                    double blRatio = bottomLength / leftLength;
                    double brRatio = bottomLength / rightLength;
                    if ((tlRatio >= 1.35 && tlRatio <= 1.75) && (blRatio >= 1.35 && blRatio <= 1.75) && (trRatio >= 1.35 && trRatio <= 1.75) && (brRatio >= 1.35 && brRatio <= 1.75))
                    {
                        double avgRatio = (tlRatio + trRatio + blRatio + brRatio) / 4.0;
                        float newWidth = 1024;
                        float newHeight = (int)Math.Round(1024 / avgRatio);

                        stream.Seek(0);

                        var props = await file.Properties.GetImagePropertiesAsync();

                        var decoder = await BitmapDecoder.CreateAsync(stream);
                        var pixelData2 = await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, new BitmapTransform(), ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);
                        var pixelData = pixelData2.DetachPixelData();
                        Source.Width = newWidth;
                        Source.Height = newHeight;

                        CanvasDevice device = CanvasDevice.GetSharedDevice();
                        CanvasRenderTarget offscreen = new CanvasRenderTarget(device, newWidth, newHeight, 96);
                        using (CanvasDrawingSession ds = offscreen.CreateDrawingSession())
                        {
                            ds.Clear(Colors.Red);

                            var bitmap = CanvasBitmap.CreateFromBytes(device, pixelData, (int)decoder.PixelWidth, (int)decoder.PixelHeight, Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized);
                            var t = general2DProjection(
                                topLeft.X, topLeft.Y,
                                0, 0,
                                topRight.X, topRight.Y,
                                newWidth, 0,
                                bottomRight.X, bottomRight.Y,
                                newWidth, newHeight,
                                bottomLeft.X, bottomLeft.Y,
                                0, newHeight);
                            for (int i = 0; i != 9; ++i) t[i] = t[i] / t[8];
                            var matrix = new Matrix4x4(t[0], t[3], 0, t[6],
                                 t[1], t[4], 0, t[7],
                                 0, 0, 1, 0,
                                 t[2], t[5], 0, t[8]);

                            ds.DrawImage(bitmap, 0, 0, new Rect(0, 0, props.Width, props.Height), 1, CanvasImageInterpolation.Linear, matrix);
                        }

                        using (var memory = new InMemoryRandomAccessStream())
                        {
                            await offscreen.SaveAsync(memory, CanvasBitmapFileFormat.Png, 1);
                            memory.Seek(0);

                            var charRects = Native.MrzRecognizer.BinarizeAndFindCharacters(memory, out string mrz);

                            offscreen = new CanvasRenderTarget(device, newWidth, newHeight, 96);
                            using (CanvasDrawingSession ds = offscreen.CreateDrawingSession())
                            {
                                ds.Clear(Colors.Red);

                                var bitmap = CanvasBitmap.CreateFromBytes(device, pixelData, (int)decoder.PixelWidth, (int)decoder.PixelHeight, Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized);
                                var t = general2DProjection(
                                    topLeft.X, topLeft.Y,
                                    0, 0,
                                    topRight.X, topRight.Y,
                                    newWidth, 0,
                                    bottomRight.X, bottomRight.Y,
                                    newWidth, newHeight,
                                    bottomLeft.X, bottomLeft.Y,
                                    0, newHeight);
                                for (int i = 0; i != 9; ++i) t[i] = t[i] / t[8];

                                var matrix = new Matrix4x4(t[0], t[3], 0, t[6],
                                     t[1], t[4], 0, t[7],
                                     0, 0, 1, 0,
                                     t[2], t[5], 0, t[8]);

                                ds.DrawImage(bitmap, 0, 0, new Rect(0, 0, props.Width, props.Height), 1, CanvasImageInterpolation.Linear, matrix);

                                foreach (var line in charRects)
                                {
                                    foreach (var box in line)
                                    {
                                        ds.DrawRectangle(box, Colors.Red, 2);
                                    }
                                }
                            }

                            await offscreen.SaveAsync(memory, CanvasBitmapFileFormat.Png, 1);
                            var bitmapImage = new BitmapImage();
                            await bitmapImage.SetSourceAsync(memory);

                            Source.Source = bitmapImage;

                            var result = ParseMrz(mrz);
                            String type = "unknown";
                            if (result.Type == RecognitionResult.TYPE_PASSPORT)
                                type = "Passport";
                            else if (result.Type == RecognitionResult.TYPE_ID)
                                type = "ID";

                            String info = "Type: " + type + "\n" +
                                    "Number: " + result.Number + "\n" +
                                    "First name: " + result.FirstName + "\nLast name: " + result.LastName + "\n" +
                                    "Gender: " + (result.Gender == RecognitionResult.GENDER_MALE ? "male" : (result.Gender == RecognitionResult.GENDER_FEMALE ? "female" : "unknown")) + "\n" +
                                    "Birth date: " + result.birthDay + "." + result.birthMonth + "." + result.birthYear + "\n" +
                                    "Expiry date: " + (result.DoesNotExpire ? "does not expire" : (result.expiryDay + "." + result.expiryMonth + "." + result.expiryYear)) + "\n" +
                                    "Issuing country: " + (result.IssuingCountry) + "\n" +
                                    "Nationality: " + (result.Nationality) + "\n";

                            Mrz.Text = result.Raw;
                            Info.Text = info;
                        }
                    }

                    stream.Dispose();

                    //Lines.Children.Clear();
                    //Lines.Children.Add(new Line { X1 = topLeft.X, Y1 = topLeft.Y, X2 = topRight.X, Y2 = topRight.Y, Stroke = new SolidColorBrush(Colors.Red), StrokeThickness = 2 });
                    //Lines.Children.Add(new Line { X1 = topRight.X, Y1 = topRight.Y, X2 = bottomRight.X, Y2 = bottomRight.Y, Stroke = new SolidColorBrush(Colors.Red), StrokeThickness = 2 });
                    //Lines.Children.Add(new Line { X1 = bottomRight.X, Y1 = bottomRight.Y, X2 = bottomLeft.X, Y2 = bottomLeft.Y, Stroke = new SolidColorBrush(Colors.Red), StrokeThickness = 2 });
                    //Lines.Children.Add(new Line { X1 = bottomLeft.X, Y1 = bottomLeft.Y, X2 = topLeft.X, Y2 = topLeft.Y, Stroke = new SolidColorBrush(Colors.Red), StrokeThickness = 2 });
                }
            }
        }

        private RecognitionResult ParseMrz(string mrz)
        {
            String[] mrzLines = mrz.Split('\n');
            RecognitionResult result = new RecognitionResult();
            if (mrzLines.Length >= 2 && mrzLines[0].Length >= 30 && mrzLines[1].Length == mrzLines[0].Length)
            {
                result.Raw = string.Join("\n", mrzLines);
                //Dictionary<String, String> countries = getCountriesMap();
                char type = mrzLines[0][0];
                if (type == 'P')
                { // passport
                    result.Type = RecognitionResult.TYPE_PASSPORT;
                    if (mrzLines[0].Length == 44)
                    {
                        result.IssuingCountry = mrzLines[0].Substr(2, 5);
                        int lastNameEnd = mrzLines[0].IndexOf("<<", 6);
                        if (lastNameEnd != -1)
                        {
                            result.LastName = mrzLines[0].Substr(5, lastNameEnd).Replace('<', ' ').Replace('0', 'O').Trim();
                            result.FirstName = mrzLines[0].Substring(lastNameEnd + 2).Replace('<', ' ').Replace('0', 'O').Trim();
                            if (result.FirstName.Contains("   "))
                            {
                                result.FirstName = result.FirstName.Substr(0, result.FirstName.IndexOf("   "));
                            }
                        }
                        String number = mrzLines[1].Substr(0, 9).Replace('<', ' ').Replace('O', '0').Trim();
                        int numberChecksum = checksum(number);
                        if (numberChecksum == getNumber(mrzLines[1][9]))
                        {
                            result.Number = number;
                        }
                        result.Nationality = mrzLines[1].Substr(10, 13);
                        String birthDate = mrzLines[1].Substr(13, 19).Replace('O', '0').Replace('I', '1');
                        int birthDateChecksum = checksum(birthDate);
                        if (birthDateChecksum == getNumber(mrzLines[1][19]))
                        {
                            parseBirthDate(birthDate, ref result);
                        }
                        result.Gender = parseGender(mrzLines[1][20]);
                        String expiryDate = mrzLines[1].Substr(21, 27).Replace('O', '0').Replace('I', '1');
                        int expiryDateChecksum = checksum(expiryDate);
                        if (expiryDateChecksum == getNumber(mrzLines[1][27]) || mrzLines[1][27] == '<')
                        {
                            parseExpiryDate(expiryDate, ref result);
                        }

                        // Russian internal passports use transliteration for the name and have a number one digit longer than fits into the standard MRZ format
                        if ("RUS".Equals(result.IssuingCountry) && mrzLines[0][1] == 'N')
                        {
                            result.Type = RecognitionResult.TYPE_INTERNAL_PASSPORT;
                            String[] names = result.FirstName.Split(' ');
                            result.FirstName = cyrillicToLatin(russianPassportTranslit(names[0]));
                            if (names.Length > 1)
                                result.MiddleName = cyrillicToLatin(russianPassportTranslit(names[1]));
                            result.LastName = cyrillicToLatin(russianPassportTranslit(result.LastName));
                            if (result.Number != null)
                                result.Number = result.Number.Substr(0, 3) + mrzLines[1][28] + result.Number.Substring(3);
                        }
                        else
                        {
                            result.FirstName = result.FirstName.Replace('8', 'B');
                            result.LastName = result.LastName.Replace('8', 'B');
                        }
                        result.LastName = capitalize(result.LastName);
                        result.FirstName = capitalize(result.FirstName);
                        result.MiddleName = capitalize(result.MiddleName);
                    }
                }
                else if (type == 'I' || type == 'A' || type == 'C')
                { // id
                    result.Type = RecognitionResult.TYPE_ID;
                    if (mrzLines.Length == 3 && mrzLines[0].Length == 30 && mrzLines[2].Length == 30)
                    {
                        result.IssuingCountry = mrzLines[0].Substr(2, 5);
                        String number = mrzLines[0].Substr(5, 14).Replace('<', ' ').Replace('O', '0').Trim();
                        int numberChecksum = checksum(number);
                        if (numberChecksum == mrzLines[0][14] - '0')
                        {
                            result.Number = number;
                        }

                        String birthDate = mrzLines[1].Substr(0, 6).Replace('O', '0').Replace('I', '1');
                        int birthDateChecksum = checksum(birthDate);
                        if (birthDateChecksum == getNumber(mrzLines[1][6]))
                        {
                            parseBirthDate(birthDate, ref result);
                        }
                        result.Gender = parseGender(mrzLines[1][7]);
                        String expiryDate = mrzLines[1].Substr(8, 14).Replace('O', '0').Replace('I', '1');
                        int expiryDateChecksum = checksum(expiryDate);
                        if (expiryDateChecksum == getNumber(mrzLines[1][14]) || mrzLines[1][14] == '<')
                        {
                            parseExpiryDate(expiryDate, ref result);
                        }
                        result.Nationality = mrzLines[1].Substr(15, 18);
                        int lastNameEnd = mrzLines[2].IndexOf("<<");
                        if (lastNameEnd != -1)
                        {
                            result.LastName = mrzLines[2].Substr(0, lastNameEnd).Replace('<', ' ').Trim();
                            result.FirstName = mrzLines[2].Substring(lastNameEnd + 2).Replace('<', ' ').Trim();
                        }
                    }
                    else if (mrzLines.Length == 2 && mrzLines[0].Length == 36)
                    {
                        result.IssuingCountry = mrzLines[0].Substr(2, 5);
                        if ("FRA".Equals(result.IssuingCountry) && type == 'I' && mrzLines[0][1] == 'D')
                        { // French IDs use an entirely different format
                            result.Nationality = "FRA";
                            result.LastName = mrzLines[0].Substr(5, 30).Replace('<', ' ').Trim();
                            result.FirstName = mrzLines[1].Substr(13, 27).Replace("<<", ", ").Replace('<', ' ').Trim();
                            String number = mrzLines[1].Substr(0, 12).Replace('O', '0');
                            if (checksum(number) == getNumber(mrzLines[1][12]))
                            {
                                result.Number = number;
                            }
                            String birthDate = mrzLines[1].Substr(27, 33).Replace('O', '0').Replace('I', '1');
                            if (checksum(birthDate) == getNumber(mrzLines[1][33]))
                            {
                                parseBirthDate(birthDate, ref result);
                            }
                            result.Gender = parseGender(mrzLines[1][34]);
                            result.DoesNotExpire = true;
                        }
                        else
                        {
                            int lastNameEnd = mrzLines[0].IndexOf("<<");
                            if (lastNameEnd != -1)
                            {
                                result.LastName = mrzLines[0].Substr(5, lastNameEnd).Replace('<', ' ').Trim();
                                result.FirstName = mrzLines[0].Substring(lastNameEnd + 2).Replace('<', ' ').Trim();
                            }
                            String number = mrzLines[1].Substr(0, 9).Replace('<', ' ').Replace('O', '0').Trim();
                            int numberChecksum = checksum(number);
                            if (numberChecksum == getNumber(mrzLines[1][9]))
                            {
                                result.Number = number;
                            }
                            result.Nationality = mrzLines[1].Substr(10, 13);
                            String birthDate = mrzLines[1].Substr(13, 19).Replace('O', '0').Replace('I', '1');
                            if (checksum(birthDate) == getNumber(mrzLines[1][19]))
                            {
                                parseBirthDate(birthDate, ref result);
                            }
                            result.Gender = parseGender(mrzLines[1][20]);
                            String expiryDate = mrzLines[1].Substr(21, 27).Replace('O', '0').Replace('I', '1');
                            if (checksum(expiryDate) == getNumber(mrzLines[1][27]) || mrzLines[1][27] == '<')
                            {
                                parseExpiryDate(expiryDate, ref result);
                            }
                        }
                    }
                    result.FirstName = capitalize(result.FirstName.Replace('0', 'O').Replace('8', 'B'));
                    result.LastName = capitalize(result.LastName.Replace('0', 'O').Replace('8', 'B'));
                }
                else
                {
                    return null;
                }
                if (string.IsNullOrEmpty(result.FirstName) && string.IsNullOrEmpty(result.LastName))
                    return null;
                //result.IssuingCountry = countries.get(result.IssuingCountry);
                //result.Nationality = countries.get(result.Nationality);
                return result;
            }

            return null;
        }

        #region Utils

        private static String capitalize(String s)
        {
            if (s == null)
                return null;
            char[] chars = s.ToArray();
            bool prevIsSpace = true;
            for (int i = 0; i < chars.Length; i++)
            {
                if (!prevIsSpace && char.IsLetter(chars[i]))
                {
                    chars[i] = char.ToLower(chars[i]);
                }
                else
                {
                    prevIsSpace = chars[i] == ' ';
                }
            }
            return new String(chars);
        }

        private static int checksum(String s)
        {
            int val = 0;
            char[] chars = s.ToArray();
            int[] weights = new int[] { 7, 3, 1 };
            for (int i = 0; i < chars.Length; i++)
            {
                int charVal = 0;
                if (chars[i] >= '0' && chars[i] <= '9')
                {
                    charVal = chars[i] - '0';
                }
                else if (chars[i] >= 'A' && chars[i] <= 'Z')
                {
                    charVal = chars[i] - 'A' + 10;
                }
                val += charVal * weights[i % weights.Length];
            }
            return val % 10;
        }

        private static void parseBirthDate(String birthDate, ref RecognitionResult result)
        {
            try
            {
                result.birthYear = int.Parse(birthDate.Substr(0, 2));
                result.birthYear = result.birthYear < DateTime.Now.Year % 100 - 5 ? (2000 + result.birthYear) : (1900 + result.birthYear);
                result.birthMonth = int.Parse(birthDate.Substr(2, 4));
                result.birthDay = int.Parse(birthDate.Substring(4));
            }
            catch (Exception ignore)
            {
            }
        }

        private static void parseExpiryDate(String expiryDate, ref RecognitionResult result)
        {
            try
            {
                if ("<<<<<<".Equals(expiryDate))
                {
                    result.DoesNotExpire = true;
                }
                else
                {
                    result.expiryYear = 2000 + int.Parse(expiryDate.Substr(0, 2));
                    result.expiryMonth = int.Parse(expiryDate.Substr(2, 4));
                    result.expiryDay = int.Parse(expiryDate.Substring(4));
                }
            }
            catch (Exception ignore)
            {
            }
        }

        private static int parseGender(char gender)
        {
            switch (gender)
            {
                case 'M':
                    return RecognitionResult.GENDER_MALE;
                case 'F':
                    return RecognitionResult.GENDER_FEMALE;
                default:
                    return RecognitionResult.GENDER_UNKNOWN;
            }
        }

        private static String russianPassportTranslit(String s)
        {
            const String cyrillic = "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ";
            const String latin = "ABVGDE2JZIQKLMNOPRSTUFHC34WXY9678";
            char[] chars = s.ToArray();
            for (int i = 0; i < chars.Length; i++)
            {
                int idx = latin.IndexOf(chars[i]);
                if (idx != -1)
                    chars[i] = cyrillic[idx];
            }
            return new String(chars);
        }

        private static String cyrillicToLatin(String s)
        {
            const String alphabet = "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ";
            String[] replacements = new string[] { "A","B","V","G","D","E","E","ZH","Z","I","I","K","L","M","N","O","P","R","S","T","U","F","KH","TS","CH","SH","SHCH","IE","Y","","E","IU","IA"};
            for (int i = 0; i < replacements.Length; i++)
            {
                s = s.Replace(alphabet.Substr(i, i + 1), replacements[i]);
            }
            return s;
        }

        private static int getNumber(char c)
        {
            if (c == 'O')
                return 0;
            if (c == 'I')
                return 1;
            if (c == 'B')
                return 8;
            return c - '0';
        }

        #endregion

        float[] adj(float[] m)
        { // Compute the adjugate of m
            return new float[] {
              m[4] * m[8] - m[5] * m[7], m[2] * m[7] - m[1] * m[8], m[1] * m[5] - m[2] * m[4],
              m[5] * m[6] - m[3] * m[8], m[0] * m[8] - m[2] * m[6], m[2] * m[3] - m[0] * m[5],
              m[3] * m[7] - m[4] * m[6], m[1] * m[6] - m[0] * m[7], m[0] * m[4] - m[1] * m[3]
            };
        }

        float[] multmm(float[] a, float[] b)
        { // multiply two matrices
            var c = new float[9];
            for (var i = 0; i != 3; ++i)
            {
                for (var j = 0; j != 3; ++j)
                {
                    var cij = 0f;
                    for (var k = 0; k != 3; ++k)
                    {
                        cij += a[3 * i + k] * b[3 * k + j];
                    }
                    c[3 * i + j] = cij;
                }
            }
            return c;
        }
        float[] multmv(float[] m, float[] v)
        { // multiply matrix and vector
            return new float[] {
              m[0] * v[0] + m[1] * v[1] + m[2] * v[2],
              m[3] * v[0] + m[4] * v[1] + m[5] * v[2],
              m[6] * v[0] + m[7] * v[1] + m[8] * v[2]
            };
        }
        //function pdbg(m, v)
        //{
        //    var r = multmv(m, v);
        //    return r + " (" + r[0] / r[2] + ", " + r[1] / r[2] + ")";
        //}
        float[] basisToPoints(float x1, float y1, float x2, float y2, float x3, float y3, float x4, float y4)
        {
            var m = new float[] {
              x1, x2, x3,
              y1, y2, y3,
               1, 1, 1
            };
            var v = multmv(adj(m), new float[] { x4, y4, 1 });
            return multmm(m, new float[] {
              v[0], 0, 0,
              0, v[1], 0,
              0, 0, v[2]
            });
        }
        float[] general2DProjection(
          float x1s, float y1s, float x1d, float y1d,
          float x2s, float y2s, float x2d, float y2d,
          float x3s, float y3s, float x3d, float y3d,
          float x4s, float y4s, float x4d, float y4d
        )
        {
            var s = basisToPoints(x1s, y1s, x2s, y2s, x3s, y3s, x4s, y4s);
            var d = basisToPoints(x1d, y1d, x2d, y2d, x3d, y3d, x4d, y4d);
            return multmm(d, adj(s));
        }
        float[] project(float[] m, float x, float y)
        {
            var v = multmv(m, new float[] { x, y, 1 });
            return new float[] { v[0] / v[2], v[1] / v[2] };
        }

    }

    public class RecognitionResult
    {
        public const int TYPE_PASSPORT = 1;
        public const int TYPE_ID = 2;
        public const int TYPE_INTERNAL_PASSPORT = 3;
        public const int TYPE_DRIVER_LICENSE = 4;
        public const int GENDER_MALE = 1;
        public const int GENDER_FEMALE = 2;
        public const int GENDER_UNKNOWN = 0;

        public string Raw { get; set; }

        public int Type { get; set; }
        public String FirstName { get; set; }
        public String MiddleName { get; set; }
        public String LastName { get; set; }
        public String Number { get; set; }
        public int expiryYear, expiryMonth, expiryDay;
        public int birthYear, birthMonth, birthDay;
        public String IssuingCountry { get; set; }
        public String Nationality { get; set; }
        public int Gender { get; set; }
        public bool DoesNotExpire { get; set; }
        public bool MainCheckDigitIsValid { get; set; }
    }
}
