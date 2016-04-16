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
using Oxide.Game.Rust.Libraries;

namespace Oxide.Ext.Catalyst.Plugins
{
	public class Catalyst : CSPlugin
	{
		private CatalystExtension Extension;
		private Oxide.Ext.Catalyst.Libraries.Catalyst library;
		private Core.Libraries.Plugins plugins = Interface.Oxide.GetLibrary<Core.Libraries.Plugins>();

		public Catalyst(CatalystExtension extension)
	    {
			this.Name = "Catalyst";
			this.Title = "Catalyst Plugin Manager / RustServers.IO";
			this.Author = "Calytic";
			this.Version = extension.Version;
			this.HasConfig = true;
	    	Extension = extension;
	    }

		[HookMethod("Init")]
	    private void Init ()
		{
			library = Interface.Oxide.GetLibrary<Oxide.Ext.Catalyst.Libraries.Catalyst> ("Catalyst");
			if (library == null) 
			{
				Interface.Oxide.LogError("[Catalyst] Library not found");
				return;
			}
			Command command = Interface.Oxide.GetLibrary<Command> ("Command");
			command.AddConsoleCommand ("catalyst.update", this, "ccUpdate");
			command.AddConsoleCommand ("catalyst.require", this, "ccRequire");
			command.AddConsoleCommand ("catalyst.remove", this, "ccRemove");
			command.AddConsoleCommand ("catalyst.validate", this, "ccValidate");
			command.AddConsoleCommand ("catalyst.source", this, "ccSource");
			command.AddConsoleCommand ("catalyst.config", this, "ccConfig");
			command.AddConsoleCommand ("catalyst.search", this, "ccSearch");
			command.AddConsoleCommand ("catalyst.info", this, "ccInfo");
			command.AddConsoleCommand ("catalyst.debug", this, "ccDebug");

			if (library.Settings.Debug) 
			{
				Interface.Oxide.LogInfo ("[Catalyst] Loaded");
			}
		}

		[HookMethod("ccDebug")]
		void ccDebug (ConsoleSystem.Arg arg)
		{
			if (arg.connection != null) {
				arg.ReplyWith ("Permission Denied");
				return;
			}

			if (arg.Args.Length == 1) {
				bool debug;
				if(bool.TryParse(arg.Args[0], out debug))
				{
					library.Settings.Debug = debug;
				}
				
			} else {
				library.Settings.Debug = !library.Settings.Debug;
			}

			if(library.Settings.Debug) {
				Interface.Oxide.LogInfo("[Catalyst] Debug mode: enabled");
			} else {
				Interface.Oxide.LogInfo("[Catalyst] Debug mode: disabled");
			}
		}

		[HookMethod("ccConfig")]
		void ccConfig (ConsoleSystem.Arg arg)
		{
			if (arg.connection != null) 
			{
				arg.ReplyWith ("Permission Denied");
				return;
			}

			if (arg.Args != null && arg.Args.Length == 2 || arg.Args.Length == 3) 
			{
				Plugin plugin = plugins.Find (arg.Args [0]);

				if (plugin != null) {
					string key = arg.Args [1];
					string[] parts = key.Split (new char[] { '.' });

					if (arg.Args.Length == 2) 
					{
						object val = plugin.Config.Get (parts);
						if (val != null) 
						{
							Interface.Oxide.LogInfo (key + " : " + val.ToString ());
						} 
						else 
						{
							Interface.Oxide.LogInfo ("No setting found");
						}
					} 
					else 
					{
						object val = arg.Args [2];
						bool bl = false;
						if (bool.TryParse (val.ToString (), out bl)) 
						{
							val = bl;
						}
						plugin.Config.Set (parts, val);
						plugin.Config.Save ();
					}
				} 
				else 
				{
					Interface.Oxide.LogInfo ("No plugin found");
				}
			}
			else 
			{
				Interface.Oxide.LogInfo ("catalyst.config setting [value]");
			}
		}

		[HookMethod("ccSource")]
		void ccSource(ConsoleSystem.Arg arg)
		{
			if (arg.connection != null)
			{
				arg.ReplyWith("Permission Denied");
				return;
			}

			if (arg.Args != null && arg.Args.Length == 1) 
			{
				string name = arg.Args [0];

				if (!library.Settings.SourceList.Contains (name)) 
				{
					library.Settings.SourceList.Add (name);
					Interface.Oxide.LogInfo ("[Catalyst] Source added!");
				} 
				else 
				{
					library.Settings.SourceList.Remove (name);
					Interface.Oxide.LogInfo ("[Catalyst] Source removed!");
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

				Interface.Oxide.LogInfo (sb.ToString ());
			}
		}

		[HookMethod("ccSearch")]
		void ccSearch (ConsoleSystem.Arg arg)
		{
			if (arg.connection != null) 
			{
				arg.ReplyWith ("Permission Denied");
				return;
			}

			if (arg.Args != null && arg.Args.Length > 0) 
			{
				string name = string.Join(" ", arg.Args).Trim();;
				string[] plugins = library.FindPlugin (name);

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

					Interface.Oxide.LogInfo(sb.ToString());
				} 
				else 
				{
					Interface.Oxide.LogInfo("No Plugin Found!");
				}
			} 
			else 
			{
				Interface.Oxide.LogInfo("catalyst.search PluginName [PluginName] ...");
			}
		}

