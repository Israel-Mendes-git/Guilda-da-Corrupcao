# Dispara gatilhos do Editor do Unity com a compilacao ja assentada.
#
#   .\unity-run.ps1 -Triggers "RunSceneSetup.trigger","RunBarSkin.trigger","RunPlayModeTest.trigger"
#
# POR QUE ISTO EXISTE
#
# O watcher de gatilhos (Assets/Scripts/Core/BarSkin.cs e PlayModeProbe.cs) roda
# no EditorApplication.update e NAO espera o refresh de assets. Consequencias, as
# duas ja vividas:
#
#   1. O gatilho executa a versao VELHA do codigo, e o resultado parece um bug do
#      que voce acabou de escrever. O "Montar Cena" rodou duas vezes assim.
#   2. Se o gatilho for o de Play Mode, o Unity recompila JA DENTRO do Play Mode,
#      faz domain reload e mata a corrotina do probe no meio. Nenhum relatorio e
#      gravado, e o Editor pode ficar preso num reload que nao termina -- sem
#      recompilar, sem consumir gatilho e sem erro no console.
#
# A receita: esperar a Assembly-CSharp.dll ficar mais nova que o .cs mais
# recente, dar folga para o domain reload, e so entao criar o .trigger --
# mantendo o foco na janela o tempo todo, DENTRO do mesmo comando. O Unity nao
# recompila sem foco, e perde o foco assim que outro comando roda.
param(
  [Parameter(Mandatory=$true)][string[]]$Triggers,
  [int]$TimeoutSeconds = 480
)

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$dll  = "$root\Library\ScriptAssemblies\Assembly-CSharp.dll"

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class WinRun {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int n);
}
"@ -ErrorAction SilentlyContinue

# O pid muda: o Editor pode ser reiniciado por fora no meio do trabalho.
# Script que guarda o pid reporta "nao recompilou" falsamente.
$unity = Get-Process Unity -ErrorAction SilentlyContinue |
         Where-Object { $_.MainWindowTitle -like "*Guilda-da-Corrupcao*" } | Select-Object -First 1
if (-not $unity) { Write-Output "ERRO: Unity nao esta aberto no projeto."; exit 1 }

function Focar { [void][WinRun]::SetForegroundWindow($unity.MainWindowHandle) }
[void][WinRun]::ShowWindow($unity.MainWindowHandle, 9)

# ATENCAO: as mensagens aqui usam Write-Host, nao Write-Output. Em PowerShell,
# Write-Output dentro de uma funcao entra no VALOR DE RETORNO -- o "return $true"
# viraria um array, e o "if (-not (EsperarCompilar))" nunca dispararia. Foi esse
# bug que deixou o Montar Cena rodar com codigo velho duas vezes seguidas.
function EsperarCompilar {
  $cs = Get-ChildItem "$root\Assets\Scripts" -Recurse -Filter *.cs |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1

  if ((Test-Path $dll) -and (Get-Item $dll).LastWriteTime -gt $cs.LastWriteTime) {
    Write-Host "  compilacao ja assentada (DLL $((Get-Item $dll).LastWriteTime))"
    return $true
  }

  Write-Host "  esperando recompilar (mais novo: $($cs.Name) @ $($cs.LastWriteTime))"
  $fim = (Get-Date).AddSeconds(240)
  while ((Get-Date) -lt $fim) {
    Focar
    Start-Sleep -Milliseconds 900
    if ((Test-Path $dll) -and (Get-Item $dll).LastWriteTime -gt $cs.LastWriteTime) {
      Write-Host "  recompilou: DLL @ $((Get-Item $dll).LastWriteTime)"
      # Folga para o domain reload terminar e os watchers se reinscreverem.
      $pausa = (Get-Date).AddSeconds(6)
      while ((Get-Date) -lt $pausa) { Focar; Start-Sleep -Milliseconds 600 }
      return $true
    }
  }
  return $false
}

if (-not (EsperarCompilar)) { Write-Output "TIMEOUT esperando a compilacao."; exit 2 }

foreach ($t in $Triggers) {
  Write-Output "--- $t ---"

  # Para os dois gatilhos que produzem relatorio, esperar o ARQUIVO mudar -- nao
  # basta o .trigger sumir, porque ele some no instante em que o Editor o le.
  $relatorio = if ($t -eq "RunPlayModeTest.trigger") { "$root\PlayModeReport.txt" }
               elseif ($t -eq "RunSmokeTest.trigger") { "$root\SmokeTestReport.txt" }
               else { $null }

  $marca = if ($relatorio -and (Test-Path $relatorio)) { (Get-Item $relatorio).LastWriteTime }
           else { [DateTime]::MinValue }

  New-Item -ItemType File -Path (Join-Path $root $t) -Force | Out-Null

  $fim = (Get-Date).AddSeconds($TimeoutSeconds)
  $consumido = $false
  $pronto = $false

  while ((Get-Date) -lt $fim) {
    Focar
    Start-Sleep -Milliseconds 900

    if (-not $consumido -and -not (Test-Path (Join-Path $root $t))) {
      $consumido = $true
      Write-Output "  consumido"
      if (-not $relatorio) { $pronto = $true; break }
    }

    if ($consumido -and $relatorio -and (Test-Path $relatorio)) {
      if ((Get-Item $relatorio).LastWriteTime -gt $marca) {
        Write-Output "  relatorio novo @ $((Get-Item $relatorio).LastWriteTime)"
        $pronto = $true
        break
      }
    }
  }

  if (-not $pronto) { Write-Output "  TIMEOUT em $t"; exit 3 }

  if (-not (EsperarCompilar)) { Write-Output "TIMEOUT esperando a compilacao."; exit 2 }
}

Write-Output "TUDO OK"
