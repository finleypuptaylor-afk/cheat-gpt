# Windows 1.1 parameter update validation

GitHub Windows runner build/test step succeeded: https://github.com/finleypuptaylor-afk/cheat-gpt/actions/runs/38990297864
Source commit: 5d9307231e054150445f99d8519a3cf478daf10b on codex/windows-ram-settings.

The test detected total physical RAM and CPU logical processors, validated recommendations, restarted the bundled engine with 8,192-token context and two threads (or fewer on a smaller machine), generated a streamed reply with custom sampling parameters, captured the native sidebar/settings, cancelled a reply, cleared the conversation and verified engine cleanup. The verified ZIP and SHA256 were preserved in the Windows-validation artifact.

The existing repository workflow's final release-upload step still referenced the old 1.0 ZIP filename and failed after the successful build/test/artifact steps. No release assets were changed. The local Build/windows.yml is prepared to retain packages as build artifacts instead; publishing workflow changes requires additional GitHub workflow authorization. The verified Windows 1.1 package was downloaded separately for local delivery. This distinction matters: the executable checks passed; the workflow as a whole reports failure due to its stale publishing step.

This update adds native parameter settings. It does not port the 3.79 GB knowledge library, Chrome bridge, or all Chrome personalization controls to Windows.
