param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$Prompt
)

$ErrorActionPreference = 'Stop'

# codex 실행 파일 찾기
# 1) PATH에 있는 codex
# 2) Codex 데스크톱 앱 설치 위치 (%LOCALAPPDATA%\OpenAI\Codex\bin\<버전 폴더>\codex.exe) 중 가장 최근 파일
#    앱이 업데이트되면 버전 폴더 이름이 바뀌므로 이름을 고정하지 않고 검색한다
function Find-Codex {
    $fromPath = Get-Command codex -ErrorAction SilentlyContinue
    if ($fromPath) { return $fromPath.Source }

    $appBin = Join-Path $env:LOCALAPPDATA 'OpenAI\Codex\bin'
    if (Test-Path $appBin) {
        $found = Get-ChildItem $appBin -Recurse -Filter 'codex.exe' -File -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
        if ($found) { return $found.FullName }
    }

    throw "codex 실행 파일을 찾을 수 없습니다. Codex CLI를 설치하거나 PATH에 추가하세요. (확인한 위치: PATH, $appBin)"
}

$codexPath = Find-Codex
Write-Host "codex: $codexPath"

$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$taskPrompt = @"
Generate an image using the built-in image generation tool for this request:
$Prompt

Save the generated image in this project's GeneratedImages directory with a unique filename.
Return the absolute path to the actual saved image. Verify that the file exists.
If image generation is unavailable, report that limitation and stop; do not use a paid API fallback.
Do not read or copy authentication secrets. Do not modify other project files.
"@
# 주의: 호출할 때 2>&1로 오류 출력을 합치지 않는다.
# Codex는 시작 메시지를 stderr로 내보내는데, PowerShell 5.1에서 2>&1을 쓰면 이를 오류로 바꿔 위의 'Stop' 설정 때문에 바로 중단된다.
$taskPrompt | & $codexPath exec --cd $PSScriptRoot --sandbox workspace-write --enable image_generation -
exit $LASTEXITCODE
