# Survey API reverse proxy — LAN test only

This branch adds an **opt-in** YARP reverse proxy to SmartOps.4D.Web. It forwards only the documented Android survey API paths to the 4D HTTP server without changing the existing web bridge. Do not deploy this experimental HTTP listener to a public network.

## Run in Visual Studio

1. Check out `test/survey-api-reverse-proxy` and restore NuGet packages.
2. Select the **SmartOps.4D.Web (Survey Proxy Test)** launch profile.
3. Start 4D and run `GO_StartLAN` (4D HTTP service, normally port 8045).
4. Start SmartOps.4D.Web. This test profile sets `SurveyProxy__Enabled=true`.
5. On the PC, verify `http://127.0.0.1:5245/Account/Login` still works.
6. From the phone, browse to `http://192.168.0.15:8085/api/health`. The expected response is 4D's health response.
7. In the MAUI app, change only its server URL to `http://192.168.0.15:8085`, then attempt login and test jobs, media and checks.

The test binds the web app to `127.0.0.1:5245` and the mobile-only endpoint to `0.0.0.0:8085`. Requests other than the documented survey API endpoints receive HTTP 404 on port 8085. The full web interface remains available only via localhost:5245.

The proxy forwards `POST` for the documented survey endpoints, including `/Home/SurveyAppVersion`, and `GET /api/health`. It preserves the original request path and query. Configure the upstream with `FourD__BaseUrl=http://127.0.0.1:8045/` if 4D accepts loopback connections; otherwise the app uses its existing LAN-address detection logic.

## Firewall rule for trusted LAN testing

In an elevated PowerShell window:

```powershell
New-NetFirewallRule -DisplayName "SmartOps Survey Proxy Test 8085" -Direction Inbound -Protocol TCP -LocalPort 8085 -RemoteAddress LocalSubnet -Profile Private -Action Allow
```

Only use this on a trusted Private network. To remove the test rule afterward:

```powershell
Remove-NetFirewallRule -DisplayName "SmartOps Survey Proxy Test 8085"
```

## Disable

Select the original `SmartOps.4D.Web` launch profile or set `SurveyProxy__Enabled=false`. This disables the proxy and the LAN listener. The test is isolated from `main`.

**Security:** The proxy is a transport pass-through, **not** an additional authentication boundary. The 4D survey API still provides its own authentication. Do not expose port 8085 to the internet; production use requires HTTPS, appropriately protected server access and a reviewed authentication model.

**Testing status:** Source change only. This environment has not performed a .NET build or an end-to-end device test.
