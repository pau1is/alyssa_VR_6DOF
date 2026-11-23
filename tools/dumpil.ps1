# Dumps fields / methods / IL of game types matching a regex - handy for finding what to patch.
# Example:
#   powershell -ExecutionPolicy Bypass -File tools\dumpil.ps1 -GameDir "D:\SteamLibrary\steamapps\common\ALYSSA" `
#     -Asm Assembly-CSharp -Pattern "^ObjectPickup$" -Method "TryPickupObject"
param(
  [Parameter(Mandatory = $true)][string]$GameDir,
  [string]$Asm = "Assembly-CSharp",
  [Parameter(Mandatory = $true)][string]$Pattern,
  [string]$Method = "",
  [switch]$NoBody
)
$managed = Join-Path $GameDir "ALYSSA_Data\Managed"
Add-Type -Path (Join-Path $GameDir "BepInEx\core\Mono.Cecil.dll")
$rp = New-Object Mono.Cecil.ReaderParameters
$res = New-Object Mono.Cecil.DefaultAssemblyResolver
$res.AddSearchDirectory($managed)
$rp.AssemblyResolver = $res
$m = [Mono.Cecil.ModuleDefinition]::ReadModule((Join-Path $managed "$Asm.dll"), $rp)
function AllTypes($types) { foreach ($t in $types) { $t; if ($t.HasNestedTypes) { AllTypes $t.NestedTypes } } }
foreach ($t in (AllTypes $m.Types)) {
  if ($t.FullName -notmatch $Pattern) { continue }
  "==== TYPE $($t.FullName) : $($t.BaseType)"
  foreach ($f in $t.Fields) { "  F {0}{1} {2} {3}" -f ($(if ($f.IsPublic) { "public " } else { "" })), ($(if ($f.IsStatic) { "static " } else { "" })), $f.FieldType.Name, $f.Name }
  foreach ($me in $t.Methods) {
    if ($Method -and $me.Name -notmatch $Method) { continue }
    "  M $($me.ReturnType.Name) $($me.Name)(" + (($me.Parameters | ForEach-Object { $_.ParameterType.Name + " " + $_.Name }) -join ", ") + ")"
    if (-not $NoBody -and $me.HasBody) {
      foreach ($i in $me.Body.Instructions) {
        $op = $i.Operand
        $s = if ($op -is [Mono.Cecil.MethodReference]) { $op.DeclaringType.Name + "::" + $op.Name }
             elseif ($op -is [Mono.Cecil.FieldReference]) { $op.DeclaringType.Name + "::" + $op.Name }
             elseif ($op -is [Mono.Cecil.Cil.Instruction]) { "IL_{0:x4}" -f $op.Offset }
             else { "$op" }
        "      IL_{0:x4} {1} {2}" -f $i.Offset, $i.OpCode, $s
      }
    }
  }
}
