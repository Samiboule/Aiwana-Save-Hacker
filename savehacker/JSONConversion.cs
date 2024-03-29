﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

//This file needs an overhaul
//A lot of the classes call each other in a confusing manner
//A lot of functions probably need better names
//Hex conversion code was taken from elsewhere and could probably be improved for the purpose of this project

namespace JSONConversion
{
    class GMSWeird //Rename this, I don't know what format this is yet
    {
        public static string ToJSON(string input)
        {
            string[] entries = input.Split(",");
            string[,] splitentries = new string[entries.Length, 3];
            for (int i = 0; i < entries.Length; i++)
            {
                string[] arr = entries[i].Split(':');
                splitentries[i, 0] = arr[0];
                splitentries[i, 1] = arr[1];
                splitentries[i, 2] = arr[2];
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("{ ");

            for (int i = 0; i < entries.Length; i++)
            {
                sb.Append("\"" + GMSstringtoJSON(splitentries[i, 1]) + "\": ");
                switch (splitentries[i, 0])
                {
                    case "1":
                        sb.Append(GMSdoubletoJSON(splitentries[i, 2]) + ", ");
                        break;
                    case "3":
                        sb.Append("\"" + GMSstringtoJSON(splitentries[i, 2]) + "\", ");
                        break;
                    default:
                        sb.Append(entries[i] + ", ");
                        break;
                }
            }
            sb.Remove(sb.Length - 2, 2);
            sb.Append(" }");
            string result = sb.ToString();
            return result;
        }

        public static string FromJSON(string input)
        {
            StringBuilder sb = new StringBuilder();
            input = input.TrimStart('{');
            input = input.TrimEnd('}');
            input = input.Trim();
            string[] arr = input.Split(", ");
            for (int i = 0; i < arr.Length; i++)
            {
                string[] dict = arr[i].Split(": ");
                if (dict[1].Contains("\""))
                {
                    sb.Append("3:" + JSONtoGMSstring(dict[0].Trim('\"')) + ":" + JSONtoGMSstring(dict[1]).Trim('\"') + ",");
                }
                else if (Regex.Match(dict[1], "(.:[0-9A-F]+:[0-9A-F]*)").Success)
                {
                    sb.Append(dict[1] + ",");
                }
                else
                {
                    sb.Append("1:" + JSONtoGMSstring(dict[0].Trim('\"')) + ":" + JSONtoGMSdouble(dict[1]) + ",");
                }
            }
            return sb.ToString().TrimEnd(',');
        }

        public static string GMSdoubletoJSON(string value)
        {
            while (value.Length < 16)
            {
                value += "00";
            }
            byte[] doublebytes = new byte[8];
            for (int i = 0; 2 * i < value.Length; i++)
            {
                doublebytes[7 - i] = Convert.ToByte(value.Substring(2 * i, 2), 16);
            }
            double result = BitConverter.ToDouble(doublebytes, 0);
            return result.ToString().Contains('.') ? result.ToString() : result.ToString() + ".000000";
        }

        public static string JSONtoGMSdouble(string value)
        {
            double number = double.Parse(value);
            byte[] bytes = BitConverter.GetBytes(number);
            StringBuilder sb = new StringBuilder();
            for (int i = bytes.Length - 1; i > 0; i--)
            {
                sb.Append(bytes[i].ToString("X2"));
            }
            string result = sb.ToString().TrimEnd('0');
            if (result.Length % 2 == 1)
            {
                return result + "0";
            }
            else
            {
                return result;
            }
        }

        public static string GMSstringtoJSON(string hex)
        {
            if (hex.Length % 2 == 1)
                throw new ArgumentException("The binary key cannot have an odd number of digits");

            byte[] arr = new byte[hex.Length >> 1];

            for (int i = 0; i < hex.Length >> 1; ++i)
            {
                arr[i] = (byte)((GetHexVal(hex[i << 1]) << 4) + (GetHexVal(hex[(i << 1) + 1])));
            }

            return Encoding.UTF8.GetString(arr);
        }

        public static string JSONtoGMSstring(string varname)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(varname);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                sb.Append(bytes[i].ToString("X2"));
            }
            return sb.ToString();
        }

