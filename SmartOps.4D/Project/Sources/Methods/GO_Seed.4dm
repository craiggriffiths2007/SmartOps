//%attributes = {}

// GO_Seed
// Import fictional sample data into a completely empty SmartOps 4D database.
// Demo credentials are deliberately PUBLIC and are not safe for a production system.
// Roll back all changes if an error occurs.

var $service:=cs.SmartOpsService.new()

var $name : Text
var $record : Object
var $value : Variant
var $field : Text
var $e : 4D.Entity

var $currentTable : Text
var $currentField : Text
var $currentRef : Text
var $recordNumber : Integer
var $stage : Text

$currentTable:=""
$currentField:=""
$currentRef:=""
$recordNumber:=0
$stage:="Checking database"

// Refuse to import into a populated database.
For each ($name; $service.schema)
	If (ds[$service.schema[$name].nativeTable].all().length>0)
		ALERT("Sample import refused: "+$name+" already contains data. Use a new trial data file.")
		return 
	End if 
End for each 

// Load the sample data.
var $data:=JSON Parse(File("/RESOURCES/sample-data.json").getText())

// References to previously created records.
var $refs:=New object

ds.startTransaction()

Try
	
	For each ($record; $data.records)
		
		$recordNumber:=$recordNumber+1
		$currentTable:=$record.table
		$currentRef:=$record.ref
		$currentField:=""
		$stage:="Resolving tenant"
		
		var $tenant:=0
		
		If ($record.tenant#"")
			$tenant:=$refs[$record.tenant]
		End if 
		
		// Create or update the record.
		$stage:="Creating entity"
		
		If (OB Is defined($record; "updateRef"))
			
			$e:=ds[$service.schema[$record.table].nativeTable].get($refs[$record.updateRef])
			
		Else 
			
			$e:=$service.newRow($record.table; $tenant)
			
		End if 
		
		// Assign fields from sample JSON.
		$stage:="Assigning fields"
		
		For each ($field; $record.values)
			
			$currentField:=$field
			$value:=$record.values[$field]
			
			// Resolve references to other seeded records.
			If (Value type($value)=Is object)
				
				$value:=$refs[$value.ref]
				
			Else 
				
				If (Value type($value)=Is text)
					
					If ($value="$today")
						$value:=Substring(Timestamp; 1; 10)
					End if 
					
					If ($value="$now")
						$value:=Timestamp
					End if 
					
				End if 
				
			End if 
			
			// Assign to the mapped native 4D field.
			$stage:="Setting field "+$field
			
			$e[$service.schema[$record.table].fieldMap[$field]]:=$value
			
		End for each 
		
		// Hash demo account passwords.
		$currentField:="PasswordHash"
		$stage:="Setting demo password"
		
		If ($record.table="GoAccount")
			
			var $seedPassword:=$data.demoPassword
			
			If (OB Is defined($record; "password"))
				$seedPassword:=$record.password
			End if 
			
			$e.PasswordHash:=Generate password hash(Generate digest($seedPassword; SHA256 digest); New object("cost"; 12))
			
		End if 
		
		// Save the record.
		$stage:="Saving entity"
		$currentField:=""
		
		$service.save($e)
		
		// Remember the new ID for subsequent references.
		$stage:="Saving reference"
		$refs[$record.ref]:=$e.Id
		
		// Associate tenants with their own tenant IDs.
		If ($record.table="Tenant")
			
			$stage:="Assigning tenant ID"
			$currentField:="GoTenantId"
			
			$e.GoTenantId:=$e.Id
			
			$stage:="Saving tenant"
			$service.save($e)
			
		End if 
		
	End for each 
	
	// Commit only if every record was imported.
	$stage:="Committing transaction"
	
	ds.validateTransaction()
	
	// Completed successfully; startup stays silent.
	
Catch
	
	ds.cancelTransaction()
	
	ALERT("Sample data import rolled back:"+Char(13)+\
		"Record: "+String($recordNumber)+Char(13)+\
		"Table: "+$currentTable+Char(13)+\
		"Reference: "+$currentRef+Char(13)+\
		"Field: "+$currentField+Char(13)+\
		"Stage: "+$stage+Char(13)+\
		"Error: "+JSON Stringify(Last errors))
	
End try
