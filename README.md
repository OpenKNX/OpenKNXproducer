
OpenKNXproducer
===
**OpenKNXproducer** is a  commandline tool to create knxprod files for ETS.

It is the successor of [multiply-channels](https://github.com/mumpf/multiply-channels), which will be discontinued. multiply-channels and OpenKNXproducer are compatible, as long as you want to convert simple xml-sources using ETS 5.x. As soon as you use the include-functionality of these tools, there is no compatibility anymore.

For [OpenKNX_Projects](https://github.com/OpenKNX) or for knxprod-creation with ETS 6 you should use OpenKNXproducer.

### Features
The current version provides the following verbs: 
- **create**     Check given xml file and create knxprod
- **check**      Execute sanity checks on given xml file
- **knxprod**    Create knxprod file from given xml file without checks
- **new**        Create new xml file with a fully commented and working mini exaple
- **help**       Display more information on a specific command.
- **version**    Display version information.

This tool also creates a header file with defines for all parameter and communication object definitions in the XML. **Requirements:** ETS must be installed. Supported versions are: ETS 4, ETS 5.5, ETS 5.6, ETS 5.7 and ETS 6.0. The correct ETS converter version is automatically found dependent on xmlns of provided xml document. 
This project uses **.NET 6.0**.
##
### Examples:
- ``OpenKNXproducer new --ProductName=TempSensor --AppNumber=567 Sensor``

  Creates initial Sensor.xml, do sanity checks produce Sensor.h, produce Sensor.knxprod

- ``OpenKNXproducer create Sensor``

  Reads Sensor.xml, do sanity checks, produce Sensor.h, produce Sensor.knxprod

- ``OpenKNXproducer knxprod -o device.knxpord Sensor.xml``

  Reads Sensor.xml, produce device.knxprod

- ``OpenKNXproducer check Sensor.xml``

  Reads Sensor.xml, do sanity checks

##
### Tipps:
If all sanity checks were OK and the knxprod file was created, there might be still problems using this file in ETS. Because the user is editing an xml file, there might be many other problems there, which are not checked by this tool yet.

**As a general hint:**
- if there is a problem importing the knxprod file into the product catalog (import function in ETS), there is usually a problem with Parameters-, ParameterTypes-, ComObjects-Section or the document structure itself.
- if import was successful, but you cannot add the device to your project, usually there is a problem in Dynamic-, ParameterRef-, ComObjectRef-Section.

If you find a situation, which is not yet checked but leads to errors in ETS, report a issue and I will add a new check or just create a pull request. Thank you. 

##
### Installation
This release is compatible with **Linux**, **macOS**, and **Windows 10/11** (x86, x64, Arm).
### 1. Download and Extract the ZIP file
Download the latest release and extract the entire contents of the ZIP file into a directory.
### 2. Run the installation script
 ### Windows
- Right-click on `Install-OpenKNX-Tools.ps1` → **Run with PowerShell**.
  Confirm any security warnings by selecting **Open File**.
- The tools will be copied to the target directories automatically. Progress will be displayed in the console.
- **Installation Hints**: **Windows: Script Execution Policy**
  - If you see "not digitally signed" error: **Solution - then try this:**
  - ```PowerShell.exe -ExecutionPolicy Bypass -File .\Install-OpenKNX-Tools.ps1```

 ### macOS / Linux**
  Ensure **PowerShell (pwsh)** is installed: [PowerShell GitHub Releases](https://github.com/PowerShell/powershell/releases)
- ```pwsh ./Install-OpenKNX-Tools.ps1```
- **Installation Hints**:
  - **macOS**: Security Block on First Launch* When starting OpenKNXproducer for the first time, macOS may block the app (unidentified developer). Try to start the app → macOS shows a warning. 
  - **Solution:**
    - Go to System Settings → Security & Privacy → General → Click Allow Anyway
    - Start the app again.

### 3. Remove / Uninstallation
- Use Remove-OpenKNX-Tools.ps1 following similar steps.
- ```pwsh ./Remove-OpenKNX-Tools.ps1```