        public static int GetHexVal(char hex)
        {
            int val = (int)hex;
            //For uppercase A-F letters:
            //return val - (val < 58 ? 48 : 55);
            //For lowercase a-f letters:
            //return val - (val < 58 ? 48 : 87);
            //Or the two combined, but a bit slower:
            return val - (val < 58 ? 48 : (val < 97 ? 55 : 87));
        }
    }

    class GM8DSMaps
    {
        public static string ToJSON(string input)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{ ");
            int len = GMSDSMaps.ComputeStringLength(input.Substring(8, 8));
            input = input.Remove(0, 16);
            Queue<string> keys = new Queue<string>(len);
            Queue<string> values = new Queue<string>(len);
            for (int i = 0; i < len; i++)
            {
                int keylen = GMSDSMaps.ComputeStringLength(input.Substring(24, 8));
                keys.Enqueue("\"" + GMSWeird.GMSstringtoJSON(input.Substring(32, 2 * keylen)) + "\"");
                input = input.Remove(0, (keylen * 2) + 32);
            }
            for (int i = 0; i < len; i++)
            {
                switch (input.Substring(0, 8))
                {
                    case "00000000":
                        values.Enqueue(GMSDSMaps.GMSdoubletoJSON(input.Substring(8, 16)));
                        input = input.Remove(0, 32);
                        break;
                    case "01000000":
                        int stringlen = 2 * GMSDSMaps.ComputeStringLength(input.Substring(24, 8));
                        string str = GMSWeird.GMSstringtoJSON(input.Substring(32, stringlen));
                        values.Enqueue("\"" + str + "\"");
                        input = input.Remove(0, 32 + stringlen);
                        break;
                    default:
                        throw new InvalidDataException("Unknown datatype, please go bug the dev.");
                }
            }
            for (int i = 0; i < len; i++)
            {
                sb.Append(keys.Dequeue() + ": " + values.Dequeue() + ", ");
            }
            sb.Remove(sb.Length - 2, 2);
            string result = sb.ToString();
            return result + " }";
        }

        public static string FromJSON(string input)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("91010000");
            input = input.TrimStart('{');
            input = input.TrimEnd('}');
            input = input.Trim();
            string[] arr = input.Split(", ");
            byte[] bytes = BitConverter.GetBytes((uint)arr.Length);
            for (int i = 0; i < bytes.Length; i++)
            {
                sb.Append(bytes[i].ToString("X2"));
            }

            Queue<string> keys = new Queue<string>(arr.Length);
            Queue<string> values = new Queue<string>(arr.Length);

            for (int i = 0; i < arr.Length; i++)
            {
                StringBuilder keysb = new StringBuilder();
                StringBuilder valsb = new StringBuilder();
                keysb.Append("010000000000000000000000");
                string[] dict = arr[i].Split(": ");
                bytes = BitConverter.GetBytes((uint)dict[0].Trim('\"').Length);
                for (int j = 0; j < bytes.Length; j++)
                {
                    keysb.Append(bytes[j].ToString("X2"));
                }
                keysb.Append(GMSWeird.JSONtoGMSstring(dict[0].Trim('\"')));
                if (dict[1].Contains("\""))
                {
                    valsb.Append("010000000000000000000000");
                    bytes = BitConverter.GetBytes((uint)dict[1].Trim('\"').Length);
                    for (int j = 0; j < bytes.Length; j++)
                    {
                        valsb.Append(bytes[j].ToString("X2"));
                    }
                    valsb.Append(GMSWeird.JSONtoGMSstring(dict[1].Trim('\"')));
                }
                else
                {
                    valsb.Append("00000000" + GMSDSMaps.JSONtoGMSdouble(dict[1]) + "00000000");
                }
                keys.Enqueue(keysb.ToString());
                values.Enqueue(valsb.ToString());
            }

            for (int i = 0; i < arr.Length; i++)
            {
                sb.Append(keys.Dequeue());
            }
            for (int i = 0; i < arr.Length; i++)
            {
                sb.Append(values.Dequeue());
            }
            return sb.ToString();
        }
    }
    class GMSDSMaps
    {
        public static string ToJSON(string input)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(input.Substring(0, 8) + "{ ");
            input = input.Remove(0, 16);
            while (input.Length > 0)
            {
                input = input.Remove(0, 8);
                int keylen = ComputeStringLength(input.Substring(0, 8));
                input = input.Remove(0, 8);
                sb.Append("\"" + GMSWeird.GMSstringtoJSON(input.Substring(0, 2 * keylen)) + "\": ");
                input = input.Remove(0, keylen * 2);
                switch (input.Substring(0, 8))
                {
                    case "00000000":
                        sb.Append(GMSdoubletoJSON(input.Substring(8, 16)));
                        input = input.Remove(0, 24);
                        break;
                    case "0D000000":
                        sb.Append((GMSdoubletoJSON(input.Substring(8, 16)) == "1.000000") ? "true" : "false");
                        input.Remove(0, 24);
                        break;
                    case "01000000":
                        int stringlen = 2 * ComputeStringLength(input.Substring(8, 8));
                        string str = GMSWeird.GMSstringtoJSON(input.Substring(16, stringlen));
                        sb.Append("\"" + str + "\"");
                        input = input.Remove(0, 16 + stringlen);
                        break;
                    default:
                        throw new InvalidDataException("Unknown datatype, please go bug the dev.");
                }
                sb.Append(", ");
            }
            sb.Remove(sb.Length - 2, 2);
            string result = sb.ToString();
            return result + " }";
        }

        public static string FromJSON(string input)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(input.Substring(0, input.IndexOf('{')));
            input = input.Remove(0, input.IndexOf('{'));
            input = input.TrimStart('{');
            input = input.TrimEnd('}');
            input = input.Trim();
            string[] arr = input.Split(", ");
            byte[] bytes = BitConverter.GetBytes((uint)arr.Length);
            for (int i = 0; i < bytes.Length; i++)
            {
                sb.Append(bytes[i].ToString("X2"));
            }

            for (int i = 0; i < arr.Length; i++)
            {
                sb.Append("01000000");
                string[] dict = arr[i].Split(": ");
                bytes = BitConverter.GetBytes((uint)dict[0].Trim('\"').Length);
                for (int j = 0; j < bytes.Length; j++)
                {
                    sb.Append(bytes[j].ToString("X2"));
                }
                sb.Append(GMSWeird.JSONtoGMSstring(dict[0].Trim('\"')));
                if (dict[1].Contains("\""))
                {
                    sb.Append("01000000");
                    bytes = BitConverter.GetBytes((uint)dict[1].Trim('\"').Length);
                    for (int j = 0; j < bytes.Length; j++)
                    {
                        sb.Append(bytes[j].ToString("X2"));
                    }
                    sb.Append(GMSWeird.JSONtoGMSstring(dict[1].Trim('\"')));
                }
                else if (dict[1] == "true" || dict[1] == "false")
                {
                    sb.Append("0D000000" + JSONtoGMSdouble((dict[1] == "true") ? "1" : "0"));
                }
                else
                {
                    sb.Append("00000000" + JSONtoGMSdouble(dict[1]));
                }
            }
            return sb.ToString();
        }

        public static int ComputeStringLength(string input)
        {
            byte[] keylenbytes = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                keylenbytes[i] = Convert.ToByte(input.Substring(2 * i, 2), 16);
            }
            return BitConverter.ToInt32(keylenbytes);
        }
        public static string GMSdoubletoJSON(string value)
        {
            byte[] doublebytes = new byte[8];
            for (int i = 0; 2 * i < value.Length; i++)
            {
                doublebytes[i] = Convert.ToByte(value.Substring(2 * i, 2), 16);
            }
            double result = BitConverter.ToDouble(doublebytes, 0);
            return result.ToString().Contains('.') ? result.ToString() : result.ToString() + ".000000";
        }

        public static string JSONtoGMSdouble(string value)
        {
            double number = double.Parse(value);
            byte[] bytes = BitConverter.GetBytes(number);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                sb.Append(bytes[i].ToString("X2"));
            }
            string result = sb.ToString();
            return result;
        }
    }
}