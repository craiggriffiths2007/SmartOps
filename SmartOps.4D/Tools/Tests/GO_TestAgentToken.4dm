// Isolated native regression: Survey tokens belong to Agent, without expiry.
var $report:=New object("success";False)
GO_Seed
ds.startTransaction()
Try
	var $mobile:=cs.SmartOpsSurveyService.new()
	var $service:=cs.SmartOpsService.new()
	var $company:=ds.GoCompany.query("Code = :1";"SMARTOPS").first()
	var $agent:=ds.Agent.query("GoTenantId = :1 AND Code = :2";$company.Id;"ADMIN").first()
	var $login:=New object("TenantCode";"SMARTOPS";"AgentCode";"ADMIN";"Password";"Passowrd1!")
	var $sessionsBefore:=ds.GoSession.all().length
	var $reply:=$mobile.login($login)
	$agent.reload()
	var $token:=$agent.AuthenticationString
	If (($reply.status#200) | (Length($token)=0) | (ds.GoSession.all().length#$sessionsBefore))
		throw(500;"Login must store the token on Agent without creating a session")
	End if
	var $body:=New object("TenantCode";"SMARTOPS";"AgentCode";"ADMIN";"AuthenticationString";$token)
	var $account:=$mobile.authenticate($body)
	If ($account.AgentId#$agent.Id)
		throw(500;"Agent token resolved the wrong account")
	End if
	// Even an expired legacy session must not override the persistent Agent token.
	var $expired:=$service.newRow("GoSession";$company.Id)
	$expired.AccountId:=$account.Id
	$expired.TokenHash:=Generate digest($token;SHA256 digest)
	$expired.ExpiresAt:=$service.now()-86400
	$service.save($expired)
	$mobile.authenticate($body)
	var $invalid : Text
	For each ($invalid;New collection("";"invalid-token"))
		$body.AuthenticationString:=$invalid
		var $rejected:=False
		Try
			$mobile.authenticate($body)
		Catch
			$rejected:=True
		End try
		If (Not($rejected))
			throw(500;"Empty or incorrect token was accepted")
		End if
	End for each
	$body.AuthenticationString:=$token
	$body.TenantCode:="OTHER"
	$rejected:=False
	Try
		$mobile.authenticate($body)
	Catch
		$rejected:=True
	End try
	If (Not($rejected))
		throw(500;"Token crossed company boundaries")
	End if
	$body.TenantCode:="SMARTOPS"
	$account.Active:=False
	$service.save($account)
	$rejected:=False
	Try
		$mobile.authenticate($body)
	Catch
		$rejected:=True
	End try
	If (Not($rejected))
		throw(500;"Disabled agent account was accepted")
	End if
	$account.Active:=True
	$service.save($account)
	$mobile.login($login)
	$rejected:=False
	Try
		$mobile.authenticate($body)
	Catch
		$rejected:=True
	End try
	If (Not($rejected))
		throw(500;"A new login must replace the previous token")
	End if
	$agent.reload()
	$body.AuthenticationString:=$agent.AuthenticationString
	$mobile.authenticate($body)
	$report.success:=True
	$report.checks:="Agent token persistence; no session creation; expired legacy session ignored; empty/invalid tokens rejected; tenant isolation; disabled account rejected; new login replaces previous token"
Catch
	$report.errors:=Last errors
End try
ds.cancelTransaction()
File(Folder(fk database folder).path+"agent-token-test-result.json").setText(JSON Stringify($report))
QUIT 4D
