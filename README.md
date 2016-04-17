## Catalyst 

**Version**: ALPHA-0.0.5

Plugin & dependency management extension by [RustServers.IO](http://rustservers.io) for the [Oxide](http://oxidemod.org) modding framework.

### Features

* Free & Open-Source
* Automaticaly install plugins and all of their dependencies
* Automatically update an individual plugin or every plugin
* Avoids circular dependencies && dependency hell

### Installation

1. Move Oxide.Ext.Catalyst into your RustDedicated_Data/Managed folder
2. Restart server

### Usage

* **Update**

  Updates all plugins to latest versions

  ````catalyst.update *````

  Updates specified plugins to latest versions

  ````catalyst.update PluginName [PluginName] [...]````

* **Require**

  Installs specified plugins on server

  ````catalyst.require PluginName [Version]````

* **Remove**

  Uninstalls specified plugins from server

  ````catalyst.remove PluginName [PluginName] [...]````

* **Configuration**

  Command-line editing of plugin configuration files (alpha)

  ````catalyst.config Setting.SubSetting [NewValue]````

* **Search**

  Search available source repositories for a plugin by name, description, or requirements

  ````catalyst.search search terms````

* **Info**

  Find available information on a specific plugin

  ````catalyst.info PluginName````

* **Validate**

  Check if requirements are valid

  ``catalyst.validate``

### Sourcing

Plugins installed by this extension are currently not sourced directly from Oxide.

Catalyst sources plugins directly from GitHub, the default repository may be found here [Calytic/oxideplugins](https://github.com/Calytic/oxideplugins)

In order to add or remove sources:

* **Source Service**

  Add/Remove source service where plugins made be found

  ````catalyst.source http://source.url.com````

### Contribute

We are happy to review community contributions, please send updates as pull requests on GitHub.

### License

MIT
