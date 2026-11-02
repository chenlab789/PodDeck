# Assemble the promo: intro + [real screen] + outro, with music bed and UI sfx.
# .\assemble.ps1 -Format 916|169 [-Real path\to\real.mp4] -Out file.mp4
param(
    [ValidateSet('916', '169')] [string]$Format = '916',
    [string]$Real = '',
    [string]$Out = ''
)
$ff = if ($env:FFMPEG) { $env:FFMPEG } else { 'ffmpeg' }
$root = $PSScriptRoot
$size = if ($Format -eq '916') { '1080x1920' } else { '1920x1080' }
$dir = Join-Path $root "out\v$Format"
$intro = Join-Path $dir "intro_$size.mp4"
$outro = Join-Path $dir "outro_$size.mp4"
$aud = Join-Path $root 'out\audio'
if (-not $Out) { $Out = Join-Path $root "out\PodDeck_promo_$Format.mp4" }

$introLen = 12.5; $outroLen = 3.0
$realLen = 0.0
if ($Real) { $realLen = [double](& $ff -hide_banner -i $Real 2>&1 | Select-String 'Duration: (\d+):(\d+):([\d.]+)' | ForEach-Object { $m = $_.Matches[0].Groups; [int]$m[1].Value * 3600 + [int]$m[2].Value * 60 + [double]$m[3].Value }) }
$total = $introLen + $realLen + $outroLen
$outroAt = [int](($introLen + $realLen) * 1000)

$inputs = @('-i', $intro)
$v = '[0:v]'
$n = 1
if ($Real) { $inputs += @('-i', $Real); $v += "[$n`:v]"; $n++ }
$inputs += @('-i', $outro); $v += "[$n`:v]"; $n++
$segs = $n
$inputs += @('-i', (Join-Path $aud 'music.wav'), '-i', (Join-Path $aud 'sfx_intro.wav'), '-i', (Join-Path $aud 'sfx_outro.wav'))
$mi = $n; $si = $n + 1; $so = $n + 2
$fade = [math]::Round($total - 1.5, 2)
$filter = "$v" + "concat=n=$segs`:v=1:a=0,format=yuv420p[v];" +
    "[$mi`:a]atrim=0:$total,afade=t=out:st=$fade`:d=1.5,volume=0.55[m];" +
    "[$si`:a]volume=0.9[s1];" +
    "[$so`:a]adelay=$outroAt|$outroAt,volume=0.9[s2];" +
    "[m][s1][s2]amix=inputs=3:normalize=0:duration=first,alimiter=limit=0.9[a]"
& $ff -y -hide_banner -loglevel error @inputs -filter_complex $filter -map '[v]' -map '[a]' -c:v libx264 -crf 17 -preset slow -c:a aac -b:a 192k -movflags +faststart $Out
"wrote $Out ($([math]::Round($total,1)) s)"

