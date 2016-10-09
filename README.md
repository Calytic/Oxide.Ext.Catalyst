## Catalyst 

**Version**: ALPHA-0.0.8

Plugin & dependency management extension by [RustServers.IO](http://rustservers.io) for the [Oxide](http://oxidemod.org) modding framework.

### Features

* Free & Open-Source
* Automatically install plugins and all of their dependencies
* Automatically update an individual plugin or every plugin
* Avoids circular dependencies & dependency hell

### Installation

1. Move ``Oxide.Ext.Catalyst.dll`` into the ``RustDedicated_Data/Managed`` folder
2. Restart server

### Usage

* **Update**

  Updates all plugins to latest versions

  ````catalyst.update *````

  Updates specified plugins to latest versions

  ````catalyst.update PluginName [PluginName] [...]````

* **Require**

  Installs specified plugin

  ````catalyst.require PluginName [Version]````
  
* **RequireAll**

  Installs all specified plugins

  ````catalyst.requireall PluginName [PluginName] ...````

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

Plugins available for installation through Catalyst are indirectly sourced from Oxide.

Original authors are attributed and their corresponding documentation and possible licensing information is included.

If you identify any misattribution or a missing license please do not hesitate to contact us.

More information on the intermediate web service used for Catalyst may be found in [SOURCE.md](https://github.com/Calytic/Oxide.Ext.Catalyst/blob/master/docs/SOURCE.md)

* **Source Service**

  Add/Remove source service where plugins are found

  ````catalyst.source http://source.url.com````

### Contribute

We are happy to review community contributions, please send updates as pull requests on GitHub.

### License

MIT
