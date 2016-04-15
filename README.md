## Catalyst 

Version: ALPHA-0.0.3

Plugin & dependency management extension by [RustServers.IO](http://rustservers.io) for the [Oxide](http://oxidemod.org) modding framework.

### Installation

### Usage

* **Update**

  Updates all plugins to latest versions

  ````catalyst.update *````

  Updates specified plugins to latest versions

  ````catalyst.update PluginName [PluginName] [...]````

* **Require**

  Installs specified plugins on server

  ````catalyst.require PluginName [PluginName] [...]````

* **Remove**

  Uninstalls specified plugins from server

  ````catalyst.remove PluginName [PluginName] [...]````

* **Source Service**

  Add/Remove source service where plugins made be found

  ````catalyst.source http://source.url.com````

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

### Contribute

We are happy to review community contributions, please send updates as pull requests on GitHub.

### License

MIT
