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
	    private void Init()
	    {
			library = Interface.Oxide.GetLibrary<Oxide.Ext.Catalyst.Libraries.Catalyst> ("Catalyst");
			Command command = Interface.Oxide.GetLibrary<Command>("Command");
			command.AddConsoleCommand("catalyst.update", this, "ccUpdate");
			command.AddConsoleCommand("catalyst.require", this, "ccRequire");
			command.AddConsoleCommand("catalyst.remove", this, "ccRemove");
			command.AddConsoleCommand("catalyst.validate", this, "ccValidate");
			command.AddConsoleCommand("catalyst.source", this, "ccSource");
			command.AddConsoleCommand("catalyst.config", this, "ccConfig");
			Interface.Oxide.LogInfo("[Catalyst] Loaded");
		}

		[HookMethod("ccConfig")]
		void ccConfig (ConsoleSystem.Arg arg)
		{
			if (arg.connection != null)
			{
				arg.ReplyWith("Permission Denied");
				return;
			}

			if (arg.Args.Length == 2 || arg.Args.Length == 3) {
				Plugin plugin = plugins.Find (arg.Args [0]);

				if (plugin != null) {
					string key = arg.Args [1];
					string[] parts = key.Split (new char[] {'.'});

					if (arg.Args.Length == 2) {
						object val = plugin.Config.Get (parts);
						if (val != null) {
							arg.ReplyWith (val.ToString ());
						} else {
							arg.ReplyWith ("No setting found");
						}
					} else {
						object val = arg.Args [2];
						bool bl = false;
						if (bool.TryParse (val.ToString(), out bl)) {
							val = bl;
						}
						plugin.Config.Set(parts, val);
					}
				} else {
					arg.ReplyWith("No plugin found");
				}
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

			if (arg.Args.Length == 1) {
				string name = arg.Args [0];

				if (!library.Settings.SourceList.Contains (name)) {
					library.Settings.SourceList.Add (name);
					Interface.Oxide.LogInfo ("[Catalyst] Source added!");
				} else {
					library.Settings.SourceList.Remove (name);
					Interface.Oxide.LogInfo ("[Catalyst] Source removed!");
				}
			} else {
				StringBuilder sb = new StringBuilder ();
				sb.AppendLine ("Sources:");
				foreach (string source in library.Settings.SourceList) {
					sb.AppendLine (source);
				}

				Interface.Oxide.LogInfo (sb.ToString ());
			}
		}

		[HookMethod("ccUpdate")]
		void ccUpdate(ConsoleSystem.Arg arg)
		{
			if (arg.connection != null)
			{
				arg.ReplyWith("Permission Denied");
				return;
			}

			if (arg.Args.Length == 1) {
				string name = arg.Args [0];
				if (library.Settings.Debug) {
					Debug.Log("Updating " + name);
				}

				library.BeginCommit();
				HandleResult (library.UpdatePlugin (name), "Updating " + name);
				library.EndCommit();
			} else {
				library.BeginCommit();
				foreach (KeyValuePair<string, string> kvp in library.Settings.Require) {
					HandleResult (library.UpdatePlugin (kvp.Key), "Updating " + kvp.Key);
				}
				library.EndCommit();
			}
		}

		[HookMethod("ccRequire")]
		void ccRequire (ConsoleSystem.Arg arg)
		{
			arg.ReplyWith("Requiring..");
			if (arg.connection != null)
			{
				arg.ReplyWith("Permission Denied");
				return;
			}

			if (arg.Args.Length == 1) {
				string name = arg.Args [0];

				if (library.Settings.Debug) {
					Debug.Log("Requiring " + name);
				}

				library.BeginCommit();
				HandleResult(library.InstallPlugin (name), "Installing " + name);
				library.EndCommit();
			}
		}

		[HookMethod("ccRemove")]
		void ccRemove(ConsoleSystem.Arg arg)
		{
			if (arg.connection != null)
			{
				arg.ReplyWith("Permission Denied");
				return;
			}

			if (arg.Args.Length == 1) {
				string name = arg.Args [0];

				if (library.Settings.Debug) {
					Debug.Log("Removing " + name);
				}

				library.BeginCommit();
				HandleResult(library.RemovePlugin (name), "Removing " + name);
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

			int errors = 0;

			if (library.Settings.Debug) {
				Debug.Log("Validating");
			}

			foreach (KeyValuePair<string, string> kvp in library.Settings.Require) {
				var obj = library.GetPluginInfo (kvp.Key);
				if (obj == null) {
					Interface.Oxide.LogWarning ("[Catalyst]" + kvp.Key + " does not exist");
					errors++;
				}
			}

			if (errors > 0) {
				Interface.Oxide.LogWarning ("[Catalyst] Validation failed");
			} else {
				Interface.Oxide.LogInfo ("[Catalyst] Validation success!");
			}
		}

		void HandleResult(object result, string action) {
			if (result is string || result is bool || result == null) {
				if (result is string) {
					Interface.Oxide.LogError ("[Catalyst] " + result.ToString ());
				} else {
					Interface.Oxide.LogError ("[Catalyst] Unknown Error: " + action);
				}
			} else {
				Interface.Oxide.LogInfo ("[Catalyst] " + action + " complete");
			}
		}
	}
}