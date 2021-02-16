using Microsoft.Win32;
using UndertaleModLib;
using System.Diagnostics;
using System.IO;

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