// COPYRIGHT 2016 RUSTSERVERS.IO
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Net;
using System.Threading;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Oxide.Core;
using Oxide.Core.Libraries;

namespace Oxide.Ext.Catalyst.Libraries
{
	public class Catalyst : Library
	{
		internal enum CommitType
		{
			Write,
			Delete,
			Require,
			Remove
		}

		public enum StatusMessage
		{
			SourceMismatch,
			VersionMismatch,
			NoUpgradePath
		}

		internal class CommitAction
		{
			public CommitType type;
			public string path;
			public string src;
			public string name;
			public string version;

			public CommitAction (CommitType type, string name, string path, string version = "*", string src = "")
			{
				this.name = name;
				this.type = type;
				this.path = path;
				this.version = version;
				this.src = src;
			}

			public override string ToString ()
			{
				return name + " " + version;
			}
		}

		public abstract class CommitQuery
		{
			protected Catalyst library;

			protected CommitQuery (Catalyst library)
			{
				this.library = library;
				if (library.Settings.Debug) {
					library.DebugLog ("Creating query: " + GetType ().Name);
				}
			}

			public abstract void Handle ();

		}


		public Queue<CommitQuery> _queue = new Queue<CommitQuery> ();
		readonly Thread _worker;
		bool _running = true;
		readonly object _syncroot = new object ();
		readonly AutoResetEvent _workevent = new AutoResetEvent (false);

		DependencyValidator _Validator;
		List<CommitAction> commitActions = new List<CommitAction> ();
		List<string> commitErrors = new List<string> ();
		bool isValidCommit;
		public bool IsCommitting;

		internal class PluginStore
		{
			public string name;
			public JObject data;
			public string version;
			public long lastCheckLong;

			[JsonIgnore]
			public DateTime lastChecked {
				get {
					return DateTime.FromBinary (lastCheckLong);
				}
				set {
					lastCheckLong = value.ToBinary ();
				}
			}

			[JsonConstructor]
			public PluginStore (string name, JObject data, string version, long lastCheckLong)
			{
				this.name = name;
				this.data = data;
				this.version = version;
				this.lastCheckLong = lastCheckLong;
			}

			public PluginStore (string name, JObject data, string version)
			{
				this.name = name;
				this.data = data;
				if (version == string.Empty && data ["version"] != null) {
					version = data ["version"].ToString ();
				}
				this.version = version;
				lastCheckLong = DateTime.Now.ToBinary ();
			}

			[JsonIgnore]
			internal string PluginVersion {
				get {
					if (data is JObject) {
						if (data ["plugin"] != null) {
							return data ["plugin"] ["version"].ToString ();
						}
					}

					return string.Empty;
				}
				set {
					if (data is JObject) {
						if (data ["plugin"] != null) {
							data ["plugin"] ["version"] = value;
						}
					}
				}
			}

			[JsonIgnore]
			internal bool Matching {
				get {
					return version == PluginVersion;
				}
			}
		}

		Dictionary<string, PluginStore> pluginStore = new Dictionary<string, PluginStore> ();
		Dictionary<string, JObject> pluginCache = new Dictionary<string, JObject> ();

		WebRequests webrequest = Interface.Oxide.GetLibrary<WebRequests> ();
		Core.Libraries.Plugins plugins = Interface.Oxide.GetLibrary<Core.Libraries.Plugins> ();

		public override bool IsGlobal => false;

		readonly string _ConfigDirectory;
		readonly string _DataDirectory;
		readonly string _PluginDirectory;
		readonly DataFileSystem _DataFileSystem;

		public CatalystSettings Settings;

		WebClient _WebClient = new WebClient ();
		CatalystExtension Extension;
		public string currentGame = "rust";

		string [] exts =
		{
			"cs",
			"py",
			"lua",
			"js",
			"coffee"
		};

		public string [] games =
		{
			"rust",
			"universal"
		};

		public bool HasErrors {
			get {
				return commitErrors.Count () > 0;
			}
		}

