// COPYRIGHT 2016 RUSTSERVERS.IO
using System.Collections.Generic;

namespace Oxide.Ext.Catalyst
{
	public class CatalystSettings
	{
		public List<string> SourceList;

		public bool Debug;
		public Dictionary<string, string> Require;
		public Dictionary<string, string> RequireDev;
		public string Version;
	}
}