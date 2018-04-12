// COPYRIGHT 2016 RUSTSERVERS.IO
using System.Collections.Generic;

using Oxide.Core.Plugins;

namespace Oxide.Ext.Catalyst.Plugins
{
	public class PluginLoader : Core.Plugins.PluginLoader
	{
		CatalystExtension Extension;

		public PluginLoader (CatalystExtension extension)
		{
			Extension = extension;
		}

		public override IEnumerable<string> ScanDirectory (string directory)
		{
			return new string []
			{
				"Catalyst"
			};
		}

		public override Plugin Load (string directory, string name)
		{
			switch (name) {
			case "Catalyst":
				var catalystPlugin = new CovalencePlugin (Extension);
				if (LoadedPlugins.ContainsKey (name)) {
					LoadedPlugins.Remove (name);
				}
				LoadedPlugins.Add (name, catalystPlugin);
				return catalystPlugin;
			default:
				return null;
			}
		}
	}
}

