# Microsoft Store readiness

The authoritative `YouTube.Uwp.sln` source is prepared as package version
`1.6.7.6`.

## Current status

- Target device family: `Windows.Mobile`.
- Minimum and tested OS: `10.0.15063.0`.
- Architecture: ARM only.
- The release AppX is a Developer Mode sideload package signed with the
  `ZuneTracks` certificate, not a Microsoft Store package.
- The `Store | ARM` profile is configured for an unsigned Store-upload candidate
  and requires `Package.StoreAssociation.xml`.

## Required Partner Center actions

1. Reserve the app name in Partner Center using an active Microsoft Store
   developer account.
2. In Visual Studio, use **Store > Associate App with the Store** while signed
   in with an account that can read reserved app names. This creates
   `YouTube.Uwp\Package.StoreAssociation.xml` and supplies the exact Store
   identity values.
3. Confirm the Windows Mobile target is eligible for the intended listing or
   update, and complete listing, age-rating, privacy, pricing, and certification
   requirements.

The repository does not contain a private signing key, Store credentials, Store
ID, package family name, or fabricated Partner Center identity. Store packages
are re-signed by Microsoft.

## Build

From a Visual Studio developer PowerShell:

```powershell
msbuild .\YouTube.Uwp.sln /t:Restore,Build /p:Configuration=Store /p:Platform=ARM
```

This produces an ARM `.appxupload` only after Store association metadata exists.
The current release also includes an ARM sideload AppX, public certificate, and
the complete ARM deployment dependency ZIP.
