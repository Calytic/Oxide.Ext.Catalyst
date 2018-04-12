// COPYRIGHT 2016 RUSTSERVERS.IO
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Ext.Catalyst.Plugins
{
	public class CovalencePlugin : CatalystPlugin
	{
		public CovalencePlugin (CatalystExtension extension) : base (extension)
		{

		}


		protected override void AddConsoleCommand (string command, string methodName)
		{
			AddCovalenceCommand (new [] { "catalyst." + command, "c." + command }, methodName);
		}

		protected override string GetDefaultGame ()
		{
			return "rust";
		}

		[HookMethod ("ccVersion")]
		protected override void ccVersion (IPlayer player, string command, string [] args)
		{
			if (!player.IsServer) {
				player.Reply ("Permission Denied");
				return;
			}

			GetVersion ();
		}

		[HookMethod ("ccStatus")]
		protected override void ccStatus (IPlayer player, string command, string [] args)
		{
			if (!player.IsServer) {
				player.Reply ("Permission Denied");
				return;
			}

			Status ();
		}

		[HookMethod ("ccSync")]
		protected override void ccSync (IPlayer player, string command, string [] args)
		{
			if (!player.IsServer) {
				player.Reply ("Permission Denied");
				return;
			}

			Sync ();
		}

		[HookMethod ("ccCheck")]
		protected override void ccCheck (IPlayer player, string command, string [] args)
		{
			if (!player.IsServer) {
				player.Reply ("Permission Denied");
				return;
			}

			Check (args);
		}

		[HookMethod ("ccDebug")]
		protected override void ccDebug (IPlayer player, string command, string [] args)
		{
			if (!player.IsServer) {
				player.Reply ("Permission Denied");
				return;
			}

			if (args != null && args.Length == 1) {
				Debug (args [0]);
			} else {
				Debug ();
			}
		}

		[HookMethod ("ccConfig")]
		protected override void ccConfig (IPlayer player, string command, string [] args)
		{
			if (!player.IsServer) {
				player.Reply ("Permission Denied");
				return;
			}

			if (args != null && args.Length == 2 || args.Length == 3) {
				string plugin = args [0];
				string key = args [1];
				string value = null;
				if (args.Length > 2) {
					value = args [2];
				}

				Configure (plugin, key, value);
			} else {
				Log ("catalyst.config Plugin setting [value]");
			}
		}

		[HookMethod ("ccSource")]
		protected override void ccSource (IPlayer player, string command, string [] args)
		{
			if (!player.IsServer) {
				player.Reply ("Permission Denied");
				return;
			}

			if (args != null && args.Length == 1) {
				Source (args [0]);
			} else {
				Source ();
			}
		}

		[HookMethod ("ccSearch")]
		protected override void ccSearch (IPlayer player, string command, string [] args)
		{
			if (!player.IsServer) {
				player.Reply ("Permission Denied");
				return;
			}

			if (args != null && args.Length > 0) {
				var terms = string.Join (" ", args).Trim ();
				Search (terms);
			} else {
				Log ("catalyst.search PluginName [PluginName] ...");
			}
		}

		[HookMethod ("ccUpdate")]
		protected override void ccUpdate (IPlayer player, string command, string [] args)
		{
			if (!player.IsServer) {
				player.Reply ("Permission Denied");
				return;
			}

			if (args != null && args.Length > 0 && args [0] != "*") {
				Update (args);
			} else {
				Update ();
			}
		}

		[HookMethod ("ccRequire")]
		protected override void ccRequire (IPlayer player, string command, string [] args)
		{
			if (!player.IsServer) {
				player.Reply ("Permission Denied");
				return;
			}

			if (args != null && args.Length > 0) {
				string name = args [0];
				string version = "*";
				if (args.Length == 2) {
					version = args [1];
				}

				Require (name, version);
			} else {
				Log ("catalyst.require PluginName [Version] ...");
			}
		}

		[HookMethod ("ccRequireAll")]
		protected override void ccRequireAll (IPlayer player, string command, string [] args)
		{
			if (!player.IsServer) {
				player.Reply ("Permission Denied");
				return;
			}

			if (args != null && args.Length > 0) {
				foreach (string name in args) {
					string version = "*";
					if (args.Length == 2) {
						version = args [1];
					}

					Require (name, version);
				}
			} else {
				Log ("catalyst.require PluginName [PluginName] ...");
			}
		}

		[HookMethod ("ccRemove")]
		protected override void ccRemove (IPlayer player, string command, string [] args)
		{
			if (!player.IsServer) {
				player.Reply ("Permission Denied");
				return;
			}

			if (args != null && args.Length > 0) {
				Remove (args);
			} else {
				Log ("catalyst.remove PluginName [PluginName] [...]");
			}
		}

		[HookMethod ("ccValidate")]
		protected override void ccValidate (IPlayer player, string command, string [] args)
		{
			if (!player.IsServer) {
				player.Reply ("Permission Denied");

				return;
			}

			Validate ();
		}

		[HookMethod ("ccInfo")]
		protected override void ccInfo (IPlayer player, string command, string [] args)
		{
			if (!player.IsServer) {
				player.Reply ("Permission Denied");
				return;
			}

			if (args != null && args.Length == 1) {
				string name = args [0];

				Info (name);
			} else {
				Log ("catalyst.info PluginName");
			}
		}
	}
}

