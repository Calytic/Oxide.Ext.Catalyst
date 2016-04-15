// COPYRIGHT 2016 RUSTSERVERS.IO
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using Oxide.Core;
using Oxide.Core.Extensions;
using Oxide.Core.Libraries;

namespace Oxide.Ext.Catalyst
{
	public class CatalystExtension : Extension
	{
		public static CatalystExtension Instance { get; private set; }
		public static Libraries.Catalyst _Catalyst { get; private set; }
		public static Plugins.Catalyst CatalystPlugin { get; private set; }
		public CatalystExtension(ExtensionManager manager) : base(manager)
		{
			CatalystExtension.Instance = this;
		}

		public override string Name => "Catalyst";

		public override VersionNumber Version => new VersionNumber (
			(ushort) Assembly.GetExecutingAssembly().GetName().Version.Major,
			(ushort) Assembly.GetExecutingAssembly().GetName().Version.Minor,
			(ushort) Assembly.GetExecutingAssembly().GetName().Version.Build
		);

		public override string Author => "Calytic";

		public override void Load()
		{
			this.Manager.RegisterPluginLoader(new Plugins.PluginLoader(this));
			Manager.RegisterLibrary("Catalyst", _Catalyst = new Libraries.Catalyst(this));
		}

		public override void LoadPluginWatchers(string plugindir)
		{
			_Catalyst?.Initialize();
		}

		public override void OnModLoad()
		{

		}

		public override void OnShutdown()
		{
			_Catalyst?.Shutdown();
		}
	}
}
