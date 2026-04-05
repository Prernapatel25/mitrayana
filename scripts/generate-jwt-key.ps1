<#
  generate-jwt-key.ps1

  Generates a strong random JWT key (512 bits by default), prints it once, and stores it
  in this project's user-secrets under the key `Jwt:Key`.

  Usage (PowerShell):
    cd /workspace/Mitrayana.Api
    .\scripts\generate-jwt-key.ps1

  Note: This uses `dotnet user-secrets` to store the secret locally for development.
  For production, store secrets in a dedicated secret store (Azure Key Vault, AWS Secrets Manager, etc.).
#>

param(
    [int]$Bytes = 64
)

Write-Host "Generating $Bytes-byte JWT key..."
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$data = New-Object byte[] $Bytes
$rng.GetBytes($data)
$key = [Convert]::ToBase64String($data)

Write-Host "Generated key (shown once):"
Write-Host $key -ForegroundColor Yellow

Write-Host "Initializing user-secrets for this project (if not already initialized)..."
dotnet user-secrets init | Out-Null

Write-Host "Storing key in user-secrets as 'Jwt:Key'..."
dotnet user-secrets set "Jwt:Key" "$key" | Out-Null

Write-Host "Done. The JWT key has been stored in user-secrets for this project."
Write-Host "The app will pick it up as environment configuration (Jwt__Key) when run locally."
