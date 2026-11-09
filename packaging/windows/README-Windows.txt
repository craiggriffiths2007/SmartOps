SMARTOPS WEB - WINDOWS x64

This is the prebuilt, self-contained ASP.NET Core interface for SmartOps.4D.
Visual Studio, the .NET SDK and a separate .NET runtime are NOT required.

1. Download and extract the FULL SmartOps-Web-Windows-x64.zip archive.
2. Open SmartOps.4D in 4D and create a new data file if prompted.
   Let the 4D database start its API on port 8045.
3. Double-click Start-SmartOps-Web.cmd in this folder.
4. Your browser should open http://127.0.0.1:5245/Account/Login.
5. Local integration test sign-in: username admin, password Password1!
   Company code SMARTOPS is selected automatically.

Keep the SmartOps Web console window open while you use the application.
To stop the local web server, close that console window.

The web frontend tries to discover your active LAN IP for the 4D server.
If it cannot connect, run from a Command Prompt after setting:
  set "FourD__BaseUrl=http://YOUR-PC-IP:8045/"
  SmartOps.Web.exe

The supplied logins and optional agent-code-only mobile authentication are
for a trusted, private integration-testing environment only. Do not expose
the 4D API or web server publicly with the supplied credentials.

4D source and documentation: https://github.com/craiggriffiths2007/SmartOps
