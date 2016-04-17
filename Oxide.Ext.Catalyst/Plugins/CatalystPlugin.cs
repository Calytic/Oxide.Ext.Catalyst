// COPYRIGHT 2016 RUSTSERVERS.IO
using ConVar;
using Network;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using Oxide.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Timers;
using UnityEngine;


namespace Oxide.Ext.Catalyst.Plugins
{
	public abstract class CatalystPlugin : CovalencePlugin
	{
		private CatalystExtension Extension;
		private Oxide.Ext.Catalyst.Libraries.Catalyst library;

		internal CatalystPlugin(CatalystExtension extension)
	    {
			this.Name = "Catalyst";
			this.Title = "Catalyst Plugin Manager / RustServers.IO";
			this.Author = "Calytic";
			this.Version = extension.Version;
			this.HasConfig = true;
	    	Extension = extension;
	    }

		protected abstract void AddConsoleCommand(string command, string methodName);

		[HookMethod("Init")]
	    private void Init ()
		{
			library = Interface.Oxide.GetLibrary<Oxide.Ext.Catalyst.Libraries.Catalyst> ("Catalyst");
			if (library == null) 
			{
				LogError("[Catalyst] Library not found");
				return;
			}

			AddConsoleCommand ("catalyst.update", "ccUpdate");
			AddConsoleCommand ("catalyst.require", "ccRequire");
			AddConsoleCommand ("catalyst.remove", "ccRemove");
			AddConsoleCommand ("catalyst.validate", "ccValidate");
			AddConsoleCommand ("catalyst.source", "ccSource");
			AddConsoleCommand ("catalyst.config", "ccConfig");
			AddConsoleCommand ("catalyst.search", "ccSearch");
			AddConsoleCommand ("catalyst.info", "ccInfo");
			AddConsoleCommand ("catalyst.debug", "ccDebug");

			if (library.Settings.Debug) 
			{
				Log ("[Catalyst] Loaded");
			}
		}

		protected void Debug (string debugArg = null)
		{
			bool debug;
			if (string.IsNullOrEmpty (debugArg)) {
				debug = !library.Settings.Debug;
			}

			if (!bool.TryParse (debugArg, out debug)) {
				LogError("Debug mode must be true/false");
				return;
			}

			library.Settings.Debug = debug;

			if(library.Settings.Debug) {
				Log("[Catalyst] Debug mode: enabled");
			} else {
				Log("[Catalyst] Debug mode: disabled");
			}
		}

		protected void Configure (string pluginName, string key, object value = null)
		{
			Plugin plugin = plugins.Find (pluginName);
			if (plugin == null) {
				Log ("No plugin found");
				return;
			}

			string[] parts = key.Split (new char[] { '.' });

			if (value == null) 
			{
				object val = plugin.Config.Get (parts);
				if (val != null) 
				{
					Log (key + " : " + val.ToString ());
				} 
				else 
				{
					Log ("No setting found");
				}
			} 
			else 
			{
				bool bl = false;
				if (bool.TryParse (value.ToString (), out bl)) 
				{
					value= bl;
				}
				plugin.Config.Set (parts, value);
				plugin.Config.Save ();
			}
		}

		protected void Source (string name = null)
		{
			if (name != null) 
			{
				if (!library.Settings.SourceList.Contains (name)) 
				{
					library.Settings.SourceList.Add (name);
					Log ("[Catalyst] Source added!");
				} 
				else 
				{
					library.Settings.SourceList.Remove (name);
					Log ("[Catalyst] Source removed!");
				}
			} 
			else 
			{
				StringBuilder sb = new StringBuilder ();
				sb.AppendLine ("Sources:");
				foreach (string source in library.Settings.SourceList) 
				{
					sb.AppendLine (source);
				}

				Log (sb.ToString ());
			}
		}

