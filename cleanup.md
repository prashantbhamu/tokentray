# Post-merge cleanup — 17 October 2026

- Verified the provider-fix branch is an ancestor of main and that the main Git
  bundle contains complete history, then safely deleted the merged local branch
  `fix/provider-security-and-freshness`. Only `main` remains active.
- Archived 31 obsolete top-level items to the task-local
  `outputs/archive/post-merge-cleanup-2026-10-17/`, retaining their relative paths
  under `repository/`, `outputs/`, and `work/`.
- Repository archive: old circular/provider preview publish folders, five test
  result folders, and six generated bin/obj folders.
- Outputs archive: provider preview executable, branch source zip/bundle, and
  superseded popup image. The active preview image now shows installed v1.2.0.
- Work archive: old backups, extracted/decompiled libraries, initial source/tray
  snapshots, display-fix package, obsolete publish folder, loose diagnostic files
  and images, and the already-extracted SDK download zip.
- Retained the installed current app, current published release, all tracked
  source/tests/designs, build SDK/tools, current source archives/reports, display
  verification evidence, and the explicit pre-1.2.0 rollback copy.
- No account credential or settings directories were touched. No Windows startup
  settings were changed. No files were permanently deleted: compiler outputs were
  archived too after the permanent-deletion command was blocked.

To recover an archived item, copy its relative path back to the corresponding
repository, outputs, or work folder, checking for newer files before overwriting.
The deleted branch can be recreated at commit `55d3a59`; its commits remain in main
and the bundles. Compiler outputs can instead be regenerated with the normal
build/test commands.
