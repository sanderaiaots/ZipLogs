using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Ionic.Zip;

namespace ZipLogs {
	public class ZipAdder : IDisposable {
		Dictionary<string, ZipFile> files = new Dictionary<string, ZipFile>();
		private ZipFile nonMonthZipFile;
		bool isMonthBased;
		bool is64Bit;
		string zipdir;
		string zipname;
		string zipnamedateformat;

		public ZipAdder(string zipdir, string zipname, string zipnamedateformat, bool isMonthBased, bool is64Bit) {
			this.zipdir = zipdir;
			this.zipname = zipname;
			this.zipnamedateformat = zipnamedateformat;
			this.isMonthBased = isMonthBased;
			this.is64Bit = is64Bit;
			if(isMonthBased) {
				Console.WriteLine("Is month based");
			}
		}
		public ZipFile GetZipFileToAdd(FileInfo file) {
			if (isMonthBased) {
				string key = file.CreationTime.ToString("yyyyMM");
				ZipFile zipFile;
				if (!files.TryGetValue(key, out zipFile)) {
					string zipFileName = (zipdir ?? ".") + "\\" + (zipname + "_" + key + ".zip");
					zipFile = new ZipFile(zipFileName);
					Console.WriteLine("Created: " + zipFileName);
					if (is64Bit) {
						zipFile.UseZip64WhenSaving = Zip64Option.AsNecessary;
						Console.WriteLine("set UseZip64WhenSaving=" + zipFile.UseZip64WhenSaving + " - " + zipFileName);
					}
					files.Add(key, zipFile);
				}
				return zipFile;
			}
			else {
				if (nonMonthZipFile == null) {
					string zipFileName = (zipdir ?? ".") + "\\" + (zipname ?? DateTime.Now.ToString(zipnamedateformat) + ".zip");
					nonMonthZipFile = new ZipFile(zipFileName);
					if (is64Bit) {
						nonMonthZipFile.UseZip64WhenSaving = Zip64Option.AsNecessary;
						Console.WriteLine("set UseZip64WhenSaving=" + nonMonthZipFile.UseZip64WhenSaving);
					}
				}
				return nonMonthZipFile;
			}
		}
		public static List<FileSystemInfo> filesToDelete = new List<FileSystemInfo>();

		public void Save() {
			foreach (ZipFile z in files.Values) {
				Console.WriteLine("Saving: " + z.Name);
				Stopwatch sw = Stopwatch.StartNew();
				z.Save();
				Console.WriteLine("Saved: " + z.Name + ", elapsed " + sw.ElapsedMilliseconds + "ms");
			}
			if (nonMonthZipFile != null) {
				Console.WriteLine("Saving: " + nonMonthZipFile.Name);
				nonMonthZipFile.Save();
			}
		}

		public void Dispose() {
			foreach (ZipFile z in files.Values) {
				z.Dispose();
			}
			if (nonMonthZipFile != null) {
				nonMonthZipFile.Dispose();
			}
		}
	}
}
