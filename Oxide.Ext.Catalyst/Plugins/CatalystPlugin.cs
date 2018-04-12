// COPYRIGHT 2016 RUSTSERVERS.IO
using Newtonsoft.Json.Linq;

using System.Collections.Generic;
using System.Linq;
using System.Text;

using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;


namespace Oxide.Ext.Catalyst.Plugins
{
	public abstract class CatalystPlugin : CSPlugin
	{
		public class StatusQuery : Libraries.Catalyst.CommitQuery
		{
			public StatusQuery (Libraries.Catalyst library) : base (library)
			{

			}

			public override void Handle ()
			{
				library.BeginCommit ();
				var statusChanges = library.GetStatus ();
				library.EndCommit ();

				if (statusChanges.Count == 0) {
					library.Log ("No changes to plugins.  Everything up to date and matching public sources");
				} else {
					foreach (KeyValuePair<Libraries.Catalyst.StatusMessage, string> kvp in statusChanges) {
						library.Log (kvp.Value);
					}
				}
			}
		}

		public class SearchQuery : Libraries.Catalyst.CommitQuery
		{
			string terms;
			public SearchQuery (Libraries.Catalyst library, string terms) : base (library)
			{
				this.terms = terms;
			}

			public override void Handle ()
			{
				var plugins = library.FindPlugin (terms);

				if (plugins.Length > 0) {
					var sb = new StringBuilder ();
					int i = 1;
					sb.AppendLine ("Found (" + plugins.Length + ")");
					foreach (string plugin in plugins) {
						sb.AppendLine (i + ". " + plugin);
						i++;
					}

					library.Log (sb.ToString ());
				} else {
					library.Log ("No Plugin Found!");
				}
			}
		}

		public class UpdateQuery : Libraries.Catalyst.CommitQuery
		{
			string [] names;

			public UpdateQuery (Libraries.Catalyst library, params string [] names) : base (library)
			{
				this.names = names;
			}

			public override void Handle ()
			{
				if (names.Length == 0) {
					if (library.Settings.Require.Count == 0) {
						library.Log ("No plugins found. Run \"catalyst.sync\" to synchronize your plugins directory with Catalyst");
						return;
					}
					library.BeginCommit ();

					foreach (KeyValuePair<string, string> kvp in library.Settings.Require) {
						Libraries.Catalyst.PluginStore store;
						if (library.TryGetStore (kvp.Key, out store) && !store.Matching) {
							library.RemoveStore (kvp.Key);
						}
						var plugin = library.PluginExists (kvp.Key);
						if (plugin is JObject) {
							var result = library.UpdatePlugin ((JObject)plugin);
							library.HandleResult (result, "Updating " + kvp.Key);
						}
					}
					library.EndCommit ();
				} else {
					library.BeginCommit ();
					foreach (string name in names) {
						Libraries.Catalyst.PluginStore store;
						if (library.TryGetStore (name, out store) && !store.Matching) {
							library.RemoveStore (name);
						}

						var pluginResult = library.PluginExists (name);

						if (!(pluginResult is JObject) && names.Length == 1) {
							library.DebugLog ("Plugin " + name + " not found, searching instead");
							library.Enqueue (new SearchQuery (library, name));
							return;
						}

						library.DebugLog ("Updating " + name);
						var updateResult = library.UpdatePlugin ((JObject)pluginResult);
						library.HandleResult (updateResult, "Updating " + name);
					}

					library.EndCommit ();
				}
			}
		}

		public class SyncQuery : Libraries.Catalyst.CommitQuery
		{
			PluginManager Manager;

			public SyncQuery (Libraries.Catalyst library, PluginManager Manager) : base (library)
			{
				this.Manager = Manager;
			}

