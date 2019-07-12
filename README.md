# EasyIIS
Simple windows command line tool to automate turning on IIS appPools and websites, and any related windows services.

Useful if you have a large number of sites on your local development machine and you want a quick way to turn on/off quickly without having to click around in the IIS and services UI

Particularly useful for Sitecore 9 sites where you have multiple IIS AppPools, websites, and Services (solr) to manage. Now you can easily turn sites off an on quickly.

## Usage Examples

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