		string configPath = string.Empty;
		string dataPath = string.Empty;

		public Catalyst (CatalystExtension catalystExtension)
		{
			Extension = catalystExtension;
			_DataFileSystem = Interface.Oxide.DataFileSystem;
			_ConfigDirectory = Interface.Oxide.ConfigDirectory;
			_DataDirectory = Interface.Oxide.DataDirectory;
			_PluginDirectory = Interface.Oxide.PluginDirectory;
			_Validator = new DependencyValidator (this);

			configPath = Path.Combine (_ConfigDirectory, "Catalyst");
			dataPath = Path.Combine (_DataDirectory, "catalyst.cache");

			_worker = new Thread (Worker);
			_worker.Start ();
		}

		void Worker ()
		{
			while (_running || _queue.Count > 0) {
				CommitQuery query = null;
				lock (_syncroot) {
					if (_queue.Count > 0)
						query = _queue.Dequeue ();
				}
				if (query != null) {
					DebugLog ("Handling: " + query.GetType ().Name);
					query.Handle ();
					pluginCache.Clear ();
				} else if (_running)
					_workevent.WaitOne ();
			}
		}

		public void Enqueue (CommitQuery query)
		{
			lock (_syncroot) _queue.Enqueue (query);
			_workevent.Set ();
		}

		internal void Initialize ()
		{
			CheckConfig ();
			LoadCache ();
		}

		public void CheckConfig ()
		{
			if (_DataFileSystem.ExistsDatafile (configPath)) {
				Settings = _DataFileSystem.ReadObject<CatalystSettings> (configPath);

				if (Settings == null) {
					LoadDefaultConfig ();
					return;
				}
				if (Settings.Version != Extension.Version.ToString ()) {
					UpgradeConfig ();
					Settings.Version = Extension.Version.ToString ();
					SaveConfig ();
				}
			} else {
				LoadDefaultConfig ();
			}
		}

		void LoadDefaultConfig ()
		{
			Log ("Creating Default Configuration");
			Settings = new CatalystSettings ();
			Settings.Debug = false;
			Settings.SourceList = new List<string>
			{
					"http://rustservers.io"
				};
			Settings.Require = new Dictionary<string, string> ();
			Settings.RequireDev = new Dictionary<string, string> ();
			Settings.Version = Extension.Version.ToString ();

			SaveConfig ();
		}

		void UpgradeConfig ()
		{
		}

		void SaveConfig ()
		{
			_DataFileSystem.WriteObject (configPath, Settings);
		}

		void LoadCache ()
		{
			if (_DataFileSystem.ExistsDatafile (configPath)) {
				if (Settings.Debug) {
					DebugLog ("Loading plugin cache");
				}
				var data = _DataFileSystem.ReadObject<Dictionary<string, object>> (dataPath);
				foreach (var kvp in data) {
					if (kvp.Value is JObject) {
						var val = ((JObject)kvp.Value).ToObject<PluginStore> ();
						pluginStore.Add (kvp.Key, val);
					}
				}
			}
		}

		internal void SaveCache ()
		{
			if (Settings.Debug) {
				DebugLog ("Saving plugin cache");
			}
			_DataFileSystem.WriteObject (dataPath, pluginStore);
		}

		internal string SHA (string source)
		{
			var sha1 = System.Security.Cryptography.SHA1.Create ();
			var buf = Encoding.UTF8.GetBytes (source);
			var hash = sha1.ComputeHash (buf, 0, buf.Length);
			return BitConverter.ToString (hash).Replace ("-", "");
		}