			public override void Handle ()
			{
				library.BeginCommit ();

				var plugins = Manager.GetPlugins ().ToList ();

				int i = 0;
				foreach (Plugin plugin in plugins) {
					if (!plugin.IsCorePlugin && plugin.ResourceId > 0) {
						var result = library.PluginExists (plugin.Name);

						if (result is JObject) {
							if (!library.Settings.Require.ContainsKey (plugin.Name)) {
								var requireResult = library.RequirePlugin ((JObject)result);
								library.HandleResult (requireResult, "Required " + plugin.Name);
								i++;
							}
						} else {
							library.Log ("Plugin Not Found. " + plugin.Name);
						}
					}
				}

				library.Log ("Synchronized plugins (" + i + ")");

				library.EndCommit ();
			}
		}

		public class RequireQuery : Libraries.Catalyst.CommitQuery
		{
			string name;
			string version;

			public RequireQuery (Libraries.Catalyst library, string name, string version) : base (library)
			{
				this.name = name;
				this.version = version;
			}

			public override void Handle ()
			{
				library.BeginCommit ();

				library.DebugLog ("Requiring " + name);

				var result = library.PluginExists (name, version);

				if (!(result is JObject)) {
					library.Enqueue (new SearchQuery (library, name));
					return;
				}

				library.HandleResult (library.InstallPlugin ((JObject)result, version), "Installing " + name);

				library.EndCommit ();
			}
		}

		public class CheckQuery : Libraries.Catalyst.CommitQuery
		{
			string [] names;

			public CheckQuery (Libraries.Catalyst library, params string [] names) : base (library)
			{
				this.names = names;
			}

			public override void Handle ()
			{
				var resultSet = new List<string> ();
				if (names == null || (names != null && names.Length == 0)) {
					if (library.Settings.Require.Count == 0) {
						library.Log ("No plugins found. Run \"catalyst.sync\" to synchronize your plugins directory with Catalyst");
						return;
					}

					foreach (KeyValuePair<string, string> kvp in library.Settings.Require) {
						var plugin = library.PluginExists (kvp.Key);
						if (plugin is JObject) {
							var name = ((JObject)plugin) ["name"].ToString ();
							library.DebugLog ("Checking " + name);
							if (!library.CheckPlugin (name)) {
								resultSet.Add (name);
							}
						}
					}
				} else {
					foreach (string name in names) {
						library.DebugLog ("Finding " + name);
						var pluginResult = library.PluginExists (name);

						if (!(pluginResult is JObject) && names.Length == 1) {
							library.DebugLog ("Plugin " + name + " not found, searching instead");
							library.Enqueue (new SearchQuery (library, name));
							return;
						}

						library.DebugLog ("Checking " + name);
						var pluginName = ((JObject)pluginResult) ["name"].ToString ();
						if (!library.CheckPlugin (pluginName)) {
							resultSet.Add (pluginName);
						}
					}
				}

				if (resultSet.Count > 0) {
					library.SaveCache ();
					library.LogWarning ("The following plugins require updates: {0}", string.Join (", ", resultSet.ToArray ()));
				} else {
					library.Log ("All plugins are up to date");
				}
			}
		}

		public class RemoveQuery : Libraries.Catalyst.CommitQuery
		{
			string [] names;

			public RemoveQuery (Libraries.Catalyst library, params string [] names) : base (library)
			{
				this.names = names;
			}

			public override void Handle ()
			{
				library.BeginCommit ();
				foreach (string name in names) {
					library.DebugLog ("Removing " + name);

					library.HandleResult (library.RemovePlugin (name), "Removed " + name);
				}

				library.EndCommit ();
			}
		}

		public class ValidateQuery : Libraries.Catalyst.CommitQuery
		{
			public ValidateQuery (Libraries.Catalyst library) : base (library)
			{

			}

			public override void Handle ()
			{
				bool errors = false;

				library.DebugLog ("Validating");

				library.BeginCommit ();
				library.HandleResult (library.Validate (), "Validating");
				errors = library.HasErrors;
				library.EndCommit ();

				if (errors) {
					library.LogWarning ("Validation failed");
				} else {
					library.Log ("Validation success!");
				}
			}
		}



		public class InfoQuery : Libraries.Catalyst.CommitQuery
		{
			string name;

			public InfoQuery (Libraries.Catalyst library, string name) : base (library)
			{
				this.name = name;
			}

