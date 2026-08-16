# Reads token from figma/token.txt (gitignored). Usage: .\fetch-figma.ps1
param(
    [string]$FileKey = "6s6nXpb1Dkjco8jHeyvOTH",
    [int]$Depth = 2
)

$tokenPath = Join-Path $PSScriptRoot "token.txt"
if (-not (Test-Path $tokenPath)) {
    Write-Error "Missing figma/token.txt — paste your Figma personal access token there."
    exit 1
}

$token = (Get-Content $tokenPath -Raw).Trim()
$headers = @{ "X-Figma-Token" = $token }
$uri = "https://api.figma.com/v1/files/$FileKey`?depth=$Depth"

try {
    $file = Invoke-RestMethod -Uri $uri -Headers $headers -Method GET
    Write-Output "File: $($file.name)"
    Write-Output "Last modified: $($file.lastModified)"
    Write-Output "Pages:"
    foreach ($page in $file.document.children) {
        Write-Output "  [$($page.name)]"
        if ($page.children) {
            foreach ($frame in $page.children) {
                Write-Output "    - $($frame.name) ($($frame.type))"
            }
        }
    }
} catch {
    Write-Error $_.Exception.Message
    if ($_.ErrorDetails.Message) { Write-Error $_.ErrorDetails.Message }
    exit 1
}
