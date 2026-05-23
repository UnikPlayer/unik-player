Get-ChildItem -Recurse -File -Include *.js,*.ts,*.svelte,*.cjs,*.mjs | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    if ($content -match '192\.168\.1\.132') {
        $newContent = $content -replace '192\.168\.1\.132','127.0.0.1'
        Set-Content $_.FullName -Value $newContent -Encoding UTF8 -NoNewline
        Write-Host "Fixed: $($_.FullName)"
    }
}
Write-Host "Done!"
