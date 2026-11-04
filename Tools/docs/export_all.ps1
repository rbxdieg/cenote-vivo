$w = [Runtime.InteropServices.Marshal]::GetActiveObject("Word.Application")
foreach ($d in @($w.Documents)) { $d.Close($false) }
foreach ($f in Get-ChildItem "C:\Users\Diego\ejercicios\Entregables\*.docx") {
    $d = $w.Documents.Open($f.FullName, $false, $true)
    $pdf = "C:\Users\Diego\ejercicios\Entregables\PDF\" + $f.BaseName + ".pdf"
    $d.ExportAsFixedFormat($pdf, 17)
    $d.Close($false)
    "ok: " + $f.BaseName
}
$w.Quit()
