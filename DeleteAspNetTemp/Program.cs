using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace DeleteAspNetTemp {
	internal class Program {
		public static void Main(string[] args) {
			var mains = new[] {
				"C:\\Windows\\Microsoft.NET\\Framework\\v4.0.30319\\Temporary ASP.NET File", "C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\Temporary ASP.NET Files"
			};
			foreach (var main in mains) {
				DirectoryInfo tmp = new DirectoryInfo(main);
				if (tmp.Exists) {
					foreach (var d1 in tmp.GetDirectories()) {
						var toDelete = d1.EnumerateDirectories().OrderBy(o => o.LastWriteTime).ToList();
						
						foreach (var toDel in toDelete.Take(Math.Max(0, toDelete.Count - 3))) {
							try {
								Stopwatch sw = Stopwatch.StartNew();
								toDel.Delete(true);
								Console.WriteLine(toDel.FullName.Substring(main.Length) + $" - DELETED, elapsed {sw.ElapsedMilliseconds}ms");
							}
							catch (Exception e) {
								Console.WriteLine(toDel.FullName.Substring(main.Length) + " ERR - " + e.Message);
							}
						}
					}
				}
				else {
					Console.WriteLine("No folder: " + tmp.FullName);
				}
			}
		}
	}
}