		[HookMethod("ccUpdate")]
		void ccUpdate (ConsoleSystem.Arg arg)
		{
			if (arg.connection != null) 
			{
				arg.ReplyWith ("Permission Denied");
				return;
			}

			if (arg.Args != null && arg.Args.Length > 0 && arg.Args [0] != "*") 
			{
				library.BeginCommit ();
				foreach (string name in arg.Args) 
				{
					if (library.Settings.Debug) 
					{
						Interface.Oxide.LogInfo ("[Catalyst] Updating " + name);
					}

					if (!library.PluginExists (name) && arg.Args.Length == 1) 
					{
						ccSearch (arg);
						return;
					}
					HandleResult (library.UpdatePlugin (name), "Updating " + name);
				}

				library.EndCommit ();
			} 
			else 
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
		}

		[HookMethod("ccRequire")]
		void ccRequire (ConsoleSystem.Arg arg)
		{
			if (arg.connection != null) 
			{
				arg.ReplyWith ("Permission Denied");
				return;
			}

			if (arg.Args != null && arg.Args.Length > 0) 
			{
				library.BeginCommit ();

				string name = arg.Args [0];
				string version = "*";
				if (arg.Args.Length == 2) 
				{
					version = arg.Args[1];
				}

				if (library.Settings.Debug) 
				{
					Interface.Oxide.LogInfo ("[Catalyst] Requiring " + name);
				}

				if (!library.PluginExists (name, version)) 
				{
					ccSearch (arg);
					return;
				}

				HandleResult (library.InstallPlugin (name, version), "Installing " + name);

				library.EndCommit ();
			} 
			else 
			{
				Interface.Oxide.LogInfo ("catalyst.require PluginName [PluginName] ..." );
			}
		}

		[HookMethod("ccRemove")]
		void ccRemove (ConsoleSystem.Arg arg)
		{
			if (arg.connection != null) 
			{
				arg.ReplyWith ("Permission Denied");
				return;
			}

			if (arg.Args != null && arg.Args.Length > 0) 
			{
				library.BeginCommit();
				foreach (string name in arg.Args) {
					if (library.Settings.Debug) 
					{
						Interface.Oxide.LogInfo ("[Catalyst] Removing " + name);
					}

					HandleResult(library.RemovePlugin (name), "Removing " + name);
				}

				library.EndCommit();
			}
		}

		[HookMethod("ccValidate")]
		void ccValidate(ConsoleSystem.Arg arg)
		{
			if (arg.connection != null)
			{
				arg.ReplyWith("Permission Denied");
				return;
			}

			bool errors = false;

			if (library.Settings.Debug) 
			{
				Interface.Oxide.LogInfo ("[Catalyst] Validating");
			}

			library.BeginCommit();
			HandleResult(library.Validate(), "Validating");
			errors = library.HasErrors;
			library.EndCommit();

			if (errors) 
			{
				Interface.Oxide.LogWarning ("[Catalyst] Validation failed");
			} else {
				Interface.Oxide.LogInfo ("[Catalyst] Validation success!");
			}
		}

		[HookMethod("ccInfo")]
		void ccInfo (ConsoleSystem.Arg arg)
		{
			if (arg.connection != null) 
			{
				arg.ReplyWith ("Permission Denied");
				return;
			}

			if (arg.Args != null && arg.Args.Length == 1) 
			{
				string name = arg.Args [0];

				library.BeginCommit ();
				if (!library.PluginExists (name)) 
				{
					Interface.Oxide.LogWarning ("[Catalyst] No plugin found");
					return;
				}

				JObject pluginInfo = library.GetPluginInfo (name);

				StringBuilder sb = new StringBuilder ();

				if (pluginInfo ["plugin"] == null) 
				{
					Interface.Oxide.LogWarning ("[Catalyst] Plugin invalid");
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

				Interface.Oxide.LogInfo (sb.ToString());

				library.EndCommit();
			} 
			else 
			{
				Interface.Oxide.LogInfo ("catalyst.info PluginName");
			}
		}

		void HandleResult(object result, string action) 
		{
			if ((result is string || result is bool || result == null) && !library.IsCommitting) 
			{
				if (result is string) 
				{
					Interface.Oxide.LogError ("[Catalyst] " + result.ToString ());
				} 
				else if (result is bool && (bool)result == false) 
				{
					Interface.Oxide.LogError ("[Catalyst] Unknown Error: " + action);
				}
			} 
			else if(result is bool && (bool)result == true) 
			{
				Interface.Oxide.LogInfo ("[Catalyst] " + action + " queued.");
			}
		}
	}
}