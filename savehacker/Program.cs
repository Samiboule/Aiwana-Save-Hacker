using System;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Security.Cryptography;
using UndertaleModLib;

namespace savehacker
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Write("Please drag the exe/win file of the game you want to save hack or your edited data txt file on this window: ");
            string choice = Console.ReadLine();
            string inDir;
            choice = choice.Trim('"');
            switch (choice.Substring(choice.LastIndexOf("."))) {
                case ".exe":
                case ".win":
                    inDir = choice.Remove(choice.LastIndexOf('\\'));
                    ReadSaveFile(choice, inDir);
                    break;
                case ".txt":
                    inDir = choice.Remove(choice.LastIndexOf('\\'));
                    WriteSaveFile(choice, inDir);
                    break;
                default:
                    Console.WriteLine("Type 1 or 2 please :( the guy who programmed me is too lazy to work with anything else.");
                    break;
            }
            Console.WriteLine("Press Enter to quit the program.");
            Console.ReadLine();
            }

        static void ReadSaveFile(string inFile, string inDir)
        {
            //unzip/decompile game
            string[] array = new string[2];
            if (inFile.Contains(".exe"))
            {
                decompilation.theclan.unzip(inFile, inDir);
                array = decompilation.theclan.decomp(inDir + @"\temp\data.win");
            } else if (inFile.Contains(".win"))
            {
                array = decompilation.theclan.decomp(inFile);
            } else
            {
                throw new ArgumentException("Please only .exe or .win files.");
            }
            
            string salt = array[0];

            string path = Directory.GetParent(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)).FullName;
            if (Environment.OSVersion.Version.Major >= 6)
            {
                path = Directory.GetParent(path).ToString();
            }
            string savedir = path + @"\AppData\Local\" + array[1] + @"\Data";

            //delete temp folder
            if (Directory.Exists(inDir + @"\temp"))
            {
                Directory.Delete(inDir + @"\temp", true);
            }

            //read file
            string[] files = Directory.GetFiles(savedir);
            for (int i = 0; i < files.Length; i++)
            {
                Console.WriteLine((i+1).ToString() + ": " + files[i] + "\n");
            }
            Console.Write("Type the number of the save file(s) you want to hack and press Enter (eg. 1,2,3): ");
            string[] inputs = Console.ReadLine().Split(",");
            for (int i = 0; i < inputs.Length; i++)
            {
                string choicenumber = inputs[i];
                string choice = files[int.Parse(choicenumber) - 1];
                StreamReader savefile = new StreamReader(choice);
                string line = savefile.ReadLine();
                savefile.Close();

                line = FromBase64(line);

                //write to file
                StreamWriter outputfile = File.CreateText(inDir + @"\data" + choicenumber + ".txt");
                if (Regex.Match(line, "^([13]:[0-9A-F]+:[0-9A-F]*,)+([13]:[0-9A-F]+:[0-9A-F]*)$").Success)
                {
                    line = JSONConversion.Aconverter.ToJSON(line);
                    outputfile.WriteLine("version = \"A\"");
                } else if (Regex.Match(line, "^[0-9A-F]+$").Success)
                {
                    line = JSONConversion.Bconverter.ToJSON(line);
                    outputfile.WriteLine("version = \"B\"");
                } else
                {
                    outputfile.WriteLine("version = \"C\"");
                }
                outputfile.WriteLine(line);
                outputfile.WriteLine("seasoning=\"" + salt + "\"");
                outputfile.WriteLine("path=\"" + choice + "\"");
                outputfile.WriteLine("");
                outputfile.Write("Do not mess around with things unless you understand what you're doing.");
                outputfile.Close();
            }
            Console.WriteLine("Now edit the newly created data file(s) in the game's directory.");
        }

        static void WriteSaveFile(string inFile, string inDir)
        {
            //read data file
            StreamReader data = new StreamReader(inFile);
            string version = data.ReadLine();
            version = version.Substring(version.IndexOf('"') + 1);
            version = version.Remove(version.IndexOf('"'));
            string json = data.ReadLine();
            string salt = data.ReadLine();
            string savefile = data.ReadLine();
            salt = salt.Substring(salt.IndexOf('"') + 1);
            salt = salt.Remove(salt.IndexOf('"'));
            savefile = savefile.Substring(savefile.IndexOf('"') + 1);
            savefile = savefile.Remove(savefile.IndexOf('"'));
            string dir = savefile.Remove(savefile.LastIndexOf('\\'));
            data.Close();
            string result = ToBase64(GetMd5HashAndRecompose(version, json, salt));

            //write to new savefile
            File.Move(savefile, savefile + ".bak");
            StreamWriter outputfile = File.CreateText(savefile);
            outputfile.Write(result);
            outputfile.Close();
            Process.Start("explorer.exe", dir);
        }

        static string FromBase64(string input)
        {
            byte[] data = Convert.FromBase64String(input);
            return Encoding.UTF8.GetString(data);
        }

        static string ToBase64(string input)
        {
            byte[] plainTextBytes = Encoding.UTF8.GetBytes(input);
            return Convert.ToBase64String(plainTextBytes);
        }

        static string GetMd5HashAndRecompose(string version, string input, string salt)
        {
            //delete md5 entry from json
            int idx = input.IndexOf(", \"mapMd5\": \"");
            string mapmd5less = input.Remove(idx, 46); // 13 + 32 + 1
            if (version == "A")
            {
                mapmd5less = JSONConversion.Aconverter.FromJSON(mapmd5less);
            } else if (version == "B")
            {
                mapmd5less = JSONConversion.Bconverter.FromJSON(mapmd5less);
            }

            //append salt
            mapmd5less = mapmd5less + salt;

            //utf-16le stuff
            Encoding u16LE = Encoding.Unicode;
            byte[] bytes = u16LE.GetBytes(mapmd5less);

            //md5 hash
            MD5 md5 = MD5.Create();
            byte[] hashBytes = md5.ComputeHash(bytes);

            //to hex string
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hashBytes.Length; i++)
            {
                sb.Append(hashBytes[i].ToString("x2"));
            }
            string md5result = sb.ToString();

            //compose new md5 hash and save data json
            string result = input.Remove(idx + 13, 32);
            result = result.Insert(idx + 13, md5result);
            if (version == "A")
            {
                result = JSONConversion.Aconverter.FromJSON(result);
            } else if (version == "B")
            {
                result = JSONConversion.Bconverter.FromJSON(result);
            }
            return result;
        }
    }
}

