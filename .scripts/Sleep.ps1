param (
	[Parameter(Mandatory)] $seconds
)

Write-Output "Sleeping for $seconds seconds..."
Start-Sleep -Seconds $seconds