# LittleGarage project instructions

## CI / automation
- **Always use the CI orchestrator** in `opencode-ci-orchestrator/` for GitHub Actions repairs. It owns commits and pushes; do not commit or push directly.
- Feed the orchestrator real failed-step logs and iterate until the workflow is green.
- Never weaken, skip, or relabel tests/workflows to obtain a green result.
- Protected workflow files require manual review. If the available token cannot push workflow changes, apply the reviewed change in the GitHub web UI, then let the orchestrator commit/push the remaining changes and verify the resulting run.

## Windows player artifact
- A Unity `StandaloneWindows64` player is a **directory**, not a single file.
- The uploaded artifact must contain `LittleCarWorkshop.exe`, `UnityPlayer.dll`, and `LittleCarWorkshop_Data/`. Never upload only the `.exe`.

## Verification honesty
- Distinguish code-implemented, offline-checked, Unity-checked, build-checked, and not-checked work in docs. Do not claim Unity Editor tests or builds passed unless they actually ran in CI/Unity.