		public Dictionary<StatusMessage, string> GetStatus ()
		{
			var statusChanges = new Dictionary<StatusMessage, string> ();
			foreach (KeyValuePair<string, string> kvp in Settings.Require) {
				var loaded = plugins.Find (kvp.Key);
				var pluginInfo = GetPluginInfo (kvp.Key, kvp.Value);
				if (loaded != null && pluginInfo != null) {
					var name = pluginInfo ["name"].ToString ();
					var ext = pluginInfo ["ext"].ToString ();
					var version = pluginInfo ["version"].ToString ();
					var filename = name + "." + ext;
					var path = Path.Combine (_PluginDirectory, filename);

					var old_contents = File.ReadAllText (path);
					var new_contents = GetPluginSource (pluginInfo ["src"].ToString ());

					var sha_1 = SHA (old_contents);
					var sha_2 = SHA (new_contents);

					if (sha_1 != sha_2) {
						var loadedVersion = loaded.Version.ToString ();
						if (loadedVersion != version) {
							statusChanges.Add (StatusMessage.VersionMismatch, filename + ": local version (" + loadedVersion + ") different than remote version (" + version + ")");
						} else {
							statusChanges.Add (StatusMessage.SourceMismatch, filename + ": " + loadedVersion + " local sources are different");
						}
					}
				} else if (loaded != null) {
					statusChanges.Add (StatusMessage.NoUpgradePath, loaded.Filename + ": No upgrade path, plugin is not found on sources");
				}
			}

			return statusChanges;
		}

		internal bool IsPluginInstalled (string plugin)
		{
			if (plugins.Exists (plugin)) {
				return true;
			}

			foreach (string ext in exts) {
				var path = Path.Combine (_PluginDirectory, plugin + "." + ext);
				if (File.Exists (path)) {
					return true;
				}
			}

			return false;
		}

		internal string [] FindPlugin (string name, string version = "")
		{
			var result = new List<string> ();
			JObject results = null;
			foreach (string source in Settings.SourceList) {
				string url = string.Empty;
				//				if (string.IsNullOrEmpty(version) || version == "*")
				//				{
				//					url = source + "/s/" + name + ".json";
				//				}
				//				else
				//				{
				//					url = source + "/s/" + name + " " + version + ".json";
				//				}

				if (string.IsNullOrEmpty (version) || version == "*") {
					url = source + "/s/search.json";
				} else {
					url = source + "/s/search.json";
				}

				url += "?terms=" + name;

				if (Settings.Debug) {
					Log ("Reading " + url);
				}

				try {
					results = JObject.Parse (_WebClient.DownloadString (url));

					if (results ["data"] == null) {
						continue;
					}

					foreach (string r in results ["data"]) {
						result.Add (r);
					}

					return result.ToArray ();
				} catch (WebException ex) {
					if (ex.Status == WebExceptionStatus.ProtocolError && ex.Response != null) {
						var resp = (HttpWebResponse)ex.Response;
						if (resp.StatusCode == HttpStatusCode.NotFound)  // HTTP 404
						{
							continue;
						}
					}

					DebugLog (ex.Message);
				} catch (Exception e) {
					DebugLog (e.Message);
				}
			}

			return null;
		}

		internal bool TryGetStore (string name, out PluginStore store)
		{
			return pluginStore.TryGetValue (name, out store);
		}

		internal void RemoveStore (string name)
		{
			if (pluginStore.ContainsKey (name)) {
				pluginStore.Remove (name);
			}
		}

		string NotFoundError = "{\"404\":\"Not found\"}";

		internal bool CheckPlugin (string name)
		{
			var games = new List<string> ();

			if (name.Contains ("/")) {
				var parts = name.Split ('/');
				games.Add (parts [0]);
				name = parts [1];
			} else {
				games.AddRange (this.games);
			}

			PluginStore store;
			if (pluginStore.TryGetValue (name, out store)) {
				foreach (string source in Settings.SourceList) {
					foreach (string game in games) {
						string version = store.PluginVersion;
						string url = string.Empty;
						if (string.IsNullOrEmpty (version) || version == "*") {
							url = source + "/v/" + game + "/" + name;
						} else {
							url = source + "/v/" + game + "/" + name;
						}

						if (Settings.Debug) {
							Log ("Checking " + url);
						}

						try {
							var newVersion = _WebClient.DownloadString (url);
							if (newVersion != NotFoundError) {
								if (newVersion.Length > 0 && VersionCompare (version, newVersion)) {
									store.version = newVersion;
									return false;
								}

								return true;
							}
						} catch (Exception ex) {
							LogError (ex.Message);
						}
					}
				}
			}

			return true;
		}

