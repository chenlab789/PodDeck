# Promo v2: intro (motion) + [real screen] + outro (motion), with music bed and UI sfx.
# .\assemble2.ps1 -Format 916|169 [-Real path\to\real_<fmt>.mp4] [-Out file.mp4]
param(
    [ValidateSet('916', '169')] [string]$Format = '916',
    [string]$Real = '',
    [string]$Out = ''
)
$ErrorActionPreference = 'Stop'
$ff = 'D:\ClipCut\bin\ffmpeg.exe'
$root = $PSScriptRoot
$size = if ($Format -eq '916') { '1080x1920' } else { '1920x1080' }
$dir = Join-Path $root "out\v2_$Format"
$intro = Join-Path $dir "intro_$size.mp4"
$outro = Join-Path $dir "outro_$size.mp4"
if (-not $Out) { $Out = Join-Path $root "out\PodDeck_promo_v2_$Format.mp4" }

function Get-Dur($f) {
    $m = (& $ff -hide_banner -i $f 2>&1 | Select-String 'Duration: (\d+):(\d+):([\d.]+)').Matches[0].Groups
    [int]$m[1].Value * 3600 + [int]$m[2].Value * 60 + [double]$m[3].Value
}
$introLen = Get-Dur $intro; $outroLen = Get-Dur $outro
$realLen = if ($Real) { Get-Dur $Real } else { 0 }
$total = $introLen + $realLen + $outroLen
$outroAt = $introLen + $realLen

$aud = Join-Path $dir 'audio'
python (Join-Path $root 'make_audio2.py') $dir $aud $total $outroAt | Out-Null

$inputs = @('-i', $intro); $v = '[0:v]'; $n = 1
if ($Real) { $inputs += @('-i', $Real); $v += "[$n`:v]"; $n++ }
$inputs += @('-i', $outro); $v += "[$n`:v]"; $n++
$segs = $n
$inputs += @('-i', (Join-Path $aud 'music.wav'), '-i', (Join-Path $aud 'sfx.wav'))
$mi = $n; $si = $n + 1
$filter = "$v" + "concat=n=$segs`:v=1:a=0,format=yuv420p[v];" +
    "[$mi`:a]volume=0.75[m];[$si`:a]volume=1.0[s];" +
    "[m][s]amix=inputs=2:normalize=0:duration=first,loudnorm=I=-16:TP=-1.5:LRA=11[a]"
& $ff -y -hide_banner -loglevel error @inputs -filter_complex $filter -map '[v]' -map '[a]' -c:v libx264 -crf 17 -preset slow -c:a aac -b:a 192k -ar 48000 -movflags +faststart $Out
"wrote $Out ($([math]::Round($total,1)) s)"
