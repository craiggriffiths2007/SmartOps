// Native regression coverage for fresh seeds, existing data and rename collisions.
var $report:=New object("success";False)
Try
	GO_UpgradeCompanyCode
	GO_Seed
	var $service:=cs.SmartOpsService.new()
	var $table:=ds[$service.schema.Tenant.nativeTable]
	var $company:=$table.query("Code = :1";"SMARTOPS").first()
	If ($company=Null)
		throw(500;"Fresh seed did not use SMARTOPS")
	End if
	var $id:=$company.Id
	var $tenantId:=$company.GoTenantId
	var $counts:=New object
	var $name : Text
	For each ($name;$service.schema)
		$counts[$name]:=ds[$service.schema[$name].nativeTable].all().length
	End for each
	$company.Code:="DEMOWIND"
	$service.save($company)
	GO_UpgradeCompanyCode
	GO_UpgradeCompanyCode
	$company:=$table.get($id)
	If (($company.Code#"SMARTOPS") | ($company.GoTenantId#$tenantId) | ($table.query("Code = :1";"DEMOWIND").length#0))
		throw(500;"Rename did not preserve the company identity")
	End if
	For each ($name;$service.schema)
		If ($counts[$name]#ds[$service.schema[$name].nativeTable].all().length)
			throw(500;"Rename changed record count for "+$name)
		End if
	End for each
	var $user : Text
	For each ($user;New collection("admin";"staff";"customer";"insurer"))
		var $login:=$service.login(New object("TenantCode";"SMARTOPS";"Username";$user;"Password";"Password1!"))
		If ($login.status#200)
			throw(500;"SMARTOPS login failed for "+$user)
		End if
	End for each
	var $mobile:=cs.SmartOpsSurveyService.new()
	var $agentLogin:=$mobile.login(New object("TenantCode";"SMARTOPS";"AgentCode";"ADMIN";"Password";"Password1!"))
	If ($agentLogin.status#200)
		throw(500;"SMARTOPS Survey login failed")
	End if
	$company.Code:="DEMOWIND"
	$service.save($company)
	var $other:=$table.query("Code = :1";"OTHER").first()
	$other.Code:="SMARTOPS"
	$service.save($other)
	var $refused:=False
	Try
		GO_UpgradeCompanyCode
	Catch
		$refused:=True
	End try
	If (Not($refused) | ($table.get($id).Code#"DEMOWIND") | ($table.get($other.Id).Code#"SMARTOPS"))
		throw(500;"Conflicting company code was not safely refused")
	End if
	$report.success:=True
	$report.companyId:=$id
	$report.checks:="Empty database; fresh seed; in-place rename; repeated upgrade; record counts; four browser roles; Survey login; collision refusal"
Catch
	$report.errors:=Last errors
End try
File(Folder(fk database folder).path+"company-code-test-result.json").setText(JSON Stringify($report))
QUIT 4D
