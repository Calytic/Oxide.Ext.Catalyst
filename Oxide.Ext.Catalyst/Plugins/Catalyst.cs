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
	public class Catalyst : CSPlugin
	{
		private Core.Libraries.Plugins plugins = Interface.Oxide.GetLibrary<Core.Libraries.Plugins>();
		Oxide.Ext.Catalyst.Libraries.Catalyst library;
		private void Init()
		{
			library = Interface.Oxide.GetLibrary<Oxide.Ext.Catalyst.Libraries.Catalyst> ("Catalyst");
			Oxide.Game.Rust.Libraries.Command command = Interface.Oxide.GetLibrary<Oxide.Game.Rust.Libraries.Command>("Command");
			command.AddConsoleCommand("catalyst.update", (Plugin) this, "ccUpdate");
			command.AddConsoleCommand("catalyst.require", (Plugin) this, "ccRequire");
			command.AddConsoleCommand("catalyst.remove", (Plugin) this, "ccRemove");
			command.AddConsoleCommand("catalyst.validate", (Plugin) this, "ccValidate");
			command.AddConsoleCommand("catalyst.source", (Plugin) this, "ccSource");
			command.AddConsoleCommand("catalyst.config", (Plugin) this, "ccConfig");
		}

		[ConsoleCommand("catalyst.config")]
		void ccConfig (ConsoleSystem.Arg arg)
		{
			if (arg.connection != null && arg.connection.authLevel < 2) {
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

		[ConsoleCommand("catalyst.source")]
		void ccSource(ConsoleSystem.Arg arg)
		{
			if (arg.connection != null && arg.connection.authLevel < 2)
			{
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

		[ConsoleCommand("catalyst.update")]
		void ccUpdate(ConsoleSystem.Arg arg)
		{
			if (arg.connection != null && arg.connection.authLevel < 2)
			{
				return;
			}

			if (arg.Args.Length == 1) {
				string name = arg.Args [0];

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

		[ConsoleCommand("catalyst.require")]
		void ccRequire(ConsoleSystem.Arg arg)
		{
			if (arg.connection != null && arg.connection.authLevel < 2)
			{
				return;
			}

			if (arg.Args.Length == 1) {
				string name = arg.Args [0];

				library.BeginCommit();
				HandleResult(library.InstallPlugin (name), "Installing " + name);
				library.EndCommit();
			}
		}

		[ConsoleCommand("catalyst.remove")]
		void ccRemove(ConsoleSystem.Arg arg)
		{
			if (arg.connection != null && arg.connection.authLevel < 2)
			{
				return;
			}

			if (arg.Args.Length == 1) {
				string name = arg.Args [0];

				library.BeginCommit();
				HandleResult(library.RemovePlugin (name), "Removing " + name);
				library.EndCommit();
			}
		}

		[ConsoleCommand("catalyst.validate")]
		void ccValidate(ConsoleSystem.Arg arg)
		{
			if (arg.connection != null && arg.connection.authLevel < 2)
			{
				return;
			}

			int errors = 0;
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