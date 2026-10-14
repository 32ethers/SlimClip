# No .NET SDK needed: smoke-test FFmpeg arguments and media round trips.
$ErrorActionPreference = 'Stop'
$tests = [System.IO.Path]::GetFullPath($PSScriptRoot)
$folder = Join-Path $tests ('.scratch-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $folder | Out-Null
try {
    $source = Join-Path $folder 'aac-source.mp4'
    $compressed = Join-Path $folder 'aac-compressed.mp4'
    $sample = Join-Path $folder 'estimate.mp4'
    $opusSource = Join-Path $folder 'opus-source.mkv'
    $opusOutput = Join-Path $folder 'opus-compressed.mp4'
    $mixedSource = Join-Path $folder 'mixed-source.mkv'
    $mixedOutput = Join-Path $folder 'mixed-compressed.mp4'
    $tinySource = Join-Path $folder 'already-small.mp4'
    $largerOutput = Join-Path $folder 'lossless-output.mp4'
    & ffmpeg -hide_banner -loglevel error -f lavfi -i 'testsrc2=size=640x360:rate=24' -f lavfi -i 'sine=frequency=440:sample_rate=48000' -t 3 -map '0:v:0' -map '1:a:0' -c:v libx264 -crf 18 -c:a aac -metadata:s:a:0 'language=jpn' -y $source
    if ($LASTEXITCODE -ne 0) { throw 'Creating H.264/AAC test video failed' }
    $probe = & ffprobe -v error -show_streams -show_format -of json $source | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or $probe.streams[0].codec_name -ne 'h264' -or $probe.streams[1].codec_name -ne 'aac' -or $probe.format.duration -lt 2.9) { throw 'Probe failed' }
    $progress = & ffmpeg -hide_banner -nostdin -loglevel error -progress pipe:1 -i $source -map '0:0' -map '0:a?' -c:v libx265 -crf 28 -vf 'scale=-2:240' -c:a copy -map_metadata 0 -tag:v hvc1 -movflags +faststart -y $compressed
    if ($LASTEXITCODE -ne 0 -or -not ($progress | Select-String '^progress=end$')) { throw 'Compression/progress failed' }
    & ffmpeg -hide_banner -nostdin -loglevel error -ss 0 -i $source -t 3 -map '0:0' -map '0:a?' -c:v libx265 -crf 28 -vf 'scale=-2:240' -c:a copy -map_metadata 0 -tag:v hvc1 -movflags +faststart -y $sample
    if ($LASTEXITCODE -ne 0) { throw 'Estimate sample failed' }
    $result = & ffprobe -v error -show_streams -show_format -of json $compressed | ConvertFrom-Json
    if ($result.streams[0].codec_name -ne 'hevc' -or $result.streams[0].height -ne 240 -or $result.streams[1].codec_name -ne 'aac') { throw 'Unexpected output tracks' }
    if ($result.streams[1].tags.language -ne 'jpn') { throw 'Audio language tag was not retained' }
    $srcPackets = (& ffprobe -v error -select_streams 'a:0' -show_packets -show_data_hash sha256 -show_entries packet=data_hash -of 'csv=p=0' $source) -join "`n"
    $dstPackets = (& ffprobe -v error -select_streams 'a:0' -show_packets -show_data_hash sha256 -show_entries packet=data_hash -of 'csv=p=0' $compressed) -join "`n"
    if (-not $srcPackets -or $srcPackets -ne $dstPackets) { throw 'AAC packets were changed during copy' }
    if ([math]::Abs([double]$result.format.duration - [double]$probe.format.duration) -gt 1) { throw 'Unexpected audio/video duration drift' }
    if ([math]::Abs([double]$result.streams[1].start_time - [double]$probe.streams[1].start_time) -gt 0.15) { throw 'AAC audio start offset changed unexpectedly' }

    & ffmpeg -hide_banner -nostdin -loglevel error -i $source -map '0:v:0' -map '0:a:0' -c:v copy -c:a libopus -y $opusSource
    if ($LASTEXITCODE -ne 0) { throw 'Creating Opus test video failed' }
    & ffmpeg -hide_banner -nostdin -loglevel error -i $opusSource -map '0:0' -map '0:a?' -c:v libx265 -crf 28 -c:a copy -c:a:0 aac -b:a:0 160k -map_metadata 0 -tag:v hvc1 -movflags +faststart -y $opusOutput
    if ($LASTEXITCODE -ne 0) { throw 'Opus to AAC conversion failed' }
    $converted = & ffprobe -v error -show_streams -show_format -of json $opusOutput | ConvertFrom-Json
    if ($converted.streams[0].codec_name -ne 'hevc' -or $converted.streams[1].codec_name -ne 'aac') { throw 'Non-AAC audio was not converted' }
    if ($converted.streams[1].tags.language -ne 'jpn') { throw 'Transcoded audio language tag was not retained' }
    & ffmpeg -hide_banner -nostdin -loglevel error -i $source -i $opusSource -map '0:v:0' -map '0:a:0' -map '1:a:0' -c copy -y $mixedSource
    if ($LASTEXITCODE -ne 0) { throw 'Creating mixed-audio input failed' }
    & ffmpeg -hide_banner -nostdin -loglevel error -i $mixedSource -map '0:0' -map '0:a?' -c:v libx265 -crf 28 -c:a copy -c:a:1 aac -b:a:1 160k -map_metadata 0 -tag:v hvc1 -movflags +faststart -y $mixedOutput
    if ($LASTEXITCODE -ne 0) { throw 'Per-track AAC conversion failed' }
    $mixed = & ffprobe -v error -show_streams -of json $mixedOutput | ConvertFrom-Json
    if ($mixed.streams.Count -ne 3 -or $mixed.streams[1].codec_name -ne 'aac' -or $mixed.streams[2].codec_name -ne 'aac') { throw 'Audio tracks lost or not converted' }
    & ffmpeg -hide_banner -nostdin -loglevel error -f lavfi -i 'color=c=blue:s=128x72:r=10:d=1.2' -an -c:v libx265 -crf 51 -y $tinySource
    if ($LASTEXITCODE -ne 0) { throw 'Creating already-small HEVC source failed' }
    & ffmpeg -hide_banner -nostdin -loglevel error -i $tinySource -map '0:v:0' -c:v libx265 -crf 0 -tag:v hvc1 -movflags +faststart -y $largerOutput
    if ($LASTEXITCODE -ne 0 -or (Get-Item $largerOutput).Length -le (Get-Item $tinySource).Length) { throw 'Lossless output was not larger than test source' }
    Write-Host ('PASS: AAC copy, Opus to AAC, mixed two-track audio, H.265 MP4 progress and estimate sample {0:N0} bytes' -f (Get-Item $sample).Length)
}
finally {
    $resolved = [System.IO.Path]::GetFullPath($folder)
    if ($resolved.StartsWith($tests + [System.IO.Path]::DirectorySeparatorChar + '.scratch-', [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    } else { throw "Refusing to remove unexpected test path: $resolved" }
}
