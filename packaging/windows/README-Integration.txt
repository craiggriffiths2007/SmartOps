SMARTOPS INTEGRATION - START HERE

This download contains a working 4D integration project, fictional integration
test data, API documentation, and an OPTIONAL prebuilt Windows web interface.

QUICK START - 4D ONLY (Visual Studio is NOT required)

1. Install/open 4D with a compatible licence.
2. Open SmartOps.4D/Project/smartops4d.4DProject in 4D.
3. Create a NEW EMPTY data file if 4D prompts you.
4. Allow the 4D onStartup method to run GO_StartLAN.
   It starts the local API on port 8045 and imports integration test records
   when the database is completely empty.
5. Test the API from the same PC at http://127.0.0.1:8045/api/health
   (or http://YOUR-PC-IP:8045/api/health if 4D is bound to the LAN IP).

You can now explore the 4D methods and API without the web application.

OPTIONAL WEB INTERFACE (Windows x64)

1. Extract SmartOps-Web-Windows-x64.zip into its OWN folder.
2. Keep the 4D database open and its HTTP server running.
3. Double-click Start-SmartOps-Web.cmd in the extracted web folder.
4. Open http://127.0.0.1:5245/Account/Login in your browser.
5. Use the local test credentials:
   Company SMARTOPS (preselected), user admin, password Password1!

The web ZIP includes the ASP.NET Core runtime. You do not need Visual Studio.
The 4D database and web interface can run on the same Windows computer.
For integration over LAN, see README-Windows.txt in the web ZIP.

ANDROID APP

SmartOps Survey on Google Play:
https://play.google.com/store/apps/details?id=com.authorityx.smartops

The Android app talks directly to 4D on port 8045; the web interface is optional.
Android application SOURCE CODE IS NOT included in this download.

SECURITY

The included records and credentials are for private/local integration testing
only. Do not publish port 8045 or deploy this instance on the public internet
with the supplied test credentials. The project is source code, not the 4D
software or a 4D licence. A 4D runtime/development environment is still needed.

Documentation:
SmartOps.4D/Documentation/API-INTEGRATION.md
https://github.com/craiggriffiths2007/SmartOps
