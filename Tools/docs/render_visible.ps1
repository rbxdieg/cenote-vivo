# Exporta cada .docx a PDF con Word en modo solo lectura (no modifica el documento).
param([string[]]$Files, [string]$PdfDir)
$word = New-Object -ComObject Word.Application
$word.Visible = $true
try {
    foreach ($f in $Files) {
        $full = (Resolve-Path $f).Path
        $doc = $word.Documents.Open($full, $false, $true)
        $pdf = Join-Path $PdfDir ([IO.Path]::GetFileNameWithoutExtension($full) + ".pdf")
        $doc.ExportAsFixedFormat($pdf, 17)
        $doc.Close($false)
        "ok: $pdf"
    }
} finally { $word.Quit() }
