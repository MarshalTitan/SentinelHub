# Automated validation

Initial PR CI: https://github.com/MarshalTitan/SentinelHub/actions/runs/38845855419
- 17 policy test groups passed.
- 288 native ImGui page/state/theme/width/scale cases passed, plus collapse/restore, stack balance and keyboard/controller activation.
- Compilation: zero warnings/errors.
- Installable ZIP identity/API/version/path checks passed; bundled Core/UI DLLs exactly match verified 0.4.1 packages.

Final review adds saved-collapsed startup width preservation, volatile discovery invalidation and direct command-dispatch/failure/isolation tests. These changes require the final CI run before publication.

Limit: headless native rendering checks geometry and input contracts. Actual Dalamud lifecycle, consumer settings windows, saved placement after game restart and consumer independence still require the supervised checklist. No gameplay readiness claim is inferred from compilation or plugin loading.