		internal bool VersionCompare (string original, string newVersion)
		{
			var originalInt = VersionToInt (original);
			var newVersionInt = VersionToInt (newVersion);

			if (newVersionInt > originalInt) {
				return true;
			}

			return false;
		}

		internal int VersionToInt (string version)
		{
			//int[] baseVerion = new int[3] { 0, 0, 0 };
			if (version.Contains (".")) {
				var parts = version.Split (new string [] { "." }, StringSplitOptions.RemoveEmptyEntries);
				var i = 0;
				var final = 0;

				if (parts.Length > 0) {
					foreach (var part in parts) {
						if (!string.IsNullOrEmpty (part)) {
							switch (i) {
							case 0:
								final += ParseVersionPart (part);
								break;
							case 2:
								final += ParseVersionPart (part) * 10;
								break;
							case 3:
								final += ParseVersionPart (part) * 100;
								break;
							}

							i++;
						}
					}
				}

				return final;
			}

			int intVal;
			if (int.TryParse (version, out intVal)) {
				return intVal;
			}

			return 0;
		}

		internal int ParseVersionPart (string part)
		{
			var bytes = Encoding.ASCII.GetBytes (part);
			if (bytes.Length < 4) {
				byte [] temp = new byte [4];
				bytes.CopyTo (temp, 0);
				bytes = temp;
			}

			return BitConverter.ToInt32 (bytes, 0);
		}

		internal bool PluginCached (string name)
		{
			PluginStore store;
			if (TryGetStore (name, out store)) {
				return true;
			}

			return false;
		}

		internal object PluginExists (string name, string version = "")
		{
			var pluginInfo = GetPluginInfo (name, version);
			if (pluginInfo is JObject) {
				return pluginInfo;
			}
			return false;
		}

		internal JObject GetPluginInfo (string name, string version = "")
		{
			var games = new List<string> ();

			var pluginName = name;
			if (name.Contains ("/")) {
				var parts = name.Split ('/');
				games.Add (parts [0]);
				pluginName = parts [1];
			} else {
				games.AddRange (this.games);
			}

			var cacheName = name + "-" + version;
			JObject plugin = null;

			if (pluginCache.TryGetValue (cacheName, out plugin)) {
				return plugin;
			}

			PluginStore store;
			if (pluginStore.TryGetValue (name, out store)) {
				pluginCache.Add (name, store.data);
				return store.data;
			}

			string url = "";
			int errorCount = 0;
			var errorMessages = new List<string> ();
			foreach (string source in Settings.SourceList) {
				foreach (string game in games) {
					if (string.IsNullOrEmpty (version) || version == "*") {
						url = source + "/p/" + game + "/" + pluginName + ".json";
					} else {
						url = source + "/p/" + game + "/" + pluginName + "/" + version + ".json";
					}

					if (Settings.Debug) {
						DebugLog ("Reading " + url);
					}

					try {
						plugin = JObject.Parse (_WebClient.DownloadString (url));

						if (plugin ["404"] != null) {
							continue;
						}

						if (IsPluginValid (plugin)) {
							StorePlugin (name, version, plugin);
							plugin ["category"] = game;
							return plugin;
						}
					} catch (WebException ex) {
						if (ex.Status == WebExceptionStatus.ProtocolError && ex.Response != null) {
							var resp = (HttpWebResponse)ex.Response;
							if (resp.StatusCode == HttpStatusCode.NotFound) // HTTP 404
							{
								continue;
							}
						}

						errorCount++;
						errorMessages.Add (ex.Message);
					} catch (Exception e) {
						LogError (e.Message);
					}
				}
			}

			if (errorCount > 0) {
				foreach (string msg in errorMessages) {
					LogError (msg);
				}
			}

			return null;
		}

