// COPYRIGHT 2016 RUSTSERVERS.IO
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Net;
using System.Threading;

using Newtonsoft.Json.Linq;

using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using Oxide.Plugins;

namespace Oxide.Ext.Catalyst.Libraries
{
	public class Catalyst : Library
	{
		internal enum CommitType {
			Write,
			Delete,
			Require,
			Remove
		}

		internal class CommitAction
		{
			public CommitType type;
			public string path;
			public string src;
			public string name;
			public string version;

			public CommitAction(CommitType type, string name, string path, string version = "*", string src = "") {
				this.name = name;
				this.type = type;
				this.path = path;
				this.version = version;
				this.src = src;
			}

			public override string ToString() {
				return name + " " + version;
			}
		}

		List<CommitAction> commitActions = new List<CommitAction> ();
		List<string> commitErrors = new List<string>();
		private bool isValidCommit = false;
		private Dictionary<string, JObject> pluginCache = new Dictionary<string, JObject>();

		private Core.Libraries.WebRequests webrequest = Interface.Oxide.GetLibrary<Core.Libraries.WebRequests>();
		private Core.Libraries.Plugins plugins = Interface.Oxide.GetLibrary<Core.Libraries.Plugins>();

		public override bool IsGlobal => false;

		private readonly string _ConfigDirectory;
		private readonly string _DataDirectory;
		private readonly string _PluginDirectory;
		private readonly DataFileSystem _DataFileSystem;

		private bool _running = true;
		public CatalystSettings Settings;

		private Oxide.Ext.Catalyst.Plugins.Catalyst _CatalystPlugin;

		private WebClient _WebClient = new WebClient();

		public Catalyst(Oxide.Ext.Catalyst.Plugins.Catalyst catalystPlugin)
		{
			_CatalystPlugin = catalystPlugin;
			_DataFileSystem = Interface.Oxide.DataFileSystem;
			_ConfigDirectory = Interface.Oxide.ConfigDirectory;
			_DataDirectory = Interface.Oxide.DataDirectory;
			_PluginDirectory = Interface.Oxide.PluginDirectory;
		}
		internal void Initialize()
		{
			Interface.Oxide.LogInfo("[Catalyst] Initializing");
			CheckConfig ();
		}

		public void CheckConfig() {
			Settings = _DataFileSystem.ReadObject<CatalystSettings>(Path.Combine(_ConfigDirectory, "Catalyst"));

			if (Settings.SourceList == null)
			{
				Settings = new CatalystSettings();
				Settings.Debug = false;
				Settings.SourceList = new List<string>() {
					"http://rustservers.io/p"
				};
				Settings.Require = new Dictionary<string, string>();
				Settings.RequireDev = new Dictionary<string, string>();

				SaveConfig ();
			}

			if (_running)
			{
				Interface.Oxide.LogInfo("[Catalyst] Initialized");
			}
		}

		private void SaveConfig() {
			_DataFileSystem.WriteObject<CatalystSettings>(Path.Combine(_ConfigDirectory, "Catalyst"), Settings);
		}

		internal void Shutdown()
		{
			_running = false;
		}

		public bool IsPluginInstalled(string plugin) {
			return plugins.Exists (plugin);
		}

		internal JObject GetPluginInfo (string name)
		{
			if (pluginCache.ContainsKey (name)) {
				return pluginCache[name];
			}

			JObject plugin = null;
			foreach (string source in Settings.SourceList) {
				try {
					plugin = JObject.Parse (_WebClient.DownloadString (source + "/" + name + ".json"));

					if(plugin["404"] != null) {
						continue;
					} else {
						return plugin;
					}
				} catch(WebException ex) {
					if (ex.Status == WebExceptionStatus.ProtocolError && ex.Response != null)
					{
						var resp = (HttpWebResponse)ex.Response;
						if (resp.StatusCode == HttpStatusCode.NotFound) // HTTP 404
						{
							continue;
						}
					}
					throw;
				}
			}

			return null;
		}

		internal string GetPluginSource(string src) {
			return _WebClient.DownloadString (src);
		}

		private void CommitWrite(string name, string path, string version, string src) 
		{
			commitActions.Add (new CommitAction (CommitType.Write, name, path, version, src));
		}

		private void CommitRequire(string name, string path, string version) 
		{
			commitActions.Add (new CommitAction (CommitType.Require, name, path, version));
		}

		private void CommitDelete(string name, string path) 
		{
			commitActions.Add (new CommitAction (CommitType.Delete, name, path));
		}

		private void CommitRemove(string name, string path) 
		{
			commitActions.Add (new CommitAction (CommitType.Remove, name, path));
		}

