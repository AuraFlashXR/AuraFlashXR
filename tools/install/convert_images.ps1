Add-Type -AssemblyName System.Drawing

$srcImg = "C:\Users\Administrator\.gemini\antigravity\brain\3e33ee58-b36c-4040-a2bc-d36c110e5447\.user_uploaded\media_1792932837721_8881be70.png"
$bannerImg = "C:\Users\Administrator\.gemini\antigravity\brain\3e33ee58-b36c-4040-a2bc-d36c110e5447\.user_uploaded\media_1792932845380_6755216c.png"
$lightImg = "C:\Users\Administrator\.gemini\antigravity\brain\3e33ee58-b36c-4040-a2bc-d36c110e5447\.user_uploaded\media_1792932831819_74b2fd6e.png"

# 1. Update logo_640.png in GUI resources
Copy-Item $srcImg "e:\OTHER\VR\NEWSAuraFlashXR\vision-auraflashxr-0.6.1-alpha\tools\gui\res\logo_640.png" -Force
Copy-Item $srcImg "e:\OTHER\VR\NEWSAuraFlashXR\vision-auraflashxr-0.6.1-alpha\docs\images\logo-dark.png" -Force
Copy-Item $lightImg "e:\OTHER\VR\NEWSAuraFlashXR\vision-auraflashxr-0.6.1-alpha\docs\images\logo.png" -Force
Copy-Item $bannerImg "e:\OTHER\VR\NEWSAuraFlashXR\vision-auraflashxr-0.6.1-alpha\docs\images\banner.png" -Force

# 2. Extract emblem icon (the left symbol) from media_1792932837721_8881be70.png
$fullBmp = [System.Drawing.Bitmap]::FromFile($srcImg)
$cropRect = New-Object System.Drawing.Rectangle 0, 70, 180, 180
$emblemBmp = $fullBmp.Clone($cropRect, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$fullBmp.Dispose()

$emblemBmp.Save("e:\OTHER\VR\NEWSAuraFlashXR\vision-auraflashxr-0.6.1-alpha\tools\gui\res\emblem.png", [System.Drawing.Imaging.ImageFormat]::Png)
$emblemBmp.Save("e:\OTHER\VR\NEWSAuraFlashXR\vision-auraflashxr-0.6.1-alpha\docs\images\emblem.png", [System.Drawing.Imaging.ImageFormat]::Png)

function Make-Icon([System.Drawing.Bitmap]$source, [string]$icoPath) {
    $sizes = @(256, 128, 64, 48, 32, 16)
    $imagesData = @()

    foreach ($s in $sizes) {
        $resized = New-Object System.Drawing.Bitmap $s, $s
        $g = [System.Drawing.Graphics]::FromImage($resized)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.Clear([System.Drawing.Color]::Transparent)
        $g.DrawImage($source, 0, 0, $s, $s)
        $g.Dispose()

        $ms = New-Object System.IO.MemoryStream
        $resized.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $resized.Dispose()
        $pngBytes = $ms.ToArray()
        $ms.Dispose()

        $imagesData += ,@($s, $pngBytes)
    }

    $fs = [System.IO.File]::Create($icoPath)
    $bw = New-Object System.IO.BinaryWriter $fs

    $bw.Write([uint16]0)    # Reserved
    $bw.Write([uint16]1)    # Type (1 = ICO)
    $bw.Write([uint16]$imagesData.Count) # Image count

    $offset = 6 + (16 * $imagesData.Count)

    foreach ($item in $imagesData) {
        $s = $item[0]
        $data = $item[1]
        
        $widthByte = if ($s -ge 256) { [byte]0 } else { [byte]$s }
        $heightByte = if ($s -ge 256) { [byte]0 } else { [byte]$s }
        
        $bw.Write($widthByte)       # Width
        $bw.Write($heightByte)      # Height
        $bw.Write([byte]0)          # ColorCount
        $bw.Write([byte]0)          # Reserved
        $bw.Write([uint16]1)        # Planes
        $bw.Write([uint16]32)       # BitCount
        $bw.Write([uint32]$data.Length) # BytesInRes
        $bw.Write([uint32]$offset)      # ImageOffset

        $offset += $data.Length
    }

    foreach ($item in $imagesData) {
        $bw.Write($item[1])
    }

    $bw.Flush()
    $bw.Close()
    $fs.Close()
}

Make-Icon $emblemBmp "e:\OTHER\VR\NEWSAuraFlashXR\vision-auraflashxr-0.6.1-alpha\tools\gui\res\auraflashxr.ico"
Make-Icon $emblemBmp "e:\OTHER\VR\NEWSAuraFlashXR\vision-auraflashxr-0.6.1-alpha\tools\gui\res\auraflashxr.ico"
$emblemBmp.Dispose()

Write-Output "Successfully updated logo and created icons!"
