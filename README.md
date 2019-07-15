# EasyIIS
Simple windows command line tool to automate turning on IIS appPools and websites, and any related windows services.

Useful if you have a large number of sites on your local development machine and you want a quick way to turn on/off quickly without having to click around in the IIS and services UI.

Particularly useful for Sitecore 9 sites where you have multiple IIS AppPools (website, identityserver, xconnect), and services (solr, SQL, mongoDb) to manage. With this tool, you can easily group these together as a "site" and turn the entire group on/off via the command line/bash. Or you can turn ALL sites on and off.

## Configuration

Site list is configured in the `config.json` file:

```javascript
{  
  "sites": [
    {
      "name": "mysite1",
      "appPools": [
        "mysite1"
      ],
      "websites": [
        "mysite1"
      ],
      "services": [
        "solr-mysite1"
      ]
    },
    {
      "name": "mysite2",
      "appPools": [
        "mysite2.identityserver",
        "mysite2.verndale-local.com",
        "mysite2.xconnect"
      ],
      "websites": [
        "mysite2.identityserver",
        "mysite2.verndale-local.com",
        "mysite2.xconnect"
      ],
      "services": [
        "solr-mysite2-9004"
      ]
    }
  ]
}
```

## Path Variable

I recommend you add a local user path variable to the folder you downloaded or cloned this repo to. This will enable you to use EasyIIS.exe in any directory/folder in command line - or better yet, in the git bash console.

Example install location:

`C:\Tools\EasyIIS\latest\EasyIIS.exe`

So in this example, you would simply add a user path variable to the folder `C:\Tools\EasyIIS\latest\` and you should be able to call "EasyIIS" in any folder in command line or git bash.

## Usage Examples
_Note: Including the .exe is optional, it is present here for demonstration purposes._ 

`EasyIIS.exe`  
_(default / no command line parameters) = all on._

`EasyIIS.exe all up`  
_(turns all appPool, website and all services off)_

`EasyIIS.exe all down`  
_(turns all appPool, website and services off)_

`EasyIIS.exe mysite up`  
_(turns specific site appPool, website and services on)_

`EasyIIS.exe mysite down`  
_(turns specific site appPool, website and services off)_
