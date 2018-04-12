// COPYRIGHT 2016 RUSTSERVERS.IO
using System;
using System.Collections.Generic;

using Newtonsoft.Json.Linq;

namespace Oxide.Ext.Catalyst
{
	class DependencyValidator
	{
		Libraries.Catalyst library;
		List<string> validatorVersions = new List<string> ();
		List<string> validatorPlugins = new List<string> ();

		public DependencyValidator (Libraries.Catalyst library)
		{
			this.library = library;
		}

		public bool Passes ()
		{
			library.DebugLog ("Checking in plugins");
			try {
				CheckInAll ();
			} catch (Exception e) {

			}

			return !library.HasErrors;
		}

		public void Clear ()
		{
			validatorPlugins.Clear ();
			validatorVersions.Clear ();
		}

		void CheckInAll (Dictionary<string, string> require = null)
		{
			if (require == null) {
				require = library.Settings.Require;
			}

			foreach (KeyValuePair<string, string> kvp in require) {
				var obj = library.GetPluginInfo (kvp.Key);
				if (obj == null) {
					library.Error (kvp.Key + " does not exist or invalid");
				} else {
					CheckIn (obj);
				}
			}
		}

		void CheckIn (JObject pluginInfo)
		{

			var pluginName = pluginInfo ["name"].ToString ();
			var versionSig = pluginName + "-" + pluginInfo ["version"];
			if (validatorPlugins.Contains (pluginName)) {
				if (!validatorVersions.Contains (versionSig)) {
					library.Error ("Cannot resolve multiple versions of same plugin: " + pluginName);
					return;
				}
			} else {
				validatorPlugins.Add (pluginName);
			}

			if (!validatorVersions.Contains (versionSig)) {
				validatorVersions.Add (versionSig);

				var requires = pluginInfo ["plugin"] ["require"];
				if (requires != null) {
					CheckInAll (requires.ToObject<Dictionary<string, string>> ());
				}
			}
		}
	}
}