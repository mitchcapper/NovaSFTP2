using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace NovaSFTP2.Model {
	[Serializable]
	public class SettingsInfo {
		public HostInfo[] hosts;
		public string ignore_regex;
		public SettingsInfo() {
			ignore_regex = @"( [.](svn|git|tmp[/\\]) | mrgtmp | ~ | \.tmp$)";
			hosts = new HostInfo[0];
		}
		
	}

	[Serializable]
	public class HostInfo {
		public string name;
		public string host;
		public int port = 22;
		public string username = "root";
		public string localFolder;
		public bool recursive;
		public string remoteFolder;
		public override string ToString() {
			return name??"";
		}
		public static string GetUserAppDataPath() {
			string path = string.Empty;
			Assembly assm;
			Type at;
			object[] r;

			// Get the .EXE assembly
			assm = Assembly.GetEntryAssembly();
			// Get a 'Type' of the AssemblyCompanyAttribute
			at = typeof(AssemblyCompanyAttribute);
			// Get a collection of custom attributes from the .EXE assembly
			r = assm.GetCustomAttributes(at, false);
			// Get the Company Attribute
			AssemblyCompanyAttribute ct =
						  ((AssemblyCompanyAttribute)(r[0]));
			// Build the User App Data Path
			path = Environment.GetFolderPath(
						Environment.SpecialFolder.LocalApplicationData);
			path += @"\NovaSFTP2";
			if (!Directory.Exists(path))
				Directory.CreateDirectory(path);
			return path;
		}
		private static string file_path = GetUserAppDataPath() + "\\program_settings.xml";
		public static async Task SaveSettings(SettingsInfo info) {
			using (var writer = new StreamWriter(file_path)) {
				await ObjDumpToFile(info, writer);
			}
		}
		public static async Task ObjDumpToFile(Object obj, StreamWriter file) {
			var x = new System.Xml.Serialization.XmlSerializer(obj.GetType());
			var text = "";

			if (obj.GetType() != Type.GetType("System.String")) {
				MemoryStream stream = new MemoryStream();
				x.Serialize(stream, obj);
				stream.Seek(0, SeekOrigin.Begin);
				using (StreamReader rd = new StreamReader(stream)) {
					text = rd.ReadToEnd();
				}
			} else
				text = (string)obj;
			await file.WriteAsync(text).ConfigureAwait(false);
		}
		public static SettingsInfo LoadSettings() {
			var ret = new SettingsInfo();
			if (File.Exists(file_path)) {
				try {
					using (StreamReader reader = new StreamReader(file_path)) {
						var serializer = new XmlSerializer(typeof(SettingsInfo));
						ret = (SettingsInfo) serializer.Deserialize(reader);
					}
				} catch {
					try {
						using (StreamReader reader = new StreamReader(file_path)) {
							var serializer = new XmlSerializer(typeof (HostInfo[]));
							ret.hosts = (HostInfo[]) serializer.Deserialize(reader);
						}
					} catch {}
				}
			}
			return ret;
		}
	}

}
