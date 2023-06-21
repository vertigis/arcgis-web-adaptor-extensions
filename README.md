# ArcGIS Web Adaptor Extensions
Server-based applications like VertiGIS Studio Printing, Reporting, and Workflow need to make server-to-server requests to ArcGIS Enterprise. 
If and ArcGIS Web Adaptor is configured with Windows Authentication enabled and Anonymous Authentication disabled these requests will be blocked because the Web Adaptor captures both the Windows identity of the Application Pool user and the ArcGIS access token of the end user. These two credentials will not agree and the backend ArcGIS Enterprise service will respond with an error. 

The VertiGIS Web Adaptor Extensions provide a targeted solution. The extensions modify the ArcGIS Web Adaptor to discard the Windows identity, for select trusted service accounts, after it has been validated. This allows the Web Adaptor to still require Windows Authentication for all requests, while passing only ArcGIS access tokens on to the backend ArcGIS Enterprise service when the request is from a known server application.

## Requirements
ArcGIS Web Adaptor for Microsoft IIS version 11.1 and newer.

For ArcGIS versions prior to 11.1 see https://support.vertigis.com/hc/en-us/articles/11461061234066-Install-the-Geocortex-Web-Adaptor-Extensions.

## Usage

### Installation
1. Download the latest release from https://github.com/vertigis/arcgis-web-adaptor-extensions/releases/latest
   - Ensure the zip archive is not blocked by the operating system after you download it
1. Extract the zip archive into the Web Adaptor directory
   - For example, `C:\inetpub\wwwroot\arcgis`
1. Open an administrative command prompt on the Web Adaptor machine
1. Navigate the command prompt to the Web Adaptor directory
   - For example, `cd C:\inetpub\wwwroot\arcgis`
1. Execute the command `vs-wae add`

### Add a trusted server application
1. Open an administrative command prompt on the Web Adaptor machine
1. Navigate the command prompt to the Web Adaptor directory
   - For example, `cd C:\inetpub\wwwroot\arcgis`
1. Execute the command `vs-wae trust [group/user]`
   - Where `[group/user]` is the name of the Windows service account running the trusted applications, or the name of a Windows group containing the account(s).
   - For example:
       - `vs-wae trust IIS AppPool\VertiGISStudioReporting`
       - `vs-wae trust DOMAIN\ServiceAccount`
       - `vs-wae trust DOMAIN\MachineName$`

### Remove a trusted server application
1. Open an administrative command prompt on the Web Adaptor machine
1. Navigate the command prompt to the Web Adaptor directory
   - For example, `cd C:\inetpub\wwwroot\arcgis`
1. Execute the command `vs-wae trust --remove [group/user]`
   - Where `[group/user]` is the name of the Windows group or user to remove.