namespace decompilation
{
    class theclan
    {
        public static void unzip(string inFile, string inDir)
        {
            string extractorPath = GetPathForExe("7zFM.exe");
            extractorPath = extractorPath + "7z.exe";
            string command = "/C \"\"" + extractorPath + "\" e \"" + inFile + "\" -o\"" + inDir + "\\temp\" *.win -r\"";
            Process cmd = Process.Start("CMD.exe", command);
            cmd.WaitForExit();
        }

        public static string[] decomp(string inFile)
        {
            FileStream file = new FileStream(inFile, FileMode.Open, FileAccess.Read);
            UndertaleData data = UndertaleIO.Read(file);
            var scrSetGlobalOptions = data.Scripts.ByName("scrSetGlobalOptions");
            var vars = scrSetGlobalOptions.Code.FindReferencedVars();
            var locals = scrSetGlobalOptions.Code.Instructions;
            string instruction = "";
            for (int i = 0; i < locals.Count; i++)
            {
                if (locals[i].ToString().Contains("md5StrAdd"))
                {
                    instruction = locals[i - 1].ToString();
                    instruction = instruction.Substring(instruction.IndexOf('"') + 1);
                    instruction = instruction.Remove(instruction.IndexOf('"'));
                    break;
                }
            }
            string name = data.GeneralInfo.Name.Content;
            file.Close();
            string[] array = { instruction, name };
            return array;
        }

        private const string keyBase = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";
        public static string GetPathForExe(string fileName)
        {
            RegistryKey localMachine = Registry.LocalMachine;
            RegistryKey fileKey = localMachine.OpenSubKey(string.Format(@"{0}\{1}", keyBase, fileName));
            object result = null;
            if (fileKey != null)
            {
                result = fileKey.GetValue("Path");
                fileKey.Close();
            }


            return (string)result;
        }
    }
}

