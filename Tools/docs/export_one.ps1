$w = New-Object -ComObject Word.Application
$f = Get-Item "C:\Users\Diego\ejercicios\Entregables\Entregable 6*.docx"
$d = $w.Documents.Open($f.FullName, $false, $true)
$d.ExportAsFixedFormat("C:\Users\Diego\ejercicios\Entregables\PDF\" + $f.BaseName + ".pdf", 17)
$d.Close($false); $w.Quit(); "ok"
