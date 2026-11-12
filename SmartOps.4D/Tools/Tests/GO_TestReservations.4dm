var $report:=New object("success";False)
GO_Seed
var $service:=cs.SmartOpsService.new()
ds.startTransaction()
Try
	var $account:=ds.GoAccount.query("Role = :1";"Admin").first()
	var $bridge:=cs.SmartOpsWebBridge.new($service)
	var $id:="b6bd3b04-b0d4-4278-b70d-a31b45f11111"
	var $values:=New object("Id";$id;"AgentId";ds.Agent.all().first().Id;"UserId";"4d-admin-test";"Start";"2030-01-15T09:00:00";"End";"2030-01-15T10:00:00";"ExpiresUtc";"2030-01-15T08:55:00Z")
	var $change:=New object("table";"BookingReservations";"key";$id;"recordId";0;"operation";"add";"revision";0;"values";$values;"references";New object)
	var $result:=$bridge.commit($account;New object("changes";New collection($change)))
	var $row:=ds.BookingReservations.get($id)
	If ($row=Null)
		throw(500;"Reservation GUID was not retained")
	End if
	If (($row.UserId#"4d-admin-test") | ($row.GoTenantId#$account.GoTenantId) | ($result.ids["BookingReservations:"+$id]#$id))
		throw(500;"Reservation identity or owner changed")
	End if
	$change.operation:="update"
	$change.recordId:=$id
	$change.revision:=$row.GoRevision
	$change.values:=New object("ExpiresUtc";"2030-01-15T09:05:00Z")
	$bridge.commit($account;New object("changes";New collection($change)))
	$row:=ds.BookingReservations.get($id)
	If ($row.ExpiresUtc#"2030-01-15T09:05:00Z")
		throw(500;"Reservation update failed")
	End if
	var $refused:=False
	Try
		$service.row("BookingReservations";$id;$account.GoTenantId+1000)
	Catch
		$refused:=True
	End try
	If (Not($refused))
		throw(500;"Reservation escaped company isolation")
	End if
	$change.operation:="delete"
	$change.revision:=$row.GoRevision
	$change.values:=New object
	$bridge.commit($account;New object("changes";New collection($change)))
	If (ds.BookingReservations.get($id)#Null)
		throw(500;"Reservation release failed")
	End if
	$report.success:=True
	$report.checks:="Reserve with original GUID; update expiry; company isolation; release"
Catch
	$report.errors:=Last errors
	$report.stage:=$service.debugStage
End try
ds.cancelTransaction()
File(Folder(fk database folder).path+"reservations-test-result.json").setText(JSON Stringify($report))
QUIT 4D
