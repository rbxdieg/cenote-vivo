$w = [Runtime.InteropServices.Marshal]::GetActiveObject("Word.Application")
"docs: " + $w.Documents.Count
foreach ($d in $w.Documents) { "doc: " + $d.Name + " readonly=" + $d.ReadOnly }
$d = $w.Documents.Item(1)
$pdf = "C:\Users\Diego\ejercicios\Entregables\PDF\" + [IO.Path]::GetFileNameWithoutExtension($d.FullName) + ".pdf"
$d.ExportAsFixedFormat($pdf, 17)
"exported " + (Test-Path $pdf)
