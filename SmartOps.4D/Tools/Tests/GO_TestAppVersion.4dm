var $report:=New object("success";False)
GO_Seed
var $service:=cs.SmartOpsService.new()
ds.startTransaction()
Try
	var $admin:=ds.GoAccount.query("Role = :1";"Admin").first()
	var $account:=ds.GoAccount.query("Role = :1 AND GoTenantId = :2";"Agent";$admin.GoTenantId).first()
	var $mobile:=cs.SmartOpsSurveyService.new()
	var $bridge:=cs.SmartOpsWebBridge.new($service)
	var $body:=New object("CurrentVersion";"1.0";"CurrentBuild";"1";"DeviceManufacturer";"Test";"DeviceModel";"Tablet";"Platform";"Android")
	var $reply:=$mobile.appVersion($account;$body;"Native regression")
	If ($reply.status#200)
		throw(500;"Version check without a configured release failed")
	End if
	var $values:=New object("Key";"SurveyAppLatestBuild";"Value";"12")
	var $change:=New object("table";"SystemSettings";"key";"-1";"recordId";-1;"operation";"add";"revision";0;"values";$values;"references";New object)
	var $result:=$bridge.commit($admin;New object("changes";New collection($change)))
	var $id:=$result.ids["SystemSettings:-1"]
	var $setting:=ds.SystemSettings.get($id)
	If (($setting.GoTenantId#$admin.GoTenantId) | ($setting.Value#"12"))
		throw(500;"Release build was not saved in the company's 4D settings")
	End if
	var $version : Text
	For each ($version;New collection("1";"12";"13"))
		$body.CurrentBuild:=$version
		$reply:=$mobile.appVersion($account;$body;"Native regression")
		var $check:=ds.SurveyAppRequests.query("GoTenantId = :1";$admin.GoTenantId).orderBy("Id desc").first()
		If (($check.LatestBuild#"12") | ($check.UpdateAvailable#(Num($version)<12)) | ($check.DeviceModel#"Tablet"))
			throw(500;"Build comparison or check history is incorrect")
		End if
	End for each
	// Updating the same setting must affect subsequent checks.
	$change.operation:="update"
	$change.recordId:=$id
	$change.revision:=$setting.GoRevision
	$change.values:=New object("Value";"0")
	$bridge.commit($admin;New object("changes";New collection($change)))
	$body.CurrentBuild:="1"
	$mobile.appVersion($account;$body;"")
	var $last:=ds.SurveyAppRequests.query("GoTenantId = :1";$admin.GoTenantId).orderBy("Id desc").first()
	If (($last.LatestBuild#"0") | $last.UpdateAvailable)
		throw(500;"Build zero did not disable update prompts")
	End if
	var $other:=$service.newRow("SystemSettings";999)
	$other.Key:="SurveyAppLatestBuild"
	$other.Value:="999"
	$service.save($other)
	$mobile.appVersion($account;$body;"")
	$last:=ds.SurveyAppRequests.query("GoTenantId = :1";$admin.GoTenantId).orderBy("Id desc").first()
	If ($last.LatestBuild#"0")
		throw(500;"Version settings leaked across companies")
	End if
	$change.revision:=ds.SystemSettings.get($id).GoRevision
	$change.values:=New object("Value";"1.5")
	var $rejected:=False
	Try
		$bridge.commit($admin;New object("changes";New collection($change)))
	Catch
		var $errors:=Last errors
		$rejected:=($errors[0].errCode=400)
	End try
	If (Not($rejected))
		throw(500;"Invalid build was accepted")
	End if
	$report.success:=True
	$report.checks:="4D setting creation and update; version-check logging; older/equal/newer installed builds; disable prompts; tenant isolation; invalid build rejection"
Catch
	$report.errors:=Last errors
	$report.stage:=$service.debugStage
End try
ds.cancelTransaction()
File(Folder(fk database folder).path+"app-version-test-result.json").setText(JSON Stringify($report))
QUIT 4D