		public void BeginCommit() 
		{
			isValidCommit = false;
			commitErrors = new List<string>();
			pluginCache = new Dictionary<string, JObject>();
		}

		private string Error (string msg)
		{
			isValidCommit = false;
			commitErrors.Add(msg);
			return msg;
		}

		public void EndCommit ()
		{
			if (!isValidCommit) {
				StringBuilder sb = new StringBuilder();
				foreach (string error in commitErrors) {
					sb.AppendLine(error);
				}
				Interface.Oxide.LogError(sb.ToString());
				return;
			}

			foreach (CommitAction commit in commitActions) 
			{
				switch (commit.type) {
				case CommitType.Write:
					System.IO.File.WriteAllText (commit.path, commit.src);
					break;
				case CommitType.Delete:
					System.IO.File.Delete (commit.path);
					break;
				case CommitType.Require:
					if (!Settings.Require.ContainsKey (commit.name)) {
						Settings.Require.Add (commit.name, commit.version);
						SaveConfig ();
					}
					break;
				case CommitType.Remove:
					if (Settings.Require.ContainsKey (commit.name)) {
						Settings.Require.Remove (commit.name);
						SaveConfig ();
					}
					break;
				}
			}

			commitActions.Clear();
		}

		public object InstallPlugin(string plugin) {
			JObject pluginInfo = GetPluginInfo (plugin);
			if (pluginInfo != null) {
				return InstallPlugin (pluginInfo);
			}

			return "No plugin found";
		}

		private object InstallPlugin (JObject pluginInfo, string version = "*")
		{
			string name = pluginInfo ["name"].ToString ();
			if (IsPluginInstalled (name)) {
				return Error ("Already installed");
			}

			string v = pluginInfo ["version"].ToString ();
			bool matchingVersion = false;
			if (v == version) {
				matchingVersion = true;
			}

			string ext = pluginInfo ["ext"].ToString ();
			string path = Path.Combine (_PluginDirectory, name + "." + ext);

			var requires = pluginInfo ["plugin"] ["require"];
			if (requires != null) {
				foreach (string require in requires) {
					if (!isValidCommit)
						break;
					InstallPlugin (require);
				}
			}

			if (isValidCommit) {
				// INSTALL PLUGIN
				if (!Settings.Require.ContainsKey (name)) {
					CommitRequire (name, path, version);
				}
				if (!matchingVersion) {
					string src = GetPluginSource (pluginInfo ["src"].ToString ().Replace (@"\", ""));
					CommitWrite (name, path, version, src);
				} 

				return true;
			}

			return false;
		}

		public object UpdatePlugin(string plugin) {
			JObject pluginInfo = GetPluginInfo (plugin);
			if (pluginInfo != null) {
				return UpdatePlugin (pluginInfo);
			}

			return Error("No plugin found");
		}

		internal object UpdatePlugin (JObject pluginInfo, string version = "*")
		{
			string name = pluginInfo ["name"].ToString ();
			if (!IsPluginInstalled (name)) {
				return Error ("Not installed");
			}

			string v = pluginInfo ["version"].ToString ();
			bool matchingVersion = false;
			if (v == version) {
				matchingVersion = true;
			}

			string ext = pluginInfo ["ext"].ToString ();
			string path = Path.Combine (_PluginDirectory, name + "." + ext);

			var requires = pluginInfo ["plugin"] ["require"];
			if (requires != null) {
				foreach (string require in requires) {
					if (!isValidCommit)
						break;
					UpdatePlugin (require);
				}
			}

			if (isValidCommit) {
				// UPDATE PLUGIN
				if (!matchingVersion) {
					if (System.IO.File.Exists (path)) {
						CommitDelete (name, path);
					}
					string src = GetPluginSource (pluginInfo ["src"].ToString ().Replace (@"\", ""));
					CommitWrite (name, path, version, src);
				}

				return true;
			}

			return false;
		}

		public object RemovePlugin(string plugin) {
			JObject pluginInfo = GetPluginInfo (plugin);
			if (pluginInfo != null) {
				return RemovePlugin (pluginInfo);
			}

			return Error("No plugin found");
		}

		internal object RemovePlugin(JObject pluginInfo) {
			string name = pluginInfo ["name"].ToString();
			if (!IsPluginInstalled (name)) {
				return Error("Not installed");
			}

			string ext = pluginInfo["ext"].ToString();
			string path = Path.Combine(_PluginDirectory, name+"."+ext);

			if (System.IO.File.Exists (path)) {
				// REMOVE PLUGIN
				CommitRemove(name, path);
				CommitDelete(name, path);
			} else {
				return Error("File does not exist");
			}

			return true;
		}
	}
}
