# Sceneweaver 0.15.1 — Safe recovery backup storage

Saving over an existing file no longer leaves a raw .bak beside it. Stagehand can discover those backups as stages and apply their automatic display rules, creating duplicate scenery.

Recovery copies now live in %LOCALAPPDATA%\Sceneweaver\Backups, in per-destination folders, as ZIP archives. Each archive contains original file bytes in `original` and the full destination path in `destination.txt`. Archives remain after successful saves. To recover manually, extract `original` and copy it to the recorded destination while the stage is hidden.

Backups are flushed before destination replacement. Backup failure leaves the destination unchanged. Rollback extracts the archive onto the target volume before replacement, supporting backup storage on a different drive. Legacy adjacent .bak files are not automatically deleted; move them outside all Stagehand folders, including subfolders. Already displayed duplicates may need hiding/reloading or changing areas.

Validation: 94 tests pass, including repeated saves leaving only the intended stage file, retained original bytes, backup failure and multi-file rollback. Plugin and CLI release builds checked. In-game validation remains pending. This fixes backup discovery; it does not establish the cause of the previous native crash.
