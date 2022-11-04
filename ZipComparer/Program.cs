using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Ionic.Zip;

namespace ZipComparer {
	internal class Program {
		public static void Main(string[] args) {
			if (args.Length > 0) {
				DirectoryInfo info = new DirectoryInfo(args[0]);
				FileComparer f = new FileComparer();
				f.ProcessDir(info);
				Console.WriteLine("--------------------------------------------------------------");
				Console.WriteLine("Found different apps: " + f.Files.Count);
				Console.WriteLine("--------------------------------------------------------------");
				f.Compare();
				Console.WriteLine("--------------------------------------------------------------");
				Console.WriteLine("Files to delete: " + f.FilesToDelete.Count);
				Console.WriteLine("Files to delete size: " + f.FilesToDelete.Sum(s => s.Length / 1024.0 / 1024.0) + "MB");
				if (args.Any(a => "-D".Equals(a, StringComparison.OrdinalIgnoreCase))) {
					Stopwatch sw = Stopwatch.StartNew();
					Console.WriteLine("Will delete files");
					f.Delete();
					Console.WriteLine($"Deleted in {sw.ElapsedMilliseconds}ms");
				}
			}
			else {
				Console.WriteLine(@"first argument is folder where to check files. in addition add flag -D to actually delete files.");
			}
		}


	}

	public class FileComparer {
		public Dictionary<string, List<FileInfo>> Files = new Dictionary<string, List<FileInfo>>(StringComparer.OrdinalIgnoreCase);
		public List<FileInfo> FilesToDelete = new List<FileInfo>();

		public void ProcessDir(DirectoryInfo dir) {
			Add(dir.GetFiles());
			foreach (DirectoryInfo upDir in dir.GetDirectories()) {
				ProcessDir(upDir);
			}
		}

		public void Add(IEnumerable<FileInfo> files) {
			foreach (var file in files) {
				if (file.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && file.Name.IndexOf("_backup_", StringComparison.OrdinalIgnoreCase) >= 0) {
					string app = file.Name.Split('_').First();
					//Console.WriteLine($"{app} - Found file - {file.FullName}");
					AddF(app, file);
				}
			}
		}

		private void AddF(string app, FileInfo file) {
			List<FileInfo> files;
			if (!Files.TryGetValue(app, out files)) {
				files = new List<FileInfo>();
				Files.Add(app, files);
			}
			files.Add(file);
		}

		public void Compare() {
			foreach (KeyValuePair<string,List<FileInfo>> file in Files) {
				if (file.Value.Count <= 1) {
					continue;
				}
				var filesByDate = file.Value.OrderBy(o => o.CreationTime).ToList();
				FileInfo chIdx = filesByDate[0];
				
				for (int i = 1; i < filesByDate.Count; i++) {
					var curr = filesByDate[i];
					using (ZipFile zipA = new ZipFile(chIdx.FullName)) {
						using (ZipFile zipB = new ZipFile(curr.FullName)) {
							string cpr = CompareZipByCrc(zipA, zipB);
							if (cpr == null) {
								//Files identical
								FilesToDelete.Add(filesByDate[i]);
								Console.WriteLine($"DEL({file.Key}): " + curr.FullName);
							}
							else {
								Console.WriteLine($"NOT({file.Key})({cpr}): " + curr.FullName);
								//compare with next one
								chIdx = curr;
							}
						} 
					}
				}
			}
		}

		public void Delete() {
			foreach (FileInfo fileInfo in FilesToDelete) {
				fileInfo.Delete();
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <returns>if null, then Eq else first file that is different</returns>
		private string CompareZipByCrc(ZipFile a, ZipFile b) {
			if (a.Count != b.Count) {
				return "count";
			}
			
			using (var bIter = b.EntriesSorted.GetEnumerator()) {
				foreach (var an in a.EntriesSorted) {
					bool isMoveOk = bIter.MoveNext();
					var bn = isMoveOk ? bIter.Current : null;
					if (!an.FileName.Equals(bn?.FileName, StringComparison.OrdinalIgnoreCase)) {
						return an.FileName;
					}
					else 
						if (an.Crc != bn.Crc) {
							return an.FileName;
						}
					
				}

				if (bIter.MoveNext()) {
					return bIter.Current.FileName;
				}
			}

			return null;
		}
	}
}