		internal void StorePlugin (string name, string version, JObject data)
		{
			var cacheName = name + "-" + version;
			if (pluginCache.ContainsKey (cacheName)) {
				pluginCache [cacheName] = data;
			} else {
				pluginCache.Add (cacheName, data);
			}

			PluginStore store;
			if (pluginStore.TryGetValue (name, out store)) {
				store.data = data;
				store.lastCheckLong = DateTime.Now.ToBinary ();
			} else {
				pluginStore.Add (name, new PluginStore (name, data, version));
			}
		}

		internal string GetPluginSource (string url)
		{
			if (Settings.Debug) {
				Log ("Downloading: " + url);
			}

			var src = _WebClient.DownloadString (url.Replace (@"\", ""));
			var bytes = Encoding.Default.GetBytes (src);
			src = Encoding.UTF8.GetString (bytes);
			return src;
		}

		void CommitWrite (string name, string path, string version, string src)
		{
			commitActions.Add (new CommitAction (CommitType.Write, name, path, version, src));
		}

		void CommitRequire (string name, string path, string version)
		{
			commitActions.Add (new CommitAction (CommitType.Require, name, path, version));
		}

		void CommitDelete (string name, string path)
		{
			commitActions.Add (new CommitAction (CommitType.Delete, name, path));
		}

		void CommitRemove (string name, string path)
		{
			commitActions.Add (new CommitAction (CommitType.Remove, name, path));
		}

		public void BeginCommit ()
		{
			if (Settings.Debug) {
				Log ("Begin Commit");
			}
			IsCommitting = true;
			isValidCommit = true;
			commitErrors = new List<string> ();
			pluginCache = new Dictionary<string, JObject> ();
		}

		internal string Error (string msg)
		{
			if (Settings.Debug) {
				Log ("Commit Error: " + msg);
			}
			isValidCommit = false;
			commitErrors.Add (msg);
			return msg;
		}

		public void EndCommit ()
		{
			if (Settings.Debug) {
				Log ("Ending Commit");
			}

			IsCommitting = false;

			if (!isValidCommit) {
				if (commitErrors.Count () > 0) {
					foreach (string error in commitErrors) {
						LogError (error);
					}
				}
				return;
			}

			bool saveConfig = false;

			if (commitActions.Count > 0) {
				foreach (CommitAction commit in commitActions) {
					if (Settings.Debug) {
						Log (commit.type.ToString () + ": " + commit.name);
					}
					switch (commit.type) {
					case CommitType.Write:
						File.WriteAllText (commit.path, commit.src);
						break;
					case CommitType.Delete:
						File.Delete (commit.path);
						break;
					case CommitType.Require:
						if (!Settings.Require.ContainsKey (commit.name)) {
							Settings.Require.Add (commit.name, commit.version);
							saveConfig = true;
						}
						break;
					case CommitType.Remove:
						if (Settings.Require.ContainsKey (commit.name)) {
							Settings.Require.Remove (commit.name);
							saveConfig = true;
						}
						break;
					}
				}
			}

			if (saveConfig) {
				SaveConfig ();
			}

			SaveCache ();

			commitActions.Clear ();
			_Validator.Clear ();
		}

		public object InstallPlugin (string plugin, string version = "*")
		{
			var pluginInfo = GetPluginInfo (plugin, version);
			if (pluginInfo != null) {
				try {
					return InstallPlugin (pluginInfo, version);
				} catch (Exception ex) {
					return Error (ex.Message);
				}
			}

			return Error ("No plugin found");
		}

		public void SuggestPlugins (string source, params string [] plugins)
		{
			Log (source + " suggests the following plugins: " + string.Join (", ", plugins));
		}

		internal object RequirePlugin (JObject pluginInfo, string version = "*")
		{
			if (!isValidCommit) {
				return false;
			}
			var name = pluginInfo ["name"].ToString ();
			var ext = pluginInfo ["ext"].ToString ();
			var category = pluginInfo ["category"].ToString ();
			var path = Path.Combine (_PluginDirectory, name + "." + ext);

			if (!Settings.Require.ContainsKey (name) && !Settings.Require.ContainsKey (category + "/" + name)) {
				CommitRequire (category + "/" + name, path, version);
				return true;
			}

			return false;
		}

		internal object InstallPlugin (JObject pluginInfo, string version = "*")
		{
			var name = pluginInfo ["name"].ToString ();
			var ext = pluginInfo ["ext"].ToString ();
			var category = pluginInfo ["category"].ToString ();
			var qualifiedName = category + "/" + name;

			var path = Path.Combine (_PluginDirectory, name + "." + ext);

			var v = pluginInfo ["version"].ToString ();
			var matchingVersion = false;
			if (IsPluginInstalled (name)) {
				var p = plugins.Find (name);
				var loadedVersion = p.Version.ToString ();
				if (loadedVersion == v) {
					matchingVersion = true;
				}
			}

			var requires = pluginInfo ["plugin"] ["require"];
			if (requires != null) {
				try {
					var dependencies = requires.ToObject<Dictionary<string, string>> ();
					foreach (KeyValuePair<string, string> kvp in dependencies) {
						if (!isValidCommit)
							break;
						InstallPlugin (kvp.Key, kvp.Value);
					}
				} catch (Exception e) {
					LogError (e.Message);
				}
			}

			var suggests = pluginInfo ["plugin"] ["suggest"];
			if (suggests != null) {
				try {
					var suggestedDependencies = suggests.ToObject<Dictionary<string, string>> ();

					if (suggestedDependencies.Count > 0) {
						SuggestPlugins (name, suggestedDependencies.Keys.ToArray ());
					}
				} catch (Exception e) {
					LogError (e.Message);
				}
			}


			if (!Validate ()) {
				return false;
			}

			if (isValidCommit) {
				// INSTALL PLUGIN
				if (!Settings.Require.ContainsKey (qualifiedName)) {
					CommitRequire (qualifiedName, path, version);
				}

				if (!matchingVersion || !IsPluginInstalled (name)) {
					var src = GetPluginSource (pluginInfo ["src"].ToString ());
					CommitWrite (name, path, version, src);
				} else {
					return name + " already installed.";
				}

				return true;
			}

			return false;
		}

		public object UpdatePlugin (string plugin, string version = "*")
		{
			var pluginInfo = GetPluginInfo (plugin, version);
			if (pluginInfo != null) {
				try {
					return UpdatePlugin (pluginInfo, version);
				} catch (Exception ex) {
					return Error (ex.Message);
				}
			}

			return Error ("No plugin found");
		}

		internal List<JObject> GetPluginChildren (JObject pluginInfo)
		{
			var objs = new List<JObject> ();
			objs.Add (pluginInfo);

			var requires = pluginInfo ["plugin"] ["require"];
			if (requires != null) {
				var dependencies = requires.ToObject<Dictionary<string, string>> ();
				foreach (KeyValuePair<string, string> kvp in dependencies) {
					var childPlugin = GetPluginInfo (kvp.Key, kvp.Value);
					if (childPlugin != null) {
						objs.Add (childPlugin);
					}
				}
			}

			return objs;
		}

		internal object UpdatePlugin (JObject pluginInfo, string version = "*")
		{
			var name = pluginInfo ["name"].ToString ();
			var category = pluginInfo ["category"].ToString ();
			var qualifiedName = category + "/" + name;
			if (!IsPluginInstalled (name)) {
				DebugLog ("Plugin not installed, installing: " + name);
				return InstallPlugin (name, version);
			}

			var v = pluginInfo ["version"].ToString ();
			var matchingVersion = false;
			var existingPlugin = plugins.Find (name);
			if (existingPlugin != null) {
				var loadedVersion = existingPlugin.Version.ToString ();
				if (loadedVersion == v) {
					matchingVersion = true;
				}
			}

			var ext = pluginInfo ["ext"].ToString ();
			var path = Path.Combine (_PluginDirectory, name + "." + ext);

			var requires = pluginInfo ["plugin"] ["require"];
			if (requires != null) {
				var dependencies = requires.ToObject<Dictionary<string, string>> ();
				foreach (KeyValuePair<string, string> kvp in dependencies) {
					if (!isValidCommit)
						break;

					DebugLog ("Installing dependency: " + kvp.Key);
					UpdatePlugin (kvp.Key, kvp.Value);
				}
			}

			if (!Validate ()) {
				DebugLog ("Validation failed");
				return false;
			}

			if (isValidCommit) {
				// UPDATE PLUGIN
				if (!matchingVersion) {
					DebugLog ("Committing changes");
					if (File.Exists (path)) {
						CommitDelete (name, path);
					}
					var src = GetPluginSource (pluginInfo ["src"].ToString ());
					CommitWrite (name, path, version, src);
				} else {
					return name + " already up-to-date.";
				}

				DebugLog ("Commit valid");

				return true;
			}

			DebugLog ("Update invalid");

			return false;
		}

		public object RemovePlugin (string plugin)
		{
			var pluginInfo = GetPluginInfo (plugin);
			if (pluginInfo != null) {
				try {
					return RemovePlugin (pluginInfo);
				} catch (Exception ex) {
					return Error (ex.Message);
				}
			}

			return Error ("No plugin found");
		}

		internal object RemovePlugin (JObject pluginInfo)
		{
			//TODO: CHECK IF ANY PLUGINS DEPEND ON THIS ONE AND REMOVE THEM TOO
			var name = pluginInfo ["name"].ToString ();

			var ext = pluginInfo ["ext"].ToString ();
			var path = Path.Combine (_PluginDirectory, name + "." + ext);

			if (File.Exists (path)) {
				// REMOVE PLUGIN
				CommitRemove (name, path);
				if (IsPluginInstalled (name) && !HasErrors) {
					CommitDelete (name, path);
				}
			} else {
				return Error ("File does not exist");
			}

			return true;
		}

		internal bool Validate ()
		{
			return _Validator.Passes ();
		}

		internal bool IsPluginValid (JObject pluginInfo)
		{
			if (pluginInfo ["name"] == null) return false;
			if (pluginInfo ["version"] == null) return false;
			if (pluginInfo ["ext"] == null) return false;
			if (pluginInfo ["src"] == null) return false;
			if (pluginInfo ["plugin"] == null) return false;

			return true;
		}

		public override void Shutdown ()
		{
			SaveConfig ();
		}


		internal void HandleResult (object result, string action)
		{
			if ((result is string || result is bool || result == null) && !IsCommitting) {
				if (result is string) {
					LogError (result.ToString ());
				} else if (result is bool && (bool)result == false) {
					LogError ("Unknown Error: " + action);
				}
			} else if (result is bool && (bool)result == true) {
				DebugLog (action + " queued.");
			} else if (result is string) {
				Log (result.ToString ());
			}
		}

		internal void Log (string format, params object [] args)
		{
			Interface.Oxide.LogInfo ("[{0}] {1}", "Catalyst", args.Length > 0 ? string.Format (format, args) : format);
		}

		internal void DebugLog (string format, params object [] args)
		{
			if (!Settings.Debug) return;
			Interface.Oxide.LogDebug ("[{0}] {1}", "Catalyst", args.Length > 0 ? string.Format (format, args) : format);
		}

		internal void LogWarning (string format, params object [] args)
		{
			Interface.Oxide.LogWarning ("[{0}] {1}", "Catalyst", args.Length > 0 ? string.Format (format, args) : format);
		}

		internal void LogError (string format, params object [] args)
		{
			Interface.Oxide.LogError ("[{0}] {1}", "Catalyst", args.Length > 0 ? string.Format (format, args) : format);
		}

		internal void Puts (string format, params object [] args)
		{
			Interface.Oxide.LogInfo ("[{0}] {1}", "Catalyst", args.Length > 0 ? string.Format (format, args) : format);
		}
	}
}
