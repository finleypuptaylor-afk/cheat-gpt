# Windows release maintenance

The Windows client checks the signed feed in the dedicated `windows-updates` GitHub release. This release is deliberately **not** marked latest, preserving the Mac Sparkle channel's existing latest-release behavior.

Build `Sources/CheatGPT.csproj` and run both the UI/inference smoke test and `--updater-tests`. `build.ps1` creates a complete first-install `Cheat-GPT-Windows-<version>-x64.zip` plus a smaller runtime-only `Cheat-GPT-Windows-<version>-update.zip`. The latter preserves the existing model/engine and unrelated files. Increase the project version before each release; the feed uses three numeric components.

On the owner's Mac, use `publish-windows.py --bootstrap <full.zip> --patch <update.zip> --version <x.y.z> --commit <verified-source-sha> --gh <gh-path> --output <metadata-directory> --publish`. Without `--publish` it only validates/signs local metadata. The helper retrieves the RSA key from login Keychain account `windows-updates`, service `CheatGPT.Windows.UpdateSigning`, checks it against update-public.pem, verifies both archives, uploads packages before metadata, and leaves the Mac latest release unchanged. No private signing key is stored in Git, CI, or release files. Back up the signing key securely; existing clients trust its embedded public key.

Metadata is exact UTF-8 JSON with version, URL, SHA256 and size. `windows-update.sig` is a detached RSA PKCS#1 SHA256 signature over those exact bytes. Failed/mismatched metadata is rejected without installation. Future feed/signature replacement is not atomic; a client checking in the short replacement window safely retries at its next check.

Updater test coverage: valid and tampered signatures, wrong signer/host, version comparison, size/hash mismatch, ZIP traversal and case duplicates, incomplete packages, backups, rollback on a locked target file, preservation of unrelated/model files, parent exit wait, single-instance coordination and the real installation entry function. Test signing keys are generated per run and injected only through the internal test call; the normal command-line installer always uses the embedded production key.

The existing remote workflow still has an older final release-upload command. Its app build/test and artifact steps work; the owner publishes signed releases with the helper above. The local windows.yml is an artifact-only replacement ready for adoption when GitHub workflow write authorization is available. A stale workflow publishing step does not invalidate successful executable tests or the separately verified artifact.
