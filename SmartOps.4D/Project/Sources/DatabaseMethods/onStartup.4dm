// Start the native 4D HTTP API and browser workspace automatically.
// GO_StartLAN seeds a completely empty development database.
GO_StartLAN

// Open the login screen only when the HTTP web server started successfully.
var $server:=WEB Server
If ($server.isRunning)
    GO_Open
End if