			public override void Handle ()
			{
				library.BeginCommit ();
				var result = library.PluginExists (name);
				if (!(result is JObject)) {
					library.LogWarning ("No plugin found");
					return;
				}

				var pluginInfo = (JObject)result;

				var sb = new StringBuilder ();

				if (!library.IsPluginValid (pluginInfo)) {
					library.LogWarning ("Plugin invalid");
					return;
				}

				name = pluginInfo ["name"].ToString ();
				var desc = pluginInfo ["plugin"] ["description"].ToString ();
				var author = pluginInfo ["plugin"] ["author"].ToString ();
				var version = pluginInfo ["plugin"] ["version"].ToString ();

				sb.AppendLine (name + " by " + author);
				sb.AppendLine ("Version: " + version);
				sb.AppendLine ("Description: " + desc);

				var requires = pluginInfo ["plugin"] ["require"];
				if (requires != null && requires.Count () > 0) {
					sb.AppendLine ("Require: ");
					foreach (string require in requires) {
						sb.AppendLine (require);
					}
				}

				library.Log (sb.ToString ());

				library.EndCommit ();
			}
		}

		CatalystExtension Extension;
		protected Libraries.Catalyst library;
		Core.Libraries.Plugins plugins;

		protected CatalystPlugin (CatalystExtension extension)
		{
			Name = "Catalyst";
			Title = "Catalyst";
			Author = "RustServers.IO";
			Version = extension.Version;
			HasConfig = true;
			Extension = extension;
		}

		protected abstract void AddConsoleCommand (string command, string methodName);

		[HookMethod ("Init")]
		void Init ()
		{
			plugins = Interface.Oxide.GetLibrary<Core.Libraries.Plugins> ();
			library = Interface.Oxide.GetLibrary<Libraries.Catalyst> ();
			library.currentGame = GetDefaultGame ();
			if (library == null) {
				LogError ("Library not found");
				return;
			}

			AddConsoleCommand ("update", "ccUpdate");
			AddConsoleCommand ("check", "ccCheck");
			AddConsoleCommand ("require", "ccRequire");
			AddConsoleCommand ("remove", "ccRemove");
			AddConsoleCommand ("validate", "ccValidate");
			AddConsoleCommand ("source", "ccSource");
			AddConsoleCommand ("config", "ccConfig");
			AddConsoleCommand ("search", "ccSearch");
			AddConsoleCommand ("info", "ccInfo");
			AddConsoleCommand ("debug", "ccDebug");
			AddConsoleCommand ("status", "ccStatus");
			AddConsoleCommand ("sync", "ccSync");
			AddConsoleCommand ("version", "ccVersion");

			Check ();
		}

		[HookMethod ("OnPluginLoaded")]
		void OnPluginLoaded (Plugin plugin)
		{
			Libraries.Catalyst.PluginStore store;
			if (library.TryGetStore (plugin.Name, out store)) {
				store.PluginVersion = plugin.Version.ToString ();

				library.SaveCache ();
			}
		}

		protected abstract string GetDefaultGame ();
		protected abstract void ccUpdate (IPlayer player, string command, string [] args);
		protected abstract void ccRequire (IPlayer player, string command, string [] args);
		protected abstract void ccRequireAll (IPlayer player, string command, string [] args);
		protected abstract void ccRemove (IPlayer player, string command, string [] args);
		protected abstract void ccValidate (IPlayer player, string command, string [] args);
		protected abstract void ccSource (IPlayer player, string command, string [] args);
		protected abstract void ccConfig (IPlayer player, string command, string [] args);
		protected abstract void ccSearch (IPlayer player, string command, string [] args);
		protected abstract void ccInfo (IPlayer player, string command, string [] args);
		protected abstract void ccDebug (IPlayer player, string command, string [] args);
		protected abstract void ccStatus (IPlayer player, string command, string [] args);
		protected abstract void ccSync (IPlayer player, string command, string [] args);
		protected abstract void ccVersion (IPlayer player, string command, string [] args);
		protected abstract void ccCheck (IPlayer player, string command, string [] args);

