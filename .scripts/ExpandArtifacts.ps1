param (
	[Parameter(Mandatory)] $inputPath,
	[Parameter(Mandatory)] $outputPath
)

Set-Location $inputPath

function ExpandConfigs() {
	param($configurationName, $config)	

	$folders = Get-ChildItem -Path $armPath -Recurse -Directory

	foreach($folder in $folders) {
		$relativePath = Resolve-Path -Path $folder.FullName -Relative
		$folderOutputPath = Join-Path $outputPath $relativePath

		New-Item -Path $folderOutputPath -ItemType Directory

		$files = Get-ChildItem -Path $folder.FullName -File

		foreach($file in $files) {
			$fileName = $file.Name

			if ($fileName.StartsWith("_")) {
				$content = Get-Content -Path $file.FullName
				
				foreach($property in $config.psobject.Properties) {
					$name = $property.Name

					if ($property.Value.gettype().Name -eq "Boolean") {
						$content = $content -Replace "`"@@$name@@`"", $property.Value.ToString().ToLower()
					}
					else {
						$content = $content -Replace "@@$name@@", $property.Value
					}
				}
				
				$content = $content -Replace "@@BuildId@@", $env:BUILD_BUILDID
				
				$outputFile = Join-Path $folderOutputPath "$configurationName.$fileName"
				Set-Content -Path $outputFile -Value $content
			}
			else {
				Copy-Item $file.FullName -Destination $folderOutputPath -Container:$false
			}
		}
	}
}

$configs = (Get-Content -Path (Join-Path $inputPath 'configs.json') | ConvertFrom-Json)

foreach($config in $configs.psobject.Properties) {
	ExpandConfigs -configurationName $config.Name -config $config.Value
}