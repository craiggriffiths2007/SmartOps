// Start this method to test SmartOps.Survey on a phone or tablet over a private LAN.
// HTTP listens on port 8045. HTTPS is enabled only when both certificate files exist.
// Do not expose the demo server to the public internet.
GO_UpgradeCompanyCode
// Seed fictional data only when every mapped table is empty.
var $seedService:=cs.SmartOpsService.new()
var $seedTable : Text
var $isEmpty:=True
For each ($seedTable; $seedService.schema)
    If (ds[$seedService.schema[$seedTable].nativeTable].all().length>0)
        $isEmpty:=False
        break
    End if
End for each
If ($isEmpty)
    GO_Seed
End if
var $server:=WEB Server
If ($server.isRunning)
    $server.stop()
End if

var $certificateFolder:=Folder(fk database folder)
var $hasCertificate:=$certificateFolder.file("cert.pem").exists & $certificateFolder.file("key.pem").exists
var $settings:=New object("HTTPEnabled"; True; "HTTPSEnabled"; $hasCertificate; "HTTPPort"; 8045; "HTTPSPort"; 443; "IPAddressToListen"; "0.0.0.0"; "scalableSession"; True; "CORSEnabled"; False; "defaultHomepage"; "index.html"; "rootFolder"; Folder(fk database folder).folder("WebFolder"); "maxRequestSize"; 16000000)
var $result:=$server.start($settings)
If (Not($result.success))
    ALERT("SmartOps LAN web server failed to start: "+JSON Stringify($result))
End if
