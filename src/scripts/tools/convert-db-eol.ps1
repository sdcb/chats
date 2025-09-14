param(
    [string]$Dir = "src/BE/DB"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-EolMode {
    param([byte[]]$Bytes)
    $hasCRLF = $false
    $hasBareLF = $false
    $hasBareCR = $false
    for ($i = 0; $i -lt $Bytes.Length; $i++) {
        $b = $Bytes[$i]
        if ($b -eq 10) { # LF
            if ($i -eq 0 -or $Bytes[$i-1] -ne 13) { $hasBareLF = $true } else { $hasCRLF = $true }
        } elseif ($b -eq 13) { # CR
            if ($i + 1 -ge $Bytes.Length -or $Bytes[$i+1] -ne 10) { $hasBareCR = $true }
        }
    }
    if ($hasBareCR -and -not $hasBareLF -and -not $hasCRLF) { return 'CR only' }
    elseif ($hasBareLF -and -not $hasCRLF -and -not $hasBareCR) { return 'LF only' }
    elseif ($hasCRLF -and -not $hasBareLF -and -not $hasBareCR) { return 'CRLF only' }
    elseif (-not $hasCRLF -and -not $hasBareLF -and -not $hasBareCR) { return 'No newlines' }
    else { return 'Mixed' }
}

function Convert-ToLFBytes {
    param([byte[]]$Bytes)
    $ms = New-Object System.IO.MemoryStream
    $i = 0
    while ($i -lt $Bytes.Length) {
        $b = $Bytes[$i]
        if ($b -eq 13) { # CR
            if ($i + 1 -lt $Bytes.Length -and $Bytes[$i+1] -eq 10) {
                # CRLF -> LF
                [void]$ms.WriteByte(10)
                $i += 2
                continue
            } else {
                # bare CR -> LF
                [void]$ms.WriteByte(10)
                $i += 1
                continue
            }
        }
        [void]$ms.WriteByte($b)
        $i += 1
    }
    return $ms.ToArray()
}

Write-Host "Scanning $Dir for *.cs (non-recursive) ..." -ForegroundColor Cyan
$files = Get-ChildItem -LiteralPath $Dir -Filter '*.cs' -File | Sort-Object Name
if (-not $files) { Write-Host "No .cs files found." -ForegroundColor Yellow; exit 0 }

# Before summary
$before = foreach ($f in $files) {
    $bytes = [System.IO.File]::ReadAllBytes($f.FullName)
    [PSCustomObject]@{ Name = $f.Name; Mode = (Get-EolMode -Bytes $bytes) }
}

$before | Format-Table -AutoSize
""
"Summary (before):"
$before | Group-Object Mode | Select-Object Name,Count | Format-Table -AutoSize

# Convert
foreach ($f in $files) {
    $bytes = [System.IO.File]::ReadAllBytes($f.FullName)
    $mode = Get-EolMode -Bytes $bytes
    if ($mode -ne 'LF only' -and $mode -ne 'No newlines') {
        $newBytes = Convert-ToLFBytes -Bytes $bytes
        if (-not ($bytes.Length -eq $newBytes.Length -and [System.Linq.Enumerable]::SequenceEqual($bytes, $newBytes))) {
            [System.IO.File]::WriteAllBytes($f.FullName, $newBytes)
        }
    }
}

# After summary
""
Write-Host "Re-scanning after conversion ..." -ForegroundColor Cyan
$after = foreach ($f in $files) {
    $bytes = [System.IO.File]::ReadAllBytes($f.FullName)
    [PSCustomObject]@{ Name = $f.Name; Mode = (Get-EolMode -Bytes $bytes) }
}
$after | Format-Table -AutoSize
""
"Summary (after):"
$after | Group-Object Mode | Select-Object Name,Count | Format-Table -AutoSize
