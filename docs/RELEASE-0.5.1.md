# Sceneweaver 0.5.1 — File-open feedback

Opening a Stagehand, Intoner or Sceneweaver file now selects the Scene tab, displays the imported project name and object list, and selects the first object for inspection. Previously an import could leave the asset browser visible.

Operation status now appears directly below the Open buttons. File-open failures display a dialog with the filename and error, retain the current scene, and record the exception in the Dalamud log. When unsaved changes prevent an immediate import, a message identifies the pending file and object count above the existing discard/keep controls.

Validation: the user's existing Stagehand save imports successfully through the converter with its project name and all 27 objects. The 50 automated tests and Release builds pass. The Windows picker and tab transition still require verification in the running game; the reported failure has not been reproduced there. This release adds visible diagnostics if there is an additional runtime import problem.
