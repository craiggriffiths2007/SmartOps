// Normalise historical demo company codes without changing the tenant ID or relationships.
// Safe to repeat. Never overwrite or merge a separate company that already uses SMARTOPS.
var $service:=cs.SmartOpsService.new()
var $table:=ds[$service.schema.Tenant.nativeTable]
var $old:= $table.query("Code = :1 OR Code = :2";"DEMOWIND";"SSMARTOPS")
If ($old.length=0)
    return
End if
If (($old.length#1) | ($table.query("Code = :1";"SMARTOPS").length>0))
    throw(409;"Company rename refused: legacy company is ambiguous or SMARTOPS already exists")
End if
ds.startTransaction()
Try
    var $company:=$old.first()
    $company.Code:="SMARTOPS"
    $service.save($company)
    ds.validateTransaction()
Catch
    ds.cancelTransaction()
    throw(409;"Could not rename legacy demo company to SMARTOPS; no company data was changed")
End try
