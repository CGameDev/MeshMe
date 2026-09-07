# Original Mesh Me360 vector mark, rasterized to Windows multi-resolution ICO.
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$iconFolder=Join-Path $PSScriptRoot '../src/MeshMe360.Desktop/Assets'
New-Item -ItemType Directory -Force $iconFolder | Out-Null
$frames=@()
foreach($size in @(16,20,24,32,40,48,64,128,256)){
 $bitmap=[Drawing.Bitmap]::new($size*4,$size*4)
 $graphics=[Drawing.Graphics]::FromImage($bitmap)
 $graphics.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
 $graphics.ScaleTransform($size/64.0,$size/64.0)
 $background=[Drawing.Drawing2D.GraphicsPath]::new()
 $background.AddArc(0,0,64,64,180,90);$background.AddArc(192,0,64,64,270,90)
 $background.AddArc(192,192,64,64,0,90);$background.AddArc(0,192,64,64,90,90);$background.CloseFigure()
 $dark=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#142a3b'))
 $graphics.FillPath($dark,$background)
 $points=[Drawing.PointF[]]@([Drawing.PointF]::new(24,199),[Drawing.PointF]::new(72,60),[Drawing.PointF]::new(102,60),[Drawing.PointF]::new(133,121),[Drawing.PointF]::new(177,60),[Drawing.PointF]::new(222,60),[Drawing.PointF]::new(222,199),[Drawing.PointF]::new(180,199),[Drawing.PointF]::new(180,125),[Drawing.PointF]::new(130,188),[Drawing.PointF]::new(91,122),[Drawing.PointF]::new(66,199))
 $blue=[Drawing.Drawing2D.LinearGradientBrush]::new([Drawing.Point]::new(40,50),[Drawing.Point]::new(210,215),[Drawing.ColorTranslator]::FromHtml('#67caff'),[Drawing.ColorTranslator]::FromHtml('#1478da'))
 $graphics.FillPolygon($blue,$points)
 $small=[Drawing.Bitmap]::new($size,$size);$g=[Drawing.Graphics]::FromImage($small);$g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic;$g.DrawImage($bitmap,0,0,$size,$size)
 $stream=[IO.MemoryStream]::new();$small.Save($stream,[Drawing.Imaging.ImageFormat]::Png);$frames+=,@{Size=$size;Bytes=$stream.ToArray()}
 if($size -eq 256){$small.Save((Join-Path $iconFolder 'MeshMe360.png'),[Drawing.Imaging.ImageFormat]::Png)}
 $stream.Dispose();$g.Dispose();$small.Dispose();$blue.Dispose();$dark.Dispose();$background.Dispose();$graphics.Dispose();$bitmap.Dispose()
}
$out=[IO.File]::Create((Join-Path $iconFolder 'MeshMe360.ico'));$writer=[IO.BinaryWriter]::new($out)
try{
 $writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$frames.Count)
 $offset=6+16*$frames.Count
 foreach($frame in $frames){$dimension=if($frame.Size -eq 256){0}else{$frame.Size};$writer.Write([byte]$dimension);$writer.Write([byte]$dimension);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$frame.Bytes.Length);$writer.Write([uint32]$offset);$offset+=$frame.Bytes.Length}
 foreach($frame in $frames){$writer.Write([byte[]]$frame.Bytes)}
}finally{$writer.Dispose();$out.Dispose()}
