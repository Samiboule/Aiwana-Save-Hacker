using System;
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
            Console.WriteLine("1. Read save file for editing");
            Console.WriteLine("2. Write edited data to new save file");
            Console.Write("Type 1 or 2: ");
            string choice = Console.ReadLine();
            string inFile;
            string inDir;
            switch (choice) {
                case "1":
                    Console.Write("Drag the game to savehack on this window and press Enter: ");
                    inFile = Console.ReadLine();
                    inFile = inFile.Trim('"');
                    inDir = inFile.Remove(inFile.LastIndexOf('\\'));
                    ReadSaveFile(inFile, inDir);
                    break;
                case "2":
                    Console.Write("Drag the data text file on this window and press Enter: ");
                    inFile = Console.ReadLine();
                    inFile = inFile.Trim('"');
                    inDir = inFile.Remove(inFile.LastIndexOf('\\'));
                    WriteSaveFile(inFile, inDir);
                    break;
                default:
                    Console.WriteLine("Type 1 or 2 please :( the guy who programmed me is too lazy to work with anything else.");
                    break;
            }
            Console.WriteLine("Press Enter to quit the program:");
            Console.ReadLine();
        }

        static void ReadSaveFile(string inFile, string inDir)
        {
            //unzip game
            decompilation.theclan.unzip(inFile, inDir);

            //decompile
            string[] array = decompilation.theclan.decomp(inDir + @"\temp\data.win");
            string salt = array[0];

            string path = Directory.GetParent(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)).FullName;
            if (Environment.OSVersion.Version.Major >= 6)
            {
                path = Directory.GetParent(path).ToString();
            }
            string savedir = path + @"\AppData\Local\" + array[1] + @"\Data";

            //delete temp folder
            Directory.Delete(inDir + @"\temp", true);

            //read file
            string[] files = Directory.GetFiles(savedir);
            for (int i = 0; i < files.Length; i++)
            {
                Console.WriteLine((i+1).ToString() + ": " + files[i] + "\n");
            }
            Console.WriteLine("Type the number of the save file you want to hack and press Enter: ");
            string choicenumber = Console.ReadLine();
            string choice = files[int.Parse(choicenumber) - 1];
            StreamReader savefile = new StreamReader(choice);
            string line = savefile.ReadLine();
            savefile.Close();

            line = FromBase64(line);
            
            //write to file
            StreamWriter outputfile = File.CreateText(inDir + @"\data" + choicenumber + ".txt");
            outputfile.WriteLine(line);
            outputfile.WriteLine("seasoning=\"" + salt + "\"");
            outputfile.WriteLine("path=\"" + choice + "\"");
            outputfile.WriteLine("");
            outputfile.Write("Do not mess around with things unless you understand what you're doing.");
            outputfile.Close();
        }

        static void WriteSaveFile(string inFile, string inDir)
        {
            //read data file
            StreamReader data = new StreamReader(inFile);
            string json = data.ReadLine();
            string salt = data.ReadLine();
            string savefile = data.ReadLine();
            salt = salt.Substring(salt.IndexOf('"') + 1);
            salt = salt.Remove(salt.IndexOf('"'));
            savefile = savefile.Substring(savefile.IndexOf('"') + 1);
            savefile = savefile.Remove(savefile.IndexOf('"'));
            string dir = savefile.Remove(savefile.LastIndexOf('\\'));
            data.Close();
            string result = ToBase64(GetMd5HashAndRecompose(json, salt));

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

        static string GetMd5HashAndRecompose(string input, string salt)
        {
            //delete md5 entry from json
            int idx = input.IndexOf(", \"mapMd5\": \"");
            string mapmd5less = input.Remove(idx, 46); // 13 + 32 + 1

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
            return result.Insert(idx + 13, md5result);
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
