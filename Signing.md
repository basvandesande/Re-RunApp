# App signing — Re-RunApp

This project is published via the Microsoft Partner Center (Microsoft Store). Signing for Store submissions is handled through the Partner Center / Store-association flow. If you publish through the Store, you should not need to ship your own signing certificate for published updates — the Store provides the signing identity when you associate the app.

However, when you build or distribute packages locally (sideloading, testing, or running outside Visual Studio's Store-association flow) you must sign the package yourself. Keep a secure copy of any certificate you use for production updates — if you lose the original PFX used to sign released packages, you cannot produce a drop-in update for existing installs signed with that lost certificate.

Below are the recommended steps and commands for both Store publishing and local signing.

## 1 — Store publishing (recommended for public releases)
- In Visual Studio use: **Project > Store > Associate App with the Store**.
  - This associates your project/package identity with the Partner Center entry.
  - After association the Store handles final signing for store-published packages.

Notes:
- Associating with the Store is the preferred route for production updates in the Microsoft Store.
- If you originally uploaded packages signed with a private cert outside the Store, you must keep that cert if you want to produce compatible updates later.

## 2 — Local signing for sideloading or local distribution
When you need to produce a signed package yourself (for local installs or testing outside the Store), use a certificate (PFX) to sign the MSIX/AppX.

A simple workflow:
1. Create or obtain a PFX (self-signed for development; CA-signed for production).
2. Add or reference the PFX in the project (or sign the built package with SignTool).
3. Sign the final package.

### Create a self‑signed PFX (PowerShell)
Run as an elevated PowerShell prompt (change password/subject to suit your environment):

```powershell
$pwd = ConvertTo-SecureString -String "P@ssw0rd!" -Force -AsPlainText
$cert = New-SelfSignedCertificate -Subject "CN=Re-RunApp" -Type CodeSigningCert -KeyExportPolicy Exportable -KeyLength 2048 -HashAlgorithm SHA256 -CertStoreLocation "Cert:\CurrentUser\My"
Export-PfxCertificate -Cert "Cert:\CurrentUser\My\$($cert.Thumbprint)" -FilePath ".\Re-RunApp_TemporaryKey.pfx" -Password $pwd
```
- Keep the .pfx and the password in a secure location (do not commit to source control).

### Option A — Let MSBuild use the PFX (packaging from Visual Studio / msbuild)
Add the PFX to your project root (or a safe path) and reference in the csproj:

```xml
<PropertyGroup>
  <PackageCertificateKeyFile>Re-RunApp_TemporaryKey.pfx</PackageCertificateKeyFile>
  <GenerateTemporaryStoreCertificate>False</GenerateTemporaryStoreCertificate>
</PropertyGroup>
```
Then build the package (Visual Studio packaging or `dotnet msbuild /t:Restore;Pack` workflows as appropriate). MSBuild will pick up the PFX and sign the package during the packaging step.

### Option B — Sign an already produced MSIX with SignTool
If you have an unsigned MSIX/Appx, use SignTool to sign it:

```powershell
# Example: sign with a PFX
signtool sign /fd SHA256 /sha1 <YourCertThumbprint> /f .\Re-RunApp_TemporaryKey.pfx /p "P@ssw0rd!" "path\to\your.appx"
```
or (use /fd SHA256 and /a to auto-select):

```powershell
signtool sign /fd SHA256 /a /f .\Re-RunApp_TemporaryKey.pfx /p "P@ssw0rd!" "path\to\your.msix"
```
- Replace the password and file paths as appropriate.

## 3 — Export a certificate from the Windows cert store
If your certificate is in the Windows cert store (CurrentUser\My), export it to a PFX:

```powershell
# list certs
Get-ChildItem Cert:\CurrentUser\My | Format-Table Thumbprint, Subject, NotAfter

# export (replace THUMBPRINT and password)
$pwd = ConvertTo-SecureString -String "P@ssw0rd!" -Force -AsPlainText
Export-PfxCertificate -Cert "Cert:\CurrentUser\My\THUMBPRINT" -FilePath ".\Re-RunApp_TemporaryKey.pfx" -Password $pwd
```

## 4 — Important operational notes
- Keep your production signing PFX and its password secure and backed up. Losing this certificate prevents producing compatible signed updates for sideloaded installations.
- Do not store production PFX and password in source control. Use secure storage (Azure Key Vault, company secure file share, password manager).
- For Microsoft Store publishing, prefer Store association — the Store is the canonical source of truth for updates and signing for Store-distributed apps.
- If you published via sideloading or custom channels with a certificate you no longer have, users will likely need to install a new app package with a new identity for future updates.

## 5 — Quick troubleshooting
- If Visual Studio complains about a missing PFX: check that `PackageCertificateKeyFile` references an existing file and that `<GenerateTemporaryStoreCertificate>` is set appropriately.
- To re‑associate with the Store: **Project > Store > Associate App with the Store**.
- To find stray PFX files in your workspace:

```powershell
Get-ChildItem -Path . -Filter *.pfx -Recurse -ErrorAction SilentlyContinue
```

