$ErrorActionPreference='SilentlyContinue'
$r=$null
foreach($br in @('master','main')){
  try { $r=Invoke-RestMethod -Uri ("https://api.github.com/repos/UncomplicatedCustomServer/UncomplicatedCustomItems/git/trees/"+$br+"?recursive=1") -TimeoutSec 40; if($r){ break } } catch {}
}
if(-not $r){ Write-Output 'TREE_FAIL'; exit }
$paths = $r.tree | ForEach-Object { $_.path }
Write-Output '=== ROOT FILES ==='
$paths | Where-Object { $_ -notmatch '/' }
Write-Output '=== MARKDOWN ==='
$paths | Where-Object { $_ -like '*.md' }
Write-Output '=== EXISTING /Data or Config samples ==='
$paths | Where-Object { $_ -like '*Example*' -or $_ -like '*example*' -or $_ -like '*demo*' -or $_ -like '*Demo*' }
