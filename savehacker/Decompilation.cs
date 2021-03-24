using Microsoft.Win32;
using UndertaleModLib;
using System.Diagnostics;
using System.IO;

namespace decompilation
{
    class Theclan
    {
        public static void Unzip(string inFile, string inDir)
        {
            string extractorPath = GetPathForExe("7zFM.exe");
            extractorPath += "7z.exe";

            //Check if data.win exists in the exe, if not the game may be YYC'd
            Process firstcmd = new Process();
            ProcessStartInfo info = new ProcessStartInfo(extractorPath);
            info.Arguments = "l \"" + inFile + "\"";
            info.RedirectStandardOutput = true;
            info.UseShellExecute = false;
            firstcmd.StartInfo = info;
            firstcmd.Start();
            StreamReader reader = firstcmd.StandardOutput;
            string output = reader.ReadToEnd();
            firstcmd.WaitForExit();

            if (output.Contains("data.win"))
            {
                string command = "/C \"\"" + extractorPath + "\" e \"" + inFile + "\" -o\"" + inDir + "\\temp\" *.win -r\"";
                Process secondcmd = Process.Start("CMD.exe", command);
                secondcmd.WaitForExit();
            } else
            {
                throw new InvalidDataException("Could not find data.win file in the exe. Was this game compiled with YYC?");
            }
        }

        public static string[] Decompile(string inFile)
        {
            FileStream file = new FileStream(inFile, FileMode.Open, FileAccess.Read);
            UndertaleData data = UndertaleIO.Read(file);
            var scrSetGlobalOptions = data.Scripts.ByName("scrSetGlobalOptions");
            var locals = scrSetGlobalOptions.Code.Instructions;
            string instruction = "";
            for (int i = 0; i < locals.Count; i++)
            {
                if (locals[i].ToString().Contains("md5StrAdd"))
                {
                    instruction = locals[i - 1].ToString();
                    instruction = instruction[(instruction.IndexOf('"') + 1)..];
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