namespace JSONConversion
{
    class Aconverter
    {
        public static string ToJSON(string input)
        {
            string[] entries = input.Split(",");
            string[,] splitentries = new string[entries.Length,3];
            for (int i = 0; i < entries.Length; i++)
            {
                string[] arr = entries[i].Split(':');
                splitentries[i,0] = arr[0];
                splitentries[i, 1] = arr[1];
                splitentries[i, 2] = arr[2];
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("{ ");

            for (int i = 0; i < entries.Length; i++)
            {
                sb.Append("\"" + GMSvarnametostring(splitentries[i, 1]) + "\": ");
                switch (splitentries[i,0])
                {
                    case "1":
                        sb.Append(GMSdoubletostring(splitentries[i, 2]) + ", ");
                        break;
                    case "3":
                        sb.Append("\"" + GMSvarnametostring(splitentries[i, 2]) + "\", ");
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
                    sb.Append("3:" + stringtoGMSvarname(dict[0].Trim('\"')) + ":" + stringtoGMSvarname(dict[1]).Trim('\"') + ",");
                } else if (Regex.Match(dict[1], "(.:[0-9A-F]+:[0-9A-F]*)").Success)
                {
                    sb.Append(dict[1] + ",");
                } else
                {
                    sb.Append("1:" + stringtoGMSvarname(dict[0].Trim('\"')) + ":" + stringtoGMSdouble(dict[1]) + ",");
                }
            }
            return sb.ToString().TrimEnd(',');
        }

        public static string GMSdoubletostring(string value)
        {
            while (value.Length < 16)
            {
                value = value + "00";
            }
            byte[] doublebytes = new byte[8];
            for (int i = 0; 2*i < value.Length; i++)
            {
                doublebytes[7-i] = Convert.ToByte(value.Substring(2*i,2), 16);
            }
            double result = BitConverter.ToDouble(doublebytes, 0);
            return result.ToString().Contains('.') ? result.ToString() : result.ToString() + ".000000";
        }
        
        public static string stringtoGMSdouble(string value)
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
            } else
            {
                return result;
            }
        }

        public static string GMSvarnametostring(string hex)
        {
            if (hex.Length % 2 == 1)
                throw new Exception("The binary key cannot have an odd number of digits");

            byte[] arr = new byte[hex.Length >> 1];

            for (int i = 0; i < hex.Length >> 1; ++i)
            {
                arr[i] = (byte)((GetHexVal(hex[i << 1]) << 4) + (GetHexVal(hex[(i << 1) + 1])));
            }

            return Encoding.UTF8.GetString(arr);
        }

        public static string stringtoGMSvarname(string varname)
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

    class Bconverter
    {
        public static string ToJSON(string input)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(input.Substring(0, 8) + "{ ");
            input = input.Remove(0, 16);
            while (input.Length > 0)
            {
                input = input.Remove(0, 8);
                int keylen = ComputeLength(input.Substring(0, 8));
                input = input.Remove(0, 8);
                sb.Append("\"" + Aconverter.GMSvarnametostring(input.Substring(0, 2 * keylen)) + "\": ");
                input = input.Remove(0, keylen * 2);
                switch (input.Substring(0, 8))
                {
                    case "00000000":
                        sb.Append(GMSdoubletostring(input.Substring(8, 16)));
                        input = input.Remove(0, 24);
                        break;
                    case "0D000000":
                        sb.Append((GMSdoubletostring(input.Substring(8, 16)) == "1.000000") ? "true" : "false");
                        input.Remove(0, 24);
                        break;
                    case "01000000":
                        int stringlen = 2 * ComputeLength(input.Substring(8, 8));
                        string str = Aconverter.GMSvarnametostring(input.Substring(16, stringlen));
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
                sb.Append(Aconverter.stringtoGMSvarname(dict[0].Trim('\"')));
                if (dict[1].Contains("\""))
                {
                    sb.Append("01000000");
                    bytes = BitConverter.GetBytes((uint)dict[1].Trim('\"').Length);
                    for (int j = 0; j < bytes.Length; j++)
                    {
                        sb.Append(bytes[j].ToString("X2"));
                    }
                    sb.Append(Aconverter.stringtoGMSvarname(dict[1].Trim('\"')));
                } else if (dict[1] == "true" || dict[1] == "false")
                {
                    sb.Append("0D000000" + stringtoGMSdouble((dict[1] == "true") ? "1" : "0"));
                } else
                {
                    sb.Append("00000000" + stringtoGMSdouble(dict[1]));
                }
            }
            return sb.ToString();
        }

        public static int ComputeLength(string input)
        {
            byte[] keylenbytes = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                keylenbytes[i] = Convert.ToByte(input.Substring(2 * i, 2), 16);
            }
            return BitConverter.ToInt32(keylenbytes);
        }
        public static string GMSdoubletostring(string value)
        {
            byte[] doublebytes = new byte[8];
            for (int i = 0; 2 * i < value.Length; i++)
            {
                doublebytes[i] = Convert.ToByte(value.Substring(2 * i, 2), 16);
            }
            double result = BitConverter.ToDouble(doublebytes, 0);
            return result.ToString().Contains('.') ? result.ToString() : result.ToString() + ".000000";
        }

        public static string stringtoGMSdouble(string value)
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
