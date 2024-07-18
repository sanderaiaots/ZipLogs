using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace DeleteLogs {
	public class Program {

		private static Dictionary<string, string> parameters = new Dictionary<string, string>();
		private static StringBuilder log = new StringBuilder();
		private static List<FileSystemInfo> filesToDelete = new List<FileSystemInfo>(); 

		private static int howOldFilesToDelete;

		static void Main(string[] args) {
            Console.ReadLine();
			Log("Started: " + DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss"));
			parameters = CreateParameters(args);
			if(GetParam("h")!=null || GetParam("help")!=null) {
				PrintHelp();
				return;
			}

			howOldFilesToDelete = int.Parse(GetParam("howold") ?? "0");
			string pathToRead = GetParam("source");
			if (String.IsNullOrEmpty(pathToRead)) {
				PrintHelp();
				return;                    
			}
			DirectoryInfo dirInfo = new DirectoryInfo(pathToRead);
			ScanFolder(dirInfo, "");
			if (filesToDelete.Count > 0) {
				Stopwatch sw = Stopwatch.StartNew();
	
				foreach (FileSystemInfo file in filesToDelete) {
					try {
						file.Delete();
						Log("deleted " + file.FullName);
					}
					catch (Exception ex) {
						Log("unable to delete " + file.FullName + ", ex=" + ex);
					}
				}
				Log("Deleting elapsed " + sw.ElapsedMilliseconds + "ms");
			}
			else {
				Log("No files to delete");
			}
			Log("Finished: "+DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss"));

		}

		private static void PrintHelp() {
			Log("Kasuta: dotnet run -source c:\\temp -howold 7");
			Log("-source : Milline kataloog kustutada");
			Log("-howold : kui vanad failid päevades kustutada");
			Log("-help / -h : prindib selle sama asja");
		}

		private static Dictionary<string, string> CreateParameters(string[] args) {
			Dictionary<string, string> retParams = new Dictionary<string, string>();
			for (int i = 0; i < args.Length; i++) {
				if ((args[i].StartsWith("-") || args[i].StartsWith("/")) && (i + 1) < args.Length && !(args[i + 1].StartsWith("-"))) {
					retParams.Add(args[i].ToLower().Substring(1), args[i + 1]);
					i++;
				}
				else if((args[i].StartsWith("-") || args[i].StartsWith("/")) && ((i + 1) >= args.Length || args[i + 1].StartsWith("-") || args[i + 1].StartsWith("/")) ) {
					retParams.Add(args[i].ToLower().Substring(1), "");
				}
			}
			return retParams;
		}

		private static void Log(string s, params object[] args) {
			string date = DateTime.Now.ToString("yyyyMMdd HHmmss.fff");
			Console.WriteLine(date + ">" + s, args);
			log.AppendFormat(s, args);
			log.AppendLine();
		}

		private static void ScanFolder( DirectoryInfo pathToRead, string startDir) {
			foreach (FileInfo file in pathToRead.GetFiles()) {
				int daysOld = (DateTime.Now - file.LastWriteTime).Days;
				if ((DateTime.Now - file.LastWriteTime).Days >= howOldFilesToDelete) {
					filesToDelete.Add(file);
				}
				else {
					Log("Not deleted: " + file + " file is " + daysOld+"d old < "+howOldFilesToDelete+"d");
				}
			}
			foreach (DirectoryInfo dir in pathToRead.GetDirectories()) {
				if ((DateTime.Now - dir.LastWriteTime).Days >= howOldFilesToDelete) {
					ScanFolder(dir, startDir + dir.Name + "\\");
					if ((DateTime.Now - dir.LastWriteTime).Days >= howOldFilesToDelete) {
						try {
							filesToDelete.Add(dir);
						}
						catch(Exception ex) {
							Log("Ei saanus kustutada kataloogi: "+dir.FullName);
						}
					}
				}
			}
		}

		private static string GetParam(string name) {
			string ret;
			if(!parameters.TryGetValue(name, out ret)) {
				ret = null;
			}
			return ret;
		}

	}
}
