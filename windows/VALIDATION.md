# Windows 1.2 automatic-update validation

Windows runner: https://github.com/finleypuptaylor-afk/cheat-gpt/actions/runs/39081589475
Verified app source commit: 14c3ac22af3c15539838b3c7b856e6d160224c27.

The native app build and UI/inference smoke test passed, including RAM detection, engine restart with selected context/threads, real streamed replies, cancellation, clearing the conversation and stopping the model. The settings screenshot shows Automatic updates enabled.

Fourteen updater checks passed: signature verification, metadata tampering and wrong signer/host rejection, version downgrade/equality rejection, package hash/size validation, ZIP traversal and case-duplicate rejection, incomplete-package rejection, backups, rollback when a target file is locked, unrelated/model-file preservation, parent-process exit waiting and the real installer entry function. Fixture signing keys are ephemeral test keys; production command-line installation always uses the embedded public key. Actual Windows shutdown/power loss was not simulated; shutdown deferral was reviewed in source.

After the shutdown/startup fix, the final app core was retrieved from the Windows artifact and combined with unchanged runtime/model files. All 305 installed-file SHA256 hashes matched the final runner manifest before packaging. Both ZIP archives passed CRC checks. The embedded production public key matched the owner’s signing key.

Release: https://github.com/finleypuptaylor-afk/cheat-gpt/releases/tag/windows-updates
The public signed feed was fetched without authentication and verified with the pinned key. GitHub’s server-side SHA256 digests match both delivered ZIPs and the signed patch hash/size. The dedicated Windows release is not marked latest; the Mac latest-release channel remains v1.2.

The runner’s legacy final upload step still targets the older Windows 1.0 filename and reports failure after successful build/test/artifact steps. The owner published the independently verified packages with the local signing helper. This does not imply a successful legacy workflow as a whole.

Limits: this is an owner-signed feed/package updater, not Windows Authenticode signing. Transaction rollback covers normal file errors; a power/disk failure mid-install is not a tested recovery guarantee. App folders must be writable without elevation. Auto checks run on startup and every 12 hours while open, and can be disabled. Cached verified updates install when quitting normally or at the next startup after Windows shutdown. Preferences and models remain separate from runtime-only updates.
