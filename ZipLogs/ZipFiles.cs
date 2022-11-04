﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.IO.Packaging;
using System.Text;
using System.Threading;
using Ionic.Zip;
using Ionic.Zlib;

namespace ZipLogs {
	internal class ZipFiles {
		private static bool is64BitZip = false;
		private static bool lookIntoDir;
		private static int howOldFilesToDelete;
		private static bool deleteFilesAfterZip = bool.Parse(ConfigurationManager.AppSettings["DeleteFilesAfterZip"] ?? "false");
		private static Dictionary<string, string> parameters = new Dictionary<string, string>();
		private static StringBuilder log = new StringBuilder();
		private static DateTime? minFileDate = null;
		private static DateTime? maxFileDate = null;
		private static bool isAnyFileSaved = false;
		private static int addedFileCount = 0;
		private static long addedByteCount = 0;
		private static Stopwatch addedSw = new Stopwatch();
		private static List<FileSystemInfo> filesToDelete = new List<FileSystemInfo>(); 

		private static string GetParam(string name) {
			string ret;
			if(!parameters.TryGetValue(name, out ret)) {
				ret = null;
			}
			return ret;
		}

		private static void Main(string[] args) {
			Log("Started: " + DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss"));
			parameters = CreateParameters(args);

			if(GetParam("h")!=null || GetParam("help")!=null) {
				PrintHelp();
				return;
			}

			is64BitZip = GetParam("int64") != null;

			howOldFilesToDelete = int.Parse(GetParam("howold") ?? "0");
			deleteFilesAfterZip = GetParam("d") != null || GetParam("delete")!=null;
			lookIntoDir = GetParam("dir") != null;

			string pathToRead = GetParam("source") ?? ".";
			if (GetParam("zipname") == null && GetParam("zipnamedateformat") == null) {
				Log("Üks parameetritest: -zipName või -zipNameDateFormat peab olema määratud");
				return;
			}
			string zipFileName = (GetParam("zipdir") ?? ".") + "\\" + (GetParam("zipname") ?? DateTime.Now.ToString(GetParam("zipnamedateformat")) + ".zip");
			Log("Saving zip to: " + zipFileName);
			DirectoryInfo dirInfo = new DirectoryInfo(pathToRead);
			using (ZipAdder zipFile = new ZipAdder(GetParam("zipdir"), GetParam("zipname"), GetParam("zipnamedateformat"), GetParam("monthly")!=null, is64BitZip)) {
				
				addedSw.Start();
				PackFolder(zipFile, dirInfo, "");
				if (addedFileCount > 0) {
					PrintWriteStatistics();
					Stopwatch sw = Stopwatch.StartNew();
					zipFile.Save();
					Log("Saved zipfile " + zipFileName + ", elapsed " + sw.ElapsedMilliseconds + "ms");
					PrintWriteStatistics();
					sw.Reset();
					sw.Start();
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
					Log("No files packed. Not saving zipfile: " + zipFileName);
				}
			}
			

			Log("Finished: "+DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss"));

			if (isAnyFileSaved && GetParam("addlogfile") != null) {
				if (GetParam("monthly") != null) {
					Log("Log file is not currenly implemented on Montly grouping");
				}
				else {
					AddLogFileToZip(zipFileName);
				}
			}
			if (isAnyFileSaved && GetParam("minmaxdate") != null && GetParam("monthly") == null) {
				FileInfo file = new FileInfo(zipFileName);
				string newName = GetParam("zipname") ?? DateTime.Now.ToString(GetParam("zipnamedateformat"));
				if (minFileDate != null) {
					newName += "_" + minFileDate.Value.ToString("yyyyMMdd");
				}
				if (maxFileDate != null) {
					newName += "-" + maxFileDate.Value.ToString("yyyyMMdd");
				}       
				newName += ".zip";
				newName = (GetParam("zipdir") ?? ".") + "\\" + newName;
				file.MoveTo(newName);
				Log("Added new name: " + newName);
			}

//			AddFileToZip("Output.zip", @"C:\Windows\Notepad.exe");
//			AddFileToZip("Output.zip", @"C:\Windows\System32\Calc.exe");
		}

		private static void PrintHelp() {
			Log("Kasuta: ZipLogs -source c:\\temp -zipName -howold 7 -delete");
			Log("-source : Milline kataloog pakkida, kui ei määra kasutatakse hetke kausta");
			Log("-zipName : millisesse faili pakkida, ei ole kohustuslik");
			Log("-zipNameDateFormat : kui ei määra -zipName määra kuupäeva formaat, ntx: yyyyMMdd");
			Log("-zipdir : kui ei määra siis . muidu kaust kuhu pakitud fail salvestatakse");
			Log("-howold : kui vanad failid päevades zip-i lisatakse");
			Log("-delete / -d : kas kustutada ka pakitud failid");
			Log("-dir : kas võta vanad failid ka kausta seest mida on hiljuti muudetud");
			Log("-addlogfile : lisa fail logist, mis programm on kirjutanud");
			Log("-minmaxdate : lisa pakitud failid min ja max kuupäe faili nimesse");
			Log("-monthly : adds files by created date to montlhly files, file name patter yyyyMM");
			Log("-int64 : kasuta 64bit zip file versiooni");
			Log("-help / -h : prindib selle sama asja");
		}

		public static void PrintWriteStatistics() {
			Log("Added " + addedFileCount + " files, " + (addedByteCount/(1024f*1024f)).ToString("0.00") + "MB, " + (addedFileCount*1000f/addedSw.ElapsedMilliseconds).ToString("0.0") + " files per second. Elapsed " + addedSw.ElapsedMilliseconds + "ms");
		}

		private static Dictionary<string, string> CreateParameters(string[] args) {
			Dictionary<string, string> retParams = new Dictionary<string, string>();
			for (int i = 0; i < args.Length; i++) {
				if ((args[i].StartsWith("-") || args[i].StartsWith("/")) && (i + 1) < args.Length && !(args[i + 1].StartsWith("-") || args[i + 1].StartsWith("/"))) {
					retParams.Add(args[i].ToLower().Substring(1), args[i + 1]);
					i++;
				}
				else if((args[i].StartsWith("-") || args[i].StartsWith("/")) && ((i + 1) >= args.Length || args[i + 1].StartsWith("-") || args[i + 1].StartsWith("/")) ) {
					retParams.Add(args[i].ToLower().Substring(1), "");
				}
			}
			return retParams;
		}

		private static void PackFolder(ZipAdder zipFile, DirectoryInfo pathToRead, string startDir) {
			foreach (FileInfo file in pathToRead.GetFiles()) {
				int daysOld = (DateTime.Now - file.LastWriteTime).Days;
				if ((DateTime.Now - file.LastWriteTime).Days >= howOldFilesToDelete) {
					AddFileToZipO(zipFile, startDir, file);
					if (deleteFilesAfterZip) {
						filesToDelete.Add(file);
						//file.Delete();
						//Log("Deleted: " + file);
					}
				}
				else {
					Log("Not deleted: " + file + " file is " + daysOld+"d old < "+howOldFilesToDelete+"d");
				}
			}
			foreach (DirectoryInfo dir in pathToRead.GetDirectories()) {
				if (lookIntoDir || (DateTime.Now - dir.LastWriteTime).Days >= howOldFilesToDelete) {
					PackFolder(zipFile, dir, startDir + dir.Name + "\\");
					if (deleteFilesAfterZip && (DateTime.Now - dir.LastWriteTime).Days >= howOldFilesToDelete) {
						try {
							filesToDelete.Add(dir);
							//dir.Delete();
							//Log("Deleted: "+dir);
						}
						catch(Exception ex) {
							Log("Ei saanus kustutada kataloogi: "+dir.FullName);
						}
					}
				}
			}
		}

		private const long BUFFER_SIZE = 4096;

		private static void AddFileToZipO(ZipAdder fileA, string dir, FileInfo fileToAdd) {
			ZipFile file = fileA.GetZipFileToAdd(fileToAdd);
			if (minFileDate == null || minFileDate.Value > fileToAdd.LastWriteTime) {
				minFileDate = fileToAdd.LastWriteTime;
			}
			if (maxFileDate == null || maxFileDate.Value < fileToAdd.LastWriteTime) {
				maxFileDate = fileToAdd.LastWriteTime;
			}
			file.CompressionLevel = CompressionLevel.BestCompression;
			string destFilename = ".\\" + dir + fileToAdd.Name;
			Log("Adding: " + destFilename);
			ZipEntry zip = file.AddFile(fileToAdd.FullName, dir);
			addedFileCount++;
			addedByteCount += fileToAdd.Length;
			if (addedFileCount%100 == 0) {
				//write stats about evry 100 file written
				PrintWriteStatistics();
			}
		}

		private static void AddFileToZip(string zipFilename, string dir, FileInfo fileToAdd) {
			/*
			using (ZipFile file = new ZipFile(zipFilename)) {
				AddFileToZipO(file, dir, fileToAdd);
				file.Save();
			}
			isAnyFileSaved = true;
			*/
			/*
			using (Package zip = Package.Open(zipFilename, FileMode.OpenOrCreate)) {
				string destFilename = ".\\" + dir + fileToAdd.Name;
				Log("Adding: " + destFilename);
				Uri uri = PackUriHelper.CreatePartUri(new Uri(destFilename, UriKind.Relative));
				if (zip.PartExists(uri)) {
					zip.DeletePart(uri);
				}
				PackagePart part = zip.CreatePart(uri, "", CompressionOption.Maximum);
				
				using (FileStream fileStream = fileToAdd.OpenRead()) {
					using (Stream dest = part.GetStream()) {
						CopyStream(fileStream, dest);
					}
				}
			}
			*/
		}

	

		private static void AddLogFileToZip(string zipFilename) {
			using (ZipFile file = new ZipFile(zipFilename)) {
				file.CompressionLevel = CompressionLevel.BestCompression;
				Log("Adding: zipProcessLog.txt");
				file.AddEntry("zipProcessLog.txt", log.ToString(), Encoding.UTF8);
				file.Save();
			}
		}

		private static void CopyStream(FileStream inputStream, Stream outputStream) {
			long bufferSize = inputStream.Length < BUFFER_SIZE ? inputStream.Length : BUFFER_SIZE;
			byte[] buffer = new byte[bufferSize];
			int bytesRead = 0;
			long bytesWritten = 0;
			while ((bytesRead = inputStream.Read(buffer, 0, buffer.Length)) != 0) {
				outputStream.Write(buffer, 0, bytesRead);
				bytesWritten += bufferSize;
			}
		}

		private static void Log(string s, params object[] args) {
			string date = DateTime.Now.ToString("yyyyMMdd HHmmss.fff");
			Console.WriteLine(date + ">" + s, args);
			log.AppendFormat(s, args);
			log.AppendLine();
		}
	}
}