		protected void Search (string terms)
		{
			string[] plugins = library.FindPlugin (terms);

			if (plugins.Length > 0) 
			{
				StringBuilder sb = new StringBuilder();
				int i = 1;
				sb.AppendLine ("Found (" + plugins.Length + ")");
				foreach (string plugin in plugins) 
				{
					sb.AppendLine (i + ". " + plugin);
					i++;
				}

				Log(sb.ToString());
			} 
			else 
			{
				Log("No Plugin Found!");
			}
		}

		protected void Update (params string[] names)
		{
			if (names.Length == 0) 
			{
				library.BeginCommit ();

				foreach (KeyValuePair<string, string> kvp in library.Settings.Require) 
				{
					if (library.PluginExists (kvp.Key)) 
					{
						HandleResult (library.UpdatePlugin (kvp.Key), "Updating " + kvp.Key);
					}
				}
				library.EndCommit();
			} 
			else 
			{
				library.BeginCommit ();
				foreach (string name in names) 
				{
					if (library.Settings.Debug) 
					{
						Log ("[Catalyst] Updating " + name);
					}

					if (!library.PluginExists (name) && names.Length == 1) 
					{
						Search(name);
						return;
					}
					HandleResult (library.UpdatePlugin (name), "Updating " + name);
				}

				library.EndCommit ();
			}
		}

		protected void Require (string name, string version)
		{
			library.BeginCommit ();

			if (library.Settings.Debug) 
			{
				Log ("[Catalyst] Requiring " + name);
			}

			if (!library.PluginExists (name, version)) 
			{
				Search (name);
				return;
			}

			HandleResult (library.InstallPlugin (name, version), "Installing " + name);

			library.EndCommit ();
		}

		protected void Remove (params string[] names)
		{
			library.BeginCommit();
			foreach (string name in names) {
				if (library.Settings.Debug) 
				{
					Log ("[Catalyst] Removing " + name);
				}

				HandleResult(library.RemovePlugin (name), "Removing " + name);
			}

			library.EndCommit();
		}

		protected void Validate () {
			bool errors = false;

			if (library.Settings.Debug) 
			{
				Log ("[Catalyst] Validating");
			}

			library.BeginCommit();
			HandleResult(library.Validate(), "Validating");
			errors = library.HasErrors;
			library.EndCommit();

			if (errors) 
			{
				LogWarning ("[Catalyst] Validation failed");
			} else {
				Log ("[Catalyst] Validation success!");
			}
		}

		protected void Info(string name) 
		{
			library.BeginCommit ();
			if (!library.PluginExists (name)) 
			{
				LogWarning ("[Catalyst] No plugin found");
				return;
			}

			JObject pluginInfo = library.GetPluginInfo (name);

			StringBuilder sb = new StringBuilder ();

			if (pluginInfo ["plugin"] == null) 
			{
				LogWarning ("[Catalyst] Plugin invalid");
			}

			name = pluginInfo ["name"].ToString();
			string desc = pluginInfo["plugin"]["description"].ToString();
			string author = pluginInfo["plugin"]["author"].ToString();
			string version = pluginInfo["plugin"]["version"].ToString();

			sb.AppendLine(name + " by " + author);
			sb.AppendLine("Version: " + version);
			sb.AppendLine("Description: " + desc);

			var requires = pluginInfo ["plugin"] ["require"];
			if (requires != null) 
			{
				sb.AppendLine("Require: "); 
				foreach (string require in requires) {
					sb.AppendLine(require);	
				}
			}

			Log (sb.ToString());

			library.EndCommit();	
		}

		void HandleResult(object result, string action) 
		{
			if ((result is string || result is bool || result == null) && !library.IsCommitting) 
			{
				if (result is string) 
				{
					LogError ("[Catalyst] " + result.ToString ());
				} 
				else if (result is bool && (bool)result == false) 
				{
					LogError ("[Catalyst] Unknown Error: " + action);
				}
			} 
			else if(result is bool && (bool)result == true) 
			{
				Log ("[Catalyst] " + action + " queued.");
			}
		}
	}
}