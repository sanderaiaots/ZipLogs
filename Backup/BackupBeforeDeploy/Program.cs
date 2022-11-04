using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Ionic.Zip;

namespace BackupBeforeDeploy {
	internal class Program {
		private static StringBuilder log = new StringBuilder();

		private static void Main(string[] args) {
			DirectoryInfo backupSaveLocation = new DirectoryInfo(ConfigurationManager.AppSettings["BackupSaveLocation"]);
			if (!backupSaveLocation.Exists) {
				throw new Exception("BackupSaveLocation " + backupSaveLocation + " is not excist");
			}
			
			if (ConfigurationManager.AppSettings["AddDateFolder"] == "true") {
				backupSaveLocation = new DirectoryInfo(backupSaveLocation.FullName + "\\" + DateTime.Today.ToString("yyyyMMdd"));
				if (!backupSaveLocation.Exists) {
					backupSaveLocation.Create();
				}
			}

			Console.WriteLine("Staring backup. Please check if you have BackupSaveLocation and FolderToBackupN configured in app.config\r\n");
			List<DirectoryInfo> dirsToBackup = ConfigurationManager.AppSettings.AllKeys.Where(w => w.StartsWith("FolderToBackup")).Select(s => new DirectoryInfo(ConfigurationManager.AppSettings[s])).Where(w => w.Exists).ToList();
			foreach (var directoryInfo in dirsToBackup) {
				string zipFileName;
				int idx = 0;
				do {
					zipFileName = backupSaveLocation.FullName + "\\" + directoryInfo.Name + "_backup_" + DateTime.Today.ToString("yyyyMMdd") + (idx > 0 ? "_" + idx : "") + ".zip";
					idx++;
				} while (File.Exists(zipFileName));
				
				Log("Start adding {0} to {1}", directoryInfo.FullName, zipFileName);
				Stopwatch sw = Stopwatch.StartNew();
				using (ZipFile zip = new ZipFile(zipFileName)) {
					zip.AddDirectory(directoryInfo.FullName);
					zip.Save();
				}
				Log("End adding {0} to {1}, elapsed {2}ms", directoryInfo.FullName, zipFileName, sw.ElapsedMilliseconds);
			}
		}

		private static void Log(string s, params object[] args) {
			Console.WriteLine(s, args);
			log.AppendFormat(s, args);
			log.AppendLine();
		}
	}
}