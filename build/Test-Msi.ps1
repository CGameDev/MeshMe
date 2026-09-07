# SPDX-License-Identifier: AGPL-3.0-only
# Read-only MSI table assertions. Does not install or register the product.
[CmdletBinding()]
param([string]$Msi='artifacts/msi-0.1.1/MeshMe360-0.1.1-x64.msi',[string]$Manifest='artifacts/msi-0.1.1/generated/payload-sha256.json')
$ErrorActionPreference='Stop'
$installer=New-Object -ComObject WindowsInstaller.Installer
$db=$installer.OpenDatabase([IO.Path]::GetFullPath($Msi),0)
function Read-Rows([string]$sql){
    $view=$db.OpenView($sql);$null=$view.Execute()
    try {while($record=$view.Fetch()){$values=@();for($i=1;$i -le $record.GetType().InvokeMember('FieldCount','GetProperty',$null,$record,$null);$i++){$values+=,$record.StringData($i)};,$values}} finally {$null=$view.Close()}
}
$properties=@{};Read-Rows 'SELECT `Property`,`Value` FROM `Property`' | ForEach-Object {$properties[$_[0]]=$_[1]}
if($properties.ProductName -ne 'Mesh Me360' -or $properties.ProductVersion -ne ([xml](Get-Content (Join-Path $PSScriptRoot '../Directory.Build.props') -Raw)).Project.PropertyGroup.Version){throw 'Wrong product identity'}
if($properties.ALLUSERS -eq '1'){throw 'Installer unexpectedly per-machine'}
$files=@(Read-Rows 'SELECT `File` FROM `File`')
$expected=(Get-Content $Manifest -Raw | ConvertFrom-Json -AsHashtable).Count
if($files.Count -ne $expected){throw "File count mismatch: $($files.Count) / $expected"}
$controls=@(Read-Rows 'SELECT `Dialog_`,`Control`,`Type`,`Property`,`Text` FROM `Control`')
$license=$controls | Where-Object {$_[2] -eq 'ScrollableText' -and $_[4] -match 'GNU AFFERO GENERAL PUBLIC LICENSE'}
if(-not $license){throw 'Full AGPL not in the MSI license control'}
$accept=$controls | Where-Object {$_[2] -eq 'CheckBox' -and $_[3] -eq 'LicenseAccepted'}
if(-not $accept){throw 'License acceptance control missing'}
$conditions=@(Read-Rows 'SELECT `Dialog_`,`Control_`,`Action`,`Condition` FROM `ControlCondition`')
if(-not($conditions | Where-Object {$_[2] -eq 'Disable' -and $_[3] -match 'LicenseAccepted'})){throw 'License acceptance is not gating installation'}
$registry=@(Read-Rows 'SELECT `Root`,`Key`,`Name`,`Value` FROM `Registry`')
if($registry | Where-Object {$_[0] -ne '1'}){throw 'Registry writes outside HKCU'}
foreach($ext in @('.stl','.obj','.3mf','.step','.stp','.bumpmesh')){
 if(-not($registry | Where-Object {$_[1] -eq "Software\Classes\$ext\OpenWithProgids"})){throw "Missing Open with: $ext"}
 if($registry | Where-Object {$_[1] -eq "Software\Classes\$ext" -and -not $_[2]}){throw "Unexpected shared default takeover: $ext"}
}
$shortcuts=@(Read-Rows 'SELECT `Name`,`Target` FROM `Shortcut`')
if($shortcuts.Count -ne 2){throw 'App/guide Start menu shortcuts missing'}
[pscustomobject]@{Result='PASS';Files=$files.Count;License='Full AGPL and acceptance gate';Scope='per-user HKCU';Shortcuts=$shortcuts.Count;CommonMeshDefaults='preserved'} | ConvertTo-Json
[Runtime.InteropServices.Marshal]::FinalReleaseComObject($db) | Out-Null
[Runtime.InteropServices.Marshal]::FinalReleaseComObject($installer) | Out-Null
