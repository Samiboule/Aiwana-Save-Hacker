using System;
using System.Text.RegularExpressions;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Security.Cryptography;

namespace savehacker
{
    class Program
    {
        static void Main(string[] args) //TODO IF DATA.WIN IN DIRECTORY DON'T UNZIP AND JUST USE THAT DATA.WIN
        {
            string choice;
            if (args.Length == 1)
            {
                choice = args[0];
            }
            else
            {
                Console.Write("Please drag the exe/win file of the game you want to save hack or your edited data txt file on this window: ");
                choice = Console.ReadLine();
                choice = choice.Trim('"');
            }
            string inDir = choice.Remove(choice.LastIndexOf('\\'));
            switch (choice.Substring(choice.LastIndexOf("."))) {
                case ".exe":
                case ".win":
                    ReadSaveFile(choice, inDir);
                    break;
                case ".txt":
                    WriteSaveFile(choice, inDir);
                    break;
                default:
                    Console.WriteLine("Type 1 or 2 please :( the guy who programmed me is too lazy to work with anything else.");
                    break;
            }
            Console.WriteLine("Press Enter to quit the program.");
            Console.ReadLine();
        }

        static void ReadSaveFile(string inFile, string inDir) //ALSO TODO ADD SUPPORT FOR K2W
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
                    line = JSONConversion.GMSWeird.ToJSON(line);
                    outputfile.WriteLine("version = \"A\"");
                } else if (Regex.Match(line, "^[0-9A-F]+$").Success)
                {
                    line = JSONConversion.GMSDSMaps.ToJSON(line);
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
                mapmd5less = JSONConversion.GMSWeird.FromJSON(mapmd5less);
            } else if (version == "B")
            {
                mapmd5less = JSONConversion.GMSDSMaps.FromJSON(mapmd5less);
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
                result = JSONConversion.GMSWeird.FromJSON(result);
            } else if (version == "B")
            {
                result = JSONConversion.GMSDSMaps.FromJSON(result);
            }
            return result;
        }
    }
}