		protected void GetVersion ()
		{
			Log ("Version: " + Extension.Version.ToString ());
		}

		protected void Status ()
		{
			library.Enqueue (new StatusQuery (library));
		}

		protected void Check (params string [] names)
		{
			library.Enqueue (new CheckQuery (library, names));
		}

		protected void Debug (string debugArg = null)
		{
			bool debug;
			if (string.IsNullOrEmpty (debugArg)) {
				debug = !library.Settings.Debug;
			}

			if (!bool.TryParse (debugArg, out debug)) {
				LogError ("Debug mode must be true/false");
				return;
			}

			library.Settings.Debug = debug;

			if (library.Settings.Debug) {
				Log ("Debug mode: enabled");
			} else {
				Log ("Debug mode: disabled");
			}
		}

		protected void Configure (string pluginName, string key, object value = null)
		{
			var plugin = plugins.Find (pluginName);
			if (plugin == null) {
				Log ("No plugin found");
				return;
			}

			var parts = key.Split (new char [] { '.' });

			if (value == null) {
				var val = plugin.Config.Get (parts);
				if (val != null) {
					Log (key + " : " + val);
				} else {
					Log ("No setting found");
				}
			} else {
				bool bl = false;
				if (bool.TryParse (value.ToString (), out bl)) {
					value = bl;
				}
				plugin.Config.Set (parts, value);
				plugin.Config.Save ();
			}
		}

		protected void Source (string name = null)
		{
			if (name != null) {
				if (!library.Settings.SourceList.Contains (name)) {
					library.Settings.SourceList.Add (name);
					Log ("Source added!");
				} else {
					library.Settings.SourceList.Remove (name);
					Log ("Source removed!");
				}
			} else {
				var sb = new StringBuilder ();
				sb.AppendLine ("Sources:");
				foreach (string source in library.Settings.SourceList) {
					sb.AppendLine (source);
				}

				Log (sb.ToString ());
			}
		}

		protected void Search (string terms)
		{
			library.Enqueue (new SearchQuery (library, terms));
		}

		protected void Update (params string [] names)
		{
			if (names.Length == 1 && names [0].Trim () == "*") {
				names = new string [0];
			}
			library.Enqueue (new UpdateQuery (library, names));
		}

		protected void Sync ()
		{
			library.Enqueue (new SyncQuery (library, Manager));
		}

		protected void Require (string name, string version)
		{
			library.Enqueue (new RequireQuery (library, name, version));
		}

		protected void Remove (params string [] names)
		{
			library.Enqueue (new RemoveQuery (library, names));
		}

		protected void Validate ()
		{
			library.Enqueue (new ValidateQuery (library));
		}

		protected void Info (string name)
		{
			library.Enqueue (new InfoQuery (library, name));
		}

		protected void Game (string name)
		{
			if (library.games.Contains (name.Trim ().ToLower ())) {
				library.currentGame = name.Trim ().ToLower ();
			} else {
				LogWarning ("No such game '{0}'", name);
			}
		}

		protected void Log (string format, params object [] args)
		{
			Interface.Oxide.LogInfo ("[{0}] {1}", Title, args.Length > 0 ? string.Format (format, args) : format);
		}

		protected void DebugLog (string format, params object [] args)
		{
			if (!library.Settings.Debug) return;
			Interface.Oxide.LogInfo ("[{0}] {1}", Title, args.Length > 0 ? string.Format (format, args) : format);
		}

		protected void LogWarning (string format, params object [] args)
		{
			Interface.Oxide.LogWarning ("[{0}] {1}", Title, args.Length > 0 ? string.Format (format, args) : format);
		}

		protected void LogError (string format, params object [] args)
		{
			Interface.Oxide.LogError ("[{0}] {1}", Title, args.Length > 0 ? string.Format (format, args) : format);
		}

		protected void Puts (string format, params object [] args)
		{
			Interface.Oxide.LogInfo ("[{0}] {1}", Title, args.Length > 0 ? string.Format (format, args) : format);
		}
	}
}